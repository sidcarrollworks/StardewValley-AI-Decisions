using NpcSchedules;

namespace NpcMemory.Tests;

/// <summary>Shared helpers for NpcMemory tests.</summary>
public static class TestHelpers
{
    /// <summary>A minimal region map with Town/Mountain/Beach/Forest/Desert and 2-hour blocks.</summary>
    public static RegionMap Regions(int blockMinutes = 120)
    {
        var map = new RegionMap
        {
            BlockMinutes = blockMinutes,
            Regions = new Dictionary<string, string[]>
            {
                ["Town"] = new[] { "Town", "SeedShop" },
                ["Mountain"] = new[] { "Mountain" },
                ["Beach"] = new[] { "Beach" },
                ["Forest"] = new[] { "Forest" },
                ["Desert"] = new[] { "Desert" },
            },
        };
        map.Rebuild();
        return map;
    }

    /// <summary>A tiny routine: 12 ticks in every block of one region, nothing elsewhere.</summary>
    public static NpcRoutine Routine(string npc, string region, int blockMinutes = 120)
    {
        int blockCount = TimeUtils.BlockCount(blockMinutes);
        var column = new int[blockCount];
        Array.Fill(column, TimeUtils.TicksPerBlock(blockMinutes)); // 12 ticks per block
        var routine = new NpcRoutine
        {
            Name = npc,
            BlockMinutes = blockMinutes,
            HomeRegion = region,
            HomeLocation = region,
            RegionTicks = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase) { [region] = column },
        };
        routine.TotalTicks = blockCount * TimeUtils.TicksPerBlock(blockMinutes);
        return routine;
    }

    public static int TickOf(int seasonIndex, int day, int hour, int minute)
    {
        // hour/minute in military time (e.g. 9, 0 -> 0900); tick index like NpcSchedules.TimeUtils
        int time = hour * 100 + minute;
        return NpcSchedules.TimeUtils.TickIndex(time);
    }
}
