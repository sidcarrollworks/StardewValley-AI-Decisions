namespace NpcIntents;

/// <summary>Knobs for <see cref="Newsworthiness"/>. All public and tunable; never saved.</summary>
public sealed class NewsworthinessOptions
{
    /// <summary>Entries scoring below this are never offered to the model.</summary>
    public double MinNews { get; set; } = 1.0;

    /// <summary>An "unusual" sighting needs the belief's region share in that block under this.</summary>
    public double UnusualShare { get; set; } = 0.15;

    /// <summary>An "unusual" sighting needs at least this much evidence (total counts) in the entry's block.</summary>
    public double UnusualEvidence { get; set; } = 12;

    /// <summary>
    /// Base news weight per diary kind (docs/spec/diary.md kinds table). Kinds with
    /// context-dependent weights are handled inside <see cref="Newsworthiness"/> and need no
    /// entry here: Saw (relationship rules), GiftReceived (by taste, +2 on a birthday),
    /// SawGift (2, or 3 at 6+ hearts), WentLooking (4, or 2 if found). TriedToReach and
    /// HeldAGrudge always score 0 and are also handled there.
    /// </summary>
    public IReadOnlyDictionary<string, double> KindWeights { get; set; } = DefaultWeights();

    /// <summary>The spec's fixed weights, so callers can start from them.</summary>
    public static Dictionary<string, double> DefaultWeights() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["IgnoredBy"] = 3,
        ["Talked"] = 1,
        ["QuestHelped"] = 4,
        ["Festival"] = 2,
        ["MissedFestival"] = 2,
        ["PassedBy"] = 2,
        ["BirthdayForgotten"] = 4,
        ["AcceptedInvite"] = 4,
        ["StoodUp"] = 4,
        ["MissedVisit"] = 3,
        ["Traded"] = 2,
        ["StartedDating"] = 5,
        ["Engaged"] = 5,
        ["Married"] = 5,
        ["Divorced"] = 5,
        ["Anniversary"] = 5,
        ["ChattedWith"] = 1,
        ["MetUpWith"] = 1,
        ["LookedFor"] = 1,
    };
}
