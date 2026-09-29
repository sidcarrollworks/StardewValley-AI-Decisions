using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// MemoryStore: memory recorded from the NPCs' side (NPCs track the player and each other),
/// co-location = same location + within the radius, one diary line per co-located span, and the
/// version-1 save migration that adds the year to old ticks.
/// </summary>
public sealed class MemoryStoreTests
{
    private static Presence Player(string location, int x, int y) => new("Farmer", location, x, y, IsPlayer: true);
    private static Presence Npc(string name, string location, int x, int y) => new(name, location, x, y);

    private static void Tick(MemoryStore store, int tick, params Presence[] presences)
        => store.Observe(tick, presences, TestHelpers.Regions(), _ => 0);

    [Fact]
    public void NpcsRecordThePlayerInTheirOwnLedger()
    {
        var store = new MemoryStore();

        Tick(store, 10, Player("Town", 40, 20), Npc("Sam", "Town", 44, 22));

        LedgerView view = store.Ledger.View("Sam", MemoryStore.PlayerName, 10)!;
        Assert.NotNull(view);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
        Assert.Equal("Town", view.Place);
        Assert.Equal("40,20", view.Spot);            // where the player stood
        Assert.Null(store.Ledger.View(MemoryStore.PlayerName, "Sam", 10)); // the player is not an observer
    }

    [Fact]
    public void NpcsRecordEachOther()
    {
        var store = new MemoryStore();

        Tick(store, 10, Npc("Sam", "Saloon", 5, 5), Npc("Abigail", "Saloon", 7, 6));

        Assert.Equal("7,6", store.Ledger.View("Sam", "Abigail", 10)!.Spot);
        Assert.Equal("5,5", store.Ledger.View("Abigail", "Sam", 10)!.Spot);
        Assert.NotNull(store.BeliefOf("Sam", "Abigail"));
        Assert.Equal(1, store.BeliefOf("Abigail", "Sam")!.CoPresenceTicks);
    }

    [Fact]
    public void CoLocationNeedsTheSameLocationAndTheRadius()
    {
        var store = new MemoryStore();

        Tick(store, 10,
            Player("Town", 40, 20),
            Npc("Far", "Town", 49, 20),       // 9 tiles away: not seen
            Npc("Inside", "SeedShop", 40, 20), // same region and tile numbers, other location
            Npc("Edge", "Town", 48, 28));      // exactly 8 on both axes: seen

        Assert.Null(store.Ledger.View("Far", MemoryStore.PlayerName, 10));
        Assert.Null(store.Ledger.View("Inside", MemoryStore.PlayerName, 10));
        Assert.NotNull(store.Ledger.View("Edge", MemoryStore.PlayerName, 10));
    }

    [Fact]
    public void TheDiaryGetsOneLinePerCoLocatedSpanNotPerTick()
    {
        var store = new MemoryStore();

        for (int t = 10; t < 20; t++)
            Tick(store, t, Player("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6));
        Tick(store, 20, Player("Town", 5, 5), Npc("Gus", "Saloon", 6, 6)); // apart
        Tick(store, 21, Player("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6)); // a new span

        var entries = store.DiaryOf("Gus").Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(10, entries[0].AbsoluteTick);
        Assert.Equal(21, entries[1].AbsoluteTick);
        Assert.All(entries, e => Assert.Equal(MemoryStore.PlayerName, e.Subject));
        Assert.Equal(11, store.BeliefOf("Gus", MemoryStore.PlayerName)!.CoPresenceTicks);
    }

    [Fact]
    public void AGapInTicksStartsANewSpan()
    {
        var store = new MemoryStore();

        Tick(store, 10, Player("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6));
        Tick(store, 130, Player("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6)); // next day, same pair

        Assert.Equal(2, store.DiaryOf("Gus").Entries.Count);
    }

    [Fact]
    public void TheNightBreaksASpanEvenThoughTheTicksAreAdjacent()
    {
        // 1:50 AM (last tick of day 0) and 6:00 AM (first tick of day 1) are consecutive ticks.
        var store = new MemoryStore();

        Tick(store, GameClock.TicksPerDay - 1, Player("FarmHouse", 5, 5), Npc("Spouse", "FarmHouse", 6, 6));
        Tick(store, GameClock.TicksPerDay, Player("FarmHouse", 5, 5), Npc("Spouse", "FarmHouse", 6, 6));

        Assert.Equal(new[] { GameClock.TicksPerDay - 1, GameClock.TicksPerDay },
            store.DiaryOf("Spouse").Entries.Select(e => e.AbsoluteTick).ToArray());
    }

    [Fact]
    public void DiariesAreCapped()
    {
        var store = new MemoryStore { MaxDiaryEntries = 3 };

        for (int t = 0; t < 10; t += 2) // every other tick -> a new span each time
            Tick(store, t, Player("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6));

        var entries = store.DiaryOf("Gus").Entries;
        Assert.Equal(3, entries.Count);
        Assert.Equal(new[] { 4, 6, 8 }, entries.Select(e => e.AbsoluteTick).ToArray());
    }

    [Fact]
    public void HeartsMakeAnNpcLearnThePlayersRoutineFaster()
    {
        var store = new MemoryStore();
        var presences = new[] { Player("Town", 5, 5), Npc("Close", "Town", 6, 6), Npc("Stranger", "Town", 4, 4) };

        store.Observe(0, presences, TestHelpers.Regions(), npc => npc == "Close" ? 8 : 0);

        double close = store.BeliefOf("Close", MemoryStore.PlayerName)!.Counts["Town"][0];
        double stranger = store.BeliefOf("Stranger", MemoryStore.PlayerName)!.Counts["Town"][0];
        Assert.True(close > stranger);
        Assert.Equal(MemoryStore.PlayerLearningStrength(8), close, 10);
        Assert.Equal(1.0, store.BeliefOf("Close", "Stranger")!.Counts["Town"][0], 10); // NPC subjects learn at 1
    }

    [Fact]
    public void JsonRoundTripKeepsEverything()
    {
        var store = new MemoryStore();
        Tick(store, 10, Player("Town", 40, 20), Npc("Sam", "Town", 44, 22), Npc("Abigail", "Town", 41, 21));

        var copy = MemoryStore.FromJson(store.ToJson());

        Assert.Equal(store.ToJson(), copy.ToJson());
        Assert.Equal("40,20", copy.Ledger.View("Sam", MemoryStore.PlayerName, 10)!.Spot);
        Assert.Single(copy.DiaryOf("Abigail").About("Sam"));
        Assert.NotNull(copy.BeliefOf("Sam", "Abigail"));
    }

    // ---- version-1 migration --------------------------------------------------------------------

    [Theory]
    [InlineData(1, 5000, 1000, 1000)]                          // year 1: unchanged
    [InlineData(2, 5000, 1000, 13440 + 1000)]                  // earlier this year -> year 2
    [InlineData(2, 5000, 13000, 13000)]                        // later in the year than now -> last year
    [InlineData(3, 100, 200, 13440 + 200)]                     // year 3, winter tick -> year 2
    public void YearlessTicksMigrateToTheRightYear(int year, int nowInYear, int oldTick, int expected)
    {
        GameTime now = GameClock.FromAbsoluteTick(nowInYear) with { Year = year };

        Assert.Equal(expected, MemoryStore.MigrateYearlessTick(oldTick, now));
    }

    [Fact]
    public void AVersion1SaveMigratesSoOldMemoriesAreNotFresh()
    {
        // A year-1 winter 28 sighting, loaded on spring 1 of year 2: it must read as Gone, not NamedSpot.
        var oldLedger = new Ledger();
        int winter28 = GameClock.AbsoluteTick(new GameTime(3, 28, 60));
        oldLedger.Record("Player", "Sam", "Town", "Town", winter28, "1,1");
        var oldDiary = new Diary();
        oldDiary.Append(new DiaryEntry(winter28, "Player", "Saw", "Town"));
        var oldBelief = new RoutineBelief("Player", "Sam");
        oldBelief.NoteCoPresence(3);
        var model = new Dictionary<string, string>
        {
            ["diary"] = new Diary().ToJson(),
            ["ledger"] = oldLedger.ToJson(),
            ["beliefs"] = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["Sam"] = oldBelief.ToJson() }),
            ["npcDiaries"] = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["Sam"] = oldDiary.ToJson() }),
        };
        var now = new GameTime(0, 1, 0, Year: 2);

        var store = MemoryStore.FromVersion1(model, now);

        int nowTick = GameClock.AbsoluteTick(now);
        LedgerView view = store.Ledger.View("Player", "Sam", nowTick)!;
        Assert.Equal(LedgerDetail.Gone, view.Detail);
        Assert.Equal(winter28, view.AbsoluteTick);                 // stays in year 1
        Assert.Equal(winter28, store.DiaryOf("Sam").Entries[0].AbsoluteTick);
        Assert.Equal(3, store.BeliefOf("Player", "Sam")!.CoPresenceTicks);
    }
}
