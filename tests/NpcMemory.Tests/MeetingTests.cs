using Xunit;

namespace NpcMemory.Tests;

/// <summary>Meetings between ticks (MemoryStore.NoteMeetings): the player ran past villagers
/// between two ten-minute ticks and nobody noticed (live test 2026-10-03).</summary>
public sealed class MeetingTests
{
    private const string P = MemoryStore.PlayerName;
    private static Presence You(int x, int y, string at = "Town") => new("Farmer", at, x, y, IsPlayer: true);
    private static Presence Npc(string name, int x, int y, string at = "Town") => new(name, at, x, y);

    [Fact]
    public void AVillagerTheTickMissedSeesThePlayerAtOnce_Once()
    {
        var store = new MemoryStore(1);
        // The tick: the player is far from Gus.
        store.Observe(100, new[] { You(0, 0), Npc("Gus", 30, 30), Npc("Pam", 2, 2) }, TestHelpers.Regions());
        Assert.Null(store.Ledger.View("Gus", P, 100));
        Assert.NotNull(store.Ledger.View("Pam", P, 100)); // Pam was already near at the tick

        // A second later the player runs past Gus.
        IReadOnlyList<string> met = store.NoteMeetings(100, new[] { You(29, 29), Npc("Gus", 30, 30), Npc("Pam", 2, 2) }, TestHelpers.Regions());
        Assert.Equal(new[] { "Gus" }, met); // Pam saw the player at the tick already; she isn't "met" again
        LedgerView? view = store.Ledger.View("Gus", P, 100);
        Assert.Equal((0, 0, LedgerDetail.NamedSpot), (view!.HopCount, view.AgeTicks, view.Detail));
        Assert.Single(store.DiaryOf("Gus").Entries, e => e.Kind == "Saw" && e.Subject == P);

        // The next second: nothing new. The next tick, still together: the span continues, no second Saw.
        Assert.Empty(store.NoteMeetings(100, new[] { You(29, 29), Npc("Gus", 30, 30) }, TestHelpers.Regions()));
        store.Observe(101, new[] { You(29, 29), Npc("Gus", 30, 30) }, TestHelpers.Regions());
        Assert.Single(store.DiaryOf("Gus").Entries, e => e.Kind == "Saw" && e.Subject == P);
    }

    [Fact]
    public void OnlyTheSameLocationWithinRangeCounts_AndNothingBeforeTheTicksObserve()
    {
        var store = new MemoryStore(1);
        store.Observe(100, new[] { You(0, 0) }, TestHelpers.Regions());
        Assert.Empty(store.NoteMeetings(100, new[] { You(0, 0), Npc("Gus", 9, 0) }, TestHelpers.Regions())); // 9 tiles: too far
        Assert.Empty(store.NoteMeetings(100, new[] { You(0, 0), Npc("Pam", 1, 1, at: "Saloon") }, TestHelpers.Regions())); // elsewhere
        Assert.Empty(store.NoteMeetings(101, new[] { You(0, 0), Npc("Gus", 1, 0) }, TestHelpers.Regions())); // tick 101 not observed yet
        Assert.Equal(new[] { "Gus" }, store.NoteMeetings(100, new[] { You(0, 0), Npc("Gus", 8, 0) }, TestHelpers.Regions()));
    }
}
