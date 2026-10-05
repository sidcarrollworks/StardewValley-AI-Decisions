namespace NpcMemory;

/// <summary>Knobs for which diary entries are forgotten first (<see cref="DiaryKeep"/>). Not saved.</summary>
public sealed class DiaryKeepOptions
{
    /// <summary>A plain sighting ("I saw X here") weighs this much against an event's 1.</summary>
    public double SawWeight { get; set; } = 0.1;

    /// <summary>A sighting within this many ticks of an event about the same person was a
    /// witnessing, and weighs as much as the event.</summary>
    public int WitnessTicks { get; set; } = 6;

    /// <summary>An entry's weight halves every this many days.</summary>
    public double HalfLifeDays { get; set; } = 21;

    /// <summary>Entries from today and the days before it, this many days in all, are never
    /// dropped while older ones remain (the motives, gossip and lines read the last few days).</summary>
    public int ProtectDays { get; set; } = 2;

    /// <summary>Over the cap, trim this share of it at once, so the scoring runs every few dozen
    /// entries rather than on every one.</summary>
    public double BatchShare { get; set; } = 0.1;
}

/// <summary>
/// Which diary entries are forgotten first when a diary is full (Sid, 2026-10-05: "non important
/// information should leave first. 'I saw x here' isn't worth holding onto unless they witness
/// something happen. Entries that are about a person a character has little regard for get thrown
/// out sooner"). Pure and deterministic: an entry's weight is its kind's (a plain sighting is
/// light), halved every <see cref="DiaryKeepOptions.HalfLifeDays"/>, times
/// <c>0.5 + |regard|</c> (capped at 1.5) for the person it is about. Strong feelings either way
/// are remembered.
/// </summary>
public static class DiaryKeep
{
    public static double Score(DiaryEntry e, int nowTick, double regard, bool witnessed, DiaryKeepOptions o)
    {
        double kind = e.Kind == "Saw" && !witnessed ? o.SawWeight : 1;
        double days = Math.Max(0, nowTick - e.AbsoluteTick) / (double)GameClock.TicksPerDay;
        double age = Math.Pow(0.5, days / Math.Max(0.001, o.HalfLifeDays));
        return kind * age * (0.5 + Math.Min(1, Math.Abs(regard)));
    }

    /// <summary>
    /// The entries to keep, in their original order, when <paramref name="entries"/> must shrink
    /// to <paramref name="keep"/>. Entries from the last <see cref="DiaryKeepOptions.ProtectDays"/>
    /// go last; the rest go lowest weight first, the oldest first among equals.
    /// </summary>
    public static List<DiaryEntry> Keep(IReadOnlyList<DiaryEntry> entries, int keep,
        Func<string, double> regardFor, DiaryKeepOptions o)
    {
        int drop = entries.Count - Math.Max(0, keep);
        if (drop <= 0)
            return entries.ToList();

        int now = entries.Max(e => e.AbsoluteTick);
        int protectFrom = (GameClock.DayIndex(now) - (o.ProtectDays - 1)) * GameClock.TicksPerDay;

        // Event ticks per person, for "was this sighting a witnessing?"
        var events = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (DiaryEntry e in entries)
            if (e.Kind != "Saw")
                (events.TryGetValue(e.Subject, out var ticks) ? ticks : events[e.Subject] = new List<int>()).Add(e.AbsoluteTick);
        bool Witnessed(DiaryEntry e)
            => events.TryGetValue(e.Subject, out var ticks) && ticks.Any(t => Math.Abs(t - e.AbsoluteTick) <= o.WitnessTicks);

        var regard = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double RegardOf(string subject)
            => regard.TryGetValue(subject, out double r) ? r : regard[subject] = regardFor(subject);

        IEnumerable<int> order = Enumerable.Range(0, entries.Count)
            .OrderBy(i => entries[i].AbsoluteTick >= protectFrom ? 1 : 0)       // recent days last
            .ThenBy(i => entries[i].AbsoluteTick >= protectFrom
                ? 0 : Score(entries[i], now, RegardOf(entries[i].Subject), Witnessed(entries[i]), o))
            .ThenBy(i => i);                                                     // oldest first
        var dropped = new HashSet<int>(order.Take(drop));
        return Enumerable.Range(0, entries.Count).Where(i => !dropped.Contains(i)).Select(i => entries[i]).ToList();
    }
}
