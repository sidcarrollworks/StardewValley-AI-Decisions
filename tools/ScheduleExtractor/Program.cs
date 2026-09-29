using System.Text.Json;
using NpcSchedules;

namespace ScheduleExtractor;

/// <summary>
/// Command-line wrapper: schedule JSON files in, region x time-block counts out.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var opts = ParseArgs(args);
        if (opts == null)
            return 2;
        if (opts.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var regions = RegionMap.Load(opts.RegionsPath);
            Directory.CreateDirectory(opts.OutDir);

            var extractor = new RoutineExtractor(regions);
            var routines = new List<NpcRoutine>();

            foreach (string file in Directory.EnumerateFiles(opts.SchedulesDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string npc = Path.GetFileNameWithoutExtension(file);
                Dictionary<string, string> schedules;
                try
                {
                    schedules = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                        ?? new Dictionary<string, string>();
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"skipping {file}: not a key -> script JSON object ({ex.Message})");
                    continue;
                }

                var options = new ExtractorOptions
                {
                    Hearts = opts.Hearts,
                    Friends = opts.Friends,
                    MailReceived = opts.Mail,
                    Seed = opts.Seed,
                };
                routines.Add(extractor.Extract(npc, schedules, options));
            }

            if (routines.Count == 0)
            {
                Console.Error.WriteLine($"no NPC schedule files found in {opts.SchedulesDir}");
                return 1;
            }

            WriteRoutines(routines, opts, regions);
            if (opts.Seed.HasValue)
                WritePrior(routines, opts);

            PrintSummary(routines, regions);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private sealed class CliOptions
    {
        public bool ShowHelp;
        public string SchedulesDir = "";
        public string OutDir = "";
        public string RegionsPath = "";
        public int Hearts;
        public int? Seed;
        public string Observer = "Player";
        public RelationshipKind Strength = RelationshipKind.Other;
        public Dictionary<string, int> Friends = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Mail = new(StringComparer.OrdinalIgnoreCase);
    }

    private static CliOptions? ParseArgs(string[] args)
    {
        var opts = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    opts.ShowHelp = true;
                    return opts;
                case "--schedules":
                    opts.SchedulesDir = RequireValue(args, ref i);
                    break;
                case "--out":
                    opts.OutDir = RequireValue(args, ref i);
                    break;
                case "--regions":
                    opts.RegionsPath = RequireValue(args, ref i);
                    break;
                case "--seed":
                    opts.Seed = int.Parse(RequireValue(args, ref i));
                    break;
                case "--observer":
                    opts.Observer = RequireValue(args, ref i);
                    break;
                case "--strength":
                    opts.Strength = Enum.Parse<RelationshipKind>(RequireValue(args, ref i), ignoreCase: true);
                    break;
                case "--hearts":
                    opts.Hearts = int.Parse(RequireValue(args, ref i));
                    break;
                case "--friend":
                    (string friendName, int friendHearts) = ParseNameNumber(RequireValue(args, ref i));
                    opts.Friends[friendName] = friendHearts;
                    break;
                case "--mail":
                    opts.Mail.Add(RequireValue(args, ref i));
                    break;
                default:
                    Console.Error.WriteLine($"unknown argument '{args[i]}' (see --help)");
                    return null;
            }
        }

        if (opts.SchedulesDir.Length == 0 || opts.OutDir.Length == 0)
        {
            Console.Error.WriteLine("--schedules and --out are required (see --help)");
            return null;
        }
        if (!Directory.Exists(opts.SchedulesDir))
        {
            Console.Error.WriteLine($"--schedules directory not found: {opts.SchedulesDir}");
            return null;
        }

        opts.RegionsPath = opts.RegionsPath.Length > 0
            ? Path.GetFullPath(opts.RegionsPath)
            : FindRegionsJson();
        if (!File.Exists(opts.RegionsPath))
        {
            Console.Error.WriteLine($"regions.json not found at {opts.RegionsPath}; pass --regions <path>");
            return null;
        }
        return opts;
    }

    private static string FindRegionsJson()
    {
        // walk up from the exe directory looking for data/regions.json (repo checkout), then fall
        // back to regions.json next to the exe
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "data", "regions.json");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(AppContext.BaseDirectory, "regions.json");
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"missing value after '{args[i]}'");
        return args[++i];
    }

    private static (string Name, int Hearts) ParseNameNumber(string value)
    {
        int colon = value.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(value[(colon + 1)..], out int hearts))
            throw new ArgumentException($"expected Name:hearts, got '{value}'");
        return (value[..colon], hearts);
    }

    private static void WriteRoutines(List<NpcRoutine> routines, CliOptions opts, RegionMap regions)
    {
        var doc = new
        {
            blockMinutes = regions.BlockMinutes,
            rainChance = regions.RainChance,
            player = new { hearts = opts.Hearts, friends = opts.Friends, mail = opts.Mail.ToArray() },
            seed = opts.Seed,
            npcs = routines.Select(r => new
            {
                name = r.Name,
                home = new { location = r.HomeLocation, region = r.HomeRegion },
                keyDays = r.KeyDays.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(p => p.Key, p => p.Value),
                blocks = r.BuildCells(regions),
                unmapped = r.UnmappedLocations,
                warnings = r.Warnings,
            }),
        };

        string jsonPath = Path.Combine(opts.OutDir, "routines.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
        Console.WriteLine($"wrote {jsonPath}");

        string csvPath = Path.Combine(opts.OutDir, "routines.csv");
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("npc,region,block,ticks,share");
        foreach (NpcRoutine routine in routines)
            foreach (RoutineCell cell in routine.BuildCells(regions))
                writer.WriteLine($"{routine.Name},{cell.Region},{cell.Block},{cell.Ticks},{cell.Share:0.####}");
        Console.WriteLine($"wrote {csvPath}");
    }

    private static void WritePrior(List<NpcRoutine> routines, CliOptions opts)
    {
        var priorOptions = new PriorOptions { Kind = opts.Strength };
        var doc = new
        {
            observer = opts.Observer,
            seed = opts.Seed,
            strength = opts.Strength.ToString().ToLowerInvariant(),
            npcs = routines.Select(r => new
            {
                name = r.Name,
                counts = RoutinePrior.Build(r, opts.Observer, (opts.Seed ?? 0).ToString(), priorOptions)
                    .Select(c => new { region = c.Region, block = c.Block, count = c.Count }),
            }),
        };

        string path = Path.Combine(opts.OutDir, $"prior-{opts.Observer}-{opts.Seed}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
        Console.WriteLine($"wrote {path}");
    }

    private static void PrintSummary(List<NpcRoutine> routines, RegionMap regions)
    {
        Console.WriteLine();
        foreach (NpcRoutine routine in routines)
        {
            var top = routine.BuildCells(regions)
                .GroupBy(c => c.Region)
                .Select(g => (Region: g.Key, Ticks: g.Sum(c => c.Ticks)))
                .OrderByDescending(g => g.Ticks)
                .Take(3)
                .Select(g => $"{g.Region} {g.Ticks / (double)routine.TotalTicks:P0}");
            Console.WriteLine($"{routine.Name,-14} home={routine.HomeLocation,-16} top: {string.Join("  ", top)}");
            if (routine.UnmappedLocations.Count > 0)
                Console.WriteLine($"    unmapped locations: {string.Join(", ", routine.UnmappedLocations)}");
            foreach (string warning in routine.Warnings)
                Console.WriteLine($"    warning: {warning}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
@"Usage: dotnet run --project tools/ScheduleExtractor -- [options]

Options:
  --schedules <dir>   Directory of per-NPC JSON files (key -> script string). Required.
  --out <dir>         Output directory for routines.json / routines.csv / prior files. Required.
  --regions <path>    Path to regions.json (default: data/regions.json next to the repo).
  --seed <int>        Simulation seed. Same seed + same inputs = same output. Also enables
                      the prior output file.
  --observer <name>   Observer NPC for the prior (default: Player).
  --strength <kind>   Prior strength: family | friend | other (default: other).
  --hearts <n>        Observer's hearts with the target NPC (default 0).
  --friend <Name>:<n> Observer's hearts with another NPC, for NOT friendship commands (repeatable).
  --mail <id>         Mail/world-state ID the player received, for MAIL commands and Pam's bus (repeatable).
");
    }
}
