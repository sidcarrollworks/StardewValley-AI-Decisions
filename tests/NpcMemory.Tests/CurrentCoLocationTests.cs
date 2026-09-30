using NpcSchedules;
using Xunit;

namespace NpcMemory.Tests;

public class CurrentCoLocationTests
{
    [Fact]
    public void CoLocatedWithPlayerNow_ListsOnlyCurrentPairs()
    {
        var store = new MemoryStore();
        RegionMap regions = TestHelpers.Regions();

        store.Observe(0, new Presence[]
        {
            new Presence("Haley", "Town", 5, 5),
            new Presence(MemoryStore.PlayerName, "Town", 5, 6, IsPlayer: true),
        }, regions);

        Assert.Equal(new[] { "Haley" }, store.CoLocatedWithPlayerNow());
    }

    [Fact]
    public void CoLocatedWithPlayerNow_EmptiesWhenThePlayerLeaves()
    {
        var store = new MemoryStore();
        RegionMap regions = TestHelpers.Regions();

        store.Observe(0, new Presence[]
        {
            new Presence("Haley", "Town", 5, 5),
            new Presence(MemoryStore.PlayerName, "Town", 5, 6, IsPlayer: true),
        }, regions);
        store.Observe(1, new Presence[]
        {
            new Presence("Haley", "Town", 5, 5),
            new Presence(MemoryStore.PlayerName, "Town", 40, 40, IsPlayer: true), // > 8 tiles
        }, regions);

        Assert.Empty(store.CoLocatedWithPlayerNow());
    }

    [Fact]
    public void CoLocatedWithPlayerNow_DoesNotCountNpcPairsWithoutThePlayer()
    {
        var store = new MemoryStore();
        RegionMap regions = TestHelpers.Regions();

        store.Observe(0, new Presence[]
        {
            new Presence("Haley", "Town", 5, 5),
            new Presence("Abigail", "Town", 5, 6), // NPC-NPC pair: the player is absent
        }, regions);

        Assert.Empty(store.CoLocatedWithPlayerNow());
    }
}
