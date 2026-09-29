namespace NpcMemory;

/// <summary>
/// A point in game time, expressed in the game's own units: season index (0=spring ..
/// 3=winter), day of month (1..28), ten-minute tick (0..119), and year (1-based, like
/// <c>Game1.year</c>). Absolute ordering is provided by <see cref="GameClock.AbsoluteTick"/>,
/// which counts from spring 1 of year 1 and keeps increasing across years, so a year-1 memory
/// never reads as fresh in year 2. <paramref name="Year"/> defaults to 1 so year-less callers
/// (tests, the shadow harness) keep their old tick values.
/// </summary>
public readonly record struct GameTime(int SeasonIndex, int DayOfMonth, int Tick, int Year = 1);

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

    /// <summary>Flatten a time to an absolute tick counted from spring 1, year 1 (tick 0).
    /// A year below 1 is treated as year 1.</summary>
    public static int AbsoluteTick(GameTime time)
        => (Math.Max(1, time.Year) - 1) * TicksPerYear
         + time.SeasonIndex * TicksPerSeason
         + (time.DayOfMonth - 1) * TicksPerDay
         + time.Tick;

    public static GameTime FromAbsoluteTick(int tick)
    {
        int clamped = Math.Max(0, tick);
        int year = clamped / TicksPerYear + 1;
        int withinYear = clamped % TicksPerYear;
        int season = withinYear / TicksPerSeason;
        int withinSeason = withinYear % TicksPerSeason;
        int day = withinSeason / TicksPerDay + 1;
        int tickOfDay = withinSeason % TicksPerDay;
        return new GameTime(season, day, tickOfDay, year);
    }

    /// <summary>The calendar day an absolute tick falls on (0 = spring 1, year 1). A game day runs
    /// 6:00 to 2:00, so everything before the next 6:00 belongs to the same day.</summary>
    public static int DayIndex(int absoluteTick)
        => Math.Max(0, absoluteTick) / TicksPerDay;

    /// <summary>The first tick (6:00) of the calendar day <paramref name="dayIndex"/>.</summary>
    public static int DayStartTick(int dayIndex)
        => Math.Max(0, dayIndex) * TicksPerDay;

    /// <summary>Whole calendar days from one tick to another (0 = same day; 1 = the next day).</summary>
    public static int DaysBetween(int fromAbsoluteTick, int toAbsoluteTick)
        => Math.Max(0, DayIndex(toAbsoluteTick) - DayIndex(fromAbsoluteTick));

    /// <summary>Non-negative elapsed ticks between two absolute ticks (0 if from >= to).</summary>
    public static int AgeTicks(int fromAbsoluteTick, int toAbsoluteTick)
        => Math.Max(0, toAbsoluteTick - fromAbsoluteTick);
}
