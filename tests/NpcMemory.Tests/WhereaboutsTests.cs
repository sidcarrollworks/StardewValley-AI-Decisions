using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// Finding someone from memory: an NPC asks the NPCs it is with (gossip rules apply), and answers
/// "where is X now?" from its own sighting, a tip, or its habit knowledge for this hour.
/// </summary>
public sealed class WhereaboutsTests
{
    private const string Player = MemoryStore.PlayerName;

    private static Presence You(string location, int x, int y) => new("Farmer", location, x, y, IsPlayer: true);
    private static Presence Npc(string name, string location, int x, int y) => new(name, location, x, y);

    private static void Tick(MemoryStore store, int tick, params Presence[] presences)
        => store.Observe(tick, presences, TestHelpers.Regions(), _ => 0);

    // ---- asking around ----------------------------------------------------------------------------

    [Fact]
    public void AnNpcAsksOnlyTheNpcsItIsWithRightNow()
    {
        var store = new MemoryStore();
        Tick(store, 5, You("Town", 40, 20), Npc("Sam", "Town", 42, 21), Npc("Pam", "Town", 44, 22));
        Tick(store, 10, Npc("Abigail", "Saloon", 5, 5), Npc("Sam", "Saloon", 6, 6), Npc("Pam", "Town", 10, 10));

        AskResult result = store.AskAround("Abigail", Player, 10);

        Assert.Equal(new[] { "Sam" }, result.Asked);   // Pam saw you too, but isn't here
        Assert.Equal(new[] { "Sam" }, result.Told);
        LedgerView view = store.Ledger.View("Abigail", Player, 10)!;
        Assert.Equal(1, view.HopCount);
        Assert.Equal("Sam", view.ToldBy);
        Assert.Equal("Town", view.Place);
        Assert.Equal(5, view.AbsoluteTick);             // Sam's sighting, not re-freshened
    }

    [Fact]
    public void ThePlayerIsNeverAskedAboutThemselves()
    {
        var store = new MemoryStore();
        Tick(store, 10, You("Saloon", 5, 5), Npc("Abigail", "Saloon", 6, 6), Npc("Sam", "Saloon", 7, 7));

        AskResult result = store.AskAround("Abigail", Player, 10);

        Assert.DoesNotContain(Player, result.Asked);
        Assert.Equal(new[] { "Sam" }, result.Asked);
        Assert.Empty(result.Told);                      // Abigail sees you herself: nothing fresher to learn
        Assert.Equal(0, store.Ledger.View("Abigail", Player, 10)!.HopCount);
    }

    [Fact]
    public void ATipCanTravelTwoHopsButNoFurther()
    {
        var store = new MemoryStore();
        Tick(store, 5, You("Beach", 10, 10), Npc("Willy", "Beach", 12, 10));
        Tick(store, 7, Npc("Willy", "FishShop", 3, 3), Npc("Elliott", "FishShop", 4, 4));
        Tick(store, 9, Npc("Elliott", "Saloon", 3, 3), Npc("Leah", "Saloon", 4, 4));
        Tick(store, 11, Npc("Leah", "Forest", 3, 3), Npc("Marnie", "Forest", 4, 4));

        Assert.Equal(new[] { "Willy" }, store.AskAround("Elliott", Player, 7).Told);   // hop 1
        Assert.Equal(new[] { "Elliott" }, store.AskAround("Leah", Player, 9).Told);    // hop 2
        Assert.Empty(store.AskAround("Marnie", Player, 11).Told);                      // hop 3 refused

        LedgerView leah = store.Ledger.View("Leah", Player, 9)!;
        Assert.Equal(2, leah.HopCount);
        Assert.Equal("Elliott", leah.ToldBy);
        Assert.Null(store.Ledger.View("Marnie", Player, 11));
    }

    [Fact]
    public void AnOlderTipNeverReplacesAFresherSighting()
    {
        var store = new MemoryStore();
        Tick(store, 5, You("Town", 40, 20), Npc("Sam", "Town", 42, 21));
        Tick(store, 8, You("Saloon", 5, 5), Npc("Abigail", "Saloon", 6, 6));
        Tick(store, 10, Npc("Abigail", "Town", 30, 30), Npc("Sam", "Town", 31, 31));

        Assert.Empty(store.AskAround("Abigail", Player, 10).Told);
        Assert.Equal("Saloon", store.Ledger.View("Abigail", Player, 10)!.Place);
    }

    // ---- looking for someone ------------------------------------------------------------------------

    [Fact]
    public void SomeoneInSightIsSeenNow()
    {
        var store = new MemoryStore();
        Tick(store, 10, You("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6));

        Whereabouts found = store.LookFor("Gus", Player, 10, 120);

        Assert.Equal(WhereaboutsSource.SeenNow, found.Source);
        Assert.Equal("Saloon", found.Place);
    }

    [Fact]
    public void AnEarlierSightingTodayIsWhereTheNpcSawYou()
    {
        var store = new MemoryStore();
        Tick(store, 10, You("Saloon", 5, 5), Npc("Gus", "Saloon", 6, 6));

        Whereabouts found = store.LookFor("Gus", Player, 13, 120);

        Assert.Equal(WhereaboutsSource.SeenToday, found.Source);
        Assert.Equal("Saloon", found.Place);
        Assert.Equal(3, found.AgeTicks);
        Assert.Equal(0, found.HopCount);
    }

    [Fact]
    public void ATipSaysWhoToldIt()
    {
        var store = new MemoryStore();
        Tick(store, 5, You("Town", 40, 20), Npc("Sam", "Town", 42, 21));
        Tick(store, 10, Npc("Abigail", "Saloon", 5, 5), Npc("Sam", "Saloon", 6, 6));
        store.AskAround("Abigail", Player, 10);

        Whereabouts found = store.LookFor("Abigail", Player, 10, 120);

        Assert.Equal(WhereaboutsSource.Told, found.Source);
        Assert.Equal("Sam", found.ToldBy);
        Assert.Equal("Town", found.Place);
        Assert.Equal(1, found.HopCount);
        Assert.True(found.HasPlace);
    }

    /// <summary>Seen with the player at the beach at 8:00 (block 1) on three earlier days.</summary>
    private static MemoryStore WithABeachMorningHabit(int days = 3, int hearts = 0)
    {
        var store = new MemoryStore();
        for (int day = 0; day < days; day++)
            store.Observe(GameClock.DayStartTick(day) + 12, new[] { You("Beach", 10, 10), Npc("Willy", "Beach", 11, 11) },
                TestHelpers.Regions(), _ => hearts);
        return store;
    }

    [Fact]
    public void WithNoSightingTodayTheHabitForThisHourIsUsed()
    {
        MemoryStore store = WithABeachMorningHabit();
        int thisMorning = GameClock.DayStartTick(5) + 14; // 8:20, block 1, nobody has seen you today

        Whereabouts found = store.LookFor("Willy", Player, thisMorning, 120);

        Assert.Equal(WhereaboutsSource.Habit, found.Source);
        Assert.Equal("Beach", found.Place);
        Assert.Equal(1.0, found.HabitShare, 10);
        Assert.Equal(LedgerDetail.Region, found.Detail);
    }

    [Fact]
    public void AHabitNeedsEnoughEvidence()
    {
        MemoryStore twoMornings = WithABeachMorningHabit(days: 2);
        int thisMorning = GameClock.DayStartTick(5) + 14;

        Assert.Equal(WhereaboutsSource.Unknown, twoMornings.LookFor("Willy", Player, thisMorning, 120).Source);

        // Hearts make each morning together count for more, so a friend learns faster.
        MemoryStore friend = WithABeachMorningHabit(days: 2, hearts: 4);
        Assert.Equal(WhereaboutsSource.Habit, friend.LookFor("Willy", Player, thisMorning, 120).Source);
    }

    [Fact]
    public void AHabitIsOnlyForTheSameHour()
    {
        MemoryStore store = WithABeachMorningHabit();
        int thisEvening = GameClock.DayStartTick(5) + 80; // 19:20, a block with nothing learned

        Assert.Equal(WhereaboutsSource.Unknown, store.LookFor("Willy", Player, thisEvening, 120).Source);
    }

    [Fact]
    public void AHabitNeedsOneRegionToDominate()
    {
        var store = new MemoryStore();
        for (int day = 0; day < 4; day++)
        {
            string where = day % 2 == 0 ? "Beach" : "Town";
            store.Observe(GameClock.DayStartTick(day) + 12, new[] { You(where, 10, 10), Npc("Willy", where, 11, 11) },
                TestHelpers.Regions(), _ => 0);
        }
        var options = new WhereaboutsOptions { MinHabitShare = 0.6 };

        Whereabouts found = store.LookFor("Willy", Player, GameClock.DayStartTick(5) + 14, 120, options);

        Assert.Equal(WhereaboutsSource.Unknown, found.Source); // 50/50 is not a habit at 60%
    }

    [Fact]
    public void AFadedSightingWithoutAPlaceFallsBackToTheHabit()
    {
        MemoryStore store = WithABeachMorningHabit();
        int today = GameClock.DayStartTick(5);
        store.Observe(today + 1, new[] { You("Town", 40, 20), Npc("Willy", "Town", 41, 21) }, TestHelpers.Regions(), _ => 0);

        // 16+ hours later the sighting is only "earlier today" (no place); block 9 has no habit either.
        Whereabouts late = store.LookFor("Willy", Player, today + 110, 120);
        Assert.Equal(WhereaboutsSource.SeenToday, late.Source);
        Assert.False(late.HasPlace);

        // Next morning at 8:20 the sighting is gone, and the habit answers.
        Whereabouts nextMorning = store.LookFor("Willy", Player, GameClock.DayStartTick(6) + 14, 120);
        Assert.Equal(WhereaboutsSource.Habit, nextMorning.Source);
        Assert.Equal("Beach", nextMorning.Place);
    }

    [Fact]
    public void NobodyKnowsIsUnknown()
    {
        var store = new MemoryStore();
        Tick(store, 10, You("Town", 40, 20), Npc("Sam", "Town", 42, 21));

        Whereabouts found = store.LookFor("Sam", Player, GameClock.DayStartTick(3) + 10, 120); // days later, no habit

        Assert.Equal(WhereaboutsSource.Unknown, found.Source);
        Assert.False(found.HasPlace);
        Assert.Equal(WhereaboutsSource.Unknown, store.LookFor("Pam", Player, 10, 120).Source);
    }

    // ---- the pieces underneath ------------------------------------------------------------------------

    [Fact]
    public void ATipRemembersItsTellerAcrossASave()
    {
        var ledger = new Ledger();
        ledger.Record("Sam", Player, "Town", "Town", 5, "1,1");
        Assert.True(ledger.Gossip("Sam", "Abigail", Player, 6));

        Ledger copy = Ledger.FromJson(ledger.ToJson());

        Assert.Equal("Sam", copy.View("Abigail", Player, 6)!.ToldBy);
        Assert.Null(copy.View("Sam", Player, 6)!.ToldBy);
        Assert.Equal(new[] { "Player" }, copy.SubjectsOf("sam"));
        Assert.Empty(copy.SubjectsOf("Nobody"));
    }

    [Fact]
    public void BestGuessAtWeighsOneBlock()
    {
        var belief = new RoutineBelief("Willy", Player);
        belief.Observe("Beach", 1, 0, 3.0);
        belief.Observe("Town", 1, 0, 1.0);
        belief.Observe("Town", 4, 0, 9.0);

        BlockGuess guess = belief.BestGuessAt(1)!;
        Assert.Equal("Beach", guess.Region);
        Assert.Equal(0.75, guess.Share, 10);
        Assert.Equal(4.0, guess.Evidence, 10);
        Assert.Equal("Town", belief.BestGuessAt(4)!.Region);
        Assert.Null(belief.BestGuessAt(2));

        belief.Observe("Town", 1, 0, 2.0); // 3 vs 3: tie goes to the name, ascending
        Assert.Equal("Beach", belief.BestGuessAt(1)!.Region);
    }
}
