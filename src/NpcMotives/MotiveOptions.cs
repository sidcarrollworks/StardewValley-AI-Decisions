namespace NpcMotives;

/// <summary>
/// Tuning for the motives engine (docs/spec/motives.md, "Tuning constants"). Code defaults, never
/// saved, so a change applies to old saves. Every number is a first guess to tune from the
/// playtest log.
/// </summary>
public sealed class MotiveOptions
{
    // ---- stresses ------------------------------------------------------------------------------
    public double HearsayFactor { get; set; } = 0.5;
    /// <summary>A story weighs up to this much more on a listener drawn to someone in it
    /// (ledger-gossip.md, "Relevance"): the player at <see cref="DrawnHearts"/> or more.</summary>
    public double MaxRelevance { get; set; } = 2;
    public int DrawnHearts { get; set; } = 8;
    /// <summary>Hearsay seen first-hand within this many days of hearing it becomes lasting.</summary>
    public int ConfirmWindowDays { get; set; } = 7;
    public int ElasticWindowDays { get; set; } = 3;
    public int YieldCount { get; set; } = 3;
    public int YieldWindowDays { get; set; } = 5;
    public double YieldPlastic { get; set; } = 0.3;
    public double SevereMagnitude { get; set; } = 0.7;

    // ---- regard --------------------------------------------------------------------------------
    public double RegardHealRate { get; set; } = 0.03;
    public double RegardFadeRate { get; set; } = 0.005;
    public double FamiliarityPerHeart { get; set; } = 0.03;
    public double FamiliarityPerPositiveRegard { get; set; } = 0.2;
    public double HostileFamiliarityPerGrudge { get; set; } = 0.15;

    /// <summary>Per-character retention (motives.md, "Regard"): 0.5 for everyone not listed. Pam
    /// forgets most slights (Sid, 2026-10-01). Moves to temperament-overrides.json when the
    /// extractor accepts the key (temperament.md, "retention").</summary>
    public Dictionary<string, double> Retention { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pam"] = 0.2,
    };

    // ---- mood ----------------------------------------------------------------------------------
    public double RollWeight { get; set; } = 0.3;
    public double RollSkew { get; set; } = 0.3;
    public double TailChance { get; set; } = 1.0 / 40.0;
    public double MoodSway { get; set; } = 0.15;
    public double MoodTilt { get; set; } = 0.10;

    // ---- acts ----------------------------------------------------------------------------------
    public Dictionary<Act, double> ActCost { get; set; } = new()
    {
        [Act.Emote] = 0.20,
        [Act.Letter] = 0.25,
        [Act.QueuedLine] = 0.30,
        [Act.AskForHelp] = 0.30,
        [Act.Bubble] = 0.35,
        [Act.FarmVisit] = 0.45,
        [Act.WalkUp] = 0.50,
        [Act.Visit] = 0.70,
        [Act.Interrupt] = 0.80,
    };

    /// <summary>The weakest motive (its intensity, frustration included) worth each act: boldness
    /// decides who dares, this decides whether the reason is big enough for the act (Sid,
    /// 2026-10-02, after Haley's letter over "misses you 0.10"). First guesses, to tune.</summary>
    public Dictionary<Act, double> ActMinStrength { get; set; } = new()
    {
        [Act.Emote] = 0.0,
        [Act.Bubble] = 0.1,
        [Act.QueuedLine] = 0.15,
        [Act.WalkUp] = 0.2,
        [Act.Letter] = 0.3,
        [Act.AskForHelp] = 0.3,
        [Act.FarmVisit] = 0.4,
        [Act.Visit] = 0.4,
        [Act.Interrupt] = 0.6,
    };

    /// <summary>A light act: an emote (a wave or a glare), or a bubble whose motive is only a
    /// greeting. It doesn't ask for the player's time, so it uses none of the daily attempts and
    /// isn't held back by the strong reserve (Sid, 2026-10-02).</summary>
    public static bool IsLight(Act act, Motive motive) => act == Act.Emote || (act == Act.Bubble && motive == Motive.Greeting);

    /// <summary>A villager who misses the player, worries or has news asks the people around it
    /// where the player is at this strength and above (<see cref="MotiveDrive.Seeking"/>; the
    /// urge ladder used urge 0.45 before it was retired, D31).</summary>
    public double AskAroundStrength { get; set; } = 0.2;

    // ---- history at install (docs/spec/vanilla-sources.md; RegardHistory) --------------------
    /// <summary>Gifts and heart events from before the mod count half: their age is unknown, and
    /// live regard would have faded since.</summary>
    public double HistoryFade { get; set; } = 0.5;
    /// <summary>History's warmth approaches this and never passes it, so play still has room.</summary>
    public double HistoryMaxWarmth { get; set; } = 0.8;
    /// <summary>History's grudge stops here, below <see cref="GrudgeThreshold"/>: nobody arrives
    /// already past the friendship penalty.</summary>
    public double HistoryMaxGrudge { get; set; } = 0.5;
    public double HistoryDatingFloor { get; set; } = 0.3;
    public double HistoryEngagedFloor { get; set; } = 0.4;
    public double HistoryMarriedFloor { get; set; } = 0.5;
    public double HistoryDivorcedGrudge { get; set; } = 0.4;

    public double MinStrengthFor(Act act) => ActMinStrength.TryGetValue(act, out double m) ? m : 0;

    /// <summary>Greeting strength before warmth: someone familiar (2+ hearts, or regard 0.2+), and
    /// an acquaintance (met, fewer hearts). An acquaintance's greeting stays below a bubble's
    /// minimum, so only an emote: bold villagers wave, shy ones mostly don't (Sid, 2026-10-02).</summary>
    public double GreetingFamiliar { get; set; } = 0.15;
    public double GreetingAcquaintance { get; set; } = 0.08;

    public double HostileSurcharge { get; set; } = 0.30;
    public double IntensityWeight { get; set; } = 0.5;
    public double ClearBand { get; set; } = 0.15;
    public double AvoidLevel { get; set; } = 0.3;
    public double FrustrationStep { get; set; } = 0.1;
    public double VentRelief { get; set; } = 0.5;
    public int StrongReserve { get; set; } = 2;
    public double StrongIntensity { get; set; } = 0.5;
    public double MinMotiveForAction { get; set; } = 0.05;
    public double MinMotiveForChoice { get; set; } = 0.2;

    /// <summary>At most this many motives go to the model's "which first?" question.</summary>
    public int MaxChoiceOptions { get; set; } = 5;

    // ---- pacing (the ladder's caps, kept: motives.md, "Which motive goes first") ---------------
    // Light acts (an emote, or a bubble that only greets: IsLight) count toward none of these:
    // a wave doesn't ask for the player's time (Sid, 2026-10-02). They have their own cap per NPC.
    public int MaxAttemptsPerNpcPerDay { get; set; } = 2;

    /// <summary>The town's daily attempts that ask for the player's attention. 12, up from 6 once
    /// waves stopped counting (Sid, 2026-10-02: raise it, lower it after testing if need be).</summary>
    public int MaxAttemptsPerDay { get; set; } = 12;
    public int MaxLightActsPerNpcPerDay { get; set; } = 2;
    public int MaxQueuedLinesPerDay { get; set; } = 2;

    /// <summary>Letters and requests by letter, across all NPCs.</summary>
    public int MaxLettersPerDay { get; set; } = 1;
    public int MaxInterruptsPerWeek { get; set; } = 1;

    /// <summary>Unannounced visits, across all NPCs (find.md: 1 to 2 a week).</summary>
    public int MaxVisitsPerWeek { get; set; } = 2;

    /// <summary>Minimum ticks between two attempts by one NPC, between a talk with the player and
    /// the NPC's next attempt, and before a motive that was passed over is weighed again (the same
    /// inputs would give the same answer, so asking the model every tick would waste calls).</summary>
    public int CooldownTicks { get; set; } = 6;

    /// <summary>An in-person attempt not answered within this many ticks (and before the day
    /// ends) is ignored. A queued line waits for the rest of the day, a letter until the end of
    /// the next day.</summary>
    public int ResponseWindowTicks { get; set; } = 6;

    // ---- grudge --------------------------------------------------------------------------------
    public double GrudgeThreshold { get; set; } = 0.75;
    public int FriendshipPenalty { get; set; } = 20;
    public int PenaltyCooldownDays { get; set; } = 7;

    /// <summary>After a friendship penalty regard moves up by this, so it takes more bad acts to
    /// repeat it (motives.md, "Grudge and friendship loss").</summary>
    public double GrudgeRelief { get; set; } = 0.3;

    public double RetentionOf(string npc)
        => Retention.TryGetValue(npc, out double r) ? Math.Clamp(r, 0, 1) : 0.5;
}
