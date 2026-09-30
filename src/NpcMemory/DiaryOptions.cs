namespace NpcMemory;

/// <summary>Knobs for the day-end diary notes (docs/spec/diary.md). All public and tunable; never saved.</summary>
public sealed class DiaryOptions
{
    /// <summary>PassedBy: co-located ticks today at or above this.</summary>
    public int PassedByMinTicks { get; set; } = 6;

    /// <summary>PassedBy: hearts with the player at or above this.</summary>
    public int PassedByHearts { get; set; } = 2;

    /// <summary>BirthdayForgotten: hearts at or above this.</summary>
    public int BirthdayHearts { get; set; } = 3;
}
