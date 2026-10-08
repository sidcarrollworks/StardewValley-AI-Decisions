namespace UnderGlass.Sim;

/// <summary>Summary numbers for one group of acts across many runs (design section 10).</summary>
public sealed record GroupStats(
    string Group,
    int Acts,
    double MeanWitnesses,
    double MeanReach,
    double Saturated,
    double InBand,
    double Died,
    double MeanDaysSpreading,
    double ActorRight,
    double ActorWrong,
    double ActorUnknown);

/// <summary>How the town sleeps and works (design rule 1), across many runs.</summary>
public sealed record BodyStats(
    double MeanBedtime,
    double BedtimeSpread,
    double MeanWake,
    double MeanSleepHours,
    double MissedAlarmShare,
    double CollapsesPerSeason,
    double LatePerSeason);

/// <summary>Across many runs: how far stories go, who gets blamed, how often a scandal ends in a
/// confrontation, how often each tier happens, and how the town sleeps.</summary>
public sealed record RunStats(
    int Runs,
    IReadOnlyList<GroupStats> Groups,
    double ConfrontationsPerSeason,
    double ConfrontationsRight,
    double ScandalsConfronted,
    IReadOnlyDictionary<Tier, double> PerYear,
    BodyStats Body);

public static class Metrics
{
    /// <summary>Which group an act kind belongs to: its tier (design rule 9).</summary>
    public static string GroupOf(ActKind k) => k.Tier.ToString().ToLowerInvariant();

    /// <summary>Reach band for a scandal that spreads well without saturating (design section 10).</summary>
    public const double BandLow = 0.4, BandHigh = 0.7;

    /// <param name="injectedOnly">Count only acts the harness placed, to measure spread on a known
    /// number of scandals.</param>
    public static RunStats Summarise(IReadOnlyList<SimResult> runs, IReadOnlyList<ActKind> kinds, bool injectedOnly = false)
    {
        var groups = new List<GroupStats>();
        foreach (Tier tier in Enum.GetValues<Tier>())
        {
            string group = tier.ToString().ToLowerInvariant();
            var rows = new List<(int Witnesses, double Reach, int Days, int Right, int Wrong, int Unknown, int Holders)>();
            foreach (SimResult r in runs)
            {
                foreach (Act act in r.Acts)
                {
                    ActKind kind = kinds.First(k => k.Name == act.Kind);
                    if (kind.Tier != tier || injectedOnly && !act.Injected || !r.HoldersByDay.TryGetValue(act.Id, out int[]? byDay))
                        continue;
                    int others = r.CastSize - 1;
                    int final = byDay[^1];
                    int firstDay = Clock.Day(act.Tick);
                    int lastGrowth = firstDay;
                    for (int d = firstDay + 1; d < byDay.Length; d++)
                        if (byDay[d] > byDay[d - 1])
                            lastGrowth = d;
                    int right = 0, wrong = 0, unknown = 0;
                    foreach (var held in r.Beliefs.Values)
                    {
                        if (!held.TryGetValue(act.Id, out Belief? b))
                            continue;
                        if (b.Actor is null) unknown++;
                        else if (b.Actor == act.Actor) right++;
                        else wrong++;
                    }
                    rows.Add((r.Witnesses.GetValueOrDefault(act.Id), (double)final / others, lastGrowth - firstDay, right, wrong, unknown, final));
                }
            }
            if (rows.Count == 0)
                continue;
            int holders = Math.Max(1, rows.Sum(x => x.Holders));
            groups.Add(new GroupStats(group, rows.Count,
                rows.Average(x => x.Witnesses),
                rows.Average(x => x.Reach),
                rows.Count(x => x.Reach >= 0.9) / (double)rows.Count,
                rows.Count(x => x.Reach >= BandLow && x.Reach <= BandHigh && x.Days >= 3) / (double)rows.Count,
                rows.Count(x => x.Holders <= x.Witnesses) / (double)rows.Count,
                rows.Average(x => x.Days),
                rows.Sum(x => x.Right) / (double)holders,
                rows.Sum(x => x.Wrong) / (double)holders,
                rows.Sum(x => x.Unknown) / (double)holders));
        }

        double seasons = runs.Sum(r => r.Days) / (double)Clock.DaysPerSeason;
        double years = seasons / 4;
        var perYear = Enum.GetValues<Tier>().ToDictionary(t => t,
            t => runs.Sum(r => r.Acts.Count(a => !a.Injected && kinds.First(k => k.Name == a.Kind).Tier == t)) / Math.Max(1e-9, years));

        int confrontations = runs.Sum(r => r.Confrontations.Count);
        int scandals = runs.Sum(r => r.Acts.Count(a => kinds.First(k => k.Name == a.Kind).IsScandal));
        return new RunStats(runs.Count, groups,
            confrontations / Math.Max(1e-9, seasons),
            runs.Sum(r => r.Confrontations.Count(c => c.Correct)) / (double)Math.Max(1, confrontations),
            confrontations / (double)Math.Max(1, scandals),
            perYear,
            Bodies(runs, seasons));
    }

    /// <summary>Bedtimes are hours after the morning they follow (so 1:00 reads as 25); the
    /// starting night is left out.</summary>
    private static BodyStats Bodies(IReadOnlyList<SimResult> runs, double seasons)
    {
        var nights = runs.SelectMany(r => r.Sleeps).Where(s => s.SleptAt > 0 && !s.Collapsed).ToList();
        var woke = runs.SelectMany(r => r.Sleeps).Where(s => s.WokeAt is not null).ToList();
        static double Hour(int m) => Clock.OfDay(m) / 60.0;
        static double Bed(int m) => Hour(m) < 12 ? Hour(m) + 24 : Hour(m);
        var beds = nights.Select(s => Bed(s.SleptAt)).ToList();
        double mean = beds.Count > 0 ? beds.Average() : 0;
        double spread = beds.Count > 1 ? Math.Sqrt(beds.Average(b => (b - mean) * (b - mean))) : 0;
        var full = nights.Where(s => s.WokeAt is not null).ToList();
        return new BodyStats(
            mean,
            spread,
            woke.Count > 0 ? woke.Average(s => Hour(s.WokeAt!.Value)) : 0,
            full.Count > 0 ? full.Average(s => (s.WokeAt!.Value - s.SleptAt) / 60.0) : 0,
            woke.Count > 0 ? woke.Count(s => s.MissedAlarm) / (double)woke.Count : 0,
            runs.Sum(r => r.Sleeps.Count(s => s.Collapsed)) / Math.Max(1e-9, seasons),
            runs.Sum(r => r.Late.Count) / Math.Max(1e-9, seasons));
    }

    /// <summary>A stable hash of a run's log, for the determinism check (E0).</summary>
    public static string LogHash(SimResult r) => Rng.Hash(r.Log.ToArray()).ToString("x16");
}

/// <summary>The spread of regard across all ordered pairs at one snapshot (phase 0c): the mean
/// change from the baseline, percentiles of regard, the share of pairs below -0.2, at 0.4 or
/// more, and moved 0.1 or more from where they started; and the mean among kin and among the rest.</summary>
public sealed record RegardSpread(int Day, double MeanChange, double P5, double P50, double P95,
    double UnderMinus02, double AtLeast04, double Moved01, double KinMean, double NonKinMean);

/// <summary>Feelings across many runs (phase 0c; design section 3a). Per year means per 112 days.
/// A mean over nothing (no such holders, no such events) is NaN, not 0; so are war and dead towns
/// for runs shorter than a season and a year.</summary>
public sealed record FeelingStats(int Runs,
    double MeanPower, double PowerSpread, double LowPowerShare, double HighPowerShare,
    IReadOnlyList<(string Name, double Power)> LowestPower,
    IReadOnlyList<RegardSpread> BySnapshot,
    double FeudsPerYear, double KinFeudsPerYear, double FriendshipsPerYear, double ReconciliationsPerYear,
    IReadOnlyList<(string A, string B, int Seeds)> TopFeuds, IReadOnlyList<(string A, string B, int Seeds)> TopFriendships,
    double SeedsWithFeudAndFriendship, double WarTowns, double DeadTowns,
    IReadOnlyDictionary<string, double> SentimentsPerSeason, double SentimentShare,
    double DropWitnessed, double DropHeardName, double DropCorroborated, double DropConfirmed,
    double ShameStepsPerScandal, double KinDropPerScandal,
    double InnocentsResentingNamer, double WrongConfrontDrop, double BystanderRegardForConstable,
    double GiftsToLoved, double ArgumentsToDisliked, double ArgumentsInHouseholds,
    double NewsPerYear, double TriviaPerYear,
    double GrievancePerYear, double ShopSwitchesPerYear, IReadOnlyList<double> HouseholdsAtChainBySeason,
    IReadOnlyDictionary<string, double> MeanKindRegard, double RegardTowardNewcomer, double RegardFromNewcomer);

public static class FeelingMetrics
{
    /// <summary>War town: at some snapshot, more than this share of ordered pairs is below -0.2.</summary>
    public const double WarShare = 0.10;
    /// <summary>Dead town: at a year's end, fewer than this share of ordered pairs moved 0.1 or more.</summary>
    public const double DeadShare = 0.05;

    /// <param name="cast">The cast the runs used (for kin and households).</param>
    /// <param name="groceriesAt">Where each household shopped at the start.</param>
    public static FeelingStats Summarise(IReadOnlyList<SimResult> runs, IReadOnlyList<ActKind> kinds,
        IReadOnlyList<Villager> cast, IReadOnlyDictionary<string, string> groceriesAt, FeelingOptions o)
    {
        var byName = cast.ToDictionary(v => v.Name);
        bool Kin(string a, string b) => byName[a].KinOf(b) is not null || byName[b].KinOf(a) is not null;
        bool Household(string a, string b) => byName[a].Household == byName[b].Household;
        double years = runs.Sum(r => r.Days) / (Clock.DaysPerSeason * 4.0);
        double seasons = years * 4;
        static double Mean(IEnumerable<double> xs) { double s = 0; int n = 0; foreach (double x in xs) { s += x; n++; } return n == 0 ? double.NaN : s / n; }
        static double Pct(List<double> sorted, double p) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];

        // The power of acting, read at day ends.
        var powers = runs.SelectMany(r => r.PowerByDay.Values.SelectMany(v => v)).ToList();
        double meanPower = Mean(powers);
        double spread = powers.Count > 1 ? Math.Sqrt(Mean(powers.Select(p => (p - meanPower) * (p - meanPower)))) : 0;
        var lowest = runs.SelectMany(r => r.PowerByDay).GroupBy(p => p.Key)
            .Select(g => (Name: g.Key, Power: Mean(g.SelectMany(p => p.Value))))
            .OrderBy(x => x.Power).ThenBy(x => x.Name, StringComparer.Ordinal).Take(5).ToList();

        // The spread of regard at each snapshot.
        var bySnapshot = new List<RegardSpread>();
        int snapshots = runs.Count == 0 ? 0 : runs.Min(r => r.RegardSnapshots.Count);
        var war = new bool[runs.Count];
        bool seasonEnd = false;
        for (int s = 0; s < snapshots; s++)
        {
            var all = new List<double>();
            double change = 0, kinSum = 0, nonKinSum = 0;
            int under = 0, high = 0, moved = 0, kinN = 0, nonKinN = 0;
            for (int ri = 0; ri < runs.Count; ri++)
            {
                SimResult r = runs[ri];
                int n = r.Names.Count, runUnder = 0;
                double[] flat = r.RegardSnapshots[s].Regard;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        double v = flat[i * n + j], d = v - r.Baseline[(r.Names[i], r.Names[j])];
                        all.Add(v);
                        change += d;
                        if (v < -0.2) { under++; runUnder++; }
                        if (v >= 0.4) high++;
                        if (Math.Abs(d) >= 0.1) moved++;
                        if (Kin(r.Names[i], r.Names[j])) { kinSum += v; kinN++; } else { nonKinSum += v; nonKinN++; }
                    }
                // A war town is judged at season ends only (spec section 10).
                if (r.RegardSnapshots[s].Day % Clock.DaysPerSeason == Clock.DaysPerSeason - 1)
                {
                    seasonEnd = true;
                    if (runUnder > WarShare * n * (n - 1))
                        war[ri] = true;
                }
            }
            all.Sort();
            bySnapshot.Add(new RegardSpread(runs[0].RegardSnapshots[s].Day, change / Math.Max(1, all.Count), Pct(all, 0.05), Pct(all, 0.5), Pct(all, 0.95),
                under / (double)Math.Max(1, all.Count), high / (double)Math.Max(1, all.Count), moved / (double)Math.Max(1, all.Count),
                kinSum / Math.Max(1, kinN), nonKinSum / Math.Max(1, nonKinN)));
        }
        // A dead town is judged at the end of each year the runs reach (the first year's end for a
        // one-year run): dead if at any of them fewer than DeadShare of ordered pairs have moved 0.1.
        int yearDays = 4 * Clock.DaysPerSeason;
        var yearRuns = runs.Where(r => r.RegardSnapshots.Any(x => x.Day == yearDays - 1)).ToList();
        double dead = yearRuns.Count == 0 ? double.NaN : yearRuns.Count(r => r.RegardSnapshots
            .Where(x => x.Day % yearDays == yearDays - 1)
            .Any(x =>
            {
                double[] flat = x.Regard;
                int n = r.Names.Count, moved = 0;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (i != j && Math.Abs(flat[i * n + j] - r.Baseline[(r.Names[i], r.Names[j])]) >= 0.1) moved++;
                return moved < DeadShare * n * (n - 1);
            })) / (double)yearRuns.Count;

        // Ties.
        var ties = runs.SelectMany((r, ri) => r.Ties.Select(t => (Run: ri, t.A, t.B, t.What))).ToList();
        double PerYear(string what) => ties.Count(t => t.What == what) / Math.Max(1e-9, years);
        IReadOnlyList<(string, string, int)> Top(string what) => ties.Where(t => t.What == what)
            .GroupBy(t => (t.A, t.B)).Select(g => (g.Key.A, g.Key.B, g.Select(t => t.Run).Distinct().Count()))
            .OrderByDescending(x => x.Item3).ThenBy(x => x.A, StringComparer.Ordinal).ThenBy(x => x.B, StringComparer.Ordinal).Take(5).ToList();
        double both = runs.Count(r => r.Ties.Any(t => t.What == "feud") && r.Ties.Any(t => t.What == "friendship")) / (double)Math.Max(1, runs.Count);

        // Sentiments, from the log (each one made or renewed).
        var sentimentLines = runs.SelectMany(r => r.Log.Where(l => l.Contains(" sentiment ")).Select(l => l.Split(' ')[^4])).ToList(); // "... {name} {strength} act {id}"; a kind's name has spaces
        var perSeason = sentimentLines.GroupBy(n => n).OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count() / Math.Max(1e-9, seasons));
        var changes = runs.SelectMany(r => r.Feelings.Where(f => f.Toward is not null && f.Change != 0 && f.Route != "Reattributed")).ToList();
        double total = changes.Sum(f => Math.Abs(f.Change));
        double named = changes.Where(f => Math.Abs(f.Change) >= o.SentimentMin && Feelings.SentimentName(f.Route, f.Change) is not null)
            .Sum(f => Math.Abs(f.Change));

        // How far the town turns on a culprit, by how it knows: each holder's net change toward the
        // culprit over the act (a feeling that grew writes a row per top-up; a reattribution takes
        // its row back), classed by the strongest basis it reached.
        string[] strength = { "Witnessed", "HeardName", "Corroborated", "Confirmed" };
        var culprit = runs.SelectMany((r, ri) => r.Feelings
                .Where(f => f.Toward is not null && f.ActId >= 0 && f.ActId < r.Acts.Count && f.Toward == r.Acts[f.ActId].Actor && f.Change != 0 // a greeting's tone cites no act (0d.6)
                            && f.Route != "Shame" && f.Route != "Accused" && f.Route != "Confronted"
                            && kinds.First(k => k.Name == r.Acts[f.ActId].Kind).IsScandal)
                .GroupBy(f => (Run: ri, f.Holder, f.ActId)))
            .Select(g => (Change: g.Sum(f => f.Change),
                          Basis: g.Where(f => f.Route != "Reattributed").Select(f => Array.IndexOf(strength, f.Basis)).DefaultIfEmpty(-1).Max()))
            .ToList();
        double Drop(string basis) => Mean(culprit.Where(x => x.Basis == Array.IndexOf(strength, basis)).Select(x => x.Change));
        int scandals = runs.Sum(r => r.Acts.Count(a => kinds.First(k => k.Name == a.Kind).IsScandal));
        var shame = runs.SelectMany(r => r.Feelings.Where(f => f.Route == "Shame")).ToList();

        // Being named.
        // Innocents questioned who were named by someone (kin asked only for an alibi were not).
        var innocents = runs.SelectMany(r => r.Interviews.Where(i => i.Who != r.Acts[i.ActId].Actor
                && r.Feelings.Any(f => f.Holder == i.Who && f.ActId == i.ActId && f.Route == "Accused" && f.Toward is not null))
            .Select(i => (r, i))).ToList();
        double resenting = innocents.Count(x => x.r.Feelings.Any(f => f.Holder == x.i.Who && f.ActId == x.i.ActId && f.Route == "Accused"
                                                                    && f.Toward is { } namer && x.r.Regard.GetValueOrDefault((x.i.Who, namer)) <= -0.1))
                           / (double)Math.Max(1, innocents.Count);
        double wrongConfront = Mean(runs.SelectMany(r => r.Feelings.Where(f => f.Route == "Confronted" && f.Toward is not null)).Select(f => f.Change));
        double constable = Mean(runs.Where(r => r.Constable is not null).SelectMany(r => r.Names
            .Where(n => n != r.Constable && !Kin(n, r.Constable!))
            .Select(n => r.Regard[(n, r.Constable!)] - r.Baseline[(n, r.Constable!)])));

        // Law 4: whom acts are aimed at.
        var aimed = runs.SelectMany(r => r.Acts.Where(a => a.Target is not null && r.AimedAt.ContainsKey(a.Id)).Select(a => (r, a))).ToList();
        var kind = aimed.Where(x => x.a.Kind is "GaveGift" or "HelpedSomeone").ToList();
        var argued = aimed.Where(x => x.a.Kind == "Argued").ToList();
        double natural(Tier t) => runs.Sum(r => r.Acts.Count(a => !a.Injected && kinds.First(k => k.Name == a.Kind).Tier == t)) / Math.Max(1e-9, years);

        // Groceries: households at the chain at each season's end.
        var households = cast.Select(v => v.Household).Distinct().ToList();
        int seasonsRun = runs.Count == 0 ? 0 : runs[0].Days / Clock.DaysPerSeason;
        var atChain = Enumerable.Range(1, seasonsRun).Select(q => Mean(runs.Select(r =>
            (double)households.Count(h =>
            {
                string shop = groceriesAt.GetValueOrDefault(h, "Store");
                foreach (var sw in r.ShopSwitches.Where(s => s.Household == h && s.Day < q * Clock.DaysPerSeason))
                    shop = sw.To;
                return shop == "Mart";
            })))).ToList();

        var kindRegard = runs.SelectMany(r => r.KindRegard).GroupBy(p => p.Key.Kind).OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Mean(g.Select(p => p.Value)));
        string newcomer = DefaultTown.Newcomer;
        var hasNewcomer = runs.Where(r => r.Names.Contains(newcomer)).ToList();

        return new FeelingStats(runs.Count,
            meanPower, spread, powers.Count(p => p < 0.3) / (double)Math.Max(1, powers.Count), powers.Count(p => p > 0.7) / (double)Math.Max(1, powers.Count),
            lowest, bySnapshot,
            PerYear("feud"), PerYear("kin-feud"), PerYear("friendship"), PerYear("reconciled"),
            Top("feud"), Top("friendship"), both, seasonEnd ? war.Count(w => w) / (double)Math.Max(1, runs.Count) : double.NaN, dead,
            perSeason, total > 0 ? named / total : double.NaN,
            Drop("Witnessed"), Drop("HeardName"), Drop("Corroborated"), Drop("Confirmed"),
            shame.Count(f => f.Toward is null) / (double)Math.Max(1, scandals), shame.Where(f => f.Toward is not null).Sum(f => f.Change) / Math.Max(1, scandals),
            innocents.Count > 0 ? resenting : double.NaN, wrongConfront, constable,
            kind.Count > 0 ? kind.Count(x => x.r.AimedAt[x.a.Id] >= 0.4) / (double)kind.Count : double.NaN,
            argued.Count > 0 ? argued.Count(x => x.r.AimedAt[x.a.Id] < 0) / (double)argued.Count : double.NaN,
            argued.Count > 0 ? argued.Count(x => Household(x.a.Actor, x.a.Target!)) / (double)argued.Count : double.NaN,
            natural(Tier.News), natural(Tier.Trivia),
            runs.Sum(r => r.Motives.Count(x => x.Motive == "grievance")) / Math.Max(1e-9, years),
            runs.Sum(r => r.ShopSwitches.Count) / Math.Max(1e-9, years), atChain,
            kindRegard,
            Mean(hasNewcomer.SelectMany(r => r.Names.Where(n => n != newcomer).Select(n => r.Regard[(n, newcomer)]))),
            Mean(hasNewcomer.SelectMany(r => r.Names.Where(n => n != newcomer).Select(n => r.Regard[(newcomer, n)]))));
    }
}
