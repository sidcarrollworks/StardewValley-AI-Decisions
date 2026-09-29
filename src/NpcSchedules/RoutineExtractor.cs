using System.Globalization;

namespace NpcSchedules;

/// <summary>
/// Turns one NPC's raw schedule dictionary into year-wide region x time-block counts,
/// following the game's own key-picking order and script semantics (verified against
/// decompiled 1.6 NPC.cs / Game1.cs).
/// </summary>
public sealed class RoutineExtractor
{
    private static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };

    private readonly RegionMap regions;

    public RoutineExtractor(RegionMap regions)
    {
        this.regions = regions;
    }

    /// <summary>
    /// Simulate a full year (4 seasons x 28 days) for one NPC and aggregate the results.
    /// Deterministic: the same name, schedule data, options and seed always give the same result.
    /// </summary>
    public NpcRoutine Extract(string npc, Dictionary<string, string> schedules, ExtractorOptions options)
    {
        var routine = new NpcRoutine
        {
            Name = npc,
            BlockMinutes = regions.BlockMinutes,
            RegionTicks = BuildEmptyMatrix(),
        };
        var warnings = routine.Warnings;
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int seed = options.Seed ?? Fnv1a.Seed("sim", npc);
        var rng = new Random(seed);
        var simulator = new ScheduleSimulator(npc, schedules, options, rng, warnings);

        // home: regions.json override, else last stop of spring/default (bed -> spawn -> most common last stop)
        string homeLocation = InferHome(npc, schedules, warnings);
        routine.HomeLocation = homeLocation;
        routine.HomeRegion = RegionOf(homeLocation, unmapped, warnings);

        int blockMinutes = regions.BlockMinutes;
        string?[] dayRegion = new string?[TimeUtils.TicksPerDay];

        foreach (string season in Seasons)
        {
            double rainChance = regions.RainChance.TryGetValue(season, out double chance) ? chance : 0;
            for (int day = 1; day <= 28; day++)
            {
                bool rainy = rainChance > 0 && rng.NextDouble() < rainChance;
                string? key = simulator.PickDayKey(season, day, rainy, out _);
                if (key == null)
                {
                    // no schedule today: all ticks at home (game: NPC stays at default position)
                    AddFullDayAt(routine, homeLocation, homeLocation);
                    routine.KeyDays["<no schedule>"] =
                        routine.KeyDays.GetValueOrDefault("<no schedule>") + 1;
                    continue;
                }

                ParsedScript? parsed = simulator.Evaluate(key, season, new List<string>());
                if (parsed == null || parsed.NoSchedule || parsed.ParseFailed || parsed.Points.Count == 0)
                {
                    AddFullDayAt(routine, homeLocation, homeLocation);
                    string label = parsed == null ? "<no schedule>" : parsed.KeyChainLabel;
                    routine.KeyDays[label] = routine.KeyDays.GetValueOrDefault(label) + 1;
                    continue;
                }

                FillDay(dayRegion, parsed, homeLocation, unmapped, warnings);
                Accumulate(routine, dayRegion);
                routine.KeyDays[parsed.KeyChainLabel] = routine.KeyDays.GetValueOrDefault(parsed.KeyChainLabel) + 1;
            }
        }

        routine.TotalTicks = routine.RegionTicks.Values.Sum(col => col.Sum());
        foreach (string location in unmapped.OrderBy(l => l, StringComparer.OrdinalIgnoreCase))
            routine.UnmappedLocations.Add(location);
        return routine;
    }

    private Dictionary<string, int[]> BuildEmptyMatrix()
    {
        var matrix = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string region in regions.Regions.Keys.Concat(new[] { RegionMap.OtherRegion }).Distinct(StringComparer.OrdinalIgnoreCase))
            matrix[region] = new int[TimeUtils.BlockCount(regions.BlockMinutes)];
        return matrix;
    }

    private string RegionOf(string location, HashSet<string> unmapped, List<string> warnings)
    {
        if (string.IsNullOrEmpty(location))
        {
            warnings.Add("no home location could be inferred; counting ticks as Other");
            return RegionMap.OtherRegion;
        }
        string? region = regions.RegionFor(location);
        if (region == null)
        {
            unmapped.Add(location);
            return RegionMap.OtherRegion;
        }
        return region;
    }

    /// <summary>
    /// Fill one day's region-per-tick array. Ticks before the first point count at the spawn
    /// point (or home); each point counts at its destination region from its time onward.
    /// </summary>
    private void FillDay(string?[] dayRegion, ParsedScript parsed, string homeLocation,
        HashSet<string> unmapped, List<string> warnings)
    {
        Array.Clear(dayRegion);

        string startLocation = parsed.Spawn?.Location ?? homeLocation;
        string startRegion = RegionOf(startLocation, unmapped, warnings);

        foreach (SchedulePoint point in parsed.Points)
        {
            int tick = TimeUtils.TickIndex(point.Time);
            if (tick < 0)
            {
                warnings.Add($"point at time {point.Time} is outside the 600-2600 day; game never reaches it");
                continue;
            }
            dayRegion[tick] = RegionOf(point.Location, unmapped, warnings);
        }

        // walk forward: each tick takes the region of the latest point at or before it
        string? current = startRegion;
        for (int t = 0; t < dayRegion.Length; t++)
        {
            if (dayRegion[t] != null)
                current = dayRegion[t];
            dayRegion[t] = current;
        }
    }

    private void AddFullDayAt(NpcRoutine routine, string location, string homeLocation)
    {
        string?[] dayRegion = new string?[TimeUtils.TicksPerDay];
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string region = RegionOf(string.IsNullOrEmpty(location) ? homeLocation : location, unmapped, routine.Warnings);
        for (int t = 0; t < dayRegion.Length; t++)
            dayRegion[t] = region;
        Accumulate(routine, dayRegion);
        foreach (string loc in unmapped)
            routine.UnmappedLocations.Add(loc);
    }

    private void Accumulate(NpcRoutine routine, string?[] dayRegion)
    {
        int blockMinutes = regions.BlockMinutes;
        for (int tick = 0; tick < dayRegion.Length; tick++)
        {
            string? region = dayRegion[tick];
            if (region == null)
                continue;
            if (!routine.RegionTicks.TryGetValue(region, out int[]? column))
            {
                column = new int[TimeUtils.BlockCount(blockMinutes)];
                routine.RegionTicks[region] = column;
            }
            column[TimeUtils.BlockIndex(tick, blockMinutes)]++;
        }
    }

    /// <summary>
    /// Home inference: last stop of the spring (else default) script; if that stop is 'bed', the
    /// script's spawn point; if neither works, the most common last stop across all keys.
    /// </summary>
    /// <summary>
    /// Infer an NPC's home location from its schedules: the last stop of the spring (else
    /// default) script; if that stop is 'bed', the script's spawn point; if neither works, the
    /// most common last stop across all keys. Returns null when nothing can be inferred.
    /// </summary>
    public static string? InferHomeLocation(Dictionary<string, string> schedules)
    {
        foreach (string key in new[] { "spring", "default" })
        {
            if (!schedules.TryGetValue(key, out string? script))
                continue;
            string[] fields = ScriptParser.SplitFields(script);
            if (fields.Length == 0)
                continue;

            // spawn point first (used when the last stop is 'bed')
            string? spawn = null;
            string? last = null;
            for (int i = 0; i < fields.Length; i++)
            {
                string[] tokens = ScriptParser.SplitTokens(fields[i]);
                if (tokens.Length < 2)
                    continue;
                string timeToken = tokens[0].Length > 1 && tokens[0][0] == 'a' ? tokens[0].Substring(1) : tokens[0];
                if (!int.TryParse(timeToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int time))
                    continue;
                if (time == 0)
                {
                    spawn ??= tokens[1];
                    continue;
                }
                last = tokens[1];
            }

            if (last != null && !last.Equals("bed", StringComparison.OrdinalIgnoreCase))
                return last;
            if (spawn != null && !spawn.Equals("bed", StringComparison.OrdinalIgnoreCase))
                return spawn;
        }

        // most common last stop across all keys
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string script) in schedules)
        {
            string[] fields = ScriptParser.SplitFields(script);
            if (fields.Length == 0)
                continue;
            string[] tokens = ScriptParser.SplitTokens(fields[^1]);
            if (tokens.Length < 2 || tokens[0].StartsWith("GOTO") || tokens[0].StartsWith("NOT") || tokens[0].StartsWith("MAIL"))
                continue;
            string location = tokens[1];
            if (location.Equals("bed", StringComparison.OrdinalIgnoreCase))
                continue;
            counts[location] = counts.GetValueOrDefault(location) + 1;
        }
        if (counts.Count > 0)
            return counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase).First().Key;

        return null;
    }

    private string InferHome(string npc, Dictionary<string, string> schedules, List<string> warnings)
    {
        if (regions.HomeOverride(npc) is string overrideLocation)
            return overrideLocation;
        if (InferHomeLocation(schedules) is string inferred)
            return inferred;
        warnings.Add("no home location could be inferred");
        return "";
    }
}
