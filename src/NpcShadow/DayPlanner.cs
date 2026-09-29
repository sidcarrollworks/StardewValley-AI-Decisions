using NpcSchedules;

namespace NpcShadow;

/// <summary>
/// Resolves an NPC's region and location for every tick of one day, mirroring the game's
/// key-picking and script semantics through NpcSchedules.ScheduleSimulator.
/// </summary>
public sealed class DayPlanner
{
    private readonly RegionMap _regions;

    public DayPlanner(RegionMap regions)
    {
        _regions = regions;
    }

    /// <summary>
    /// Resolve one day for one NPC. `schedules` is the raw key -> script dictionary (a flat
    /// JSON object). Home location comes from regions.Homes override, else
    /// NpcSchedules.RoutineExtractor.InferHomeLocation, else "" (home region then Other).
    /// </summary>
    public DayPlan Resolve(string npc, Dictionary<string, string> schedules, string season, int day,
        ExtractorOptions options, int seed)
    {
        // One seeded RNG drives both the rain roll and the key pick, so a day is reproducible.
        var rng = new Random(seed);
        bool rainy = _regions.RainChance.TryGetValue(season, out double rainChance)
                     && rainChance > 0
                     && rng.NextDouble() < rainChance;

        var warnings = new List<string>();
        var simulator = new ScheduleSimulator(npc, schedules, options, rng, warnings);

        string? key = simulator.PickDayKey(season, day, rainy, out _);
        ParsedScript? parsed = key == null ? null : simulator.Evaluate(key, season, new List<string>());

        // home: regions.json override, else the location inferred from the schedules, else nowhere
        string homeLocation = _regions.HomeOverride(npc) ?? RoutineExtractor.InferHomeLocation(schedules) ?? "";
        string homeRegion = _regions.RegionFor(homeLocation) ?? RegionMap.OtherRegion;

        var regionByTick = new string[TimeUtils.TicksPerDay];
        var locationByTick = new string[TimeUtils.TicksPerDay];
        string keyChain;

        if (key == null || parsed == null || parsed.NoSchedule || parsed.ParseFailed || parsed.Points.Count == 0)
        {
            // the game leaves the NPC standing at home for the whole day
            Array.Fill(regionByTick, homeRegion);
            Array.Fill(locationByTick, homeLocation);
            keyChain = key == null ? "<no schedule>" : (parsed?.KeyChainLabel ?? "<no schedule>");
        }
        else
        {
            string startLocation = parsed.Spawn?.Location ?? homeLocation;
            string startRegion = _regions.RegionFor(startLocation) ?? RegionMap.OtherRegion;

            // point time -> (region, location); a time outside the live day never happens
            var tickMap = new Dictionary<int, (string Region, string Location)>();
            foreach (SchedulePoint point in parsed.Points)
            {
                int tick = TimeUtils.TickIndex(point.Time);
                if (tick < 0)
                    continue;
                tickMap[tick] = (_regions.RegionFor(point.Location) ?? RegionMap.OtherRegion, point.Location);
            }

            // walk forward: each tick keeps the latest point's place until the next point arrives
            string currentRegion = startRegion;
            string currentLocation = startLocation;
            for (int tick = 0; tick < TimeUtils.TicksPerDay; tick++)
            {
                if (tickMap.TryGetValue(tick, out (string Region, string Location) rl))
                {
                    currentRegion = rl.Region;
                    currentLocation = rl.Location;
                }
                regionByTick[tick] = currentRegion;
                locationByTick[tick] = currentLocation;
            }

            keyChain = parsed.KeyChainLabel;
        }

        return new DayPlan
        {
            Npc = npc,
            HomeRegion = homeRegion,
            HomeLocation = homeLocation,
            KeyChain = keyChain,
            RegionByTick = regionByTick,
            LocationByTick = locationByTick,
        };
    }
}
