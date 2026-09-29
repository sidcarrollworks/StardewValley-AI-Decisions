namespace NpcIntents;

/// <summary>Knobs for the overnight-intent planner. All public and tunable.</summary>
public sealed class IntentPlannerOptions
{
    /// <summary>At most this many NPCs get a line on a given morning (brief: cap 1-3).</summary>
    public int MaxNpcsPerDay { get; set; } = 3;

    /// <summary>How many of each NPC's most recent diary entries are candidates for discussion.</summary>
    public int MaxRecentDiaryEntries { get; set; } = 5;

    /// <summary>A yes/no probability at or above this makes the NPC speak.</summary>
    public double SpeakThreshold { get; set; } = 0.5;
}
