using NpcDecision;
using NpcInitiation;
using NpcIntents;
using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>The viewer's snapshot: what it shows, and that building it changes nothing.</summary>
public sealed class MindsSnapshotBuilderTests
{
    // Spring 3, year 1: day index 2. Tick 48 of the day is 2:00 pm.
    private static readonly int Today = GameClock.DayStartTick(2);
    private static readonly int Now = Today + 48;

    private static RegionMap Regions()
    {
        var map = new RegionMap
        {
            BlockMinutes = 120,
            Regions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Town"] = new[] { "Town", "SeedShop", "Saloon" },
                ["Farm"] = new[] { "Farm", "FarmHouse" },
            },
        };
        map.Rebuild();
        return map;
    }

    private static NewsContext News(string npc)
        => new(npc, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Abigail"] = "SeedShop" },
            new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase), Regions(), 0);

    private static MemoryStore Memory()
    {
        var memory = new MemoryStore();
        memory.Note("Abigail", new DiaryEntry(Today - 100, "Player", "Saw", "Town"));      // yesterday
        memory.Note("Abigail", new DiaryEntry(Today + 10, "Player", "Saw", "SeedShop"));   // today 7:40 am
        memory.Note("Abigail", new DiaryEntry(Today + 20, "Player", "TriedToReach", "Emote"));
        memory.Note("Abigail", new DiaryEntry(Today + 30, "Player", "BirthdayForgotten"));
        memory.Note("Sam", new DiaryEntry(Today + 5, "Abigail", "Saw", "Town"));
        return memory;
    }

    private static string LadderJson()
    {
        // A real ladder's own JSON: Abigail sees the player every tick and the model always says yes.
        var ladder = new InitiationLadder(new Always(1.0), seed: 3, new InitiationOptions { BaseGainPerTick = 0.4, HeartsGainPerTick = 0 });
        var view = new LedgerView("Abigail", "Player", LedgerDetail.NamedSpot, "SeedShop", 0, 0, Now - 1, "3,4");
        ladder.Tick(Now - 1, new[] { new InitiationInput("Abigail", view, false, 2) }, _ => new Diary());
        return ladder.ToJson();
    }

    private sealed class Always : IDecisionClient
    {
        private readonly double _p;
        public Always(double p) => _p = p;
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => options.Select(_ => 1.0 / options.Count).ToArray();
        public double Score(string context, double min, double max) => min;
        public double YesNo(string context, string proposition) => _p;
    }

    private static MindsInputs Inputs(IReadOnlyList<InitiationInput>? ladderInputs = null, IReadOnlyList<IntentCandidate>? plan = null)
        => new(7, Now, "Laya", true, new MindsStats(1, 0, 12, 0, 30, 49), "none", LadderJson(),
            ladderInputs ?? new[]
            {
                new InitiationInput("Abigail", new LedgerView("Abigail", "Player", LedgerDetail.Location, "SeedShop", 18, 0, Now - 18), false, 2,
                    new Whereabouts("Abigail", "Player", WhereaboutsSource.SeenToday, "SeedShop", LedgerDetail.Location, 18, 0, null, 0)),
                new InitiationInput("Sam", new LedgerView("Sam", "Player", LedgerDetail.Location, "Town", 6, 1, Now - 6, null, "Abigail"), false, 0,
                    new Whereabouts("Sam", "Player", WhereaboutsSource.Told, "Town", LedgerDetail.Location, 6, 1, "Abigail", 0)),
            },
            new[] { "Abigail" },
            plan ?? new[] { new IntentCandidate("Abigail", "I saw you at Pelican Town yesterday.", new DiaryEntry(Today - 100, "Player", "Saw", "Town"), "cited x (sampled p=1)", 2) },
            new[] { new FeedItem(1, Now, "2:00 pm", "Attempt", "Abigail", "Abigail would try Emote") },
            News);

    [Fact]
    public void BuildingASnapshotChangesNothingInMemory()
    {
        MemoryStore memory = Memory();
        string before = memory.ToJson();

        new MindsSnapshotBuilder().Build(memory, Inputs());

        Assert.Equal(before, memory.ToJson());
    }

    [Fact]
    public void NpcsComeInNameOrderWithTheClockAndStats()
    {
        MindsSnapshot s = new MindsSnapshotBuilder().Build(Memory(), Inputs());

        Assert.Equal(new[] { "Abigail", "Sam" }, s.Npcs.Select(n => n.Name));
        Assert.Equal("spring 3, year 1", s.Date);
        Assert.Equal("2:00 pm", s.Time);
        Assert.Equal(7, s.Seq);
        Assert.Equal("Laya", s.Backend);
        Assert.Equal(12, s.Stats.Calls);
        Assert.Equal(new InitiationOptions().StepThresholds, s.StepThresholds);
        Assert.Equal("Emote", s.StepNames[0]);
        Assert.Single(s.Feed);
    }

    [Fact]
    public void LadderStateComesFromTheLadderJson()
    {
        NpcMind abigail = new MindsSnapshotBuilder().Build(Memory(), Inputs()).Npcs[0];

        Assert.True(abigail.Urge >= 0.3, $"urge {abigail.Urge}");
        Assert.Equal(0, abigail.Rung);
        Assert.Equal("Emote", abigail.RungName);
        Assert.Equal(0.30, abigail.NextThreshold);
        Assert.Equal("Emote", abigail.OpenStep);  // the attempt is still open
        Assert.Equal(1, abigail.OpenForTicks);
        Assert.Equal(1, abigail.AttemptsToday);
        Assert.Equal(2, abigail.Hearts);

        NpcMind sam = new MindsSnapshotBuilder().Build(Memory(), Inputs()).Npcs[1];
        Assert.Equal(0.0, sam.Urge); // never ticked by the ladder
        Assert.Null(sam.OpenStep);
    }

    [Fact]
    public void LastSeenAndLeadReadAsPlainWords()
    {
        MindsSnapshot s = new MindsSnapshotBuilder().Build(Memory(), Inputs());

        Assert.Equal("saw you at Pierre's General Store, 3 hours ago", s.Npcs[0].LastSeen!.Summary);
        Assert.Equal("Pelican Town: Abigail saw you there an hour ago", s.Npcs[1].Lead!.Summary);
        Assert.Equal("Abigail told them you were at Pelican Town, an hour ago", s.Npcs[1].LastSeen!.Summary);
        Assert.Equal("Told", s.Npcs[1].Lead!.Source);
    }

    [Fact]
    public void ALeadWithoutAPlaceNameLeavesItOut()
    {
        // Playtest 2026-10-02: "you're right here ()" and ": saw you there an hour ago".
        Assert.Equal("you're right here",
            MindsSnapshotBuilder.LeadOf(new Whereabouts("Haley", "Player", WhereaboutsSource.SeenNow, null, null, 0, 0, null, 0))!.Summary);
        Assert.Equal("saw you there an hour ago",
            MindsSnapshotBuilder.LeadOf(new Whereabouts("Lewis", "Player", WhereaboutsSource.SeenToday, null, LedgerDetail.Location, 6, 0, null, 0))!.Summary);
    }

    [Fact]
    public void AnNpcTheLadderHasNoInputForShowsNoView()
    {
        MindsSnapshot s = new MindsSnapshotBuilder().Build(Memory(), Inputs(ladderInputs: Array.Empty<InitiationInput>()));

        Assert.All(s.Npcs, n => Assert.Null(n.LastSeen));
        Assert.All(s.Npcs, n => Assert.Null(n.Lead));
    }

    [Fact]
    public void PlannedLinesAndTheIntentFlagShow()
    {
        MindsSnapshot s = new MindsSnapshotBuilder().Build(Memory(), Inputs());

        Assert.True(s.Npcs[0].IntentToday);
        Assert.Equal("I saw you at Pelican Town yesterday.", s.Npcs[0].PlannedLine);
        Assert.False(s.Npcs[1].IntentToday);
        Assert.Null(s.Npcs[1].PlannedLine);
        PlannedLine line = Assert.Single(s.PlanToday);
        Assert.Equal("Abigail", line.Npc);
    }

    [Fact]
    public void TonightsNewsIsTodaysNewsworthyEntriesBestFirst()
    {
        NpcMind abigail = new MindsSnapshotBuilder().Build(Memory(), Inputs()).Npcs[0];

        // BirthdayForgotten (4) before Saw Player (2); TriedToReach is skipped; yesterday's Saw is old news.
        Assert.Equal(2, abigail.NewsTonight.Count);
        Assert.Contains("birthday", abigail.NewsTonight[0].Sentence);
        Assert.Equal(4, abigail.NewsTonight[0].Score);
        Assert.Contains("Pierre's General Store", abigail.NewsTonight[1].Sentence);
    }

    [Fact]
    public void WithoutANewsContextThereIsNoPreview()
    {
        MindsInputs inputs = Inputs() with { NewsFor = null };
        Assert.All(new MindsSnapshotBuilder().Build(Memory(), inputs).Npcs, n => Assert.Empty(n.NewsTonight));
    }

    [Fact]
    public void TheDiaryIsNewestFirstInPlainWords()
    {
        NpcMind abigail = new MindsSnapshotBuilder().Build(Memory(), Inputs()).Npcs[0];

        Assert.Equal(4, abigail.DiaryCount);
        Assert.Equal("birthday, and you forgot", abigail.Diary[0].Text);
        Assert.Equal("today 11:00 am", abigail.Diary[0].When);
        Assert.Equal("tried to get your attention (Emote)", abigail.Diary[1].Text);
        Assert.Equal("saw you at Pierre's General Store", abigail.Diary[2].Text);
        Assert.Equal("yesterday", abigail.Diary[3].When.Split(' ')[0]);
    }

    [Fact]
    public void TheDiaryKeepsOnlyTheNewestLines()
    {
        var memory = new MemoryStore();
        for (int i = 0; i < 20; i++)
            memory.Note("Abigail", new DiaryEntry(Today + i, "Sam", "Saw", "Town"));

        NpcMind abigail = new MindsSnapshotBuilder().Build(memory, Inputs()).Npcs.Single();

        Assert.Equal(MindsSnapshotBuilder.DiaryLines, abigail.Diary.Count);
        Assert.Equal(Today + 19, abigail.Diary[0].AbsoluteTick);
        Assert.Equal(20, abigail.DiaryCount);
    }

    [Theory]
    [InlineData(0, "6:00 am")]
    [InlineData(36, "12:00 pm")]
    [InlineData(48, "2:00 pm")]
    [InlineData(108, "12:00 am")]
    [InlineData(119, "1:50 am")]
    public void TheClockReadsLikeTheGame(int tick, string expected)
        => Assert.Equal(expected, MindsSnapshotBuilder.Clock(tick));

    [Fact]
    public void AnUnreadableLadderJsonMeansNoLadderState()
    {
        MindsInputs inputs = Inputs() with { LadderJson = "not json" };
        Assert.All(new MindsSnapshotBuilder().Build(Memory(), inputs).Npcs, n => Assert.Equal(0.0, n.Urge));
    }

    [Fact]
    public void AnIdleSnapshotIsEmpty()
    {
        MindsSnapshot idle = MindsSnapshot.Idle(3, "Fake");
        Assert.Empty(idle.Npcs);
        Assert.Equal("Fake", idle.Backend);
        Assert.Equal(3, idle.Seq);
    }
}
