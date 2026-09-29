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

    private static Ledger SeenAtZero() => SeenAtZero("Alice", "Sam");

    private static Ledger SeenAtZero(string observer, string subject)
    {
        var ledger = new Ledger();
        ledger.Record(observer, subject, Location, Region, 0);
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
        Assert.Equal(0, view.AgeTicks);
        Assert.Equal(0, view.HopCount);       // first-hand
        Assert.Equal(0, view.AbsoluteTick);
    }

    [Theory]
    [InlineData(0, LedgerDetail.NamedSpot, Location)]
    [InlineData(1, LedgerDetail.NamedSpot, Location)]
    [InlineData(119, LedgerDetail.NamedSpot, Location)]   // last tick still named
    [InlineData(120, LedgerDetail.Location, Location)]    // 2h -> Location
    [InlineData(121, LedgerDetail.Location, Location)]
    [InlineData(719, LedgerDetail.Location, Location)]    // last tick still Location
    [InlineData(720, LedgerDetail.Region, Region)]        // 12h -> Region
    [InlineData(1199, LedgerDetail.Region, Region)]       // last tick still Region
    [InlineData(1200, LedgerDetail.EarlierToday, null)]   // 20h -> no place
    [InlineData(1439, LedgerDetail.EarlierToday, null)]   // last tick still today
    [InlineData(1440, LedgerDetail.Gone, null)]           // a day -> forgotten
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
        var ledger = new Ledger { SpotTtl = 10, LocationTtl = 20, RegionTtl = 30, GoneTtl = 40 };
        ledger.Record("Alice", "Sam", Location, Region, 100);

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Alice", "Sam", 109).Detail);
        Assert.Equal(LedgerDetail.Location, Must(ledger, "Alice", "Sam", 110).Detail);
        Assert.Equal(LedgerDetail.Region, Must(ledger, "Alice", "Sam", 120).Detail);
        Assert.Equal(LedgerDetail.EarlierToday, Must(ledger, "Alice", "Sam", 130).Detail);
        Assert.Equal(LedgerDetail.Gone, Must(ledger, "Alice", "Sam", 140).Detail);
    }

    [Fact]
    public void RecordReplacesThePreviousEntry()
    {
        var ledger = SeenAtZero();
        ledger.Record("Alice", "Sam", "Mountain", "Mountain", 300);

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
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 300));
        Assert.Equal(1, Must(ledger, "Bob", "Sam", 300).HopCount);

        ledger.Record("Bob", "Sam", "SeedShop", Region, 300);

        var view = Must(ledger, "Bob", "Sam", 300);
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
        ledger.Record("Alice", "Sam", Location, Region, 500);

        var view = Must(ledger, "Alice", "Sam", 400);   // querying before the sighting

        Assert.Equal(0, view.AgeTicks);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
    }

    // ---- Gossip ----------------------------------------------------------------------------

    [Fact]
    public void GossipCopiesTheSpeakersCurrentViewAtOneMoreHop()
    {
        var ledger = SeenAtZero();

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 30));

        var view = Must(ledger, "Bob", "Sam", 30);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail);
        Assert.Equal(Location, view.Place);
        Assert.Equal(1, view.HopCount);
        Assert.Equal(30, view.AgeTicks);             // age still measured from the sighting
        Assert.Equal(0, view.AbsoluteTick);
    }

    [Fact]
    public void GossipNeverAddsDetailTheSpeakerHasAlreadyLost()
    {
        var ledger = SeenAtZero();

        // By tick 800 Alice's own view has decayed to Region.
        Assert.Equal(LedgerDetail.Region, Must(ledger, "Alice", "Sam", 800).Detail);

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 800));

        var view = Must(ledger, "Bob", "Sam", 800);
        Assert.Equal(LedgerDetail.Region, view.Detail);   // capped, never NamedSpot/Location
        Assert.Equal(Region, view.Place);                 // the region, not the shop
        Assert.NotEqual(Location, view.Place);
        Assert.Equal(1, view.HopCount);
        Assert.Equal(800, view.AgeTicks);
    }

    [Fact]
    public void GossipIsCappedAtTheSpeakersDetailEvenWhenNatureWouldBeFiner()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 800));   // Bob is told only "Region"

        // Widen the "fresh" window: by age alone the sighting would read as a NamedSpot again,
        // and Alice (first-hand) does resolve that finely — but a told fact may not get finer.
        ledger.SpotTtl = 2000;

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Alice", "Sam", 800).Detail);

        var bob = Must(ledger, "Bob", "Sam", 800);
        Assert.Equal(LedgerDetail.Region, bob.Detail);
        Assert.Equal(Region, bob.Place);

        // Passing it on again: the listener's natural detail is NamedSpot, the speaker's is
        // Region, so the stored detail must be capped to Region.
        Assert.True(ledger.Gossip("Bob", "Carol", "Sam", 800));

        var carol = Must(ledger, "Carol", "Sam", 800);
        Assert.Equal(LedgerDetail.Region, carol.Detail);
        Assert.Equal(Region, carol.Place);
        Assert.Equal(2, carol.HopCount);
    }

    [Fact]
    public void GossipDecaysWithAgeLikeTheSightingItCameFrom()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 10));

        Assert.Equal(LedgerDetail.NamedSpot, Must(ledger, "Bob", "Sam", 119).Detail);
        Assert.Equal(LedgerDetail.Location, Must(ledger, "Bob", "Sam", 120).Detail);
        Assert.Equal(LedgerDetail.Region, Must(ledger, "Bob", "Sam", 720).Detail);
        Assert.Equal(LedgerDetail.EarlierToday, Must(ledger, "Bob", "Sam", 1200).Detail);
        Assert.Equal(LedgerDetail.Gone, Must(ledger, "Bob", "Sam", 1440).Detail);
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
    public void GossipOfAForgottenSubjectKeepsItForgotten()
    {
        var ledger = SeenAtZero();

        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 2000));

        var view = Must(ledger, "Bob", "Sam", 2000);
        Assert.Equal(LedgerDetail.Gone, view.Detail);
        Assert.Null(view.Place);
        Assert.Equal(1, view.HopCount);
    }

    // ---- JSON ------------------------------------------------------------------------------

    [Fact]
    public void JsonRoundTripIsLossless()
    {
        var ledger = new Ledger { SpotTtl = 100, LocationTtl = 500, RegionTtl = 900, GoneTtl = 1300 };
        ledger.Record("Alice", "Sam", Location, Region, 480);
        ledger.Record("Alice", "Player", "Farm", "Farm", 600);
        ledger.Record("Robin", "Sam", "Mountain", "Mountain", 700);
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 1200));
        Assert.True(ledger.Gossip("Bob", "Carol", "Sam", 1220));

        string json = ledger.ToJson();
        var copy = Ledger.FromJson(json);

        Assert.Equal(100, copy.SpotTtl);
        Assert.Equal(500, copy.LocationTtl);
        Assert.Equal(900, copy.RegionTtl);
        Assert.Equal(1300, copy.GoneTtl);

        AssertSameView(ledger.View("Alice", "Sam", 1200), copy.View("Alice", "Sam", 1200));
        AssertSameView(ledger.View("Alice", "Player", 700), copy.View("Alice", "Player", 700));
        AssertSameView(ledger.View("Robin", "Sam", 1000), copy.View("Robin", "Sam", 1000));
        AssertSameView(ledger.View("Bob", "Sam", 1230), copy.View("Bob", "Sam", 1230));
        AssertSameView(ledger.View("Carol", "Sam", 1240), copy.View("Carol", "Sam", 1240));
        Assert.Null(copy.View("Dave", "Sam", 1230));

        // Hop counts survive a save/load, so the two-hop cap still holds afterwards.
        Assert.Equal(2, Must(copy, "Carol", "Sam", 1240).HopCount);
        Assert.False(copy.Gossip("Carol", "Dave", "Sam", 1240));

        // Re-serialising the copy gives byte-identical JSON, and lookups stay case-insensitive.
        Assert.Equal(json, copy.ToJson());
        Assert.NotNull(copy.View("ALICE", "sam", 1200));
    }

    [Fact]
    public void FromJsonRestoresADegradedViewExactly()
    {
        var ledger = SeenAtZero();
        Assert.True(ledger.Gossip("Alice", "Bob", "Sam", 800));   // Region, hop 1

        var copy = Ledger.FromJson(ledger.ToJson());

        var view = Must(copy, "Bob", "Sam", 800);
        Assert.Equal(LedgerDetail.Region, view.Detail);
        Assert.Equal(Region, view.Place);
        Assert.Equal(800, view.AgeTicks);
        Assert.Equal(1, view.HopCount);
        Assert.Equal(0, view.AbsoluteTick);
    }

    [Fact]
    public void EmptyLedgerRoundTrips()
    {
        var ledger = new Ledger();

        var copy = Ledger.FromJson(ledger.ToJson());

        Assert.Equal(120, copy.SpotTtl);
        Assert.Equal(720, copy.LocationTtl);
        Assert.Equal(1200, copy.RegionTtl);
        Assert.Equal(1440, copy.GoneTtl);
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
