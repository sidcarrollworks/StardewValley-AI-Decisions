using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcInitiation.Tests;

/// <summary>
/// Finding the player: NPCs who miss the player ask the NPCs around them.
/// </summary>
public sealed class FindTests
{
    private const string Player = MemoryStore.PlayerName;

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
