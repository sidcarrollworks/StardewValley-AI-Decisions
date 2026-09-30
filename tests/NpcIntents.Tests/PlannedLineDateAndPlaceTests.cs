using NpcDecision;
using NpcIntents;
using NpcMemory;
using Xunit;

namespace NpcIntents.Tests;

/// <summary>
/// Audit fixes for overnight lines: only the day just ended is news (an old diary entry is never
/// "yesterday"), and lines use player-facing place names, never internal map names.
/// </summary>
public class PlannedLineDateAndPlaceTests
{
    private static int TickOn(int dayIndex, int tickOfDay) => GameClock.DayStartTick(dayIndex) + tickOfDay;

    private static NpcMemorySnapshot Snapshot(string npc, params DiaryEntry[] diary)
        => new(npc, VoiceSheets.Voice(npc), diary, Array.Empty<string>());

    private sealed class AlwaysYes : IDecisionClient
    {
        public List<IReadOnlyList<string>> ChooseCalls { get; } = new();
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            ChooseCalls.Add(options);
            return options.Select(_ => 1.0 / options.Count).ToArray();
        }
        public double Score(string context, double min, double max) => max;
        public double YesNo(string context, string proposition) => 1.0;
    }

    [Theory]
    [InlineData("SeedShop", "Pierre's General Store")]
    [InlineData("ArchaeologyHouse", "the museum")]
    [InlineData("Saloon", "the Stardrop Saloon")]
    [InlineData("BathHouse_Pool", "the spa")]
    [InlineData("SomeModdedPlace", "Some Modded Place")]
    [InlineData("Custom_Room_B", "Custom Room B")]
    [InlineData("the beach", "the beach")]
    [InlineData("", "")]
    public void PlaceNamesAreForPlayers(string internalName, string expected)
        => Assert.Equal(expected, PlaceNames.Display(internalName));

    [Fact]
    public void RenderedLinesUseDisplayNames()
    {
        string line = new LineRenderer().Render("Abigail", "spirited", new DiaryEntry(0, "Player", "Saw", "SeedShop"));

        Assert.Equal("I saw you at Pierre's General Store yesterday.", line);
        Assert.DoesNotContain("SeedShop", line);
    }

    [Fact]
    public void TheModCanSupplyItsOwnPlaceNames()
    {
        var renderer = new LineRenderer(name => name == "SeedShop" ? "the shop" : name ?? "");

        Assert.Equal("I saw you at the shop yesterday.", renderer.Render("Abigail", "spirited", new DiaryEntry(0, "Player", "Saw", "SeedShop")));
    }

    [Theory]
    [InlineData(0, "I saw you at the beach earlier today.")]
    [InlineData(1, "I saw you at the beach yesterday.")]
    [InlineData(3, "I saw you at the beach the other day.")]
    [InlineData(30, "I saw you at the beach a while back.")]
    public void OnlyExactlyOneDayAgoIsYesterday(int daysAgo, string expected)
    {
        ILineRenderer renderer = new LineRenderer();

        Assert.Equal(expected, renderer.Render("Abigail", "spirited", new DiaryEntry(0, "Player", "Saw", "Beach"), daysAgo));
    }

    [Fact]
    public void AnOldSightingIsNotNewsEveryNight()
    {
        // The audit repro: the player passed Abigail once on day 2; she must not bring it up on
        // every later night.
        var planner = new IntentPlanner(new AlwaysYes(), new LineRenderer());
        var snapshot = Snapshot("Abigail", new DiaryEntry(TickOn(2, 30), "Player", "Saw", "SeedShop"));

        IntentPlan dayTwo = planner.Plan(new[] { snapshot }, seed: 1, sourceDay: 2);
        IntentPlan dayForty = planner.Plan(new[] { snapshot }, seed: 1, sourceDay: 40);

        Assert.Equal("I saw you at Pierre's General Store yesterday.", Assert.Single(dayTwo.Candidates).Line);
        Assert.Empty(dayForty.Candidates);
    }

    [Fact]
    public void LadderBookkeepingIsNeverTalkedAboutButBeingIgnoredIs()
    {
        // The initiation ladder writes "TriedToReach" and "IgnoredBy" lines about the player into the
        // NPC's diary. The attempt itself is not news; being ignored is, and it is said to "you".
        var decision = new AlwaysYes();
        var planner = new IntentPlanner(decision, new LineRenderer());
        var snapshot = Snapshot("Abigail",
            new DiaryEntry(TickOn(3, 10), "Player", "TriedToReach", "Emote"),
            new DiaryEntry(TickOn(3, 16), "Player", "IgnoredBy", "Emote"));

        IntentPlan plan = planner.Plan(new[] { snapshot }, seed: 2, sourceDay: 3);

        Assert.Equal(new[] { "yesterday Abigail tried to get the player's attention (an emote) and got none" },
            Assert.Single(decision.ChooseCalls));
        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("I tried to get your attention yesterday. You must have been busy.", candidate.Line);
        Assert.DoesNotContain("Player", candidate.Line);
    }

    [Fact]
    public void AnNpcWhoseOnlyNewsIsItsOwnAttemptStaysQuiet()
    {
        var decision = new AlwaysYes();
        var planner = new IntentPlanner(decision, new LineRenderer());
        var snapshot = Snapshot("Abigail", new DiaryEntry(TickOn(3, 10), "Player", "TriedToReach", "Emote"));

        Assert.Empty(planner.Plan(new[] { snapshot }, seed: 2, sourceDay: 3).Candidates);
        Assert.Empty(decision.ChooseCalls);
    }

    [Fact]
    public void OnlyTheSourceDaysEntriesAreOffered()
    {
        var decision = new AlwaysYes();
        var planner = new IntentPlanner(decision, new LineRenderer());
        var snapshot = Snapshot("Abigail",
            new DiaryEntry(TickOn(4, 10), "Sam", "Saw", "Beach"),       // two days before
            new DiaryEntry(TickOn(6, 20), "Player", "Saw", "Saloon"),   // the day being slept on
            new DiaryEntry(TickOn(6, 40), "Player", "Saw", "Saloon"),   // same again: deduped
            new DiaryEntry(TickOn(6, 90), "Pierre", "Saw", "SeedShop"));

        IntentPlan plan = planner.Plan(new[] { snapshot }, seed: 5, sourceDay: 6);

        IReadOnlyList<string> options = Assert.Single(decision.ChooseCalls);
        Assert.Equal(new[]
        {
            "yesterday Abigail saw the player at the Stardrop Saloon",
            "yesterday Abigail saw Pierre at Pierre's General Store",
        }, options);
        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal(6, GameClock.DayIndex(candidate.Source.AbsoluteTick));
        Assert.EndsWith("yesterday.", candidate.Line);
        Assert.DoesNotContain("SeedShop", candidate.Line);
    }
}
