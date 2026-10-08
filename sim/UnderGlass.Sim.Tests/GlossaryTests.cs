using System.Reflection;
using System.Text.Json;
using UnderGlass.Sim;
using Xunit;
using Xunit.Abstractions;

namespace UnderGlass.Sim.Tests;

/// <summary>The viewer's Explanation tab (sim/viewer/index.html, the "glossary" block) defines the
/// settings by the names the recorder writes. These tests keep the two in step: every setting it
/// defines exists in the code, and the recorder writes the shipped town's values beside the run's,
/// so the viewer can show which differ.</summary>
public class GlossaryTests
{
    private readonly ITestOutputHelper _out;

    public GlossaryTests(ITestOutputHelper output) => _out = output;

    private static JsonElement Glossary()
    {
        string html = File.ReadAllText(Path.Combine(Repo(), "sim", "viewer", "index.html"));
        const string open = "<script id=\"glossary\" type=\"application/json\">";
        int start = html.IndexOf(open, StringComparison.Ordinal);
        Assert.True(start >= 0, "the viewer has no glossary block");
        start += open.Length;
        int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        return JsonDocument.Parse(html[start..end]).RootElement;
    }

    private static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "sim", "viewer", "index.html")))
                return d.FullName;
        throw new InvalidOperationException("can't find the repository from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void EveryItemIsWholeAndNamedOnce()
    {
        JsonElement g = Glossary();
        foreach (string section in new[] { "settings", "terms", "actsLog" })
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement it in g.GetProperty(section).EnumerateArray())
            {
                string key = it.GetProperty("key").GetString()!;
                Assert.True(keys.Add(key), $"{section}: {key} is defined twice");
                foreach (string field in new[] { "label", "group", "definition" })
                    Assert.False(string.IsNullOrWhiteSpace(it.GetProperty(field).GetString()), $"{section}: {key} has no {field}");
            }
            Assert.NotEmpty(keys);
        }
    }

    /// <summary>A setting the glossary defines is a switch or number the code has: the feelings'
    /// bare, the others with their class. Groups that describe the run's setup or numbers fixed in
    /// the code name no option.</summary>
    [Fact]
    public void EverySettingItDefinesExists()
    {
        Assembly sim = typeof(Simulation).Assembly;
        foreach (JsonElement it in Glossary().GetProperty("settings").EnumerateArray())
        {
            string key = it.GetProperty("key").GetString()!, group = it.GetProperty("group").GetString()!;
            if (group.Contains("fixed in code", StringComparison.Ordinal) || group == "Run setup")
                continue;
            int dot = key.IndexOf('.');
            Type type = dot < 0 ? typeof(FeelingOptions) : sim.GetType("UnderGlass.Sim." + key[..dot])
                ?? throw new Xunit.Sdk.XunitException($"{key}: no class {key[..dot]}");
            string name = dot < 0 ? key : key[(dot + 1)..];
            Assert.True(type.GetProperty(name) is not null, $"{key}: {type.Name} has no {name}");
        }
    }

    /// <summary>The recorder writes every switch and number of the run's options, and the shipped
    /// town's as "defaults", with the same keys: equal for the shipped town, different where the run
    /// changed one.</summary>
    [Fact]
    public void TheRecordWritesTheShippedTownsValuesBesideTheRuns()
    {
        static JsonElement Run(ReplayOptions o) => JsonDocument.Parse(Replay.Json(o)).RootElement;
        JsonElement shipped = Run(new ReplayOptions { Seed = 1, Days = 1 });
        var settings = Scalars(shipped.GetProperty("settings"));
        var defaults = Scalars(shipped.GetProperty("defaults"));
        Assert.Equal(new[] { "Inject" }, settings.Keys.Except(defaults.Keys)); // how the run was set up, not an option
        Assert.All(defaults, d => Assert.Equal(d.Value, settings[d.Key]));
        Assert.Equal(new GossipOptions().ChatChance.ToString(System.Globalization.CultureInfo.InvariantCulture), settings["GossipOptions.ChatChance"]);
        Assert.Equal("2", settings["PlasticScale"]);

        TownData town = TownData.Default() with { Gossip = new GossipOptions { ChatChance = 0.5 } };
        var feelings = DefaultTown.Feelings();
        feelings.PlasticScale = 1;
        JsonElement changed = Run(new ReplayOptions { Seed = 1, Days = 1, Town = town, Feelings = feelings });
        var s = Scalars(changed.GetProperty("settings"));
        var d = Scalars(changed.GetProperty("defaults"));
        Assert.Equal(defaults, d);
        Assert.Equal("0.5", s["GossipOptions.ChatChance"]);
        Assert.Equal("1", s["PlasticScale"]);
        Assert.Equal(new[] { "GossipOptions.ChatChance", "PlasticScale" },
            s.Where(x => d.TryGetValue(x.Key, out string? v) && v != x.Value).Select(x => x.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>Not a requirement (a new setting may land before its definition), but listed in the
    /// test output so they are filled in.</summary>
    [Fact]
    public void RecordedSettingsWithoutADefinitionAreListed()
    {
        var defined = Glossary().GetProperty("settings").EnumerateArray().Select(it => it.GetProperty("key").GetString()!).ToHashSet();
        var recorded = Scalars(JsonDocument.Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 1 })).RootElement.GetProperty("settings")).Keys;
        string[] missing = recorded.Where(k => !defined.Contains(k)).Order(StringComparer.Ordinal).ToArray();
        _out.WriteLine(missing.Length == 0 ? "every recorded setting has a definition" : "no definition yet: " + string.Join(", ", missing));
        Assert.True(missing.Length < recorded.Count, "the glossary describes none of the recorded settings");
    }

    private static Dictionary<string, string> Scalars(JsonElement o)
        => o.EnumerateObject().Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null))
            .ToDictionary(p => p.Name, p => p.Value.ToString());
}
