namespace NpcSchedules;

/// <summary>
/// Time math for the Stardew day. The clock advances <c>timeOfDay</c> by 10 per ten-minute
/// tick, minutes wrap at 60, and the day ends when the value reaches 2600 (2:00 AM). That
/// gives exactly 120 ticks per day: tick <c>k</c> (0..119) is
/// <c>600 + (k / 6) * 100 + (k % 6) * 10</c>, i.e. hours 6..25 with minutes 0..50.
/// Verified in Game1.cs <c>performTenMinuteClockUpdate</c> (decompiled 1.6).
/// </summary>
public static class TimeUtils
{
    public const int TicksPerDay = 120;      // 20 hours x 6 ten-minute ticks
    public const int DayStart = 600;         // 6:00 AM
    public const int DayEnd = 2600;          // 2:00 AM, the cap; never a live tick

    /// <summary>Tick index for a time of day value, or -1 if the time is outside the live day.</summary>
    public static int TickIndex(int timeOfDay)
    {
        if (timeOfDay < DayStart || timeOfDay >= DayEnd)
            return -1;
        int hour = timeOfDay / 100;
        int minute = timeOfDay % 100;
        // the game clock only ever lands on minutes 0, 10, ..., 50; minutes never reach 60
        if (minute >= 60 || minute % 10 != 0)
            return -1;
        return (hour - 6) * 6 + minute / 10;
    }

    /// <summary>Time of day for a tick index.</summary>
    public static int TimeOfDay(int tick)
        => DayStart + (tick / 6) * 100 + (tick % 6) * 10;

    /// <summary>Number of 10-minute ticks in a block of <paramref name="blockMinutes"/> game minutes.</summary>
    public static int TicksPerBlock(int blockMinutes)
        => blockMinutes / 10;

    /// <summary>Number of blocks in a day for the given block size.</summary>
    public static int BlockCount(int blockMinutes)
        => TicksPerDay * 10 / blockMinutes;

    /// <summary>Block index containing a tick.</summary>
    public static int BlockIndex(int tick, int blockMinutes)
        => tick / TicksPerBlock(blockMinutes);

    /// <summary>Label for a block, e.g. "0600" for the 6:00-8:00 block.</summary>
    public static string BlockLabel(int block, int blockMinutes)
        => TimeOfDay(block * TicksPerBlock(blockMinutes)).ToString("0000");
}
