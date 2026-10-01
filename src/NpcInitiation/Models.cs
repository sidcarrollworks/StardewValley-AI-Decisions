using NpcMemory;

namespace NpcInitiation;

/// <summary>
/// The rungs of the initiation ladder, ordered mildest to strongest. The int value is the rung.
/// </summary>
public enum InitiationStep
{
    Emote = 0,          // an emote above the NPC's head
    Bubble = 1,         // showTextAboveHead
    Approach = 2,       // walk toward the player
    QueuedLine = 3,     // said next time the player talks to them
    Mail = 4,           // a letter the next morning
    ForcedDialogue = 5, // a dialogue box that interrupts the player (rare)
}

/// <summary>
/// What one NPC brings to a tick. <see cref="PlayerView"/> is the NPC's OWN ledger view of
/// "Player" (null = never seen or heard of). <see cref="Lead"/> is the NPC's best answer to
/// "where is the player?" (<see cref="MemoryStore.LookFor"/>: its own sighting, a neighbour's tip,
/// or its habit knowledge). Both come from memory only; the ladder never sees a live position.
/// </summary>
public sealed record InitiationInput(
    string Npc,
    LedgerView? PlayerView,
    bool HasPendingIntent,   // the overnight planner gave this NPC a line for today
    int Hearts,              // 0..14, friendship with the player
    Whereabouts? Lead = null);

/// <summary>
/// One thing the ladder would do, or an outcome it recorded. <see cref="Kind"/> is
/// "Attempt", "Ignored", "Responded", or "Expired" (a queued line the player never came to hear).
/// </summary>
public sealed record InitiationEvent(
    int AbsoluteTick,
    string Npc,
    string Kind,
    InitiationStep Step,
    double UrgeBefore,
    double UrgeAfter,
    string Reason,
    Whereabouts? Lead = null); // for an Approach made from a distance: where the NPC would go and why

/// <summary>One NPC's ladder state as saved (<see cref="InitiationLadder.ReadStates"/>): a
/// read-only copy for display, never fed back into the ladder.</summary>
public sealed record LadderNpcState(
    string Npc,
    double Urge,
    int Rung,
    int Day,
    int AttemptsToday,
    int? LastAttemptTick,
    int? LastContactTick,
    InitiationStep? OpenStep,
    int? OpenTick);

/// <summary>Tuning for the ladder. Never persisted, so changes take effect on old saves.</summary>
public sealed class InitiationOptions
{
    /// <summary>Urge growth per ten-minute tick (only for NPCs that have a view of the player).</summary>
    public double BaseGainPerTick { get; set; } = 0.004;

    /// <summary>Extra urge growth per tick per heart.</summary>
    public double HeartsGainPerTick { get; set; } = 0.0005;

    /// <summary>Added once per day, on the first tick of a day where the NPC has a pending intent.</summary>
    public double IntentBoost { get; set; } = 0.25;

    /// <summary>Urge is multiplied by this when a new day starts.</summary>
    public double OvernightFactor { get; set; } = 0.5;

    /// <summary>Urge drops by this when an attempt is ignored.</summary>
    public double IgnorePenalty { get; set; } = 0.2;

    /// <summary>Urge is multiplied by this when the player talks to the NPC (a response to an
    /// open attempt, or just a conversation).</summary>
    public double RespondRelief { get; set; } = 0.5;

    /// <summary>An emote, bubble, approach or forced dialogue not responded to within this many
    /// ticks (and before the day ends) is "ignored". A queued line waits until the end of the day
    /// and then quietly expires; a letter waits until the end of the next day.</summary>
    public int ResponseWindowTicks { get; set; } = 6;

    /// <summary>Minimum ticks between two attempts by the same NPC, and between a conversation
    /// with the player and the NPC's next attempt.</summary>
    public int CooldownTicks { get; set; } = 6;

    public int MaxAttemptsPerNpcPerDay { get; set; } = 2;

    /// <summary>Across all NPCs and all steps.</summary>
    public int MaxAttemptsPerDay { get; set; } = 6;

    /// <summary>Across all NPCs. Keeps passive steps from using up the daily cap.</summary>
    public int MaxQueuedLinesPerDay { get; set; } = 2;

    /// <summary>Across all NPCs: at most this many letters a day.</summary>
    public int MaxMailPerDay { get; set; } = 1;

    /// <summary>Across all NPCs; a week is dayIndex / 7.</summary>
    public int MaxForcedPerWeek { get; set; } = 1;

    /// <summary>Minimum urge per rung, indexed by (int)step.</summary>
    public double[] StepThresholds { get; set; } = { 0.30, 0.45, 0.60, 0.70, 0.80, 0.95 };

    /// <summary>Whether an ignored attempt writes an IgnoredBy diary line. Off while the ladder
    /// is shadow-mode: the player never saw the attempt, so recording "you ignored me" would be a
    /// lie the planner could later cite (week review, finding 3). One switch for all rungs today;
    /// a per-rung rollout (step 8, one rung at a time) will need a per-step set instead. Urge
    /// penalty and escalation always apply.</summary>
    public bool RecordIgnoredBy { get; set; }
}
