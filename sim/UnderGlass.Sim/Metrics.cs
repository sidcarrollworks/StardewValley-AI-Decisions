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
