using NpcSchedules;

namespace NpcSchedules.Tests;

/// <summary>Shared helpers: a minimal region map and a way to extract routines from inline schedules.</summary>
public static class TestHelpers
{
    public static RegionMap Regions(int blockMinutes = 120, Dictionary<string, double>? rain = null,
        Dictionary<string, string>? homes = null)
    {
        var map = new RegionMap
        {
            BlockMinutes = blockMinutes,
            RainChance = rain ?? new Dictionary<string, double>
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
            Homes = homes ?? new Dictionary<string, string>(),
        };
        map.Rebuild();
        return map;
    }

    /// <summary>Home-all-day script: every tick at SeedShop (Town). Keeps day-specific assertions
    /// clean, since base days only ever touch the home region.</summary>
    public const string Base = "610 SeedShop 10 10 2/1800 SeedShop 6 6 0";

    public static NpcRoutine Extract(RegionMap regions, Dictionary<string, string> schedules,
        int hearts = 0, Dictionary<string, int>? friends = null, string[]? mail = null, int? seed = 42,
        string npc = "Testy")
    {
        var options = new ExtractorOptions
        {
            Hearts = hearts,
            Friends = friends ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            MailReceived = new HashSet<string>(mail ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            Seed = seed,
        };
        return new RoutineExtractor(regions).Extract(npc, schedules, options);
    }

    public static int RegionTotal(NpcRoutine routine, string region)
        => routine.RegionTicks.TryGetValue(region, out int[]? column) ? column.Sum() : 0;

    public static int BlockTicks(NpcRoutine routine, string region, int block)
        => routine.RegionTicks.TryGetValue(region, out int[]? column) ? column[block] : 0;
}
