namespace UnderGlass.Sim;

/// <summary>Who may stand within 8 tiles of a scenario's act as it begins (batch 2 spec 5.2).</summary>
public enum Onlookers
{
    /// <summary>Anyone.</summary>
    Any,
    /// <summary>Only people of chattiness <see cref="Scenario.LonerChattiness"/> or less: a quiet edge (C3).</summary>
    Loners,
    /// <summary>Only the actor's kin and housemates (C6).</summary>
    KinOnly,
}

/// <summary>Something done to a scenario's act a set time after it begins (batch 2 spec 5.3).
/// Built: "sway" (<see cref="Simulation.Sway"/>): the mayor lets the accused off when he decides the
/// case. "settle", "read-out", "post" and "call-in" come with batch 2's reach levers (b2-5, b2-6,
/// b2-12, b2-18), "hush" and "deny" with batch 3; a scenario that asks for one is refused.</summary>
public sealed record FollowUp(int AfterMinutes, string What);

/// <summary>
/// A chosen kind in a chosen scene (batch 2 spec 5.2; research 2.3): placed as
/// <see cref="Harness.ScandalFor"/>'s act is, marked Injected, but only where and when the scene
/// holds, so a check measures reach in the scene it is about (a lone witness, a crowd at a hub, a
/// family at its own shop) instead of wherever a scandal happens to fall.
/// <list type="bullet">
/// <item>Each minute of its window (From to To, each day from Day), when someone is able, it begins
/// with a chance of 1 in <see cref="Simulation.PlacedMeanWait"/>, drawn by its key and the minute;
/// the actor is drawn among the able by the same key. A named actor acts at their first able
/// minute. After GiveUpDays days it is dropped, with a log line.</item>
/// <item>Able: free, of the kind's age, in one of its places (Places, else the kind's Allowed list),
/// with a target if the kind needs one (unless Target names one), and not the keeper of the place at
/// work; between MinInRange and MaxInRange others awake within 8 tiles (as <see cref="Scene.InRange"/>
/// counts them); those others all loners or all kin and housemates, as Onlookers asks; and, with
/// AtHub, standing inside a gathering that is on (with AtFestival, on a festival day).</item>
/// <item>NoTrace: the act leaves no trace, so only those who saw it or are told know of it.</item>
/// <item>Scenarios with the same Key place the same act at the same minute in runs that differ only
/// after it, which is how a check compares one act settled and left alone (C5, with m-3).</item>
/// </list>
/// Amount (S4's debt) comes with batch 2's debts and is refused until then. A run with no scenario is
/// the run it was: nothing here draws or logs.
/// </summary>
public sealed record Scenario(
    string Name,
    string Kind,
    string Actor = Harness.Anyone,
    string? Target = null,
    IReadOnlyList<string>? Places = null,
    int Day = 1, int From = 480, int To = 1260,
    int GiveUpDays = 3,
    int MinInRange = 0, int MaxInRange = int.MaxValue,
    Onlookers Onlookers = Onlookers.Any,
    bool AtHub = false,
    bool AtFestival = false,
    bool NoTrace = false,
    double Amount = 0,
    IReadOnlyList<FollowUp>? Then = null,
    string? Key = null)
{
    /// <summary>A loner, for <see cref="Onlookers.Loners"/>: chattiness this or less.</summary>
    public const double LonerChattiness = 0.4;

    /// <summary>The key its draws use: one key, one act.</summary>
    public string DrawKey => Key ?? Name;
}
