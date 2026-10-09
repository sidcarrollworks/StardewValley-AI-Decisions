using System.Globalization;
using System.Reflection;
using UnderGlass.Sim;
using UnderGlass.Minds;
using System.Text.Json;

// Under Glass: record one run of the default town for the viewer (sim/viewer/index.html).
//   dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 28 --html run1.html
//   dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 56 --out run1.json
// --html writes the viewer with the run inside it, one file to open in a browser; --out writes
// the run alone, for the viewer's "Open a run" button. Options as the runner's: --inject,
// --fo <Name>=<value> (any FeelingOptions knob), --desire off|observe|on, --tensions <depth>,
// --trait <Name>=<Trait>:<value> (repeatable), --feel off|observe|on, --0d6 <steps> (hermits,
// brawlers, moods that spread, missing people: b-h, t, m, as the runner's), --catalog <slices> (the
// act catalog's slices, as the runner's: returns,company,welcome,repair,sides,late), and --town <name>
// for a grown town (pelican31).
var inv = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = inv;
long seed = 1;
int days = 28;
bool inject = false;
string? outPath = null, htmlPath = null;
string? reflectionMode = null, layaUrl = null, llmUrl = null, llmModel = null, tapePath = null;
string layaModel = "typed-decisions";
double reflectionChance = 0.35;
// --town <name>: a grown town (Towns.Named), with its own cast and feelings, in place of the shipped one.
int townAt = Array.IndexOf(args, "--town");
TownData? town = townAt >= 0 && townAt + 1 < args.Length ? Towns.Named(args[townAt + 1]) : null;
FeelingOptions feelings = town?.Feelings ?? DefaultTown.Feelings();
var traits = new List<(string, Trait, double)>();
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
    switch (args[i])
    {
        case "--reflection": reflectionMode = Next(); break;
        case "--laya-url": layaUrl = Next(); break;
        case "--laya-model": layaModel = Next(); break;
        case "--llm-url": llmUrl = Next(); break;
        case "--llm-model": llmModel = Next(); break;
        case "--reflection-tape": tapePath = Next(); break;
        case "--reflection-chance": reflectionChance = double.Parse(Next(), inv); break;
        case "--seed": seed = long.Parse(Next(), inv); break;
        case "--days": days = int.Parse(Next(), inv); break;
        case "--inject": inject = true; break;
        case "--out": outPath = Next(); break;
        case "--html": htmlPath = Next(); break;
        case "--town": Next(); break; // read above
        case "--feel":
            string mode = Next();
            if (mode == "off") feelings = FeelingOptions.Off;
            else if (mode is "observe" or "on") feelings.Steer = mode == "on";
            else throw new ArgumentException("--feel off|observe|on");
            break;
        case "--desire":
            (feelings.Desire, feelings.DesireActs) = Next() switch
            {
                "off" => (false, true),
                "observe" => (true, false),
                "on" => (true, true),
                _ => throw new ArgumentException("--desire off|observe|on"),
            };
            break;
        case "--tensions": feelings.Start = Towns.WithTensions(feelings.Start, double.Parse(Next(), inv)); break; // keeps a town's own
        case "--0d6": feelings.With0d6(Next()); break;
        case "--catalog": feelings.Acts.With(Next()); break; // Replay adds the slices' rows and cards
        case "--fo":
            string kv = Next();
            Set(feelings, kv[..kv.IndexOf('=')], kv[(kv.IndexOf('=') + 1)..]);
            break;
        case "--trait":
            string spec = Next();
            int eq = spec.IndexOf('='), colon = spec.IndexOf(':');
            string? traitName = eq < 1 || colon < eq + 2 ? null
                : Enum.GetNames<Trait>().FirstOrDefault(n => string.Equals(n, spec[(eq + 1)..colon], StringComparison.OrdinalIgnoreCase));
            if (traitName is null)
                throw new ArgumentException("--trait <Name>=<Trait>:<value>, a trait one of " + string.Join(", ", Enum.GetNames<Trait>()));
            if (!(town?.Cast ?? DefaultTown.Cast()).Any(v => v.Name == spec[..eq]))
                throw new ArgumentException($"--trait: nobody called {spec[..eq]}");
            traits.Add((spec[..eq], Enum.Parse<Trait>(traitName), double.Parse(spec[(colon + 1)..], inv)));
            break;
        default: throw new ArgumentException($"unknown option {args[i]}");
    }
}
if (outPath is null && htmlPath is null)
    htmlPath = $"run-seed{seed}-{days}d.html";

// The label names only what differs from the town as it ships (seed and days are shown anyway).
var shown = new List<string>();
for (int k = 0; k < args.Length; k++)
{
    if (args[k] is "--out" or "--html" or "--seed" or "--days") { k++; continue; }
    shown.Add(args[k]);
}
string? label = shown.Count == 0 ? null : string.Join(' ', shown);
var clock = System.Diagnostics.Stopwatch.StartNew();
if (reflectionMode is not (null or "authored" or "laya" or "hybrid" or "tape"))
    throw new ArgumentException("--reflection authored|laya|hybrid|tape");
if (reflectionMode == "hybrid" && (llmUrl is null || llmModel is null))
    throw new ArgumentException("Hybrid reflection needs --llm-url and --llm-model for your local server.");
if (reflectionMode == "tape" && tapePath is null)
    throw new ArgumentException("Tape reflection needs --reflection-tape <previous run.json>.");
if ((layaUrl is not null || llmUrl is not null || llmModel is not null || tapePath is not null)
    && reflectionMode is null)
    throw new ArgumentException("Model options require --reflection.");
var reflection = new ReflectionOptions { Enabled = reflectionMode is not null, DailyChance = reflectionChance };
using var model = new ResilientReflectionMind(new ReflectionMindOptions
{
    LayaBaseUrl = reflectionMode is "laya" or "hybrid" ? layaUrl ?? "http://127.0.0.1:8000" : null,
    LayaModel = layaModel,
    GenerationBaseUrl = reflectionMode == "hybrid" ? llmUrl : null,
    GenerationModel = llmModel ?? "",
});
IReflectionMind mind = model;
if (reflectionMode == "tape")
{
    using JsonDocument tape = JsonDocument.Parse(File.ReadAllText(tapePath!));
    mind = new RecordedReflectionMind(tape.RootElement.GetProperty("reflections")
        .Deserialize<ReflectionRecord[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);
}
string json = await Replay.JsonAsync(new ReplayOptions { Seed = seed, Days = days, Town = town, Feelings = feelings,
    Inject = inject, Traits = traits, Label = label, Reflection = reflection }, mind);
if (reflection.Enabled)
{
    using JsonDocument recorded = JsonDocument.Parse(json);
    var thoughts = recorded.RootElement.GetProperty("reflections").EnumerateArray().ToArray();
    Console.WriteLine($"reflections: {thoughts.Length}; " + string.Join(", ", thoughts.GroupBy(t => t.GetProperty("answer").GetProperty("backend").GetString())
        .Select(g => $"{g.Key} {g.Count()}")));
}
Console.WriteLine($"recorded seed {seed}, {days} days in {clock.Elapsed.TotalSeconds:0.0} s ({json.Length / 1024.0 / 1024:0.0} MB)");
if (outPath is not null)
{
    File.WriteAllText(outPath, json);
    Console.WriteLine($"wrote {outPath}");
}
if (htmlPath is not null)
{
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("viewer.html")
        ?? throw new InvalidOperationException("the viewer page is missing from the build");
    string page = new StreamReader(stream).ReadToEnd();
    const string slot = "<script id=\"run-data\" type=\"application/json\"></script>";
    if (!page.Contains(slot))
        throw new InvalidOperationException("the viewer page has no slot for a run");
    // System.Text.Json escapes '<', '>' and '&', so the run cannot close the script tag early.
    // The page is written as a fragment (the published copy gets its document from the host); a
    // file opened straight from disk needs its own, or browsers fall back to quirks mode.
    page = "<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">\n</head>\n<body>\n"
        + page.Replace(slot, $"<script id=\"run-data\" type=\"application/json\">{json}</script>") + "\n</body>\n</html>\n";
    File.WriteAllText(htmlPath, page);
    Console.WriteLine($"wrote {htmlPath}: open it in a browser");
}

static void Set(FeelingOptions o, string name, string value)
{
    var prop = typeof(FeelingOptions).GetProperty(name) ?? throw new ArgumentException($"no feeling option {name}");
    object v = prop.PropertyType == typeof(bool) ? (object)bool.Parse(value)
        : prop.PropertyType == typeof(int) ? (object)int.Parse(value, CultureInfo.InvariantCulture)
        : prop.PropertyType == typeof(double) ? (object)double.Parse(value, CultureInfo.InvariantCulture)
        : throw new ArgumentException($"{name} can't be set from the command line");
    prop.SetValue(o, v);
}
