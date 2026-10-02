using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// Step 7: simulated ambient chat (docs/spec/ledger-gossip.md) and the daily belief decay.
/// </summary>
public class ChatTests
{
    private const string Player = MemoryStore.PlayerName;

    private static MemoryStore Tick(MemoryStore store, int tick, params Presence[] presences)
        => Observe(store, tick, presences);

    private static MemoryStore Observe(MemoryStore store, int tick, Presence[] presences)
    {
        store.Observe(tick, presences, TestHelpers.Regions(), _ => 0);
        return store;
    }

    private static Presence Npc(string name, string location, int x = 5, int y = 5)
        => new(name, location, x, y);

    private static Presence You(string location, int x = 10, int y = 10)
        => new(Player, location, x, y, true);

    /// <summary>ChatChance 1.0: every eligible pair chats (still once per span).</summary>
    private static readonly ChatOptions AlwaysChat = new() { ChatMinTicks = 3, ChatChance = 1.0 };
    private static readonly ChatOptions NeverChat = new() { ChatMinTicks = 3, ChatChance = 0.0 };

    [Fact]
    public void AChattingPairPassesThePlayerSightingOn()
    {
        var store = new MemoryStore(7);
        Tick(store, 0, You("Town"), Npc("Sam", "Town", 6, 6));        // Sam sees the player
        Tick(store, 1, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
        Tick(store, 2, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
        Tick(store, 3, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7)); // 3-tick span

        store.Chat(3, options: AlwaysChat);

        // Sam told Abigail: Abigail's view of the player is second-hand.
        LedgerView abigails = store.Ledger.View("Abigail", Player, 4);
        Assert.NotNull(abigails);
        Assert.Equal(1, abigails!.HopCount);
        Assert.Equal("Sam", abigails.ToldBy);
    }

    [Fact]
    public void WithAFailingDrawNothingPasses()
    {
        var store = new MemoryStore(7);
        Tick(store, 0, You("Town"), Npc("Sam", "Town", 6, 6));
        Tick(store, 1, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
        Tick(store, 2, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
        Tick(store, 3, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));

        store.Chat(3, options: NeverChat);

        Assert.Equal(0, store.Ledger.View("Abigail", Player, 4)?.HopCount ?? 0);
    }

    [Fact]
    public void APairChatsAtMostOncePerSpan()
    {
        var store = new MemoryStore(7);
        Tick(store, 0, You("Town"), Npc("Sam", "Town", 6, 6));
        for (int t = 1; t <= 5; t++)
            Tick(store, t, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));

        store.Chat(3, options: AlwaysChat);
        store.Chat(4, options: AlwaysChat);
        store.Chat(5, options: AlwaysChat);

        // The sighting passed once; a second hop never appears (one hop from Sam, no re-gossip).
        LedgerView abigails = store.Ledger.View("Abigail", Player, 6);
        Assert.NotNull(abigails);
        Assert.Equal(1, abigails!.HopCount);
    }

    [Fact]
    public void AShareableEventBecomesAHeardEntry()
    {
        var store = new MemoryStore(7);
        // Abigail receives a gift at tick 0, then chats with Sam for three ticks.
        store.Note("Abigail", new DiaryEntry(0, Player, "GiftReceived", "taste=Love;name=Sunflower"));
        Tick(store, 1, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 2, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 3, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));

        IReadOnlyList<DiaryEntry> written = store.Chat(3, options: AlwaysChat);

        DiaryEntry heard = Assert.Single(store.DiaryOf("Sam").Entries.Where(e => e.Kind == "Heard"));
        Assert.Equal(0, heard.AbsoluteTick); // the original event's tick
        IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(heard.Detail);
        Assert.Equal("Abigail", detail["from"]);
        Assert.Equal("GiftReceived", detail["kind"]);
        Assert.Contains(written, e => e.Kind == "Heard" && e.Subject == Player);
    }

    [Fact]
    public void ANonShareableKindIsNeverShared()
    {
        var store = new MemoryStore(7);
        store.Note("Abigail", new DiaryEntry(0, Player, "PassedBy", "ticks=6"));
        Tick(store, 1, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 2, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 3, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));

        store.Chat(3, options: AlwaysChat);

        Assert.DoesNotContain(store.DiaryOf("Sam").Entries, e => e.Kind == "Heard");
    }

    [Fact]
    public void TheSameEventReachesAListenerAtMostOnce()
    {
        var store = new MemoryStore(7);
        store.Note("Abigail", new DiaryEntry(0, Player, "GiftReceived", "taste=Love;name=Sunflower"));
        Tick(store, 1, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 2, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 3, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        store.Chat(3, options: AlwaysChat);

        // A NEW span: they part and meet again — the event must not be re-told.
        Tick(store, 4, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 5, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 6, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        store.Chat(6, options: AlwaysChat);

        Assert.Single(store.DiaryOf("Sam").Entries.Where(e => e.Kind == "Heard"));
    }

    [Fact]
    public void ChatIsDeterministicForTheSameSeedAndInputs()
    {
        var first = new MemoryStore(11);
        var second = new MemoryStore(11);
        for (int i = 0; i < 2; i++)
        {
            MemoryStore store = i == 0 ? first : second;
            Tick(store, 0, You("Town"), Npc("Sam", "Town", 6, 6));
            Tick(store, 1, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
            Tick(store, 2, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
            Tick(store, 3, Npc("Sam", "Town", 6, 6), Npc("Abigail", "Town", 7, 7));
            store.Chat(3); // default 0.3 chance: seed decides
        }

        Assert.Equal(
            first.Ledger.View("Abigail", Player, 4)?.HopCount ?? 0,
            second.Ledger.View("Abigail", Player, 4)?.HopCount ?? 0);
    }

    [Fact]
    public void DecayAgesOnlyBeliefsUntouchedToday()
    {
        var store = new MemoryStore(7);
        // Sam sees the player on day 0; Abigail sees the player on day 1.
        Tick(store, 10, You("Town"), Npc("Sam", "Town", 6, 6));
        Tick(store, TimeUtils.TicksPerDay + 10, You("Beach"), Npc("Abigail", "Beach", 6, 6));

        store.DecayBeliefs(TimeUtils.TicksPerDay + 20, 0.5);

        RoutineBelief sams = store.Beliefs["Sam>Player"];
        RoutineBelief abigails = store.Beliefs["Abigail>Player"];
        Assert.Equal(0.5, SamOf(sams, "Town"), 9);      // last seen yesterday: halved
        Assert.Equal(1.0, SamOf(abigails, "Beach"), 9); // touched today: no decay
    }

    private static double SamOf(RoutineBelief belief, string region)
        => belief.Counts[region].Sum();

    private static NpcRoutine Routine(string name, int blockCount = 12)
    {
        var column = new int[blockCount];
        for (int b = 0; b < blockCount; b++)
            column[b] = 1;
        return new NpcRoutine
        {
            Name = name,
            BlockMinutes = 120,
            RegionTicks = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Town"] = column,
            },
            TotalTicks = blockCount,
        };
    }

    [Fact]
    public void SeedPriors_SeedsFamilyPairsOnce()
    {
        var store = new MemoryStore(7);
        var routines = new Dictionary<string, NpcRoutine>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jas"] = Routine("Jas"),
            ["Vincent"] = Routine("Vincent"),
        };
        var homes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jas"] = "AnimalShop",
            ["Vincent"] = "AnimalShop", // same home: family
            ["Penny"] = "Trailer",
        };

        int seeded = store.SeedPriors(routines, homes, "save1", new PriorOptions { Kind = RelationshipKind.Family });
        Assert.Equal(2, seeded); // Jas>Vincent and Vincent>Jas

        // Seeding twice is a no-op (the Seeded flag survives a round trip too).
        Assert.Equal(0, store.SeedPriors(routines, homes, "save1", new PriorOptions { Kind = RelationshipKind.Family }));
        MemoryStore reloaded = MemoryStore.FromJson(store.ToJson());
        Assert.Equal(0, reloaded.SeedPriors(routines, homes, "save1", new PriorOptions { Kind = RelationshipKind.Family }));

        // The prior is real knowledge: Jas has a guess about Vincent, and a lenient floor turns
        // it into a habit lead (the strict 12-evidence floor is tuned for observed co-presence).
        RoutineBelief belief = store.Beliefs["Jas>Vincent"];
        Assert.True(belief.Seeded);
        Assert.NotNull(belief.BestGuessAt(1));
        var lenient = new WhereaboutsOptions { MinHabitEvidence = 1, MinHabitShare = 0.1 };
        Assert.Equal(WhereaboutsSource.Habit,
            store.LookFor("Jas", "Vincent", 10, 120, lenient).Source);
    }

    [Fact]
    public void ChatHeardPairsEachHeardLineWithItsListener()
    {
        var first = new MemoryStore(7);
        var second = new MemoryStore(7);
        foreach (MemoryStore store in new[] { first, second })
        {
            store.Note("Abigail", new DiaryEntry(0, Player, "GiftReceived", "taste=Love;name=Sunflower"));
            Tick(store, 1, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
            Tick(store, 2, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
            Tick(store, 3, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        }

        IReadOnlyList<(string Listener, DiaryEntry Entry)> pairs = first.ChatHeard(3, options: AlwaysChat);
        IReadOnlyList<DiaryEntry> lines = second.Chat(3, options: AlwaysChat);

        // Chat is ChatHeard without the listener: the same records, in the same order.
        Assert.Equal(
            lines.Select(e => (e.AbsoluteTick, e.Subject, e.Kind, e.Detail)),
            pairs.Select(p => (p.Entry.AbsoluteTick, p.Entry.Subject, p.Entry.Kind, p.Entry.Detail)));

        DiaryEntry heard = Assert.Single(lines.Where(l => l.Kind == "Heard"));
        (string listener, DiaryEntry entry) = Assert.Single(pairs.Where(p => p.Entry.Kind == "Heard"));
        Assert.Equal("Sam", listener);
        Assert.Equal(heard, entry);
        // The paired entry is the very line the listener's diary got.
        Assert.Contains(second.DiaryOf("Sam").Entries, e => ReferenceEquals(e, heard));
    }

    [Fact]
    public void EveryDiaryWriteGoesThroughTheNotingHook_HearsayIncluded()
    {
        // The motives' regard keeper listens on Noting (docs/spec/motives.md, "Triggers and game
        // hooks"): it must see each entry once, with the diary as it was before the entry.
        var store = new MemoryStore(7);
        var seen = new List<(string Npc, DiaryEntry Entry, int Before)>();
        store.Noting = (npc, entry, before) => seen.Add((npc, entry, before.Count));

        store.Note("Abigail", new DiaryEntry(0, Player, "GiftReceived", "taste=Love;name=Sunflower"));
        Tick(store, 1, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 2, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        Tick(store, 3, Npc("Abigail", "Town", 6, 6), Npc("Sam", "Town", 7, 7));
        store.Chat(3, options: AlwaysChat);

        Assert.Equal(("Abigail", "GiftReceived", 0), (seen[0].Npc, seen[0].Entry.Kind, seen[0].Before));
        (string listener, DiaryEntry heard, int before) = Assert.Single(seen.Where(s => s.Entry.Kind == "Heard"));
        Assert.Equal("Sam", listener);
        Assert.Equal(store.DiaryOf("Sam").Entries.Count - 1, before); // called before the append
        Assert.Contains(store.DiaryOf("Sam").Entries, e => ReferenceEquals(e, heard));
        // Every entry in every diary was seen exactly once.
        Assert.Equal(store.Diaries.Values.Sum(d => d.Entries.Count), seen.Count);
    }
}
