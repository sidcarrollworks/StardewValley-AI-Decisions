namespace NpcMemory;

/// <summary>
/// A point in game time, expressed in the game's own units: season index (0=spring ..
/// 3=winter), day of month (1..28), and ten-minute tick (0..119). Absolute ordering is
/// provided by <see cref="GameClock.AbsoluteTick"/>, which flattens a single year into
/// 0..13439. The stub models one year; the mod will extend it with a year counter later.
/// </summary>
public readonly record struct GameTime(int SeasonIndex, int DayOfMonth, int Tick);

public static class GameClock
{
    public const int SeasonsPerYear = 4;
    public const int DaysPerSeason = 28;
    public const int TicksPerDay = 120;   // 6:00..2:00, verified in NpcSchedules.TimeUtils
    public const int TicksPerSeason = DaysPerSeason * TicksPerDay; // 3360
    public const int TicksPerYear = SeasonsPerYear * TicksPerSeason; // 13440

    public static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };
    private static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    /// <summary>"spring" -> 0, etc. Returns -1 for an unknown name.</summary>
    public static int SeasonIndex(string season)
        => Array.IndexOf(SeasonNames, season);

    public static string SeasonName(int index)
        => SeasonNames[index];

    /// <summary>Day of week; day 1 of every season is a Monday (verified).</summary>
    public static string DayName(int dayOfMonth)
        => DayNames[(dayOfMonth - 1) % 7];

    /// <summary>Flatten a time to an absolute tick in [0, TicksPerYear).</summary>
    public static int AbsoluteTick(GameTime time)
        => time.SeasonIndex * TicksPerSeason + (time.DayOfMonth - 1) * TicksPerDay + time.Tick;

    public static GameTime FromAbsoluteTick(int tick)
    {
        int clamped = Math.Clamp(tick, 0, TicksPerYear - 1);
        int season = clamped / TicksPerSeason;
        int withinSeason = clamped % TicksPerSeason;
        int day = withinSeason / TicksPerDay + 1;
        int tickOfDay = withinSeason % TicksPerDay;
        return new GameTime(season, day, tickOfDay);
    }

    /// <summary>Non-negative elapsed ticks between two absolute ticks (0 if from >= to).</summary>
    public static int AgeTicks(int fromAbsoluteTick, int toAbsoluteTick)
        => Math.Max(0, toAbsoluteTick - fromAbsoluteTick);
}
