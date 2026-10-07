using System.Globalization;
using System.Reflection;
using UnderGlass.Sim;

// Under Glass: record one run of the default town for the viewer (sim/viewer/index.html).
//   dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 28 --html run1.html
//   dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 1 --days 56 --out run1.json
// --html writes the viewer with the run inside it, one file to open in a browser; --out writes
// the run alone, for the viewer's "Open a run" button. Options as the runner's: --inject,
// --fo <Name>=<value> (any FeelingOptions knob), --desire off|observe|on, --tensions <depth>,
// --trait <Name>=<Trait>:<value> (repeatable), --feel off|observe|on.
var inv = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = inv;
long seed = 1;
int days = 28;
bool inject = false;
string? outPath = null, htmlPath = null;
FeelingOptions feelings = DefaultTown.Feelings();
var traits = new List<(string, Trait, double)>();
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
    switch (args[i])
    {
        case "--seed": seed = long.Parse(Next(), inv); break;
        case "--days": days = int.Parse(Next(), inv); break;
        case "--inject": inject = true; break;
        case "--out": outPath = Next(); break;
        case "--html": htmlPath = Next(); break;
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
        case "--tensions": feelings.Start = DefaultTown.Tensions(double.Parse(Next(), inv)); break;
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
            if (!DefaultTown.Cast().Any(v => v.Name == spec[..eq]))
                throw new ArgumentException($"--trait: nobody called {spec[..eq]}");
            traits.Add((spec[..eq], Enum.Parse<Trait>(traitName), double.Parse(spec[(colon + 1)..], inv)));
            break;
        default: throw new ArgumentException($"unknown option {args[i]}");
    }
}
if (outPath is null && htmlPath is null)
    htmlPath = $"run-seed{seed}-{days}d.html";

string label = args.Length == 0 ? "the town as it ships" : string.Join(' ', args.Where((a, k) => a is not ("--out" or "--html") && (k == 0 || args[k - 1] is not ("--out" or "--html"))));
var clock = System.Diagnostics.Stopwatch.StartNew();
string json = Replay.Json(new ReplayOptions { Seed = seed, Days = days, Feelings = feelings, Inject = inject, Traits = traits, Label = label });
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
    File.WriteAllText(htmlPath, page.Replace(slot, $"<script id=\"run-data\" type=\"application/json\">{json}</script>"));
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
