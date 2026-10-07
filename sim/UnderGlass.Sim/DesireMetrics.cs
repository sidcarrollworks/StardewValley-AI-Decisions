namespace UnderGlass.Sim;

/// <summary>The town's boldest or shyest person at the start, and how their year went (the fringe
/// line; rule 18's hermits and brawlers). Per seed-year, except the stance and the hours out: free
/// time away from home, hours a day, in the first season and in the fourth (or the last, if shorter).</summary>
public sealed record FringeRow(string Name, double Boldness, double ActsPerYear, double DidPerYear, double UndergonePerYear,
    double AvoidsPerYear, double WithdrawalsPerYear, double Season1HoursOut, double Season4HoursOut, double EndStance);

/// <summary>Dislike at a season's end: the share of all ordered pairs below -0.2, split into pairs
/// across households and pairs of kin or housemates; and across households leaving out the pairs
/// that started there (the starting tensions).</summary>
public sealed record DislikeAt(int Day, double Across, double KinOrHome, double AcrossUnseeded);

/// <summary>Stance at a season's end: percentiles over everyone, and the mean number of hermits
/// (-0.5 or less) and brawlers (+0.5 or more) a town.</summary>
public sealed record StanceAt(int Day, double P10, double P50, double P90, double Hermits, double Brawlers);

/// <summary>The desire gate across many runs (phase 0d; spec section 10). Per year means per 112
/// days; counts of distinct people or pairs over a whole run (hermits, brawlers, pairs that keep
/// arguing) are per run. The answering shares count only acts whose window ends inside the run. A
/// share of nothing is NaN.</summary>
public sealed record DesireStats(int Runs,
    IReadOnlyDictionary<DesireKind, double> StirredPerYear,
    IReadOnlyDictionary<(DesireKind Motive, string Call), double> WeighedPerYear,
    IReadOnlyDictionary<string, double> GateActsPerYear,
    IReadOnlyDictionary<string, double> RateActsPerYear,
    IReadOnlyList<double> AnsweredWithin, double ReturnedWithin7,
    double GaveCausePerYear, double AvoidsPerYear, IReadOnlyList<(string Name, double PerYear)> TopAvoiders,
    double WithdrawalsPerYear, double TurnedAwayPerYear, double SnubsPerYear, double MarksPerYear,
    IReadOnlyList<(string Name, double PerYear)> TopGateArguers, double CrossHouseholdArgumentsPerYear,
    double PairsTwoEachWayPerRun, double PairsThreeEachWayPerRun,
    IReadOnlyList<DislikeAt> Dislike,
    IReadOnlyDictionary<(LifeRole Role, Outcome Outcome), double> Outcomes,
    IReadOnlyList<StanceAt> Stance, double HermitsPerRun, double BrawlersPerRun,
    (string Name, double Share) FeudConcentration,
    FringeRow? Boldest, FringeRow? Shyest, double MedianAvoidedAndWithdrewPerYear);

public static class DesireMetrics
{
    /// <summary>The days an answer in kind is looked for (spec section 10).</summary>
    public static readonly int[] AnswerDays = { 1, 3, 7 };

    public const double HermitAt = -0.5;
    public const double BrawlerAt = 0.5;

    /// <param name="cast">The cast the runs used (for kin and households).</param>
    /// <param name="o">The options the runs used (for the starting tensions).</param>
    public static DesireStats Summarise(IReadOnlyList<SimResult> runs, IReadOnlyList<Villager> cast, FeelingOptions o)
    {
        var byName = cast.ToDictionary(v => v.Name);
        bool Close(string a, string b) => byName[a].Household == byName[b].Household
                                          || byName[a].KinOf(b) is not null || byName[b].KinOf(a) is not null;
        double years = Math.Max(1e-9, runs.Sum(r => r.Days) / (Clock.DaysPerSeason * 4.0));
        double perRun = Math.Max(1, runs.Count);
        const int D = Clock.MinutesPerDay;
        static double Share(int part, int whole) => whole == 0 ? double.NaN : part / (double)whole;
        static double Pct(List<double> sorted, double p) => sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
        IReadOnlyList<(string, double)> Top(IEnumerable<string> names) => names.GroupBy(n => n)
            .Select(g => (g.Key, g.Count() / years)).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).Take(5).ToList();

        // Motives and the gate.
        var stirred = runs.SelectMany(r => r.Stirred).GroupBy(s => s.Motive).OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count() / years);
        var weighed = runs.SelectMany(r => r.Pursuits).GroupBy(p => (p.Motive, p.Call)).OrderBy(g => g.Key.Motive).ThenBy(g => g.Key.Call, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count() / years);
        var gate = runs.SelectMany(r => r.Pursued.Select(id => r.Acts[id].Kind)).GroupBy(k => k).OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count() / years);
        string[] aimed = { "Argued", "GaveGift", "HelpedSomeone" };
        var rate = aimed.ToDictionary(k => k, k => runs.Sum(r => r.Acts.Count(a => a.Kind == k && a.Target is not null && !a.Injected && !r.Pursued.Contains(a.Id))) / years);

        // Answering, across households: an argument met by one back, and a kindness returned. An act
        // counts toward a window only if the window ends inside the run.
        var answered = new int[AnswerDays.Length];
        var argumentsAcross = new int[AnswerDays.Length];
        int kindAcross = 0, returned = 0;
        foreach (SimResult r in runs)
        {
            var aimedActs = r.Acts.Where(a => a.Target is not null && a.Target != a.Actor && byName.ContainsKey(a.Actor) && byName.ContainsKey(a.Target)).ToList();
            // Each ordered pair's ticks, in order (acts are in tick order).
            ILookup<(string, string), int> Ticks(Func<Act, bool> which) => aimedActs.Where(which).ToLookup(a => (a.Actor, a.Target!), a => a.Tick);
            int? Next(ILookup<(string, string), int> ticks, string from, string to, int after)
            {
                foreach (int t in ticks[(from, to)])
                    if (t > after) return t;
                return null;
            }
            static bool IsKind(Act a) => a.Kind is "GaveGift" or "HelpedSomeone";
            var argues = Ticks(a => a.Kind == "Argued");
            var kind = Ticks(IsKind);
            int end = r.Days * D;
            foreach (Act a in aimedActs.Where(a => a.Kind == "Argued" && !Close(a.Actor, a.Target!)))
            {
                int? back = Next(argues, a.Target!, a.Actor, a.Tick);
                for (int k = 0; k < AnswerDays.Length; k++)
                {
                    if (a.Tick + AnswerDays[k] * D >= end)
                        continue;
                    argumentsAcross[k]++;
                    if (back is { } t && t - a.Tick <= AnswerDays[k] * D)
                        answered[k]++;
                }
            }
            foreach (Act a in aimedActs.Where(a => IsKind(a) && !Close(a.Actor, a.Target!)))
            {
                if (a.Tick + 7 * D >= end)
                    continue;
                kindAcross++;
                if (Next(kind, a.Target!, a.Actor, a.Tick) is { } t && t - a.Tick <= 7 * D)
                    returned++;
            }
        }

        // Keeping away.
        var life = runs.SelectMany(r => r.LifeEvents).ToList();
        int Acts(string kind) => runs.Sum(r => r.Acts.Count(a => a.Kind == kind));

        // Arguments started by the gate, and pairs that keep at it.
        var gateArgues = runs.SelectMany(r => r.Pursued.Select(id => r.Acts[id]).Where(a => a.Kind == "Argued")).ToList();
        int crossArgues = runs.Sum(r => r.Acts.Count(a => a.Kind == "Argued" && a.Target is not null && byName.ContainsKey(a.Target) && !Close(a.Actor, a.Target)));
        int two = 0, three = 0;
        foreach (SimResult r in runs)
        {
            var counts = r.Acts.Where(a => a.Kind == "Argued" && a.Target is not null && byName.ContainsKey(a.Actor) && byName.ContainsKey(a.Target) && !Close(a.Actor, a.Target))
                .GroupBy(a => (a.Actor, a.Target!)).ToDictionary(g => g.Key, g => g.Count());
            foreach (var ((a, b), n) in counts.Where(p => string.CompareOrdinal(p.Key.Actor, p.Key.Item2) < 0))
            {
                int back = counts.GetValueOrDefault((b, a));
                if (n >= 2 && back >= 2) two++;
                if (n >= 3 && back >= 3) three++;
            }
        }

        // Dislike at each season's end.
        var seeded = new HashSet<(string, string)>(o.Start.Keys);
        var dislike = new List<DislikeAt>();
        int snapshots = runs.Count == 0 ? 0 : runs.Min(r => r.RegardSnapshots.Count);
        for (int s = 0; s < snapshots; s++)
        {
            int across = 0, home = 0, unseeded = 0, all = 0;
            foreach (SimResult r in runs)
            {
                int n = r.Names.Count;
                double[] flat = r.RegardSnapshots[s].Regard;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        all++;
                        if (flat[i * n + j] >= -0.2) continue;
                        if (Close(r.Names[i], r.Names[j])) home++;
                        else
                        {
                            across++;
                            if (!seeded.Contains((r.Names[i], r.Names[j]))) unseeded++;
                        }
                    }
            }
            dislike.Add(new DislikeAt(runs[0].RegardSnapshots[s].Day, Share(across, all), Share(home, all), Share(unseeded, all)));
        }

        // How things turned out, across households.
        var settled = life.Where(e => e.Role is LifeRole.Did or LifeRole.Undergone && e.Outcome != Outcome.Open
                                      && byName.ContainsKey(e.Other) && !Close(e.Person, e.Other)).ToList();
        var outcomes = settled.GroupBy(e => (e.Role, e.Outcome)).OrderBy(g => g.Key.Role).ThenBy(g => g.Key.Outcome)
            .ToDictionary(g => g.Key, g => g.Count() / (double)settled.Count(e => e.Role == g.Key.Role));

        // Stance at season ends.
        var stance = new List<StanceAt>();
        int length = runs.Count == 0 || runs[0].Stances.Count == 0 ? 0 : runs.Min(r => r.Stances.Values.First().Length);
        for (int d = Clock.DaysPerSeason - 1; d < length; d += Clock.DaysPerSeason)
        {
            var values = runs.SelectMany(r => r.Stances.Values.Select(v => v[d])).OrderBy(x => x).ToList();
            stance.Add(new StanceAt(d, Pct(values, 0.1), Pct(values, 0.5), Pct(values, 0.9),
                runs.Average(r => r.Stances.Values.Count(v => v[d] <= HermitAt)), runs.Average(r => r.Stances.Values.Count(v => v[d] >= BrawlerAt))));
        }
        bool Ever(double[] v, Func<double, bool> test)
        {
            for (int d = Clock.DaysPerSeason - 1; d < v.Length; d += Clock.DaysPerSeason)
                if (test(v[d])) return true;
            return false;
        }
        double hermits = runs.Sum(r => r.Stances.Values.Count(v => Ever(v, x => x <= HermitAt))) / perRun;
        double brawlers = runs.Sum(r => r.Stances.Values.Count(v => Ever(v, x => x >= BrawlerAt))) / perRun;

        // How much of the feuding one person accounts for.
        // A pair that starts in a feud never ties as a new one; a cooler start that grows into one does
        // (Simulation.Feelings; SteeringTests T34).
        var feuds = runs.SelectMany(r => r.Ties.Where(t => t.What == "feud")).ToList();
        var feuder = feuds.SelectMany(t => new[] { t.A, t.B }).GroupBy(n => n)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
        var concentration = feuder is null ? ("none", double.NaN) : (feuder.Key, feuder.Count() / (double)feuds.Count);

        // The fringe: the boldest and the shyest at the start.
        FringeRow? Fringe(bool boldest)
        {
            if (runs.Count == 0 || runs[0].CharactersAtStart.Count == 0) return null;
            var order = runs[0].CharactersAtStart.OrderBy(p => p.Value.Boldness).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
            var pick = boldest ? order[^1] : order[0];
            string n = pick.Key;
            double Hours(SimResult r, int season)
            {
                if (!r.OutMinutes.TryGetValue(n, out int[]? m) || m.Length == 0) return double.NaN;
                int s = Math.Min(season, m.Length - 1);
                int days = Math.Min(Clock.DaysPerSeason, r.Days - s * Clock.DaysPerSeason);
                return m[s] / 60.0 / Math.Max(1, days);
            }
            return new FringeRow(n, pick.Value.Boldness,
                runs.Sum(r => r.Acts.Count(a => a.Actor == n && !a.Injected)) / years,
                life.Count(e => e.Person == n && e.Role == LifeRole.Did) / years,
                life.Count(e => e.Person == n && e.Role == LifeRole.Undergone) / years,
                life.Count(e => e.Person == n && e.Role == LifeRole.Avoided) / years,
                life.Count(e => e.Person == n && e.Role == LifeRole.Withdrew) / years,
                runs.Average(r => Hours(r, 0)), runs.Average(r => Hours(r, 3)),
                runs.Average(r => r.Stances.TryGetValue(n, out double[]? v) && v.Length > 0 ? v[^1] : 0));
        }
        var keptAway = cast.Select(v => life.Count(e => e.Person == v.Name && e.Role is LifeRole.Avoided or LifeRole.Withdrew) / years)
            .OrderBy(x => x).ToList();
        double median = keptAway.Count == 0 ? double.NaN
            : keptAway.Count % 2 == 1 ? keptAway[keptAway.Count / 2] : (keptAway[keptAway.Count / 2 - 1] + keptAway[keptAway.Count / 2]) / 2;

        return new DesireStats(runs.Count, stirred, weighed, gate, rate,
            answered.Select((a, k) => Share(a, argumentsAcross[k])).ToList(), Share(returned, kindAcross),
            life.Count(e => e.Role == LifeRole.GaveCause) / years,
            runs.Sum(r => r.Avoids.Count) / years, Top(runs.SelectMany(r => r.Avoids.Select(v => v.Holder))),
            runs.Sum(r => r.Withdrawals) / years, Acts(Simulation.TurnedAway) / years, Acts(Simulation.Snubbed) / years,
            runs.Sum(r => r.Marks) / years,
            Top(gateArgues.Select(a => a.Actor)), crossArgues / years, two / perRun, three / perRun,
            dislike, outcomes, stance, hermits, brawlers, concentration,
            Fringe(true), Fringe(false), median);
    }
}
