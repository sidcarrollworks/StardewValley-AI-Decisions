namespace UnderGlass.Sim;

/// <summary>
/// Placing acts to measure them (design 11b): natural scandals are too rare to measure spread in a
/// season, so the harness injects one per run at a seeded time, by whoever is first able.
/// </summary>
public static class Harness
{
    /// <summary>An actor name for a placed act meaning "whoever is first able": awake, free and
    /// somewhere the act can happen, picked by a seeded draw among them.</summary>
    public const string Anyone = "*";

    /// <summary>One scandal for this seed, from day 1 at a seeded time between 8:00 and 21:00, by
    /// whoever is first able to commit it (<see cref="Anyone"/>).</summary>
    public static (int Tick, string Actor, string Kind) ScandalFor(long seed, IReadOnlyList<ActKind> kinds)
    {
        var scandals = kinds.Where(k => k.IsScandal).OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
        ActKind kind = scandals[Rng.Range(seed, 0, scandals.Count - 1, "inject", "kind")];
        int minute = Clock.MinutesPerDay + Rng.Range(seed, Clock.At(8), Clock.At(21), "inject", "minute");
        return (minute, Anyone, kind.Name);
    }
}
