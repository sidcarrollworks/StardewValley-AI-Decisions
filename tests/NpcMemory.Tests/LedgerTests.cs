using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// The last-seen ledger: age-based detail decay and gossip that can only coarsen.
/// Every tick is absolute, so all of these tests are deterministic.
/// </summary>
public sealed class LedgerTests
{
    // Sam is in the SeedShop (region Town) at tick 0 in the fixtures below.
    private const string Location = "SeedShop";
    private const string Region = "Town";
    private const string Spot = "12,20"; // a tile inside the shop

    private static Ledger SeenAtZero() => SeenAtZero("Alice", "Sam");

    private static Ledger SeenAtZero(string observer, string subject)
    {
        var ledger = new Ledger();
        ledger.Record(observer, subject, Location, Region, 0, Spot);
        return ledger;
    }

    private static LedgerView Must(Ledger ledger, string observer, string subject, int nowTick)
    {
        var view = ledger.View(observer, subject, nowTick);
        Assert.NotNull(view);
        return view!;
    }

    private static void AssertSameView(LedgerView? expected, LedgerView? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.Observer, actual!.Observer);
        Assert.Equal(expected.Subject, actual.Subject);
        Assert.Equal(expected.Detail, actual.Detail);
        Assert.Equal(expected.Place, actual.Place);
        Assert.Equal(expected.AgeTicks, actual.AgeTicks);
        Assert.Equal(expected.HopCount, actual.HopCount);
        Assert.Equal(expected.AbsoluteTick, actual.AbsoluteTick);
    }

    // ---- Record / View ---------------------------------------------------------------------

    [Fact]
    public void FreshRecordIsANamedSpotWithTheLocation()
    {
        var ledger = SeenAtZero();

        var view = Must(ledger, "Alice", "Sam", 0);

        Assert.Equal("Alice", view.Observer);
        Assert.Equal("Sam", view.Subject);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
        Assert.Equal(Location, view.Place);
        Assert.Equal(Spot, view.Spot);
        Assert.Equal(0, view.AgeTicks);
        Assert.Equal(0, view.HopCount);       // first-hand
        Assert.Equal(0, view.AbsoluteTick);
    }

    [Theory]
    [InlineData(0, LedgerDetail.NamedSpot, Location)]
    [InlineData(11, LedgerDetail.NamedSpot, Location)]    // last tick still named
    [InlineData(12, LedgerDetail.Location, Location)]     // 2h -> Location
    [InlineData(47, LedgerDetail.Location, Location)]     // last tick still Location
    [InlineData(48, LedgerDetail.Region, Region)]         // 8h -> Region
    [InlineData(95, LedgerDetail.Region, Region)]         // last tick still Region
    [InlineData(96, LedgerDetail.EarlierToday, null)]     // 16h -> no place
    [InlineData(119, LedgerDetail.EarlierToday, null)]    // last tick still today
    [InlineData(120, LedgerDetail.Gone, null)]            // next morning 6:00 -> forgotten
    [InlineData(5000, LedgerDetail.Gone, null)]
    public void ViewDegradesAtEachThreshold(int age, LedgerDetail expectedDetail, string? expectedPlace)
    {
        var ledger = SeenAtZero();

        var view = Must(ledger, "Alice", "Sam", age);

        Assert.Equal(expectedDetail, view.Detail);
        Assert.Equal(expectedPlace, view.Place);
        Assert.Equal(age, view.AgeTicks);
        Assert.Equal(0, view.AbsoluteTick);   // the sighting's tick, not the query's
        Assert.Equal(0, view.HopCount);
    }

    [Fact]
    public void DetailNeverGetsFinerAsTimePasses()
    {
        var ledger = SeenAtZero();
        ledger.Gossip("Alice", "Bob", "Sam", 0);

        int previousAlice = -1;
        int previousBob = -1;
        for (int now = 0; now <= 2500; now++)
        {
            int alice = (int)Must(ledger, "Alice", "Sam", now).Detail;
            int bob = (int)Must(ledger, "Bob", "Sam", now).Detail;
            Assert.True(alice >= previousAlice, $"Alice got fresher at tick {now}");
            Assert.True(bob >= previousBob, $"Bob got fresher at tick {now}");
            previousAlice = alice;
            previousBob = bob;
        }
    }

    [Fact]
    public void ThresholdsAreConfigurable()
    {
        var ledger = new Ledger { SpotTtl = 10, LocationTtl = 20, RegionTtl = 30 };
        ledger.Record("Alice", "Sam", Location, Region, 10, Spot);

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Alice", "Sam", 19).Detail);
        Assert.Equal(LedgerDetail.Location, Must(ledger, "Alice", "Sam", 20).Detail);
        Assert.Equal(LedgerDetail.Region, Must(ledger, "Alice", "Sam", 30).Detail);
        Assert.Equal(LedgerDetail.EarlierToday, Must(ledger, "Alice", "Sam", 40).Detail);
        Assert.Equal(LedgerDetail.EarlierToday, Must(ledger, "Alice", "Sam", 119).Detail); // until the day ends
        Assert.Equal(LedgerDetail.Gone, Must(ledger, "Alice", "Sam", 120).Detail);
    }

    [Fact]
    public void RecordReplacesThePreviousEntry()
    {
        var ledger = SeenAtZero();
        ledger.Record("Alice", "Sam", "Mountain", "Mountain", 300, "40,10");

        var view = Must(ledger, "Alice", "Sam", 300);

        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
        Assert.Equal("Mountain", view.Place);      // not SeedShop
        Assert.Equal(0, view.AgeTicks);            // age from the new sighting
        Assert.Equal(300, view.AbsoluteTick);
    }

    [Fact]
    public void RecordReplacesRatherThanAddsASecondEntry()
    {
        var ledger = SeenAtZero();
        ledger.Record("Alice", "Sam", "Mountain", "Mountain", 300);
        ledger.Record("alice", "SAM", "Beach", "Beach", 400);   // same pair, case-insensitive

        var view = Must(ledger, "Alice", "Sam", 400);

        Assert.Equal("Beach", view.Place);
        Assert.Equal(400, view.AbsoluteTick);
        // Exactly one (observer, subject) entry survives: the JSON holds one record.
        Assert.Equal(1, CountEntries(ledger.ToJson()));
    }

    [Fact]
    public void AFirstHandRecordResetsTheHopCountToZero()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 30));
        Assert.Equal(1, Must(ledger, "Bob", "Sam", 30).HopCount);

        ledger.Record("Bob", "Sam", "SeedShop", Region, 30, Spot);

        var view = Must(ledger, "Bob", "Sam", 30);
        Assert.Equal(0, view.HopCount);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
    }

    [Fact]
    public void UnknownSubjectOrObserverHasNoView()
    {
        var ledger = SeenAtZero();

        Assert.Null(ledger.View("Alice", "Pierre", 0));   // never recorded for this observer
        Assert.Null(ledger.View("Bob", "Sam", 0));        // never recorded for this observer
        Assert.Null(ledger.View("Nobody", "Nobody", 5000));
    }

    [Fact]
    public void LookupsIgnoreNameCase()
    {
        var ledger = SeenAtZero("Alice", "Sam");

        Assert.NotNull(ledger.View("alice", "sam", 0));
        Assert.NotNull(ledger.View("ALICE", "SAM", 0));
        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "aLiCe", "sAm", 0).Detail);
        Assert.True(ledger.Gossip("ALICE", "bob", "SAM", 0));
        Assert.NotNull(ledger.View("BOB", "Sam", 0));
    }

    [Fact]
    public void ARecordInTheFutureClampsTheAgeToZero()
    {
        var ledger = new Ledger();
        ledger.Record("Alice", "Sam", Location, Region, 500, Spot);

        var view = Must(ledger, "Alice", "Sam", 400);   // querying before the sighting

        Assert.Equal(0, view.AgeTicks);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
    }

    // ---- Gossip ----------------------------------------------------------------------------

    [Fact]
    public void GossipCopiesTheSpeakersCurrentViewAtOneMoreHop()
    {
        var ledger = SeenAtZero();

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 5));

        var view = Must(ledger, "Bob", "Sam", 5);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
        Assert.Equal(Location, view.Place);
        Assert.Equal(1, view.HopCount);
        Assert.Equal(5, view.AgeTicks);              // age still measured from the sighting
        Assert.Equal(0, view.AbsoluteTick);
    }

    [Fact]
    public void GossipNeverAddsDetailTheSpeakerHasAlreadyLost()
    {
        var ledger = SeenAtZero();

        // By tick 60 Alice's own view has decayed to Region.
        Assert.Equal(LedgerDetail.Region, Must(ledger, "Alice", "Sam", 60).Detail);

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 60));

        var view = Must(ledger, "Bob", "Sam", 60);
        Assert.Equal(LedgerDetail.Region, view.Detail);   // capped, never NamedSpot/Location
        Assert.Equal(Region, view.Place);                 // the region, not the shop
        Assert.NotEqual(Location, view.Place);
        Assert.Equal(1, view.HopCount);
        Assert.Equal(60, view.AgeTicks);
    }

    [Fact]
    public void GossipIsCappedAtTheSpeakersDetailEvenWhenNatureWouldBeFiner()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 60));   // Bob is told only "Region"

        // Widen the "fresh" window: by age alone the sighting would read as a NamedSpot again,
        // and Alice (first-hand) does resolve that finely — but a told fact may not get finer.
        ledger.SpotTtl = 70;

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Alice", "Sam", 60).Detail);

        var bob = Must(ledger, "Bob", "Sam", 60);
        Assert.Equal(LedgerDetail.Region, bob.Detail);
        Assert.Equal(Region, bob.Place);

        // Passing it on again: the listener's natural detail is NamedSpot, the speaker's is
        // Region, so the stored detail must be capped to Region.
        Assert.True(ledger.Gossip("Bob", "Carol", "Sam", 60));

        var carol = Must(ledger, "Carol", "Sam", 60);
        Assert.Equal(LedgerDetail.Region, carol.Detail);
        Assert.Equal(Region, carol.Place);
        Assert.Equal(2, carol.HopCount);
    }

    [Fact]
    public void GossipDecaysWithAgeLikeTheSightingItCameFrom()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 10));

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Bob", "Sam", 11).Detail);
        Assert.Equal(LedgerDetail.Location, Must(ledger, "Bob", "Sam", 12).Detail);
        Assert.Equal(LedgerDetail.Region, Must(ledger, "Bob", "Sam", 48).Detail);
        Assert.Equal(LedgerDetail.EarlierToday, Must(ledger, "Bob", "Sam", 96).Detail);
        Assert.Equal(LedgerDetail.Gone, Must(ledger, "Bob", "Sam", 120).Detail);
    }

    [Fact]
    public void GossipStopsAfterTwoHops()
    {
        var ledger = SeenAtZero();

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 10));      // hop 1
        Assert.True(ledger.Gossip("Bob", "Carol", "Sam", 20));      // hop 2
        Assert.Equal(2, Must(ledger, "Carol", "Sam", 20).HopCount);

        Assert.False(ledger.Gossip("Carol", "Dave", "Sam", 30));    // hop 3 refused
        Assert.Null(ledger.View("Dave", "Sam", 30));

        // The refusal stored nothing, and Carol keeps her hop-2 knowledge.
        Assert.Equal(2, Must(ledger, "Carol", "Sam", 30).HopCount);
    }

    [Fact]
    public void GossipFromASpeakerWhoNeverSawTheSubjectIsRefused()
    {
        var ledger = SeenAtZero();

        Assert.False(ledger.Gossip("Bob", "Carol", "Sam", 0));
        Assert.Null(ledger.View("Carol", "Sam", 0));

        // ... and a refused gossip does not disturb what the listener already knew first-hand.
        ledger.Record("Carol", "Sam", Location, Region, 50);
        Assert.False(ledger.Gossip("Bob", "Carol", "Sam", 60));
        var kept = Must(ledger, "Carol", "Sam", 60);
        Assert.Equal(0, kept.HopCount);
        Assert.Equal(50, kept.AbsoluteTick);
    }

    [Fact]
    public void GossipOfAForgottenSubjectIsRefused()
    {
        var ledger = SeenAtZero();

        Assert.False(ledger.Gossip("Alice", "Bob", "Sam", 2000));   // Alice's view is Gone
        Assert.Null(ledger.View("Bob", "Sam", 2000));
    }

    [Fact]
    public void GossipNeverOverwritesAFresherFirstHandSighting()
    {
        // The audit repro: Bob saw Sam a tick ago; Alice's view is older. Bob keeps his own.
        var ledger = SeenAtZero();                              // Alice saw Sam at tick 0
        ledger.Record("Bob", "Sam", "Saloon", Region, 50, "3,4");

        Assert.False(ledger.Gossip("Alice", "Bob", "Sam", 51));

        var bob = Must(ledger, "Bob", "Sam", 51);
        Assert.Equal(0, bob.HopCount);
        Assert.Equal(50, bob.AbsoluteTick);
        Assert.Equal("Saloon", bob.Place);
        Assert.Equal(LedgerDetail.NamedSpot, bob.Detail);
    }

    [Fact]
    public void GossipOfTheSameSightingOnlyReplacesAHigherHopCount()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 5));      // Bob: hop 1
        Assert.True(ledger.Gossip("Bob", "Carol", "Sam", 6));      // Carol: hop 2

        Assert.False(ledger.Gossip("Carol", "Bob", "Sam", 7));     // hop 3 anyway, and not fresher
        Assert.True(ledger.Gossip("Alice", "Carol", "Sam", 8));    // same sighting, fewer hops: accepted
        Assert.Equal(1, Must(ledger, "Carol", "Sam", 8).HopCount);
        Assert.False(ledger.Gossip("Alice", "Carol", "Sam", 9));   // same sighting, same hops: nothing new
    }

    [Fact]
    public void NewerHearsayReplacesAnOlderFirstHandSighting()
    {
        var ledger = new Ledger();
        ledger.Record("Bob", "Sam", "Beach", "Beach", 10, "1,1");  // Bob's own, older
        ledger.Record("Alice", "Sam", Location, Region, 40, Spot); // Alice's, newer

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 41));

        var bob = Must(ledger, "Bob", "Sam", 41);
        Assert.Equal(1, bob.HopCount);
        Assert.Equal(40, bob.AbsoluteTick);
        Assert.Equal(Location, bob.Place);
    }

    [Fact]
    public void GossipToYourselfIsRefusedAndKeepsFirstHandKnowledge()
    {
        var ledger = SeenAtZero();

        Assert.False(ledger.Gossip("Alice", "alice", "Sam", 5));

        Assert.Equal(0, Must(ledger, "Alice", "Sam", 5).HopCount);
    }

    [Fact]
    public void GossipHandsOverTheSpotOnlyWhileItIsANamedSpot()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 5));      // NamedSpot: spot passed on
        Assert.Equal(Spot, Must(ledger, "Bob", "Sam", 5).Spot);

        ledger.Record("Alice", "Pierre", Location, Region, 0, Spot);
        Assert.True(ledger.Gossip("Alice", "Carol", "Pierre", 20)); // Location by now: no spot
        ledger.SpotTtl = 100;                                       // even if thresholds widen later
        var carol = Must(ledger, "Carol", "Pierre", 20);
        Assert.Equal(LedgerDetail.Location, carol.Detail);
        Assert.Null(carol.Spot);
    }

    // ---- calendar days, spots, years ----------------------------------------------------------

    [Fact]
    public void ALateNightSightingIsGoneAtTheNextSixAm()
    {
        // The audit repro: seen at 00:20, then viewed at 6:00 the next morning.
        var ledger = new Ledger();
        int lateNight = NpcSchedules.TimeUtils.TickIndex(2420);
        ledger.Record("Alice", "Sam", "Saloon", Region, lateNight, "5,5");

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Alice", "Sam", lateNight + 1).Detail);
        var morning = Must(ledger, "Alice", "Sam", GameClock.DayStartTick(1));
        Assert.Equal(LedgerDetail.Gone, morning.Detail);
        Assert.Null(morning.Place);
        Assert.Equal(LedgerDetail.Gone, Must(ledger, "Alice", "Sam", GameClock.DayStartTick(1) + 90).Detail);
    }

    [Fact]
    public void WithoutASpotTheFinestDetailIsTheLocation()
    {
        var ledger = new Ledger();
        ledger.Record("Alice", "Sam", Location, Region, 0);

        var view = Must(ledger, "Alice", "Sam", 0);
        Assert.Equal(LedgerDetail.Location, view.Detail);
        Assert.Equal(Location, view.Place);
        Assert.Null(view.Spot);
    }

    [Fact]
    public void NamedSpotAndLocationAreDifferentViews()
    {
        var ledger = SeenAtZero();

        var named = Must(ledger, "Alice", "Sam", 0);
        var location = Must(ledger, "Alice", "Sam", 12);

        Assert.Equal(LedgerDetail.NamedSpot, named.Detail);
        Assert.Equal(Spot, named.Spot);
        Assert.Equal(LedgerDetail.Location, location.Detail);
        Assert.Null(location.Spot);                  // the spot is forgotten, the location is not
        Assert.Equal(Location, location.Place);
    }

    [Fact]
    public void AYearOneSightingIsGoneInYearTwo()
    {
        // The audit repro: a winter 28 sighting viewed on spring 1 of year 2.
        var ledger = new Ledger();
        int winter28 = GameClock.AbsoluteTick(new GameTime(3, 28, 60, Year: 1));
        int spring1Y2 = GameClock.AbsoluteTick(new GameTime(0, 1, 0, Year: 2));
        ledger.Record("Alice", "Sam", Location, Region, winter28, Spot);

        var view = Must(ledger, "Alice", "Sam", spring1Y2);
        Assert.Equal(LedgerDetail.Gone, view.Detail);
        Assert.Equal(spring1Y2 - winter28, view.AgeTicks);
        Assert.True(view.AgeTicks > 0);
    }

    [Fact]
    public void RemapTicksShiftsEveryEntry()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 5));

        ledger.RemapTicks(t => t + GameClock.TicksPerYear);

        Assert.Equal(GameClock.TicksPerYear, Must(ledger, "Alice", "Sam", GameClock.TicksPerYear).AbsoluteTick);
        Assert.Equal(GameClock.TicksPerYear, Must(ledger, "Bob", "Sam", GameClock.TicksPerYear).AbsoluteTick);
    }

    // ---- JSON ------------------------------------------------------------------------------

    [Fact]
    public void JsonRoundTripIsLossless()
    {
        var ledger = new Ledger();
        ledger.Record("Alice", "Sam", Location, Region, 480, Spot);
        ledger.Record("Alice", "Player", "Farm", "Farm", 500);          // no spot
        ledger.Record("Robin", "Sam", "Mountain", "Mountain", 520, "7,7");
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 482));
        Assert.True(ledger.Gossip("Bob", "Carol", "Sam", 530));

        string json = ledger.ToJson();
        var copy = Ledger.FromJson(json);

        AssertSameView(ledger.View("Alice", "Sam", 481), copy.View("Alice", "Sam", 481));
        AssertSameView(ledger.View("Alice", "Player", 510), copy.View("Alice", "Player", 510));
        AssertSameView(ledger.View("Robin", "Sam", 525), copy.View("Robin", "Sam", 525));
        AssertSameView(ledger.View("Bob", "Sam", 483), copy.View("Bob", "Sam", 483));
        AssertSameView(ledger.View("Carol", "Sam", 540), copy.View("Carol", "Sam", 540));
        Assert.Equal(Spot, copy.View("Alice", "Sam", 481)!.Spot);
        Assert.Null(copy.View("Dave", "Sam", 530));

        // Hop counts survive a save/load, so the two-hop cap still holds afterwards.
        Assert.Equal(2, Must(copy, "Carol", "Sam", 540).HopCount);
        Assert.False(copy.Gossip("Carol", "Dave", "Sam", 540));

        // Re-serialising the copy gives byte-identical JSON, and lookups stay case-insensitive.
        Assert.Equal(json, copy.ToJson());
        Assert.NotNull(copy.View("ALICE", "sam", 500));
    }

    [Fact]
    public void ThresholdsAreNotSavedSoRetuningAppliesToOldSaves()
    {
        // An old save carried its thresholds; they must be ignored in favour of the code defaults.
        string oldSave = "{\"spotTtl\":100,\"locationTtl\":500,\"regionTtl\":900,\"goneTtl\":1300,\"entries\":[]}";

        var copy = Ledger.FromJson(oldSave);

        Assert.Equal(12, copy.SpotTtl);
        Assert.Equal(48, copy.LocationTtl);
        Assert.Equal(96, copy.RegionTtl);
        Assert.DoesNotContain("Ttl", new Ledger { SpotTtl = 99 }.ToJson());
    }

    [Fact]
    public void FromJsonRestoresADegradedViewExactly()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 60));   // Region, hop 1

        var copy = Ledger.FromJson(ledger.ToJson());

        var view = Must(copy, "Bob", "Sam", 60);
        Assert.Equal(LedgerDetail.Region, view.Detail);
        Assert.Equal(Region, view.Place);
        Assert.Equal(60, view.AgeTicks);
        Assert.Equal(1, view.HopCount);
        Assert.Equal(0, view.AbsoluteTick);
    }

    [Fact]
    public void EmptyLedgerRoundTrips()
    {
        var ledger = new Ledger();

        var copy = Ledger.FromJson(ledger.ToJson());

        Assert.Equal(12, copy.SpotTtl);
        Assert.Equal(48, copy.LocationTtl);
        Assert.Equal(96, copy.RegionTtl);
        Assert.Null(copy.View("Alice", "Sam", 0));
        Assert.Equal(ledger.ToJson(), copy.ToJson());
    }

    /// <summary>Number of serialised entries, via a tiny JSON walk (no string.GetHashCode games).</summary>
    private static int CountEntries(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("entries").GetArrayLength();
    }
}
