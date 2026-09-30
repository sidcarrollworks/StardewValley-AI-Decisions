using NpcDecision;
using NpcIntents;
using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcIntents.Tests;

public class PlannerNewsTests
{
    private static readonly Newsworthiness News = new();

    // ---------- helpers ----------

    private static RegionMap Regions()
    {
        var map = new RegionMap
        {
            BlockMinutes = 120,
            Regions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Town"] = new[] { "Town", "SeedShop", "Saloon" },
                ["Mountain"] = new[] { "Mountain", "ScienceHouse" },
            },
        };
        map.Rebuild();
        return map;
    }

    private static NewsContext Context(string observer, int hearts = 0)
        => new(observer,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Abigail"] = "SeedShop",
                ["Caroline"] = "SeedShop", // lives with Abigail
                ["Sebastian"] = "ScienceHouse",
            },
            new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase),
            Regions(),
            hearts);

    private static NpcMemorySnapshot Snap(string npc, int tick, string subject, string kind, string? detail = null)
        => new(npc, "voice", new DiaryEntry[] { new(tick, subject, kind, detail) },
            Array.Empty<string>(), Context(npc));

    private static IntentPlanner Planner(IDecisionClient decision)
        => new(decision, new StubRenderer(), News);

    private sealed class StubRenderer : ILineRenderer
    {
        public string Render(string npc, string voice, DiaryEntry entry)
            => $"LINE:{entry.Kind}:{entry.Subject}";
    }

    private sealed class RecordingDecisionClient : IDecisionClient
    {
        public int YesNoCalls;
        public int ChooseCalls;
        public readonly List<List<string>> ChooseOptions = new();

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            ChooseCalls++;
            ChooseOptions.Add(options.ToList());
            double p = 1.0 / options.Count;
            return options.Select(_ => p).ToArray();
        }

        public double YesNo(string context, string proposition)
        {
            YesNoCalls++;
            return 0.5;
        }

        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }

    // ---------- tests ----------

    [Fact]
    public void ZeroNewsEntries_AreNeverOptions()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Caroline", "Saw", "SeedShop"),                                  // housemate at home: 0
            new(20, "Player", "Talked", DiaryDetail.Format(("hearts", "2"))),        // 1
        }, Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        IntentPlan plan = Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("Talked", candidate.Source.Kind);
        Assert.Equal(1, client.ChooseCalls);
        string option = Assert.Single(client.ChooseOptions[0]);
        Assert.StartsWith("Talked Player", option);
    }

    [Fact]
    public void SnapshotWithNothingNewsworthy_SkipsWithoutAModelCall()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice",
            new DiaryEntry[] { new(10, "Caroline", "Saw", "SeedShop") }, // 0: housemate at home
            Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        IntentPlan plan = Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Empty(plan.Candidates);
        Assert.Equal(0, client.YesNoCalls);
        Assert.Equal(0, client.ChooseCalls);
    }

    [Fact]
    public void Options_AreOrderedByNews_NotRecency()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // news 4, older
            new(20, "Player", "Talked", DiaryDetail.Format(("hearts", "2"))),            // news 1, newer
        }, Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Equal(2, client.ChooseOptions[0].Count);
        Assert.StartsWith("BirthdayForgotten", client.ChooseOptions[0][0]); // best news first
        Assert.StartsWith("Talked", client.ChooseOptions[0][1]);
    }

    [Fact]
    public void FakeBackend_SpeakersAreTheBestNewsNpcs()
    {
        var snapshots = new[]
        {
            Snap("Abigail", 20, "Player", "Talked", DiaryDetail.Format(("hearts", "2"))),          // 1
            Snap("Alex", 20, "Player", "Saw", "Town"),                                             // 2
            Snap("Sebastian", 20, "Player", "IgnoredBy", "Emote"),                                 // 3
            Snap("Robin", 20, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4
        };

        IntentPlan plan = new IntentPlanner(new FakeDecisionClient(), new StubRenderer(), News)
            .Plan(snapshots, 42, sourceDay: 0);

        Assert.Equal(new[] { "Robin", "Sebastian", "Alex" }, plan.Candidates.Select(c => c.Npc).ToArray());
        Assert.Equal(new[] { 4.0, 3.0, 2.0 }, plan.Candidates.Select(c => c.News).ToArray());
    }

    [Fact]
    public void EqualNews_TiesBreakByName()
    {
        var snapshots = new[]
        {
            Snap("Willy", 20, "Player", "Saw", "Town"), // 2
            Snap("Alex", 20, "Player", "Saw", "Town"),  // 2
        };

        IntentPlan plan = new IntentPlanner(new FakeDecisionClient(), new StubRenderer(), News)
            .Plan(snapshots, 42, sourceDay: 0);

        Assert.Equal(new[] { "Alex", "Willy" }, plan.Candidates.Select(c => c.Npc).ToArray());
    }

    [Fact]
    public void Ranking_UsesTheBestOptionNews_NotTheSampledOne()
    {
        // Abigail has two options: news 4 and news 1; a skewed Choose makes her sample the 1.
        // Willy has a single option of news 2. The ranking key is the BEST news among the
        // options (intents.md, planned change 2), so Abigail must still rank above Willy.
        var abigail = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4
            new(20, "Player", "Talked", DiaryDetail.Format(("hearts", "2"))),            // 1
        }, Array.Empty<string>(), Context("Abigail"));
        var willy = Snap("Willy", 20, "Player", "Saw", "Town"); // 2

        IntentPlan plan = new IntentPlanner(new SkewedClient(), new StubRenderer(), News)
            .Plan(new[] { abigail, willy }, 42, sourceDay: 0);

        Assert.Equal(new[] { "Abigail", "Willy" }, plan.Candidates.Select(c => c.Npc).ToArray());
        Assert.Equal(new[] { 4.0, 2.0 }, plan.Candidates.Select(c => c.News).ToArray());
    }

    [Fact]
    public void GiftReceived_Love_Outranks_SawOfThePlayer()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "Saw", "Town"),                    // 2
            new(20, "Player", "GiftReceived", "taste=Love"),     // 5
        }, Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Equal(2, client.ChooseOptions[0].Count);
        Assert.StartsWith("GiftReceived", client.ChooseOptions[0][0]); // best news first
        Assert.StartsWith("Saw", client.ChooseOptions[0][1]);
    }

    [Fact]
    public void SnapshotBeliefs_AreImmuneToLaterObserves()
    {
        // The planner's context carries a snapshot; the game thread keeps observing the live
        // belief. A later observe must not change what the planner sees (AGENTS.md: two threads).
        var live = new RoutineBelief("Abigail", "Willy", 120);
        live.Observe("Town", TimeUtils.BlockIndex(100 % 120, 120), 100, 12);
        var snapshot = live.Snapshot();

        var context = new NewsContext("Abigail",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Willy"] = "Saloon" },
            new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase) { ["Willy"] = snapshot },
            Regions(), 0);
        var entry = new DiaryEntry(100, "Willy", "Saw", "Mountain");

        double before = News.Score(entry, context);
        Assert.Equal(2.0, before); // unusual: evidence 12, Mountain share 0 < 0.15

        live.Observe("Mountain", TimeUtils.BlockIndex(100 % 120, 120), 100, 100); // would flip the verdict

        Assert.Equal(before, News.Score(entry, context)); // the snapshot did not change
    }

    /// <summary>Chooses the LAST option (all probability mass on it), so a multi-option NPC
    /// always samples their worst ranked one.</summary>
    private sealed class SkewedClient : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            var p = new double[options.Count];
            if (options.Count > 0)
                p[^1] = 1.0;
            return p;
        }

        public double YesNo(string context, string proposition) => 0.5;
        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }
}
