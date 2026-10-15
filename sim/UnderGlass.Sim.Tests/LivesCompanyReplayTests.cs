using System.Text.Json;
using System.Text.Json.Nodes;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class LivesCompanyReplayTests
{
    [Fact]
    public void DisabledCompanyLeavesTheRecordingByteIdentical()
    {
        ReplayOptions options = Options();
        string baseline = Replay.Json(options);
        string explicitOff = Replay.Json(options with { LivesCompany = new() { Enabled = false } });
        Assert.Equal(baseline, explicitOff);
        using JsonDocument recording = JsonDocument.Parse(baseline);
        Assert.False(recording.RootElement.TryGetProperty("livesCompany", out _));
    }

    [Fact]
    public void CompanyReceiptsUseASeparateExplicitlyExperimentalBlock()
    {
        using JsonDocument recording = JsonDocument.Parse(Replay.Json(Options() with { LivesCompany = On() }));
        JsonElement block = recording.RootElement.GetProperty("livesCompany");
        Assert.Equal(1, block.GetProperty("version").GetInt32());
        Assert.Equal("immediate-company", block.GetProperty("stage").GetString());
        Assert.Equal("authored-keyed-draw", block.GetProperty("backend").GetString());
        Assert.True(block.GetProperty("settings").GetProperty("enabled").GetBoolean());
        Assert.Equal(100, block.GetProperty("settings").GetProperty("offerBase").GetDouble());
        Assert.False(recording.RootElement.TryGetProperty("lives", out _));
        foreach (string name in new[] { "updates", "choices", "answers", "outcomes" })
            Assert.Equal(JsonValueKind.Array, block.GetProperty(name).ValueKind);
        JsonElement[] updates = block.GetProperty("updates").EnumerateArray().ToArray();
        Assert.NotEmpty(updates);
        Assert.All(updates, update => Assert.Equal(JsonValueKind.String,
            update.GetProperty("episode").GetProperty("stage").ValueKind));
        Assert.NotEmpty(block.GetProperty("answers").EnumerateArray());
    }

    [Fact]
    public void DisablingCompanyRecordingChangesOnlyItsReceiptBlock()
    {
        JsonObject recorded = JsonNode.Parse(Replay.Json(Options() with { LivesCompany = On() }))!.AsObject();
        JsonObject quiet = JsonNode.Parse(Replay.Json(Options() with { LivesCompany = On() with { Record = false } }))!.AsObject();
        Assert.True(recorded.Remove("livesCompany"));
        Assert.False(quiet.ContainsKey("livesCompany"));
        Assert.Equal(recorded.ToJsonString(), quiet.ToJsonString());
    }

    private static CompanyOptions On() => new() { Enabled = true, OfferBase = 100, SeekBase = 100, AnswerScale = 0 };

    private static ReplayOptions Options()
    {
        // Fictional housemates and a synthetic warm memory; no Stardew memories are admitted here.
        Villager Person(string name) => new(name, "House", "villager", new Temperament(.5, .5, .5, .5),
            new Body(100, -1), null, Array.Empty<Haunt>(), new Dictionary<string, double>(), Array.Empty<string>());
        var feelings = new FeelingOptions();
        feelings.Acts.With("company,invitations");
        return new()
        {
            Seed = 7, Days = 2, Nights = false, LivesRouting = new() { Enabled = true }, History = History(),
            Town = new()
            {
                Cast = new[] { Person("Ann"), Person("Bo") },
                Places = new[] { new Location("Home:House", false, Enumerable.Repeat(new string('.', 20), 12).ToArray()) },
                Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(), Acts = Array.Empty<ActKind>(),
                Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
                Body = new BodyOptions { AwakeHoursAtRest = 100_000, CircadianWeight = 0 },
                Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
            },
        };
    }

    private static HistoryPack History() => new(1,
        new[] { new HistorySource("source", "synthetic:company-replay", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "fact", "Ann", "We spend time together.", KnowledgeKind.Lived,
            new[] { "source" }, new[] { new HistoryCue("person:Bo") }) },
        new[] { new HistoryReading("reading", "account", new(MemoryObjectKind.Person, "Bo"), ReadingStance.Warm,
            "I enjoy their company.", ReadingOrigin.CanonStated, AdmissionRule.R2,
            new(ReviewStatus.Reviewed, "Synthetic test only", "2026-10-15"), new[] { "source" },
            new[] { new HistoryCue("person:Bo") }) });
}
