using NpcDecision;
using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcInitiation.Tests;

/// <summary>
/// Finding the player: NPCs who miss the player ask the NPCs around them, and the ladder's
/// Approach step can send an NPC to where it believes the player is.
/// </summary>
public sealed class FindTests
{
    private const string Player = MemoryStore.PlayerName;

    private sealed class Always : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => options.Select(_ => 1.0).ToArray();
        public double Score(string context, double min, double max) => max;
        public double YesNo(string context, string proposition) => 1.0;
    }

    private static RegionMap Regions()
    {
        var map = new RegionMap
        {
            BlockMinutes = 120,
            Regions = new Dictionary<string, string[]>
            {
                ["Town"] = new[] { "Town", "SeedShop", "Saloon" },
                ["Beach"] = new[] { "Beach", "FishShop" },
            },
        };
        map.Rebuild();
        return map;
    }

    private static Presence You(string location, int x, int y) => new("Farmer", location, x, y, IsPlayer: true);
    private static Presence Npc(string name, string location, int x, int y) => new(name, location, x, y);

    private static LedgerView Told(string npc, int tick) => new(npc, Player, LedgerDetail.Location, "Beach", 4, 1, tick - 4, null, "Willy");
    private static LedgerView Gone(string npc, int tick) => new(npc, Player, LedgerDetail.Gone, null, 200, 0, tick - 200);
    private static LedgerView Near(string npc, int tick) => new(npc, Player, LedgerDetail.NamedSpot, "Saloon", 0, 0, tick, "5,5");
    private static LedgerView SeenEarlier(string npc, int tick) => new(npc, Player, LedgerDetail.Location, "Saloon", 5, 0, tick - 5);

    private static Whereabouts Lead(string npc, WhereaboutsSource source, string? place, int hops = 0, string? toldBy = null)
        => new(npc, Player, source, place, LedgerDetail.Location, 4, hops, toldBy, source == WhereaboutsSource.Habit ? 0.8 : 0);

    private static InitiationLadder Keen() => new(new Always(), 1, new InitiationOptions { BaseGainPerTick = 0.65, HeartsGainPerTick = 0 });

    private static InitiationEvent AttemptOf(IReadOnlyList<InitiationEvent> events) => Assert.Single(events, e => e.Kind == "Attempt");

    // ---- the ladder goes looking ---------------------------------------------------------------------

    [Fact]
    public void ATipSendsTheNpcToLookForYou()
    {
        var lead = Lead("Abigail", WhereaboutsSource.Told, "Beach", hops: 1, toldBy: "Willy");

        InitiationEvent attempt = AttemptOf(Keen().Tick(10, new[] { new InitiationInput("Abigail", Told("Abigail", 10), false, 3, lead) }, _ => new Diary()));

        Assert.Equal(InitiationStep.Approach, attempt.Step);
        Assert.Equal(lead, attempt.Lead);
    }

    [Fact]
    public void WithoutALeadAFriendStillWritesInstead()
    {
        var ladder = new InitiationLadder(new Always(), 1, new InitiationOptions { BaseGainPerTick = 0.9, HeartsGainPerTick = 0 });
        var unknown = Lead("Abigail", WhereaboutsSource.Unknown, null);

        InitiationEvent attempt = AttemptOf(ladder.Tick(10, new[] { new InitiationInput("Abigail", Gone("Abigail", 10), false, 3, unknown) }, _ => new Diary()));

        Assert.Equal(InitiationStep.Mail, attempt.Step);
        Assert.Null(attempt.Lead);
    }

    [Fact]
    public void AHabitIsALeadToo()
    {
        var habit = Lead("Willy", WhereaboutsSource.Habit, "Beach");

        InitiationEvent attempt = AttemptOf(Keen().Tick(10, new[] { new InitiationInput("Willy", Gone("Willy", 10), false, 0, habit) }, _ => new Diary()));

        Assert.Equal(InitiationStep.Approach, attempt.Step);
        Assert.Equal(WhereaboutsSource.Habit, attempt.Lead!.Source);
    }

    [Fact]
    public void GoingBackToWhereTheNpcSawYouComesBeforeAQueuedLine()
    {
        var seen = Lead("Gus", WhereaboutsSource.SeenToday, "Saloon");
        var ladder = new InitiationLadder(new Always(), 1, new InitiationOptions { BaseGainPerTick = 0.75, HeartsGainPerTick = 0 });

        InitiationEvent attempt = AttemptOf(ladder.Tick(10, new[] { new InitiationInput("Gus", SeenEarlier("Gus", 10), false, 0, seen) }, _ => new Diary()));

        Assert.Equal(InitiationStep.Approach, attempt.Step);
    }

    [Fact]
    public void ALeadWithNowhereToGoIsNotALead()
    {
        var faded = Lead("Gus", WhereaboutsSource.SeenToday, null); // "earlier today", no place
        var ladder = new InitiationLadder(new Always(), 1, new InitiationOptions { BaseGainPerTick = 0.75, HeartsGainPerTick = 0 });

        InitiationEvent attempt = AttemptOf(ladder.Tick(10, new[] { new InitiationInput("Gus", SeenEarlier("Gus", 10), false, 0, faded) }, _ => new Diary()));

        Assert.Equal(InitiationStep.QueuedLine, attempt.Step);
    }

    [Fact]
    public void WhenYouAreRightThereNobodyGoesLooking()
    {
        var lead = Lead("Gus", WhereaboutsSource.SeenNow, "Saloon");

        InitiationEvent attempt = AttemptOf(Keen().Tick(10, new[] { new InitiationInput("Gus", Near("Gus", 10), false, 0, lead) }, _ => new Diary()));

        Assert.Equal(InitiationStep.Emote, attempt.Step); // the mildest step, in person
        Assert.Null(attempt.Lead);
    }

    [Fact]
    public void TheLadderPublishesUrgesForTheGameThread()
    {
        var runner = new BackgroundLadder(Keen());
        Assert.Empty(runner.LatestUrges);

        runner.EnqueueTick(10, new[] { new InitiationInput("Gus", Gone("Gus", 10), false, 0) });
        Assert.True(runner.WaitIdle(TimeSpan.FromSeconds(10)));

        Assert.Equal(0.65, runner.LatestUrges["gus"], 9);
    }

    // ---- who asks around, and when -------------------------------------------------------------------

    /// <summary>Sam saw the player in town at tick 5; at tick 10 Abigail and Sam are in the Saloon.</summary>
    private static MemoryStore SamSawYou()
    {
        var memory = new MemoryStore();
        memory.Observe(5, new[] { You("Town", 40, 20), Npc("Sam", "Town", 42, 21) }, Regions(), _ => 0);
        memory.Observe(10, new[] { Npc("Abigail", "Saloon", 5, 5), Npc("Sam", "Saloon", 6, 6) }, Regions(), _ => 0);
        return memory;
    }

    private static Dictionary<string, double> Urges(params (string Npc, double Urge)[] urges)
        => urges.ToDictionary(u => u.Npc, u => u.Urge, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void AnNpcThatMissesYouAsksAroundAndLearnsWhereYouAre()
    {
        MemoryStore memory = SamSawYou();

        SearchEvent ev = Assert.Single(new PlayerSearch().Tick(memory, 10, Urges(("Abigail", 0.5))));

        Assert.Equal("Abigail", ev.Seeker);
        Assert.Equal(new[] { "Sam" }, ev.Asked);
        Assert.Equal("Sam", ev.Learned.ToldBy);
        Assert.Equal("Town", ev.Learned.Place);
        Assert.Equal(WhereaboutsSource.Told, memory.LookFor("Abigail", Player, 10, 120).Source);
    }

    [Fact]
    public void OnlyNpcsKeenEnoughAsk()
    {
        MemoryStore memory = SamSawYou();

        Assert.Empty(new PlayerSearch().Tick(memory, 10, Urges(("Abigail", 0.44))));
        Assert.Null(memory.Ledger.View("Abigail", Player, 10));
    }

    [Fact]
    public void AnNpcThatJustSawYouDoesNotAsk()
    {
        var memory = new MemoryStore();
        memory.Observe(8, new[] { You("Saloon", 5, 5), Npc("Abigail", "Saloon", 6, 6) }, Regions(), _ => 0);
        memory.Observe(10, new[] { Npc("Abigail", "Saloon", 5, 5), Npc("Sam", "Saloon", 6, 6) }, Regions(), _ => 0);

        Assert.Empty(new PlayerSearch().Tick(memory, 10, Urges(("Abigail", 0.9))));
    }

    [Fact]
    public void AskingHasACooldownButNobodyAroundDoesNotCount()
    {
        var memory = new MemoryStore();
        var search = new PlayerSearch();
        memory.Observe(5, new[] { You("Town", 40, 20), Npc("Sam", "Town", 42, 21), Npc("Pam", "Town", 43, 22) }, Regions(), _ => 0);

        // Tick 8: Abigail is alone, so she can't ask anyone and no cooldown starts.
        memory.Observe(8, new[] { Npc("Abigail", "Saloon", 5, 5) }, Regions(), _ => 0);
        Assert.Empty(search.Tick(memory, 8, Urges(("Abigail", 0.9))));

        // Tick 9: Sam walks in; she asks at once.
        memory.Observe(9, new[] { Npc("Abigail", "Saloon", 5, 5), Npc("Sam", "Saloon", 6, 6) }, Regions(), _ => 0);
        Assert.Single(search.Tick(memory, 9, Urges(("Abigail", 0.9))));

        // Ticks 10-14: Pam shows up with a fresher sighting, but Abigail is on cooldown.
        memory.Record(10, "Pam", "Beach", "Beach"); // Pam saw you at the beach at tick 10 (first-hand)
        for (int t = 10; t < 15; t++)
        {
            memory.Observe(t, new[] { Npc("Abigail", "Saloon", 5, 5), Npc("Pam", "Saloon", 6, 6) }, Regions(), _ => 0);
            Assert.Empty(search.Tick(memory, t, Urges(("Abigail", 0.9))));
        }

        // Tick 15: cooldown over, Pam's fresher tip replaces Sam's.
        memory.Observe(15, new[] { Npc("Abigail", "Saloon", 5, 5), Npc("Pam", "Saloon", 6, 6) }, Regions(), _ => 0);
        SearchEvent ev = Assert.Single(search.Tick(memory, 15, Urges(("Abigail", 0.9))));
        Assert.Equal("Pam", ev.Learned.ToldBy);
        Assert.Equal("Beach", ev.Learned.Place);
    }
}

/// <summary>Test-only shortcut: plant a first-hand sighting of the player in an NPC's ledger.</summary>
internal static class MemoryStoreTestExtensions
{
    public static void Record(this MemoryStore memory, int tick, string npc, string location, string region)
        => memory.Ledger.Record(npc, MemoryStore.PlayerName, location, region, tick, "1,1");
}
