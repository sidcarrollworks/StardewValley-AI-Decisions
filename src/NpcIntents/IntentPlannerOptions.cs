namespace NpcIntents;

/// <summary>Knobs for the overnight-intent planner. All public and tunable.</summary>
public sealed class IntentPlannerOptions
{
    /// <summary>At most this many NPCs get a line on a given morning (brief: cap 1-3).</summary>
    public int MaxNpcsPerDay { get; set; } = 3;

    /// <summary>How many of each NPC's most recent diary entries are candidates for discussion.</summary>
    public int MaxRecentDiaryEntries { get; set; } = 5;

    /// <summary>A yes/no probability at or above this makes the NPC speak. 0.25, not 0.5: the
    /// in-game week and the rewording sweep (sidecar/eval/speak_experiments.md) showed the answer
    /// band is compressed (0.2-0.5 even for a loved birthday gift), so a 0.5 gate randomly vetoed
    /// real news (a quest measured 0.49). The news filter at MinNews 2.0 now decides who has
    /// anything to say; this gate is a hard-veto floor for a genuine model "no".</summary>
    public double SpeakThreshold { get; set; } = 0.25;

    /// <summary>Diary kinds that are never talked about. "TriedToReach" is the initiation ladder's
    /// bookkeeping of its own attempt; what matters is the outcome ("IgnoredBy"), not the attempt.</summary>
    public IReadOnlyCollection<string> SkipKinds { get; set; } = new[] { "TriedToReach" };

    /// <summary>How many days back a delivered (kind, subject) counts as "recently cited" for the
    /// news cite cooldown (intents.md). 0 disables the cooldown.</summary>
    public int CiteCooldownDays { get; set; } = 3;
}
