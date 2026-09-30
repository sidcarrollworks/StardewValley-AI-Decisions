using NpcSchedules;
using Xunit;

namespace NpcMemory.Tests;

public class DayEndNotesTests
{
    [Fact]
    public void PassedBy_SixTicksAndTalkedToAnother_WritesOnce()
    {
        var store = new MemoryStore();
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 6, npc: "Haley");

        var notes = store.DayEndNotes(
            npcs: new[] { "Haley" },
            absoluteTick: 3 * GameClock.TicksPerDay + 119,
            heartsFor: _ => 2,
            birthdayFor: _ => false,
            giftedToday: _ => false,
            talkedToday: new[] { "Penny" });

        var passedBy = Assert.Single(notes);
        Assert.Equal("Haley", passedBy.Npc);
        Assert.Equal("PassedBy", passedBy.Entry.Kind);
        Assert.Equal("ticks=6", passedBy.Entry.Detail);
        Assert.Equal("PassedBy", store.DiaryOf("Haley").Entries[^1].Kind);
    }

    [Fact]
    public void PassedBy_FiveTicks_DoesNotWrite()
    {
        var store = new MemoryStore();
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 5, npc: "Haley");

        var notes = store.DayEndNotes(
            new[] { "Haley" }, 3 * GameClock.TicksPerDay + 119,
            _ => 2, _ => false, _ => false, new[] { "Penny" });

        Assert.Empty(notes);
    }

    [Fact]
    public void PassedBy_TalkedToThatNpc_DoesNotWrite()
    {
        var store = new MemoryStore();
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 6, npc: "Haley");

        var notes = store.DayEndNotes(
            new[] { "Haley" }, 3 * GameClock.TicksPerDay + 119,
            _ => 2, _ => false, _ => false, new[] { "Haley" });

        Assert.Empty(notes);
    }

    [Fact]
    public void PassedBy_TalkedToNobody_DoesNotWrite()
    {
        var store = new MemoryStore();
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 6, npc: "Haley");

        var notes = store.DayEndNotes(
            new[] { "Haley" }, 3 * GameClock.TicksPerDay + 119,
            _ => 2, _ => false, _ => false, Array.Empty<string>());

        Assert.Empty(notes);
    }

    [Fact]
    public void PassedBy_BelowHeartsFloor_DoesNotWrite()
    {
        var store = new MemoryStore();
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 6, npc: "Haley");

        var notes = store.DayEndNotes(
            new[] { "Haley" }, 3 * GameClock.TicksPerDay + 119,
            _ => 1, _ => false, _ => false, new[] { "Penny" });

        Assert.Empty(notes);
    }

    [Fact]
    public void PassedBy_SpansDoNotCrossTheNight()
    {
        var store = new MemoryStore();
        // 3 ticks at the end of day 3, 3 ticks at the start of day 4: 6 total but never 6 in one day.
        CoLocated(store, dayIndex: 3, startTick: 116, ticks: 3, npc: "Haley");
        CoLocated(store, dayIndex: 4, startTick: 0, ticks: 3, npc: "Haley");

        var notes = store.DayEndNotes(
            new[] { "Haley" }, 4 * GameClock.TicksPerDay + 119,
            _ => 2, _ => false, _ => false, new[] { "Penny" });

        Assert.Empty(notes);
    }

    [Fact]
    public void BirthdayForgotten_BirthdayHeartsNoGift_Writes()
    {
        var store = new MemoryStore();
        var notes = store.DayEndNotes(
            npcs: new[] { "Abigail", "Haley" },
            absoluteTick: 3 * GameClock.TicksPerDay + 119,
            heartsFor: npc => npc == "Abigail" ? 4 : 2,
            birthdayFor: npc => npc == "Abigail",
            giftedToday: _ => false,
            talkedToday: Array.Empty<string>());

        var birthday = Assert.Single(notes);
        Assert.Equal("Abigail", birthday.Npc);
        Assert.Equal("BirthdayForgotten", birthday.Entry.Kind);
        Assert.Equal("hearts=4", birthday.Entry.Detail);
    }

    [Fact]
    public void BirthdayForgotten_GiftGivenToday_DoesNotWrite()
    {
        var store = new MemoryStore();
        var notes = store.DayEndNotes(
            new[] { "Abigail" }, 3 * GameClock.TicksPerDay + 119,
            _ => 4, _ => true, _ => true, Array.Empty<string>());

        Assert.Empty(notes);
    }

    [Fact]
    public void BirthdayForgotten_BelowHearts_DoesNotWrite()
    {
        var store = new MemoryStore();
        var notes = store.DayEndNotes(
            new[] { "Abigail" }, 3 * GameClock.TicksPerDay + 119,
            _ => 2, _ => true, _ => false, Array.Empty<string>());

        Assert.Empty(notes);
    }

    [Fact]
    public void DayEndNotes_ReturnsInNameOrder()
    {
        var store = new MemoryStore();
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 6, npc: "Willy");
        CoLocated(store, dayIndex: 3, startTick: 10, ticks: 6, npc: "Alex");

        var notes = store.DayEndNotes(
            new[] { "Willy", "Alex" }, 3 * GameClock.TicksPerDay + 119,
            _ => 2, _ => false, _ => false, new[] { "Penny" });

        Assert.Equal(2, notes.Count);
        Assert.Equal(new[] { "Alex", "Willy" }, notes.Select(n => n.Npc).ToArray());
    }

    /// <summary>Observe `ticks` consecutive co-located ticks between the player and one NPC.</summary>
    private static void CoLocated(MemoryStore store, int dayIndex, int startTick, int ticks, string npc)
    {
        RegionMap regions = TestHelpers.Regions();
        for (int i = 0; i < ticks; i++)
        {
            int tick = dayIndex * GameClock.TicksPerDay + startTick + i;
            store.Observe(tick, new Presence[]
            {
                new Presence(npc, "Town", 5, 5),
                new Presence(MemoryStore.PlayerName, "Town", 5, 6, IsPlayer: true),
            }, regions);
        }
    }
}
