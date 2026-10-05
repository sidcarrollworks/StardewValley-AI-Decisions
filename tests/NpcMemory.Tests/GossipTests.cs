using NpcSchedules;
using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// Gossip juiciness (docs/spec/ledger-gossip.md, "Juiciness"; D25): what gets told, to whom,
/// how it weakens with each retelling and how fast it fades.
/// </summary>
public class GossipTests
{
    private const string Player = MemoryStore.PlayerName;
    private static readonly ChatOptions Always = new() { ChatMinTicks = 3, ChatChance = 1.0 };

    /// <summary>A stand-in for the stressor table's juiciness (NpcMotives isn't referenced here).</summary>
    private static double? Juice(DiaryEntry e) => e.Kind switch
    {
        "SawRummaging" => 4,
        "HatedGift" => 3,
        "QuestHelped" => 2,
        "LikedGift" => 1,
        _ => null, // a plain Saw and the rest: not gossip
    };

    /// <summary>Puts two villagers together for three ticks from <paramref name="start"/> (ticks
    /// start..start+2) and lets them chat on the last; returns the Heard lines written.</summary>
    private static IReadOnlyList<(string Listener, DiaryEntry Entry)> Meet(MemoryStore store, int start, string a, string b,
        string location = "Town", ChatOptions? o = null, Func<string, string, bool>? knows = null)
    {
        for (int t = start; t < start + 3; t++)
            store.Observe(t, new[] { new Presence(a, location, 5, 5), new Presence(b, location, 6, 6) }, TestHelpers.Regions(), _ => 0);
        return store.ChatHeard(start + 2, options: o ?? Always, juiciness: Juice, knows: knows ?? ((_, _) => false));
    }

    private static DiaryEntry? HeardBy(MemoryStore store, string npc)
        => store.DiaryOf(npc).Entries.LastOrDefault(e => e.Kind == "Heard");

    private static IReadOnlyDictionary<string, string> D(DiaryEntry e) => DiaryDetail.Parse(e.Detail);

    [Fact]
    public void APlainSightingIsNeverVolunteered()
    {
        var store = new MemoryStore(7);
        store.Note("Sam", new DiaryEntry(0, Player, "Saw", "Town"));
        Assert.Empty(Meet(store, 1, "Sam", "Abigail"));
        Assert.Null(HeardBy(store, "Abigail"));
    }

    [Fact]
    public void AScandalIsRetoldWeakerEachTime_AndStopsBelowTheVolunteerLevel()
    {
        var store = new MemoryStore(7);
        store.Note("Lewis", new DiaryEntry(0, Player, "SawRummaging", "place=Saloon"));

        Meet(store, 1, "Lewis", "Gus", "Town");
        Meet(store, 4, "Gus", "Emily", "Beach");
        Meet(store, 7, "Emily", "Shane", "Mountain");

        DiaryEntry gus = HeardBy(store, "Gus")!;
        Assert.Equal("2.8", D(gus)["j"]);
        Assert.Equal("1", D(gus)["hops"]);
        Assert.Equal("Lewis", D(gus)["from"]);
        Assert.Equal("Lewis", D(gus)["of"]);
        Assert.Equal("SawRummaging", D(gus)["kind"]);
        Assert.Equal("Saloon", D(gus)["place"]); // the original's keys travel with it
        Assert.Equal(0, gus.AbsoluteTick);         // the event's tick, for dedupe

        DiaryEntry emily = HeardBy(store, "Emily")!;
        Assert.Equal("1.96", D(emily)["j"]);
        Assert.Equal("2", D(emily)["hops"]);
        Assert.Equal("Gus", D(emily)["from"]);
        Assert.Equal("Lewis", D(emily)["of"]);

        Assert.Null(HeardBy(store, "Shane")); // 1.96 is below 2: Emily keeps it to herself
    }

    [Fact]
    public void AListenerWhoKnowsSomeoneInTheStoryHearsWhatOthersWouldNot()
    {
        // Emily's 1.96 plus the knows-someone bonus (0.5) reaches the volunteer level.
        var store = new MemoryStore(7);
        store.Note("Lewis", new DiaryEntry(0, Player, "SawRummaging", null));
        Meet(store, 1, "Lewis", "Gus", "Town");
        Meet(store, 4, "Gus", "Emily", "Beach");
        Meet(store, 7, "Emily", "Shane", "Mountain", knows: (who, person) => who == "Shane" && person == Player);

        Assert.Equal("1.372", D(HeardBy(store, "Shane")!)["j"]); // passed on without the bonus
    }

    [Fact]
    public void TheTellerNeverCountsAsSomeoneTheListenerKnows()
    {
        // A liked gift (1) plus the bonus would still be short; a quest (2) is told anyway. Here
        // the only person Abigail "knows" is the teller, so a 1.5 story stays untold.
        var store = new MemoryStore(7);
        store.Note("Sam", new DiaryEntry(0, Player, "LikedGift", null));
        Meet(store, 1, "Sam", "Abigail", knows: (_, person) => person == "Sam");
        Assert.Null(HeardBy(store, "Abigail"));
    }

    [Fact]
    public void JuicinessFadesByWholeDays_ScandalsMoreSlowlyPerPointButFromHigher()
    {
        var o = new ChatOptions();
        var quest = new DiaryEntry(10, Player, "QuestHelped", null);
        Assert.Equal(2, Gossip.Current(quest, 119, Juice, o));                       // same day: full
        Assert.Equal(1.5, Gossip.Current(quest, GameClock.TicksPerDay, Juice, o));     // next day
        Assert.Equal(0, Gossip.Current(quest, 5 * GameClock.TicksPerDay, Juice, o));   // never below 0

        var scandal = new DiaryEntry(10, Player, "SawRummaging", null);
        Assert.Equal(2.4, Gossip.Current(scandal, 2 * GameClock.TicksPerDay, Juice, o)!.Value, 6);
        Assert.Equal(1.6, Gossip.Current(scandal, 3 * GameClock.TicksPerDay, Juice, o)!.Value, 6);

        // A Heard fades from when it was heard, not from the event.
        var heard = new DiaryEntry(10, Player, "Heard", $"from=Lewis;kind=SawRummaging;subject={Player};of=Lewis;b=4;j=2.8;at={GameClock.TicksPerDay + 5};hops=1");
        Assert.Equal(2.8, Gossip.Current(heard, GameClock.TicksPerDay + 50, Juice, o)!.Value, 6);
        Assert.Equal(2.0, Gossip.Current(heard, 2 * GameClock.TicksPerDay, Juice, o)!.Value, 6);

        Assert.Null(Gossip.Current(new DiaryEntry(10, Player, "Saw", "Town"), 20, Juice, o));
    }

    [Fact]
    public void ADayOldStoryIsNoLongerVolunteered()
    {
        var store = new MemoryStore(7);
        store.Note("Robin", new DiaryEntry(0, Player, "QuestHelped", null));
        Assert.Empty(Meet(store, GameClock.TicksPerDay + 1, "Robin", "Demetrius"));
    }

    [Fact]
    public void ATellerTellsTheSameStoryToAtMostThreeListenersADay()
    {
        var store = new MemoryStore(7);
        store.Note("Lewis", new DiaryEntry(0, Player, "SawRummaging", null));
        string[] listeners = { "Gus", "Emily", "Shane", "Pam" };
        for (int i = 0; i < listeners.Length; i++)
            Meet(store, 1 + 3 * i, "Lewis", listeners[i], new[] { "Town", "Beach", "Mountain", "Forest" }[i]);

        Assert.NotNull(HeardBy(store, "Gus"));
        Assert.NotNull(HeardBy(store, "Emily"));
        Assert.NotNull(HeardBy(store, "Shane"));
        Assert.Null(HeardBy(store, "Pam")); // the fourth listener today

        // The next day the cap starts over (the story, at 3.2, is still juicy).
        Meet(store, GameClock.TicksPerDay + 1, "Lewis", "Pam", "Desert");
        Assert.NotNull(HeardBy(store, "Pam"));
    }

    [Fact]
    public void TheSameEventReachesAListenerOnce_WhateverTheRoute()
    {
        var store = new MemoryStore(7);
        store.Note("Lewis", new DiaryEntry(0, Player, "SawRummaging", null));
        Meet(store, 1, "Lewis", "Gus", "Town");
        Meet(store, 4, "Lewis", "Emily", "Beach");
        var third = Meet(store, 7, "Gus", "Emily", "Mountain"); // both already have it

        Assert.Empty(third);
        Assert.Single(store.DiaryOf("Gus").Entries, e => e.Kind == "Heard");
        Assert.Single(store.DiaryOf("Emily").Entries, e => e.Kind == "Heard");
    }

    [Fact]
    public void SomeoneInTheStoryIsNeverTold()
    {
        var store = new MemoryStore(7);
        store.Note("Pam", new DiaryEntry(0, "Haley", "HatedGift", "giver=Penny"));
        Assert.Empty(Meet(store, 1, "Pam", "Haley", "Town"));  // the subject
        Assert.Empty(Meet(store, 4, "Pam", "Penny", "Beach")); // the giver
        Assert.Single(Meet(store, 7, "Pam", "Gus", "Mountain"));
    }

    [Fact]
    public void ARumourNeverCreatesOrRefreshesAPosition()
    {
        var store = new MemoryStore(7);
        store.Note("Lewis", new DiaryEntry(0, Player, "SawRummaging", null));
        Meet(store, 1, "Lewis", "Gus");
        Assert.NotNull(HeardBy(store, "Gus"));
        Assert.Null(store.Ledger.View("Gus", Player, 4)); // a story is not a sighting (D9)
    }

    [Fact]
    public void TheSameSaveSpreadsTheSameWay()
    {
        string Run()
        {
            var store = new MemoryStore(11);
            store.Note("Lewis", new DiaryEntry(0, Player, "SawRummaging", null));
            store.Note("Robin", new DiaryEntry(2, Player, "QuestHelped", null));
            var o = new ChatOptions { ChatMinTicks = 3 }; // the default 0.3 draw: the seed decides
            var lines = new List<string>();
            for (int t = 1; t < 60; t += 3)
            {
                var all = new[] { "Lewis", "Robin", "Gus", "Emily", "Shane" };
                foreach (var pair in new[] { (all[t % 5], all[(t + 1) % 5]), (all[(t + 2) % 5], all[(t + 3) % 5]) })
                    lines.AddRange(Meet(store, t, pair.Item1, pair.Item2, pair == (all[t % 5], all[(t + 1) % 5]) ? "Town" : "Beach", o)
                        .Select(h => $"{h.Listener}:{h.Entry.Detail}"));
            }
            return string.Join("\n", lines);
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void AHeardFromBeforeJuicinessReadsAsOneHopFromItsOwner()
    {
        var old = new DiaryEntry(0, Player, "Heard", "from=Pam;kind=QuestHelped;subject=Player");
        Assert.Equal("Pam", Gossip.OwnerOf(old, "Emily"));
        Assert.Equal(1, Gossip.HopsOf(old));
        Assert.Equal(1.4, Gossip.Current(old, 5, Juice, new ChatOptions())!.Value, 6); // 2 x 0.7
    }
}
