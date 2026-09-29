using NpcSchedules;

namespace NpcShadow.Tests;

/// <summary>Shared helpers for NpcShadow tests.</summary>
public static class TestHelpers
{
    /// <summary>Region map with Town/Mountain/Beach/Forest/Desert, 2-hour blocks. Town includes
    /// SeedShop; the others are their own names.</summary>
    public static RegionMap Regions(int blockMinutes = 120)
    {
        var map = new RegionMap
        {
            BlockMinutes = blockMinutes,
            RainChance = new Dictionary<string, double>
            {
                ["spring"] = 0, ["summer"] = 0, ["fall"] = 0, ["winter"] = 0,
            },
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

    /// <summary>A subject who is home (SeedShop/Town) 0600-0850, out in Town 0900-1750,
    /// and home again 1800-2600.</summary>
    public static Dictionary<string, string> AbbySchedules() => new()
    {
        ["spring"] = "610 SeedShop 10 10 2/900 Town 40 20 0/1800 SeedShop 6 6 0",
    };

    public static ExtractorOptions NoFriends() => new() { Hearts = 0 };
}
