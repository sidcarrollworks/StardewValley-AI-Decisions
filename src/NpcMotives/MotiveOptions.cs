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
    public int MaxAttemptsPerNpcPerDay { get; set; } = 2;
    public int MaxAttemptsPerDay { get; set; } = 6;
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
