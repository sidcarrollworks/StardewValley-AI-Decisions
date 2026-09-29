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
/// "Player" (null = never seen); it is the ladder's only knowledge of where the player is.
/// </summary>
public sealed record InitiationInput(
    string Npc,
    LedgerView? PlayerView,
    bool HasPendingIntent,   // the overnight planner gave this NPC a line for today
    int Hearts);             // 0..14, friendship with the player

/// <summary>
/// One thing the ladder would do, or an outcome it recorded. <see cref="Kind"/> is
/// "Attempt", "Ignored" or "Responded".
/// </summary>
public sealed record InitiationEvent(
    int AbsoluteTick,
    string Npc,
    string Kind,
    InitiationStep Step,
    double UrgeBefore,
    double UrgeAfter,
    string Reason);

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

    /// <summary>Urge is multiplied by this when the player responds.</summary>
    public double RespondRelief { get; set; } = 0.5;

    /// <summary>An attempt not responded to within this many ticks is "ignored".</summary>
    public int ResponseWindowTicks { get; set; } = 6;

    /// <summary>Minimum ticks between two attempts by the same NPC.</summary>
    public int CooldownTicks { get; set; } = 6;

    public int MaxAttemptsPerNpcPerDay { get; set; } = 2;

    /// <summary>Across all NPCs.</summary>
    public int MaxAttemptsPerDay { get; set; } = 6;

    /// <summary>Across all NPCs; a week is dayIndex / 7.</summary>
    public int MaxForcedPerWeek { get; set; } = 1;

    /// <summary>Minimum urge per rung, indexed by (int)step.</summary>
    public double[] StepThresholds { get; set; } = { 0.30, 0.45, 0.60, 0.70, 0.80, 0.95 };

    /// <summary>Reserved; not used by the ladder.</summary>
    public double AttemptProbabilityFloor { get; set; } = 0.0;
}
