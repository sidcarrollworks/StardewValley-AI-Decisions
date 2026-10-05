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

/// <summary>Across many runs: how far stories go, who gets blamed, and how often a scandal ends in a confrontation.</summary>
public sealed record RunStats(int Runs, IReadOnlyList<GroupStats> Groups, double ConfrontationsPerSeason, double ConfrontationsRight, double ScandalsConfronted);

public static class Metrics
{
    /// <summary>Which group an act kind belongs to.</summary>
    public static string GroupOf(ActKind k) => k.IsScandal ? "scandal" : k.Valence < 0 ? "bad" : k.Valence > 0 ? "good" : "neutral";

    /// <summary>Reach band for a scandal that spreads well without saturating (design section 10).</summary>
    public const double BandLow = 0.4, BandHigh = 0.7;

    public static RunStats Summarise(IReadOnlyList<SimResult> runs, IReadOnlyList<ActKind> kinds)
    {
        var groups = new List<GroupStats>();
        foreach (string group in new[] { "scandal", "bad", "good", "neutral" })
        {
            var rows = new List<(int Witnesses, double Reach, int Days, int Right, int Wrong, int Unknown, int Holders)>();
            foreach (SimResult r in runs)
            {
                foreach (Act act in r.Acts)
                {
                    ActKind kind = kinds.First(k => k.Name == act.Kind);
                    if (GroupOf(kind) != group || !r.HoldersByDay.TryGetValue(act.Id, out int[]? byDay))
                        continue;
                    int others = r.CastSize - 1;
                    int final = byDay[^1];
                    int firstDay = act.Tick / Simulation.TicksPerDay;
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
        int confrontations = runs.Sum(r => r.Confrontations.Count);
        int scandals = runs.Sum(r => r.Acts.Count(a => kinds.First(k => k.Name == a.Kind).IsScandal));
        return new RunStats(runs.Count, groups,
            confrontations / (double)Math.Max(1, runs.Count),
            runs.Sum(r => r.Confrontations.Count(c => c.Correct)) / (double)Math.Max(1, confrontations),
            confrontations / (double)Math.Max(1, scandals));
    }

    /// <summary>A stable hash of a run's log, for the determinism check (E0).</summary>
    public static string LogHash(SimResult r) => Rng.Hash(r.Log.ToArray()).ToString("x16");
}
