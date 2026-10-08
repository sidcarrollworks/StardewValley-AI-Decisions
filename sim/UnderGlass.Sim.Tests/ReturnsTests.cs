using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's slice acts-1, Returns (acts spec 1 and 4.1-4.3): Thanked, Complimented and
/// Joked, light kind acts. In a room with two people placed by hand (SteeringTests' room, as
/// CatalogTests has it): the shy answer a gift they can't return; a light kind act settles the
/// kindness it answers and stirs no motive; a warm act from someone disliked is read cold and
/// hurts a little; the warm cap and the warm budget hold; and a slice that is off offers nothing.
/// </summary>
public class ReturnsTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly string[] Warm = { "Thanked", "Complimented", "Joked" };

    private static Villager V(string name, string household, int x, double bold, string? friend = null, int age = 30)
        => new(name, household, "villager", new Temperament(1.0, bold, 0.5, 0.6), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, 2), 0, D, 1) }, new Dictionary<string, double>(),
            friend is null ? Array.Empty<string>() : new[] { friend }, age);

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));

    /// <param name="giver">Who gives the other a gift at 10:00 on day 0 (null: nobody).</param>
    private static SimResult Scene(FeelingOptions o, int days, string? giver = "Ann", int bobAge = 30, bool friends = false, double annBold = 0.5)
    {
        var kinds = new List<ActKind> { Gift };
        kinds.AddRange(ActCatalog.Returns);
        var room = new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        var cast = new[] { V("Ann", "A", 3, annBold, friends ? "Bob" : null), V("Bob", "B", 5, 0, friends ? "Ann" : null, bobAge) };
        return new Simulation(1, cast, new[] { room }, kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: giver is null ? null : new[] { (Clock.At(10), giver, "GaveGift") }, wander: 0).Run(days);
    }

    private static FeelingOptions Returns(params (string From, string To, double Regard)[] start)
    {
        FeelingOptions o = FeelingOptions.WithDesire(start);
        o.Acts.Returns = true;
        o.GiftCost = 2; // nobody here clears a gift: the light acts are what is left
        return o;
    }

    [Fact]
    public void TheShyThankAGiftTheyCannotReturn()
    {
        // Bob (bold 0) can't clear a gift; aged 4 he may not compliment (5 and up), so he thanks.
        FeelingOptions o = Returns();
        o.ClearBand = 0.02;
        SimResult r = Scene(o, 8, bobAge: 4);
        Act thanks = Assert.Single(r.Acts, a => a.Kind == "Thanked");
        Assert.Equal(("Bob", "Ann", 0), (thanks.Actor, thanks.Target, thanks.About));
        Assert.Contains(thanks.Id, r.Pursued);
        Assert.Contains(r.Pursuits, p => p is { Holder: "Bob", ActKind: "GaveGift", Call: "no" });
        Assert.DoesNotContain(r.Pursuits, p => p is { Holder: "Bob", ActKind: "Complimented" }); // too young
        // The gift is settled as returned, from both sides; the thanks itself settles as None.
        Assert.Equal(Outcome.Returned, Assert.Single(r.LifeEvents, e => e is { Person: "Ann", Role: LifeRole.Did, ActId: 0 }).Outcome);
        LifeEvent did = Assert.Single(r.LifeEvents, e => e is { Person: "Bob", Role: LifeRole.Did } && e.ActId == thanks.Id);
        Assert.True(did.Light);
        Assert.Equal(Outcome.None, did.Outcome);
        // A thank-you is never thanked back: it stirs nothing in Ann.
        Assert.DoesNotContain(r.Stirred, s => s.Source == thanks.Id);
        Assert.Single(r.Acts, a => Warm.Contains(a.Kind));
    }

    [Fact]
    public void WithReturnsOffTheShyLeaveTheGiftIgnored()
    {
        FeelingOptions o = Returns();
        o.Acts.Returns = false;
        o.ClearBand = 0.02;
        SimResult r = Scene(o, 8, bobAge: 4);
        Assert.DoesNotContain(r.Acts, a => Warm.Contains(a.Kind)); // the rows are there, the slice is off
        Assert.Equal(Outcome.Ignored, Assert.Single(r.LifeEvents, e => e is { Person: "Ann", Role: LifeRole.Did, ActId: 0 }).Outcome);
    }

    [Fact]
    public void AComplimentFromSomeoneDislikedIsReadCold()
    {
        // Bob gives Ann a gift; Ann (bold 0.8) returns it with a compliment, but Bob holds her at
        // -0.5, below Complimented's -0.2: he reads it as flattery and takes it badly.
        FeelingOptions o = Returns(("Bob", "Ann", -0.5));
        o.StanceOn = true;
        SimResult r = Scene(o, 3, giver: "Bob", annBold: 0.8);
        Act compliment = Assert.Single(r.Acts, a => a.Kind == "Complimented");
        Assert.Equal(("Ann", "Bob"), (compliment.Actor, compliment.Target));
        Felt mood = Assert.Single(r.Feelings, f => f.Holder == "Bob" && f.ActId == compliment.Id && f.Toward is null);
        Assert.Equal("Cold", mood.Route);
        Assert.Equal(-o.Acts.ColdShare * 0.12 * (0.5 + 0.5), mood.Mood, 9); // x (0.5 + sensitivity), seen up close
        Assert.Contains(r.Feelings, f => f.Holder == "Bob" && f.ActId == compliment.Id && f.Toward == "Ann" && f.Change < 0);
        Assert.DoesNotContain(r.Stirred, s => s.Source == compliment.Id); // light: no answer, though it hurt
        Assert.True(r.Stances["Bob"][0] != 0); // the small hurt moved his stance
    }

    [Fact]
    public void AComplimentFromSomeoneLikedIsWarm()
    {
        FeelingOptions o = Returns(("Bob", "Ann", 0.3));
        SimResult r = Scene(o, 3, giver: "Bob", annBold: 0.8);
        Act compliment = Assert.Single(r.Acts, a => a.Kind == "Complimented");
        Felt mood = Assert.Single(r.Feelings, f => f.Holder == "Bob" && f.ActId == compliment.Id && f.Toward is null);
        Assert.Equal("Direct", mood.Route);
        Assert.True(mood.Mood > 0);
        Assert.DoesNotContain(r.Stirred, s => s.Source == compliment.Id); // a compliment obliges no gift
    }

    /// <summary>Ann loves Bob and knows him well (friends), so Fond reaches for the light acts every
    /// day she is with him; Bob likes her back, so they read warm.</summary>
    private static FeelingOptions Fond()
    {
        FeelingOptions o = Returns(("Ann", "Bob", 0.6), ("Bob", "Ann", 0.6));
        o.FondOn = true;
        o.SlotsPerDay = 5;
        return o;
    }

    [Fact]
    public void TheWarmCapCountsLightKindActsApart()
    {
        FeelingOptions o = Fond();
        SimResult many = Scene(o, 6, giver: null, friends: true);
        var perDay = many.Acts.Where(a => Warm.Contains(a.Kind) && a.Actor == "Ann").GroupBy(a => Clock.Day(a.Tick)).Select(g => g.Count()).ToList();
        Assert.NotEmpty(perDay);
        Assert.All(perDay, n => Assert.InRange(n, 1, o.Acts.WarmPerDay));
        Assert.Contains(perDay, n => n > 1);

        FeelingOptions one = Fond();
        one.Acts.WarmPerDay = 1;
        one.LightPerDay = 0; // the hostile cap is apart: zero there doesn't stop warmth
        SimResult capped = Scene(one, 6, giver: null, friends: true);
        var cappedPerDay = capped.Acts.Where(a => Warm.Contains(a.Kind) && a.Actor == "Ann").GroupBy(a => Clock.Day(a.Tick)).Select(g => g.Count()).ToList();
        Assert.NotEmpty(cappedPerDay);
        Assert.All(cappedPerDay, n => Assert.Equal(1, n));
    }

    [Fact]
    public void TheWarmBudgetCapsADaysRegardFromLightActs()
    {
        static IEnumerable<double> DaysRegard(SimResult r)
        {
            var warm = r.Acts.Where(a => Warm.Contains(a.Kind)).ToDictionary(a => a.Id);
            return r.Feelings.Where(f => f.Holder == "Bob" && f.Toward == "Ann" && warm.ContainsKey(f.ActId))
                .GroupBy(f => Clock.Day(warm[f.ActId].Tick)).Select(g => g.Sum(f => f.Change));
        }
        FeelingOptions free = Fond();
        free.Acts.WarmBudget = 1;
        var unbounded = DaysRegard(Scene(free, 6, giver: null, friends: true)).ToList();
        Assert.Contains(unbounded, x => x > 0.002);

        FeelingOptions tight = Fond();
        tight.Acts.WarmBudget = 0.002;
        var bounded = DaysRegard(Scene(tight, 6, giver: null, friends: true)).ToList();
        Assert.NotEmpty(bounded);
        Assert.All(bounded, x => Assert.InRange(x, 0, 0.002 + 1e-12));
    }

    [Fact]
    public void WarmActsLeaveNoMark()
    {
        // A third slight of one kind from one person leaves a mark (rule 4); a third joke does not.
        FeelingOptions o = Fond();
        o.LightActsOn = true;
        o.MarkCount = 1;
        SimResult r = Scene(o, 4, giver: null, friends: true);
        Assert.Contains(r.Acts, a => Warm.Contains(a.Kind));
        Assert.Equal(0, r.Marks);
    }

    [Fact]
    public void WatchRecordsWhatWouldHaveStartedAndStartsNothing()
    {
        FeelingOptions o = Returns();
        o.ClearBand = 0.02;
        o.Acts.Watch = true;
        SimResult watched = Scene(o, 8, bobAge: 4);
        Assert.DoesNotContain(watched.Acts, a => Warm.Contains(a.Kind));
        CatalogWatched w = Assert.Single(watched.CatalogWatch);
        Assert.Equal(("Bob", "Ann", DesireKind.Return, "Thanked"), (w.Holder, w.Subject, w.Motive, w.Kind));

        FeelingOptions off = Returns();
        off.Acts.Returns = false;
        off.ClearBand = 0.02;
        Assert.Equal(Metrics.LogHash(Scene(off, 8, bobAge: 4)), Metrics.LogHash(watched));
        Assert.Empty(Scene(off, 8, bobAge: 4).CatalogWatch);
    }
}
