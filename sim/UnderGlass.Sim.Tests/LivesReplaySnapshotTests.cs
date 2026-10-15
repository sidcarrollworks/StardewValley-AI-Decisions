using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class LivesReplaySnapshotTests
{
    [Fact]
    public async Task CallerMutationDuringReflectionCannotRewriteRecordedComparisonConfiguration()
    {
        var requested = new List<string> { "reading:ann-room" };
        var mind = new MutatingMind(() =>
        {
            requested.Clear();
            requested.Add("caller-changed-after-start");
        });
        var options = new ReplayOptions
        {
            Seed = 1, Days = 3, Town = Town(), Nights = false,
            Reflection = new() { Enabled = true, QuietMinutes = 30, DailyChance = 1 },
            LivesRouting = new() { Enabled = true, CompareHistory = requested }, History = History(),
        };
        Dictionary<string, object?> recording = await Replay.RecordAsync(options, mind);

        Assert.True(mind.Calls > 0); // proves the mutation happened while RecordAsync was running
        Assert.Equal(new[] { "caller-changed-after-start" }, requested);
        JsonElement routing = Assert.IsType<JsonElement>(recording["livesRouting"]);
        Assert.Equal(new[] { "reading:ann-room" }, routing.GetProperty("options").GetProperty("compareHistory")
            .EnumerateArray().Select(value => value.GetString()));
        JsonElement[] comparisons = routing.GetProperty("decisions").EnumerateArray()
            .Select(row => row.GetProperty("comparison")).Where(pair => pair.ValueKind == JsonValueKind.Object).ToArray();
        Assert.NotEmpty(comparisons);
        Assert.All(comparisons, pair => Assert.Equal(new[] { "reading:ann-room" }, pair.GetProperty("removedHistoryIds")
            .EnumerateArray().Select(value => value.GetString())));
    }

    private sealed class MutatingMind(Action mutate) : IReflectionMind
    {
        public int Calls { get; private set; }
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++Calls == 1) mutate();
            await Task.Yield();
            return new("I can leave that encounter alone.", "gift",
                request.Choices.ToDictionary(c => c.Id, c => c.Id == "reject" ? 1.0 : 0.0), "test");
        }
    }

    private static TownData Town()
    {
        var gift = new ActKind("GaveGift", 1.5, 1, 1, 1, 2, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, .2, .3, 1, TargetIs.Chosen, Tilt: 1));
        var help = new ActKind("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, .3, .3, 1, TargetIs.Chosen, Tilt: 1));
        Villager Person(string name, int x) => new(name, name, "villager", new Temperament(1, .5, .5, .6),
            new Body(100, -1), null, new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
            new Dictionary<string, double> { ["GaveGift"] = 1 }, Array.Empty<string>());
        FeelingOptions feelings = FeelingOptions.WithDesire();
        feelings.AnswerOn = feelings.ReturnOn = feelings.MakeUpOn = feelings.RetaliateOn = false;
        feelings.AvoidOn = feelings.WithdrawOn = false;
        return new()
        {
            Cast = new[] { Person("Ann", 3), Person("Bob", 5) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(), Acts = new[] { gift, help },
            Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static HistoryPack History() => new(1,
        new[] { new HistorySource("source", "synthetic:ann-room", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "fact", "Ann", "I spend time in this room.", KnowledgeKind.Lived,
            new[] { "source" }, new[] { new HistoryCue("place:Room") }) },
        new[] { new HistoryReading("reading:ann-room", "account", new MemoryObject(MemoryObjectKind.Place, "Room"),
            ReadingStance.Warm, "I enjoy being here.", ReadingOrigin.CanonStated, AdmissionRule.R2,
            new HistoryReview(ReviewStatus.Reviewed, "Synthetic test only", "2026-10-15"), new[] { "source" },
            new[] { new HistoryCue("place:Room") }) });
}
