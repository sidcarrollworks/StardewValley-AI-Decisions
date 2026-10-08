using UnderGlass.Sim;
using UnderGlass.Sim.Generation;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>A town written as JSON and read back runs exactly as the town written (the runner's
/// --dump-town and --town file:path).</summary>
public class TownJsonTests
{
    public static IEnumerable<object[]> Towns() => new[]
    {
        new object[] { "pelican" }, new object[] { "pelican31" }, new object[] { "pelican:60@1" },
    };

    [Theory]
    [MemberData(nameof(Towns))]
    public void ATownReadBackRunsAsTheOneWritten(string name)
    {
        TownData town = UnderGlass.Sim.Towns.Named(name);
        string json = TownJson.Write(town);
        TownData back = TownJson.Read(json);
        Assert.Equal(json, TownJson.Write(back)); // the file reads back to itself
        Assert.Equal(Census.Hash(town), Census.Hash(back));
        Assert.Equal(Metrics.LogHash(new Simulation(3, town).Run(3)), Metrics.LogHash(new Simulation(3, back).Run(3)));
    }

    [Fact]
    public void TheFileIsMadeOfWhatATownIsMadeOf()
    {
        string json = TownJson.Write(TownData.Default());
        Assert.Contains("\"Cast\"", json);
        Assert.Contains("\"Rows\"", json);
        Assert.Contains("\"Abigail\"", json);
        Assert.DoesNotContain("\"Width\"", json);     // worked out, not stored
        Assert.DoesNotContain("\"CloseCall\"", json); // code
        // A starting tension as a [from, to, regard] row.
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("Feelings").GetProperty("Start").EnumerateArray()
            .Select(r => (r[0].GetString(), r[1].GetString(), r[2].GetDouble())).ToList();
        Assert.Contains(("Pierre", "Shane", -0.3), rows);
    }

    /// <summary>A hand-edited file that breaks the town (a keeper who isn't in it) is refused, with
    /// TownCheck's finding in the message.</summary>
    [Fact]
    public void ABrokenFileIsRefused()
    {
        string json = TownJson.Write(TownData.Default());
        Assert.Contains("\"Store\": \"Pierre\"", json);
        string path = Path.Combine(Path.GetTempPath(), $"under-glass-broken-{Environment.ProcessId}.json");
        File.WriteAllText(path, json.Replace("\"Store\": \"Pierre\"", "\"Store\": \"Nobody\""));
        try
        {
            var e = Assert.Throws<ArgumentException>(() => UnderGlass.Sim.Towns.Named("file:" + path));
            Assert.Contains("Nobody", e.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A hand edit that misspells a field, writes it in the wrong case, leaves it out or sets
    /// a part to null is refused rather than read as the engine's defaults.</summary>
    [Fact]
    public void AMisspeltMissingOrNullFieldIsRefused()
    {
        string json = TownJson.Write(UnderGlass.Sim.Towns.Pelican31());
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(json.Replace("\"Wander\": 2", "\"Wandr\": 0")));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(json.Replace("\"Wander\": 2", "\"wander\": 0")));
        static string Edit(string json, Action<System.Text.Json.Nodes.JsonObject> edit)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            edit(node);
            return node.ToJsonString();
        }
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o.Remove("Wander"))));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Cast"]![0]!.AsObject().Remove("Age"))));
        var e = Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Gossip"] = null)));
        Assert.Contains("Gossip", e.Message);
        Assert.Equal(Census.Hash(UnderGlass.Sim.Towns.Pelican31()), Census.Hash(TownJson.Read(json))); // the file as written still reads
    }

    [Fact]
    public void AnEditedFileChangesTheTown()
    {
        string json = TownJson.Write(TownData.Default()).Replace("\"Wander\": 2", "\"Wander\": 0");
        Assert.Equal(0, TownJson.Read(json).Wander);
        string path = Path.Combine(Path.GetTempPath(), $"under-glass-town-{Environment.ProcessId}.json");
        File.WriteAllText(path, json);
        try
        {
            Assert.Equal(0, UnderGlass.Sim.Towns.Named("file:" + path).Wander);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
