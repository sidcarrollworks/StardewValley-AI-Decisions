using NpcDecision;
using NpcIntents;
using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcIntents.Tests;

/// <summary>
/// Step 5 (intents ready to ship): novelty with a one-option fall, one topic per subject across
/// NPCs, and the save-file shapes for the plan and delivered lines.
/// </summary>
public class PlannerNoveltyTests
{
    private static readonly Newsworthiness News = new();

    private static RegionMap Regions()
    {
        var map = new RegionMap
        {
            BlockMinutes = 120,
            Regions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Town"] = new[] { "Town", "SeedShop", "Saloon" },
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
                ["Haley"] = "HaleyHouse",
            },
            new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase),
            Regions(),
            hearts);

    private static NpcMemorySnapshot Snap(string npc, IReadOnlyList<DiaryEntry> entries,
        IReadOnlyList<string> recentLines, int hearts = 0)
        => new(npc, "voice", entries, recentLines, Context(npc, hearts));

    private sealed class StubRenderer : ILineRenderer
    {
        public string Render(string npc, string voice, DiaryEntry entry)
            => $"LINE:{entry.Kind}:{entry.Subject}";
    }

    /// <summary>Heavily favours the FIRST option (0.6/0.4), so the first pick is deterministic
    /// but a fall can still sample the second.</summary>
    private sealed class FirstOptionClient : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            var p = new double[options.Count];
            if (options.Count == 1)
                p[0] = 1.0;
            else if (options.Count > 1)
            {
                p[0] = 0.6;
                p[1] = 0.4;
            }
            return p;
        }

        public double YesNo(string context, string proposition) => 0.9;
        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }

    [Fact]
    public void ARepeatedLineFallsToTheNextOptionOnce()
    {
        // The NPC already said the gift line; the gift is sampled first (all the mass), rejected
        // as novelty, and the saw becomes the line. One fall, no second chance.
        var snapshot = Snap("Abigail", new DiaryEntry[]
        {
            new(10, "Player", "GiftReceived", "taste=Love"), // 5, sampled first
            new(20, "Player", "Saw", "Town"),                // 2, the fall
        }, new[] { "LINE:GiftReceived:Player" });

        IntentPlan plan = new IntentPlanner(new FirstOptionClient(), new StubRenderer(), News)
            .Plan(new[] { snapshot }, 42, sourceDay: 0);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("Saw", candidate.Source.Kind);
    }

    [Fact]
    public void ARepeatedLineWithNoFallLeavesTheNpcSilent()
    {
        // One option, already said: the fall finds nothing and the NPC is skipped.
        var snapshot = Snap("Abigail", new DiaryEntry[]
        {
            new(10, "Player", "GiftReceived", "taste=Love"),
        }, new[] { "LINE:GiftReceived:Player" });

        IntentPlan plan = new IntentPlanner(new FirstOptionClient(), new StubRenderer(), News)
            .Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Empty(plan.Candidates);
    }

    [Fact]
    public void TheHigherHeartsSpeakerKeepsASharedEvent()
    {
        // Both saw the player give Haley a gift at the same moment: one event, one speaker. Haley
        // has more hearts, so she takes it and Abigail drops (no fallback option).
        var eventEntry = new DiaryEntry(50, "Haley", "SawGift", "item=1");
        var abigail = Snap("Abigail", new[] { eventEntry }, Array.Empty<string>(), hearts: 0);
        var haley = Snap("Haley", new[] { eventEntry }, Array.Empty<string>(), hearts: 8);

        IntentPlan plan = new IntentPlanner(new FirstOptionClient(), new StubRenderer(), News)
            .Plan(new[] { abigail, haley }, 42, sourceDay: 0);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("Haley", candidate.Npc);
        Assert.Equal("SawGift", candidate.Source.Kind);
    }

    [Fact]
    public void EqualHeartsTheFirstRankedSpeakerKeepsTheEvent()
    {
        var eventEntry = new DiaryEntry(50, "Haley", "SawGift", "item=1");
        var abigail = Snap("Abigail", new[] { eventEntry }, Array.Empty<string>(), hearts: 2);
        var haley = Snap("Haley", new[] { eventEntry }, Array.Empty<string>(), hearts: 2);

        IntentPlan plan = new IntentPlanner(new FirstOptionClient(), new StubRenderer(), News)
            .Plan(new[] { abigail, haley }, 42, sourceDay: 0);

        Assert.Equal("Abigail", Assert.Single(plan.Candidates).Npc); // ranked first, keeps it
    }

    [Fact]
    public void ASharedEventLoserFallsToItsNextOption()
    {
        // Abigail cites the shared event first and loses it to higher-hearts Haley; she falls to
        // her own saw instead of dropping.
        var gift = new DiaryEntry(50, "Haley", "SawGift", "item=1");
        var abigail = Snap("Abigail", new DiaryEntry[]
        {
            gift,
            new(20, "Player", "Saw", "Town"),
        }, Array.Empty<string>(), hearts: 0);
        var haley = Snap("Haley", new[] { gift }, Array.Empty<string>(), hearts: 8);

        IntentPlan plan = new IntentPlanner(new FirstOptionClient(), new StubRenderer(), News)
            .Plan(new[] { abigail, haley }, 42, sourceDay: 0);

        // The shared event goes to Haley and Abigail ends up with her own saw — whether Abigail
        // sampled the gift first (takeover, one fall) or the saw first (straight pick).
        Assert.Equal(2, plan.Candidates.Count);
        IntentCandidate haleys = Assert.Single(plan.Candidates.Where(c => c.Npc == "Haley"));
        Assert.Equal("SawGift", haleys.Source.Kind);
        IntentCandidate abigails = Assert.Single(plan.Candidates.Where(c => c.Npc == "Abigail"));
        Assert.Equal("Saw", abigails.Source.Kind);
    }

    // ---- persistence shapes ------------------------------------------------------------------

    [Fact]
    public void SavedPlan_RoundTrips()
    {
        var plan = new PlanPersistence.SavedPlan(42, false, new[]
        {
            new IntentCandidate("Haley", "line", new DiaryEntry(10, "Player", "Saw", "Town"), "reason", 2),
        });

        PlanPersistence.SavedPlan? loaded = PlanPersistence.SavedPlan.FromJson(plan.ToJson());

        Assert.NotNull(loaded);
        Assert.Equal(42, loaded!.Day);
        Assert.False(loaded.Delivered);
        Assert.Equal("Haley", Assert.Single(loaded.Candidates).Npc);
        Assert.Equal(2.0, Assert.Single(loaded.Candidates).News);
    }

    [Fact]
    public void RecentLines_RoundTripAndTrim()
    {
        var lines = new Dictionary<string, List<PlanPersistence.RecentLine>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 25; i++)
            PlanPersistence.Append(lines, new PlanPersistence.RecentLine("Haley", $"line {i}", 40, "Saw", "Player"), keep: 20);

        List<PlanPersistence.RecentLine> loaded = PlanPersistence.RecentLine.FromJson(
            PlanPersistence.RecentLine.ToJson(lines.Values.SelectMany(l => l)));

        Assert.Equal(20, loaded.Count);                 // trimmed to RecentLinesKept
        Assert.Equal("line 24", loaded[^1].Line);       // newest kept
        Assert.DoesNotContain(loaded, l => l.Line == "line 4");
    }

    [Fact]
    public void RecentCitations_OnlyWithinTheCooldown()
    {
        var lines = new List<PlanPersistence.RecentLine>
        {
            new("Haley", "a", 10, "Saw", "Player"),
            new("Haley", "b", 11, "GiftReceived", "Player"),
            new("Haley", "c", 7, "QuestHelped", "Player"), // four days ago: outside 3
        };

        IReadOnlyList<(string Kind, string Subject)> citations = PlanPersistence.RecentCitations(lines, today: 11, cooldownDays: 3);

        Assert.Equal(2, citations.Count);
        Assert.Contains(("Saw", "Player"), citations);
        Assert.Contains(("GiftReceived", "Player"), citations);
    }
}
