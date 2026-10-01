namespace NpcMinds;

/// <summary>
/// Everything the NPC Minds viewer shows for one moment of the game (docs/spec/debug-tools.md,
/// "Live viewer"). Built on the game thread from memory, the ladder's last finished state and
/// the plan; immutable afterwards, so the server thread can serialize it without locks. It is a
/// display copy: nothing in it is ever read back into a decision.
/// </summary>
public sealed record MindsSnapshot(
    long Seq,                                  // rises with every published snapshot
    string Date,                               // "spring 5, year 1"
    string Time,                               // "2:30 pm"
    int AbsoluteTick,
    string Backend,                            // "Laya", "Fake", "Varied"
    bool ModelUp,
    MindsStats Stats,
    string PlanState,                          // "none", "running", "ready"
    IReadOnlyList<double> StepThresholds,      // ladder rung thresholds, for the urge bars
    IReadOnlyList<string> StepNames,
    IReadOnlyList<NpcMind> Npcs,               // name order
    IReadOnlyList<PlannedLine> PlanToday,      // the lines collected for today
    IReadOnlyList<FeedItem> Feed)              // newest first
{
    /// <summary>What the viewer shows before a save is loaded.</summary>
    public static MindsSnapshot Idle(long seq, string backend) => new(
        seq, "", "", 0, backend, false, MindsStats.None, "none",
        Array.Empty<double>(), Array.Empty<string>(), Array.Empty<NpcMind>(),
        Array.Empty<PlannedLine>(), Array.Empty<FeedItem>());
}

/// <summary>The heartbeat numbers: ladder backlog and drops, model calls, fallbacks, latency
/// (-1 where the backend does not report it).</summary>
public sealed record MindsStats(int Backlog, int Dropped, long Calls, long Fallbacks, double MedianMs, double P95Ms)
{
    public static readonly MindsStats None = new(0, 0, -1, -1, -1, -1);
}

/// <summary>One NPC's mind: ladder state, what it knows of the player, and what it might say.</summary>
public sealed record NpcMind(
    string Name,
    int Hearts,
    double Urge,
    int Rung,
    string RungName,
    double? NextThreshold,          // the urge the current rung needs; null past the top
    int AttemptsToday,
    string? OpenStep,               // an attempt still waiting for the player
    int? OpenForTicks,
    bool IntentToday,               // the overnight plan gave it a line for today
    string? PlannedLine,
    LastSeenView? LastSeen,         // its own ledger view of the player
    LeadView? Lead,                 // where it would look for the player
    IReadOnlyList<NewsPick> NewsTonight, // today's most newsworthy entries: tonight's likely picks
    IReadOnlyList<DiaryLine> Diary, // newest first
    int DiaryCount,
    TemperamentView? Temperament = null); // the seed temperament (PR #11); null when not loaded

/// <summary>A character's seed temperament for display (docs/spec/temperament.md): six behaviour
/// traits and six emotion biases, 0..1 with 0.5 typical, plus the game's own Data/Characters words
/// and a few plain words for the strongest leanings. <see cref="Seeded"/> is false for a character
/// with no row in the table (all 0.5).</summary>
public sealed record TemperamentView(
    IReadOnlyList<TraitValue> Traits,
    IReadOnlyList<TraitValue> Emotions,
    string Summary,
    string? GameTraits,
    bool Seeded);

public sealed record TraitValue(string Name, double Value);

public sealed record LastSeenView(string Detail, string? Place, int AgeTicks, int Hops, string? ToldBy, string Summary);

public sealed record LeadView(string Source, string? Place, string Summary);

public sealed record NewsPick(string Sentence, double Score);

public sealed record DiaryLine(int AbsoluteTick, string When, string Kind, string Text);

public sealed record PlannedLine(string Npc, string Line, string Reason, double News);

/// <summary>One thing that happened, for the viewer's event feed: a ladder attempt or outcome,
/// an NPC asking around, a planned line. <see cref="Kind"/> is a short tag the page colors by.</summary>
public sealed record FeedItem(long Seq, int AbsoluteTick, string When, string Kind, string? Npc, string Text);

/// <summary>One model call as the viewer shows it: what was asked, what came back, how long it
/// took, and whether the answer was the deterministic fallback.</summary>
public sealed record DecisionCall(
    long Seq,
    DateTime AtUtc,
    string Caller,                  // "ladder" or "plan"
    string Type,                    // "yesno", "choice", "score", "batch"
    string? Npc,
    string Question,
    IReadOnlyList<CallAnswer> Answers,
    double Ms,
    bool FellBack,
    string ContextHead);            // the first part of the state the model read

public sealed record CallAnswer(string Label, double Value);
