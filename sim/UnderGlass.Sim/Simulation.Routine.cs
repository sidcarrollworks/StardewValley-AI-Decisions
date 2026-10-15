namespace UnderGlass.Sim;

/// <summary>
/// Routines as character data (D54; Sid, 2026-10-14: "bedtimes are habits, not hard rules").
/// A person's own weight for a named hub (<see cref="Villager.HubWeights"/>) stands in for the hub's,
/// and 0 or less leaves the hub out of their choices. A habitual bedtime (<see cref="Villager.Bedtimes"/>)
/// adds going to bed to their free-time choices, weighted <see cref="HabitOptions.BedtimeHabitWeight"/>,
/// from the hour until they next sleep; at the first free-time decision after the hour, a pick in
/// progress is reconsidered once. Nothing here sets "tired", holds anyone asleep or makes them less
/// available: sleep starts on reaching the bed and ends when the bar is full (design rule 1), and
/// <see cref="Free"/>, reach and asking never read a schedule.
///
/// The nights record (<see cref="INightSink"/>) is written here too: each set-out for bed and why,
/// each routine checkpoint, each sleep and waking, and each choice made from nothing. The simulation
/// only writes to it: no draw, no log line and no order depends on it, so a run is the same with it
/// on or off.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Going to bed as an option in a free-time draw; never stored as anyone's haunt.</summary>
    private static readonly Haunt BedtimeHabit = new("bed", default, 0, 0, 0);

    private INightSink? _nights;
    // The options of the free-time draw just made, kept only while the nights record is on.
    private List<NightOption>? _drawn;

    /// <summary>The minute of the habitual bedtime now due, or null: tonight's kept hour (the night,
    /// noon to noon, that this minute falls in), or else last night's, if it has come and they haven't
    /// woken since. A kept hour always falls in its own day's night (<see cref="KeptHour.At"/>). Null
    /// without bedtimes.</summary>
    private static int? HabitDue(Person p, int m)
    {
        if (p.V.Bedtimes is not { Count: > 0 } list)
            return null;
        int night = NightTable.NightOf(m);
        for (int d = night; d >= Math.Max(0, night - 1); d--)
        {
            if (KeptHour.For(list, d) is not { } h)
                continue;
            int k = d * Clock.MinutesPerDay + h.Hour.At;
            if (k > m)
                continue; // not yet tonight: last night's may still be due
            return p.LastWoke < k ? k : null;
        }
        return null;
    }

    /// <summary>At the first free-time decision at or after a habitual bedtime, once a night: the pick
    /// in progress is set aside and the ordinary draw is made again, with going to bed among the options.</summary>
    private static bool BedtimeCheckpoint(Person p, int m)
    {
        if (HabitDue(p, m) is not { } k || p.HabitChecked == k)
            return false;
        p.HabitChecked = k;
        return true;
    }

    /// <summary>Adds going to bed to a free-time draw when a habitual bedtime is due, last, so every
    /// other option keeps its place; with the nights record on, keeps the options drawn from.</summary>
    private void OfferBed(Person p, int m, List<(Haunt? Haunt, double Weight)> options)
    {
        if (HabitDue(p, m) is not null)
            options.Add((BedtimeHabit, _ho.BedtimeHabitWeight));
        if (_nights is not null)
            _drawn = options.Select(o => new NightOption(OptionName(o.Haunt), Math.Round(o.Weight, 3))).ToList();
    }

    private static string OptionName(Haunt? h)
        => h is null ? "home" : ReferenceEquals(h, BedtimeHabit) ? "bed" : h.Hub is { } hub ? "hub:" + hub : "haunt:" + h.Place;

    /// <summary>Sets out for bed: home to bed, or, with no home in this world, sleep where they stand.</summary>
    private static void GoToBed(Person p, bool hasHome)
    {
        if (hasHome)
            Goal(p, p.V.Home, DefaultTown.Bed, int.MaxValue, "bed", null);
        else
            Goal(p, p.Place, p.At, int.MaxValue, "bed", null);
    }

    /// <summary>Their own weight for this hub is 0 or less: it is left out of their choices (not drawn at 0).</summary>
    private static bool NeverPicks(Person p, Gathering g)
        => p.V.HubWeights is { } own && own.TryGetValue(g.Name, out double w) && w <= 0;

    /// <summary>The body asks for bed: tiredness felt has reached their bedtime, less the margin for
    /// getting home (design rule 1). Bodies with a negative BedAt never feel it.</summary>
    private bool BodyTired(Person p, int t)
    {
        Body body = p.V.Body;
        return body.BedAt >= 0 && _bo.Tiredness(p.Energy / body.MaxEnergy, t) >= 1 - body.BedAt - _bo.HeadHomeMargin;
    }

    // ---- the nights record (write-only) ----------------------------------------------------------

    /// <summary>Records every sleep's night to <paramref name="sink"/> (null: none, the default). Call
    /// before the run; the run is the same either way.</summary>
    public void ConfigureNights(INightSink? sink)
    {
        if (_sleeps.Count > 0)
            throw new InvalidOperationException("Configure the nights record before running the simulation.");
        _nights = sink;
    }

    /// <summary><see cref="Decide"/>, noting for the nights record what it decided.</summary>
    private void DecideNoting(Person p, int m)
    {
        if (_nights is null)
        {
            Decide(p, m);
            return;
        }
        string before = p.Why;
        int checkedBefore = p.HabitChecked;
        bool held = p.DetainedUntil > m;
        _drawn = null;
        Decide(p, m);
        string name = p.V.Name;
        if (p.HabitChecked != checkedBefore)
            _nights.Note(new NightNote(m, name, NightEvent.Checkpoint, PickOf(p), p.GoalPlace, p.Haunt?.Hub, p.Energy, p.HabitChecked, _drawn));
        if (before != "bed" && p.Why == "bed")
            _nights.Note(new NightNote(m, name, NightEvent.SetOut, held ? "held" : BodyTired(p, Clock.OfDay(m)) ? "body" : "habit",
                p.GoalPlace, null, p.Energy, HabitDue(p, m) ?? -1, _drawn));
        if (before == "" && p.Why != "")
            _nights.Note(new NightNote(m, name, NightEvent.Chose, p.Why, p.GoalPlace, p.Haunt?.Hub, p.Energy, -1, _drawn));
        _drawn = null;
    }

    private static string PickOf(Person p) => p.Why == "haunt" ? OptionName(p.Haunt) : p.Why;

    private void NoteSlept(Person p, int m, string how)
        => _nights?.Note(new NightNote(m, p.V.Name, NightEvent.Slept, how, p.Place, null, p.Energy));

    private void NoteWoke(Person p, int m, string why)
        => _nights?.Note(new NightNote(m, p.V.Name, NightEvent.Woke, why, p.Place, null, p.Energy));
}
