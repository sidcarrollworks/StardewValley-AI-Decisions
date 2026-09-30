using NpcIntents;
using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcIntents.Tests;

public class NewsworthinessTests
{
    private static readonly Newsworthiness Scorer = new();

    // ---------- helpers ----------

    private static DiaryEntry Entry(int tick, string subject, string kind, string? detail = null)
        => new(tick, subject, kind, detail);

    /// <summary>A small regions.json in code: the lookup is built by Rebuild().</summary>
    private static RegionMap Regions()
    {
        var map = new RegionMap
        {
            BlockMinutes = 120,
            Regions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Farm"] = new[] { "Farm", "FarmHouse", "Backwoods" },
                ["Town"] = new[] { "Town", "SeedShop", "Saloon" },
                ["Mountain"] = new[] { "Mountain", "CarpenterShop", "ScienceHouse" },
                ["Forest"] = new[] { "Forest" },
                ["Beach"] = new[] { "Beach", "FishShop" },
            },
        };
        map.Rebuild();
        return map;
    }

    private static IReadOnlyDictionary<string, string> DefaultHomes()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sebastian"] = "ScienceHouse",
            ["Robin"] = "ScienceHouse",
            ["Abigail"] = "SeedShop",
        };

    private static IReadOnlyDictionary<string, RoutineBelief> Beliefs(params RoutineBelief[] beliefs)
    {
        var map = new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase);
        foreach (RoutineBelief belief in beliefs)
            map[belief.Subject] = belief;
        return map;
    }

    private static NewsContext Context(
        string observer,
        int hearts = 0,
        IReadOnlyList<(string Kind, string Subject)>? citations = null,
        IReadOnlyDictionary<string, string>? homes = null,
        IReadOnlyDictionary<string, RoutineBelief>? beliefs = null,
        RegionMap? regions = null)
        => new(
            observer,
            homes ?? DefaultHomes(),
            beliefs ?? new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase),
            regions ?? Regions(),
            hearts,
            citations);

    /// <summary>The 2-hour block an absolute tick falls in (days are 120 ticks).</summary>
    private static int BlockOf(int tick) => TimeUtils.BlockIndex(tick % TimeUtils.TicksPerDay, 120);

    /// <summary>A belief with one observed region/block cell.</summary>
    private static RoutineBelief Believed(string observer, string subject, int tick, string region, double strength)
    {
        var belief = new RoutineBelief(observer, subject, 120);
        belief.Observe(region, BlockOf(tick), tick, strength);
        return belief;
    }

    private static string GiftDetail(string taste, bool birthday)
        => DiaryDetail.Format(
            ("item", "(O)421"), ("name", "Sunflower"), ("taste", taste), ("birthday", birthday ? "1" : "0"));

    // ---------- Saw ----------

    [Fact]
    public void Saw_Player_ScoresTwo()
        => Assert.Equal(2.0, Scorer.Score(Entry(10, "Player", "Saw", "Town"), Context("Abigail")));

    [Fact]
    public void Saw_PlayerSubject_IsCaseInsensitive()
        => Assert.Equal(2.0, Scorer.Score(Entry(10, "player", "Saw", "Town"), Context("Abigail")));

    [Fact]
    public void Saw_PlayerAtFourHearts_AddsOne()
        => Assert.Equal(3.0, Scorer.Score(
            Entry(10, MemoryStore.PlayerName, "Saw", "Town"), Context("Abigail", hearts: 4)));

    [Fact]
    public void Saw_Player_WithRecentCitation_LosesOne()
    {
        var citations = new List<(string Kind, string Subject)> { ("Saw", "Player") };

        double score = Scorer.Score(Entry(10, "Player", "Saw", "Town"), Context("Abigail", citations: citations));

        Assert.Equal(1.0, score);
    }

    [Fact]
    public void Saw_HousemateAtHome_ScoresZero()
        => Assert.Equal(0.0, Scorer.Score(Entry(10, "Sebastian", "Saw", "ScienceHouse"), Context("Robin")));

    [Fact]
    public void Saw_HousemateAtHome_LookupsAreCaseInsensitive()
    {
        // Deliberately a case-SENSITIVE dictionary and mixed-case values.
        var homes = new Dictionary<string, string>
        {
            ["sebastian"] = "sciencehouse",
            ["ROBIN"] = "SCIENCEHOUSE",
        };

        double score = Scorer.Score(Entry(10, "Sebastian", "Saw", "ScienceHouse"), Context("Robin", homes: homes));

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Saw_HousemateAwayFromHome_ScoresHalf()
        => Assert.Equal(0.5, Scorer.Score(Entry(10, "Sebastian", "Saw", "Town"), Context("Robin")));

    [Fact]
    public void Saw_NonHousemateAtTheirHome_ScoresHalf()
        // Sebastian at ScienceHouse is his home, but the observer (Abigail) does not live there.
        => Assert.Equal(0.5, Scorer.Score(Entry(10, "Sebastian", "Saw", "ScienceHouse"), Context("Abigail")));

    [Fact]
    public void Saw_UnknownNpc_NoHomeNoBelief_ScoresHalf()
        => Assert.Equal(0.5, Scorer.Score(Entry(10, "Penny", "Saw", "Town"), Context("Abigail")));

    [Fact]
    public void Saw_MissingContextTables_ScoresHalf()
    {
        var context = new NewsContext("Abigail", null!, null!, null!, 0);

        Assert.Equal(0.5, Scorer.Score(Entry(10, "Sebastian", "Saw", "Town"), context));
    }

    [Fact]
    public void NullEntry_ScoresZero()
        => Assert.Equal(0.0, Scorer.Score(null!, Context("Abigail")));

    // ---------- Saw: unusual ----------

    [Fact]
    public void Saw_UnusualPlace_ScoresTwo()
    {
        // The observer's belief about Sam has 13 evidence in this block, only 1 of it on the
        // Mountain: seeing Sam at the CarpenterShop (Mountain) is news.
        int tick = 144; // day 2, same block as tick 24
        var belief = new RoutineBelief("Abigail", "Sam", 120);
        belief.Observe("Town", BlockOf(tick), tick, 12.0);
        belief.Observe("Mountain", BlockOf(tick), tick, 1.0);

        double score = Scorer.Score(
            Entry(tick, "Sam", "Saw", "CarpenterShop"),
            Context("Abigail", beliefs: Beliefs(belief)));

        Assert.Equal(2.0, score);
    }

    [Fact]
    public void Saw_UsualPlace_IsNotUnusual()
    {
        int tick = 144;
        var belief = new RoutineBelief("Abigail", "Sam", 120);
        belief.Observe("Town", BlockOf(tick), tick, 12.0);
        belief.Observe("Mountain", BlockOf(tick), tick, 1.0);

        double score = Scorer.Score(
            Entry(tick, "Sam", "Saw", "Saloon"), // Town: 12/13 of the evidence
            Context("Abigail", beliefs: Beliefs(belief)));

        Assert.Equal(0.5, score);
    }

    [Fact]
    public void Saw_Unusual_NeedsTheEvidenceFloor()
    {
        // Below the 12-evidence floor even a region the belief has never seen (share 0) is not
        // "unusual"; 12 is enough.
        int tick = 144;
        RoutineBelief thin = Believed("Abigail", "Sam", tick, "Town", 11.5);
        RoutineBelief enough = Believed("Abigail", "Sam", tick, "Town", 12.0);

        Assert.Equal(0.5, Scorer.Score(
            Entry(tick, "Sam", "Saw", "CarpenterShop"), Context("Abigail", beliefs: Beliefs(thin))));
        Assert.Equal(2.0, Scorer.Score(
            Entry(tick, "Sam", "Saw", "CarpenterShop"), Context("Abigail", beliefs: Beliefs(enough))));
    }

    [Fact]
    public void Saw_BeliefLearnedInAnotherBlock_IsNotUnusual()
    {
        // Plenty of evidence, but in block 1; the entry falls in block 2.
        RoutineBelief belief = Believed("Abigail", "Sam", tick: 12, "Town", 20.0);

        double score = Scorer.Score(
            Entry(24, "Sam", "Saw", "CarpenterShop"), Context("Abigail", beliefs: Beliefs(belief)));

        Assert.Equal(0.5, score);
    }

    [Fact]
    public void Saw_UnusualBeliefLookup_IsCaseInsensitive()
    {
        // Case-sensitive dictionary on purpose; the subject is "Sam", the key "SAM".
        int tick = 144;
        RoutineBelief belief = Believed("Abigail", "Sam", tick, "Town", 13.0);
        var beliefs = new Dictionary<string, RoutineBelief> { ["SAM"] = belief };

        double score = Scorer.Score(
            Entry(tick, "Sam", "Saw", "CarpenterShop"), Context("Abigail", beliefs: beliefs));

        Assert.Equal(2.0, score);
    }

    [Fact]
    public void Saw_UnmappedLocation_CountsAsOtherRegion()
    {
        int tick = 144;
        RoutineBelief belief = Believed("Abigail", "Sam", tick, "Town", 13.0);

        // Not in the region map: the sighting counts against "Other", which has 0 evidence.
        double score = Scorer.Score(
            Entry(tick, "Sam", "Saw", "GingerIsland"), Context("Abigail", beliefs: Beliefs(belief)));

        Assert.Equal(2.0, score);
    }

    // ---------- fixed-weight kinds ----------

    [Theory]
    [InlineData("IgnoredBy", 3.0)]
    [InlineData("Talked", 1.0)]
    [InlineData("QuestHelped", 4.0)]
    [InlineData("Festival", 2.0)]
    [InlineData("MissedFestival", 2.0)]
    [InlineData("PassedBy", 2.0)]
    [InlineData("BirthdayForgotten", 4.0)]
    [InlineData("AcceptedInvite", 4.0)]
    [InlineData("StoodUp", 4.0)]
    [InlineData("MissedVisit", 3.0)]
    [InlineData("Traded", 2.0)]
    [InlineData("StartedDating", 5.0)]
    [InlineData("Engaged", 5.0)]
    [InlineData("Married", 5.0)]
    [InlineData("Divorced", 5.0)]
    [InlineData("Anniversary", 5.0)]
    [InlineData("ChattedWith", 1.0)]
    [InlineData("MetUpWith", 1.0)]
    [InlineData("LookedFor", 1.0)]
    public void FixedWeightKinds_ComeFromTheTable(string kind, double expected)
        => Assert.Equal(expected, Scorer.Score(Entry(10, "Player", kind), Context("Abigail")));

    [Fact]
    public void FixedWeightKinds_KindLookup_IsCaseInsensitive()
        => Assert.Equal(3.0, Scorer.Score(Entry(10, "Player", "ignoredby"), Context("Abigail")));

    // ---------- GiftReceived ----------

    [Theory]
    [InlineData("Love", 5.0)]
    [InlineData("Like", 3.0)]
    [InlineData("Neutral", 1.0)]
    [InlineData("Dislike", 3.0)]
    [InlineData("Hate", 4.0)]
    [InlineData("love", 5.0)] // tastes are case-insensitive
    public void GiftReceived_WeighsByTaste(string taste, double expected)
        => Assert.Equal(expected, Scorer.Score(
            Entry(10, "Player", "GiftReceived", GiftDetail(taste, birthday: false)), Context("Abigail")));

    [Fact]
    public void GiftReceived_UnknownTaste_CountsAsNeutral()
    {
        Assert.Equal(1.0, Scorer.Score(
            Entry(10, "Player", "GiftReceived", "item=(O)421;name=Sunflower;taste=Yuck"), Context("Abigail")));
        Assert.Equal(1.0, Scorer.Score(
            Entry(10, "Player", "GiftReceived", "item=(O)421;name=Sunflower"), Context("Abigail")));
        Assert.Equal(1.0, Scorer.Score(
            Entry(10, "Player", "GiftReceived", null), Context("Abigail")));
    }

    [Fact]
    public void GiftReceived_OnBirthday_AddsTwo()
    {
        Assert.Equal(7.0, Scorer.Score(
            Entry(10, "Player", "GiftReceived", GiftDetail("Love", birthday: true)), Context("Abigail")));
        Assert.Equal(5.0, Scorer.Score(
            Entry(10, "Player", "GiftReceived", GiftDetail("Love", birthday: false)), Context("Abigail")));
    }

    // ---------- SawGift / WentLooking ----------

    [Fact]
    public void SawGift_Two_OrThreeAtSixHearts()
    {
        string detail = "giver=Player;name=Sunflower;taste=Love";

        Assert.Equal(2.0, Scorer.Score(Entry(10, "Penny", "SawGift", detail), Context("Abigail", hearts: 5)));
        Assert.Equal(3.0, Scorer.Score(Entry(10, "Penny", "SawGift", detail), Context("Abigail", hearts: 6)));
        Assert.Equal(3.0, Scorer.Score(Entry(10, "Penny", "SawGift", detail), Context("Abigail", hearts: 10)));
    }

    [Fact]
    public void WentLooking_Four_OrTwoWhenFound()
    {
        Assert.Equal(4.0, Scorer.Score(
            Entry(10, "Player", "WentLooking", "place=Town;found=0"), Context("Abigail")));
        Assert.Equal(2.0, Scorer.Score(
            Entry(10, "Player", "WentLooking", "place=Town;found=1"), Context("Abigail")));
    }

    // ---------- skipped and unknown kinds ----------

    [Fact]
    public void TriedToReach_ScoresZero_EvenWithHeartsAndCitations()
    {
        var citations = new List<(string Kind, string Subject)> { ("TriedToReach", "Player") };

        double score = Scorer.Score(
            Entry(10, "Player", "TriedToReach"), Context("Abigail", hearts: 8, citations: citations));

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void HeldAGrudge_ScoresZero()
        => Assert.Equal(0.0, Scorer.Score(
            Entry(10, "Player", "HeldAGrudge", "points=-4"), Context("Abigail", hearts: 8)));

    [Fact]
    public void UnknownKind_ScoresZero()
        => Assert.Equal(0.0, Scorer.Score(Entry(10, "Player", "SpokeWith"), Context("Abigail", hearts: 10)));

    // ---------- recent citations ----------

    [Fact]
    public void RecentCitations_StackOnePerMatch_CaseInsensitive()
    {
        var citations = new List<(string Kind, string Subject)>
        {
            ("IgnoredBy", "Player"),
            ("ignoredby", "player"),  // same (kind, subject), case-insensitively: still a match
            ("Talked", "Player"),     // different kind
            ("IgnoredBy", "Abigail"), // different subject
        };

        double score = Scorer.Score(Entry(10, "Player", "IgnoredBy"), Context("Abigail", citations: citations));

        Assert.Equal(1.0, score); // 3 - 1 - 1
    }

    // ---------- determinism ----------

    [Fact]
    public void Score_IsDeterministic()
    {
        int tick = 144;
        var belief = new RoutineBelief("Abigail", "Sam", 120);
        belief.Observe("Town", BlockOf(tick), tick, 12.0);
        belief.Observe("Mountain", BlockOf(tick), tick, 1.0);

        DiaryEntry entry = Entry(tick, "Sam", "Saw", "CarpenterShop");
        NewsContext context = Context("Abigail", beliefs: Beliefs(belief));

        double first = Scorer.Score(entry, context);
        double second = Scorer.Score(entry, context);
        double third = new Newsworthiness().Score(entry, context);

        Assert.Equal(2.0, first);
        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }
}
