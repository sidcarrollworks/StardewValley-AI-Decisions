namespace UnderGlass.Sim;

/// <summary>What the nights record notes (D54): setting out for bed, a routine checkpoint, falling
/// asleep, waking, and a choice made from nothing (after waking, among others).</summary>
public enum NightEvent { SetOut, Checkpoint, Slept, Woke, Chose }

/// <summary>One option of a free-time draw and its weight: "home", "bed", "haunt:&lt;place&gt;" or
/// "hub:&lt;name&gt;".</summary>
public sealed record NightOption(string Option, double Weight);

/// <summary>
/// One note for the nights record, as the simulation writes it. Why: for SetOut, "habit", "body" or
/// "held"; for Checkpoint, the pick ("bed", "home", "haunt:&lt;place&gt;" or "hub:&lt;name&gt;"); for Slept,
/// "bed", "collapse" or "start" (the run's first sleep); for Woke, "rested" or "alarm"; for Chose, the
/// goal ("home", "haunt", "work", "errand" ...). Place: the goal's place, or where they slept or woke.
/// HabitAt: the habitual bedtime due (absolute minute), -1 for none. Options: the free-time draw made
/// then, null when none was made (work, an errand, the body's bedtime).
/// </summary>
public sealed record NightNote(int Tick, string Person, NightEvent What, string? Why = null, string? Place = null,
    string? Hub = null, double Energy = 0, int HabitAt = -1, IReadOnlyList<NightOption>? Options = null);

/// <summary>The nights record's sink. The simulation writes to it and never reads from it: every
/// member returns nothing.</summary>
public interface INightSink
{
    void Note(NightNote note);
}

/// <summary>A sink that keeps every note, in the order written.</summary>
public sealed class NightList : INightSink
{
    public List<NightNote> Notes { get; } = new();

    public void Note(NightNote note) => Notes.Add(note);
}

/// <summary>A choice made from nothing: its minute, goal, place and hub, and the options drawn from
/// (null when it was no free-time draw).</summary>
public sealed record NightChoice(int At, string Why, string Place, string? Hub, IReadOnlyList<NightOption>? Options);

/// <summary>
/// One sleep, as the recording's "nights" rows hold it. Night: noon to noon, (SleptAt - 720) / 1440
/// rounded down, so the run's first sleep is night -1. HabitAt and Habit: that night's habitual
/// bedtime (an absolute minute) and its index in the person's list, from their card: the kept hour
/// covering the night's own day, which always falls in that night (<see cref="KeptHour.At"/>), on
/// every night it covers, whoever sent them to bed; null without one. SetOutAt and SetOutWhy: the last
/// time they set out for bed before falling asleep and why ("habit", "body" or "held"); null when they
/// didn't (the run's start, a collapse where they stood). CheckpointAt and CheckpointPick: that
/// night's routine checkpoint and what it picked; null without one. SleptHow: "bed", "collapse" or
/// "start". Energies are absolute, to three places, out of MaxEnergy. WokeAt, WokeWhy, WokeEnergy and
/// Next are null while they were still asleep when the run ended. Next: their first choice after
/// waking. CheckpointEnergy and CheckpointOptions: their energy at the checkpoint and the options its
/// draw was made from; SetOutEnergy and SetOutOptions: the same when they set out (no options when
/// no free-time draw sent them: the body, being held). Null where the checkpoint or set-out is.
/// </summary>
public sealed record NightRow(string Person, int Night, int? HabitAt, int? Habit, int? SetOutAt, string? SetOutWhy,
    int? CheckpointAt, string? CheckpointPick, int SleptAt, string SleptPlace, string SleptHow, double SleptEnergy,
    double MaxEnergy, int? WokeAt, string? WokeWhy, double? WokeEnergy, NightChoice? Next,
    double? CheckpointEnergy = null, IReadOnlyList<NightOption>? CheckpointOptions = null,
    double? SetOutEnergy = null, IReadOnlyList<NightOption>? SetOutOptions = null);

/// <summary>A kept bedtime as the recording writes it: Standing "sourced" when it has a source,
/// "ours" when not.</summary>
public sealed record NightKeptHour(int At, IReadOnlyList<int> Weekdays, IReadOnlyList<int> Seasons, string? Source, string Standing);

/// <summary>A person's card data the rows refer to: their body, their own hub weights and their
/// habitual bedtimes (null when none).</summary>
public sealed record NightRoutine(string Person, double MaxEnergy, double BedAt, IReadOnlyDictionary<string, double>? HubWeights,
    IReadOnlyList<NightKeptHour>? Bedtimes);

/// <summary>
/// The nights record, worked out from a run's notes (pure): one row per sleep, in person (name) order,
/// then time. Additive in the recording ("nights"); a recording without it has the nights unrecorded,
/// not sleepless.
/// </summary>
public static class NightTable
{
    /// <summary>Raised when the rows' shape changes: 2 added the checkpoint's and the set-out's energy
    /// and options.</summary>
    public const int Version = 2;

    /// <summary>The noon-to-noon night a minute falls in.</summary>
    public static int NightOf(int minute) => (int)Math.Floor((minute - Clock.MinutesPerDay / 2) / (double)Clock.MinutesPerDay);

    public static IReadOnlyList<NightRoutine> Routines(IReadOnlyList<Villager> cast)
        => cast.OrderBy(v => v.Name, StringComparer.Ordinal).Select(v => new NightRoutine(v.Name, v.Body.MaxEnergy, v.Body.BedAt,
            v.HubWeights is null ? null : new SortedDictionary<string, double>(v.HubWeights.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal),
            v.Bedtimes?.Select(k => new NightKeptHour(k.At, k.Weekdays, k.Seasons, k.Source, k.Source is null ? "ours" : "sourced")).ToList())).ToList();

    public static IReadOnlyList<NightRow> Rows(IReadOnlyList<NightNote> notes, IReadOnlyList<Villager> cast)
    {
        var byName = cast.ToDictionary(v => v.Name);
        var rows = new List<NightRow>();
        foreach (var person in notes.GroupBy(n => n.Person).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Villager? v = byName.GetValueOrDefault(person.Key);
            double max = v?.Body.MaxEnergy ?? 0;
            NightNote? setOut = null, checkpoint = null;
            int open = -1;          // the row of their latest sleep
            bool awaitingNext = false;
            foreach (NightNote n in person)
            {
                switch (n.What)
                {
                    case NightEvent.SetOut:
                        setOut = n;
                        break;
                    case NightEvent.Checkpoint:
                        checkpoint = n;
                        break;
                    case NightEvent.Slept:
                        int night = NightOf(n.Tick);
                        var habit = KeptHour.For(v?.Bedtimes, night);
                        rows.Add(new NightRow(person.Key, night,
                            habit is { } h ? night * Clock.MinutesPerDay + h.Hour.At : null, habit?.Index,
                            setOut?.Tick, setOut?.Why, checkpoint?.Tick, checkpoint?.Why,
                            n.Tick, n.Place ?? "", n.Why ?? "bed", R(n.Energy), max, null, null, null, null,
                            checkpoint is null ? null : R(checkpoint.Energy), checkpoint?.Options,
                            setOut is null ? null : R(setOut.Energy), setOut?.Options));
                        open = rows.Count - 1;
                        setOut = checkpoint = null;
                        awaitingNext = false;
                        break;
                    case NightEvent.Woke when open >= 0:
                        rows[open] = rows[open] with { WokeAt = n.Tick, WokeWhy = n.Why, WokeEnergy = R(n.Energy) };
                        awaitingNext = true;
                        break;
                    case NightEvent.Chose when awaitingNext:
                        rows[open] = rows[open] with { Next = new NightChoice(n.Tick, n.Why ?? "", n.Place ?? "", n.Hub, n.Options) };
                        awaitingNext = false;
                        break;
                }
            }
        }
        return rows;
    }

    private static double R(double x) => Math.Round(x, 3);
}
