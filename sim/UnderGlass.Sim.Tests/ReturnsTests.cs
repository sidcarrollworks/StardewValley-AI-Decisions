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
    private static SimResult Scene(FeelingOptions o, int days, string? giver = "Ann", int bobAge = 30, bool friends = false, double annBold = 0.5,
        double bobBold = 0, long seed = 1, IEnumerable<ActKind>? rows = null, string ann = "Ann", bool cara = false)
    {
        var kinds = new List<ActKind> { Gift };
        kinds.AddRange(rows ?? ActCatalog.Returns);
        var room = new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        var cast = new List<Villager> { V(ann, "A", 3, annBold, friends ? "Bob" : null), V("Bob", "B", 5, bobBold, friends ? ann : null, bobAge) };
        if (cara)
        {
            cast[0] = cast[0] with { Friends = new[] { "Bob", "Cara" } };
            cast.Add(V("Cara", "C", 7, 0, ann));
        }
        return new Simulation(seed, cast, new[] { room }, kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
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
        Assert.True(r.Stances["Bob"][0] < 0); // the small hurt withdraws the shy (boldness 0)
        Assert.Contains(r.LifeEvents, e => e is { Person: "Bob", Role: LifeRole.Undergone } && e.ActId == compliment.Id);
    }

    [Fact]
    public void AJokeFromSomeoneNotLikedIsTakenBadly()
    {
        // Ann loves Bob and knows him well, but Bob holds her at 0, below Joked's 0.1: the tease lands
        // as a jab. Only the joke is on offer, so Fond reaches for it.
        FeelingOptions o = Returns(("Ann", "Bob", 0.6), ("Bob", "Ann", 0));
        o.FondOn = true;
        o.StanceOn = true;
        SimResult r = Scene(o, 4, giver: null, friends: true, rows: ActCatalog.Returns.Where(k => k.Name == "Joked"));
        Act joke = r.Acts.First(a => a.Kind == "Joked");
        Assert.Equal(("Ann", "Bob"), (joke.Actor, joke.Target));
        Felt mood = Assert.Single(r.Feelings, f => f.Holder == "Bob" && f.ActId == joke.Id && f.Toward is null);
        Assert.Equal("Cold", mood.Route);
        Assert.Equal(-o.Acts.ColdShare * 0.08 * (0.5 + 0.5), mood.Mood, 9);
        Assert.Contains(r.Feelings, f => f.Holder == "Bob" && f.ActId == joke.Id && f.Toward == "Ann" && f.Change < 0);
        Assert.DoesNotContain(r.Stirred, s => s.Source == joke.Id);
        Assert.True(r.Stances["Bob"][Clock.Day(joke.Tick)] < 0);
        Assert.Contains(r.LifeEvents, e => e is { Person: "Bob", Role: LifeRole.Undergone } && e.ActId == joke.Id);
    }

    [Fact]
    public void EachRowKeepsItsLimits()
    {
        // No joke unless the joker knows the target well (0.5) and holds them at 0.3; none under 7.
        FeelingOptions fond = Returns(("Ann", "Bob", 0.6), ("Bob", "Ann", 0.6));
        fond.FondOn = true;
        fond.Acts.FondWarmDays = 0;
        IEnumerable<ActKind> jokes = ActCatalog.Returns.Where(k => k.Name == "Joked");
        Assert.Contains(Scene(fond, 2, giver: null, friends: true, rows: jokes).Acts, a => a.Kind == "Joked");
        FeelingOptions stranger = Returns((DefaultTown.Newcomer, "Bob", 0.6), ("Bob", DefaultTown.Newcomer, 0.6));
        stranger.FondOn = true;
        stranger.Acts.FondWarmDays = 0;
        SimResult met = Scene(stranger, 2, giver: null, rows: jokes, ann: DefaultTown.Newcomer); // the newcomer starts unknown
        Assert.True(met.Familiarity[(DefaultTown.Newcomer, "Bob")] < 0.5, $"{met.Familiarity[(DefaultTown.Newcomer, "Bob")]}"); // still under 0.5 after two days
        Assert.DoesNotContain(met.Acts, a => a.Kind == "Joked");
        FeelingOptions lukewarm = Returns(("Ann", "Bob", 0.28), ("Bob", "Ann", 0.6));
        lukewarm.FondOn = true;
        lukewarm.FondDays = 1;
        lukewarm.Acts.FondWarmDays = 0;
        SimResult luke = Scene(lukewarm, 6, giver: null, friends: true, rows: jokes);
        Assert.DoesNotContain(luke.Acts, a => a.Kind == "Joked" && a.Actor == "Ann"); // she holds him at 0.28
        Assert.Contains(luke.Acts, a => a.Kind == "Joked" && a.Actor == "Bob"); // he holds her at 0.6
        var young = Scene(fond, 4, giver: null, friends: true, rows: jokes, bobAge: 6);
        Assert.DoesNotContain(young.Acts, a => a.Kind == "Joked" && a.Actor == "Bob"); // Bob is 6
        // No compliment to someone barely known (familiarity under 0.2): the newcomer starts at 0.
        FeelingOptions o = Returns();
        o.ClearBand = 0.02;
        SimResult r = Scene(o, 4, giver: DefaultTown.Newcomer, ann: DefaultTown.Newcomer, bobBold: 0.4);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Complimented" && a.Actor == "Bob");
        Assert.DoesNotContain(r.Pursuits, p => p is { Holder: "Bob", ActKind: "Complimented" });
    }

    [Fact]
    public void TheRowsServeTheirMotivesInKindOrLess()
    {
        var o = new FeelingOptions();
        o.Acts.Returns = true;
        IReadOnlyList<ActKind> kinds = ActCatalog.Kinds(o.Acts);
        string[] Names(DesireKind k, string act) => Simulation.Served(kinds, o, k, act).Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "GaveGift", "Complimented", "Thanked" }, Names(DesireKind.Return, "GaveGift"));
        Assert.Equal(new[] { "HelpedSomeone", "GaveGift", "Complimented", "Thanked" }, Names(DesireKind.Return, "HelpedSomeone"));
        Assert.Equal(new[] { "Complimented", "Thanked" }, Names(DesireKind.Return, "Complimented")); // never a gift for a compliment
        Assert.Equal(new[] { "GaveGift", "Complimented", "Joked" }, Names(DesireKind.Fond, "GaveGift"));
        Assert.Equal(new[] { "GaveGift", "Complimented" }, Names(DesireKind.MakeUp, "GaveGift"));
        Assert.Equal(new[] { "Complimented" }, Names(DesireKind.Remorse, ""));
        Assert.Equal(new[] { "Argued" }, Names(DesireKind.Answer, "Argued"));
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
    public void WarmActsStirNothingAndNothingAnswersThem()
    {
        FeelingOptions o = Fond();
        o.GiftCost = 0.4; // a gift is affordable too
        SimResult r = Scene(o, 6, giver: null, friends: true);
        var warm = r.Acts.Where(a => Warm.Contains(a.Kind)).Select(a => a.Id).ToHashSet();
        Assert.NotEmpty(warm);
        Assert.DoesNotContain(r.Stirred, s => warm.Contains(s.Source));
        Assert.DoesNotContain(r.Acts, a => warm.Contains(a.About));
    }

    [Fact]
    public void TheWarmCapCountsLightKindActsApart()
    {
        static List<int> PerDay(SimResult r, string who) => r.Acts.Where(a => Warm.Contains(a.Kind) && a.Actor == who)
            .GroupBy(a => Clock.Day(a.Tick)).Select(g => g.Count()).ToList();
        FeelingOptions o = Fond();
        o.Acts.FondWarmDays = 0; // the cap alone
        SimResult many = Scene(o, 6, giver: null, friends: true, cara: true);
        var perDay = PerDay(many, "Ann");
        Assert.All(perDay, n => Assert.InRange(n, 1, o.Acts.WarmPerDay)); // across both of those she is fond of
        Assert.True(perDay.Count(n => n == o.Acts.WarmPerDay) >= 2, string.Join(",", perDay));

        FeelingOptions one = Fond();
        one.Acts.FondWarmDays = 0;
        one.Acts.WarmPerDay = 1;
        one.LightPerDay = 0; // the hostile cap is apart: zero there doesn't stop warmth
        var cappedPerDay = PerDay(Scene(one, 6, giver: null, friends: true, cara: true), "Ann");
        Assert.NotEmpty(cappedPerDay);
        Assert.All(cappedPerDay, n => Assert.Equal(1, n));

        FeelingOptions none = Fond();
        none.Acts.WarmPerDay = 0;
        Assert.DoesNotContain(Scene(none, 6, giver: null, friends: true).Acts, a => Warm.Contains(a.Kind));
    }

    [Fact]
    public void FondWaitsAWeekAfterASmallAct()
    {
        FeelingOptions o = Fond();
        SimResult r = Scene(o, 21, giver: null, friends: true);
        var days = r.Acts.Where(a => Warm.Contains(a.Kind) && a.Actor == "Ann" && r.Pursued.Contains(a.Id)).Select(a => Clock.Day(a.Tick)).ToList();
        Assert.True(days.Count >= 2, string.Join(",", days));
        for (int i = 1; i < days.Count; i++)
            Assert.True(days[i] - days[i - 1] >= o.Acts.FondWarmDays, string.Join(",", days));
        FeelingOptions daily = Fond();
        daily.Acts.FondWarmDays = 0;
        Assert.True(Scene(daily, 21, giver: null, friends: true).Acts.Count(a => Warm.Contains(a.Kind) && a.Actor == "Ann") > days.Count);
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

        // What the budget lets through (the change asked for, before saturation) is the budget
        // exactly, on more than one day, and for each ordered pair apart: Bob toward Ann, Cara toward Ann.
        FeelingOptions binding = Fond();
        binding.Acts.FondWarmDays = 0;
        binding.Acts.WarmBudget = 0.002;
        binding.Start = new Dictionary<(string, string), double> { [("Ann", "Bob")] = 0.6, [("Bob", "Ann")] = 0.6, [("Ann", "Cara")] = 0.6, [("Cara", "Ann")] = 0.6 };
        SimResult r = Scene(binding, 6, giver: null, friends: true, cara: true);
        var warm = r.Acts.Where(a => Warm.Contains(a.Kind)).ToDictionary(a => a.Id);
        foreach (string holder in new[] { "Bob", "Cara" })
        {
            var asked = r.Feelings.Where(f => f.Holder == holder && f.Toward == "Ann" && warm.ContainsKey(f.ActId) && warm[f.ActId].Actor == "Ann")
                .GroupBy(f => Clock.Day(warm[f.ActId].Tick)).Select(g => g.Sum(f => f.Raw)).ToList();
            Assert.True(asked.Count(x => Math.Abs(x - 0.002) < 1e-12) >= 2, $"{holder}: {string.Join(",", asked)}");
            Assert.All(asked, x => Assert.InRange(x, 0, 0.002 + 1e-12));
        }
    }

    [Fact]
    public void ALightKindRowOfATownsOwnKeepsTheOldRulesWithReturnsOff()
    {
        // A town's own light kind row (a wave) with Returns off is treated as before the catalog: it
        // stirs Return in its target. With Returns on it follows the light kind rules and stirs nothing.
        ActKind wave = new("Waved", 0.5, 1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, 0.05, 0.1, 1, TargetIs.Chosen),
            Gate: new ActGate(0, 0.02, new[] { DesireKind.Return, DesireKind.Fond }, Light: true));
        FeelingOptions off = Fond();
        off.Acts.Returns = false;
        SimResult before = Scene(off, 3, giver: null, friends: true, rows: new[] { wave });
        Act waved = before.Acts.First(a => a.Kind == "Waved");
        Assert.Contains(before.Stirred, s => s.Source == waved.Id && s.Motive == DesireKind.Return);
        SimResult on = Scene(Fond(), 3, giver: null, friends: true, rows: new[] { wave });
        var waves = on.Acts.Where(a => a.Kind == "Waved").Select(a => a.Id).ToHashSet();
        Assert.NotEmpty(waves);
        Assert.DoesNotContain(on.Stirred, s => waves.Contains(s.Source));
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

    [Fact]
    public void WatchRecordsFondOnceADay()
    {
        FeelingOptions o = Fond();
        o.Acts.Watch = true;
        SimResult r = Scene(o, 6, giver: null, friends: true);
        Assert.DoesNotContain(r.Acts, a => Warm.Contains(a.Kind));
        FeelingOptions off = Fond();
        off.Acts.Returns = false;
        Assert.Equal(Metrics.LogHash(Scene(off, 6, giver: null, friends: true)), Metrics.LogHash(r));
        var fond = r.CatalogWatch.Where(w => w.Motive == DesireKind.Fond).ToList();
        Assert.All(fond.GroupBy(w => (w.Holder, w.Subject, Clock.Day(w.Tick))), g => Assert.Single(g));
        Assert.True(fond.Select(w => Clock.Day(w.Tick)).Distinct().Count() > 1);
    }

    [Fact]
    public void WatchSeesTheSmallActAfterAGiftDeclinedOnACloseCall()
    {
        // Bob (bold 0.15, aged 4) weighs returning Ann's gift as a close call and draws no; the
        // gate then thanks her, since a thank-you clears. Watch records that thank-you.
        FeelingOptions live = Returns();
        live.GiftCost = 0.4;
        SimResult acted = Scene(live, 3, bobAge: 4, bobBold: 0.15, seed: 3);
        Assert.Contains(acted.Pursuits, p => p is { Holder: "Bob", ActKind: "GaveGift", Call: "close-no" });
        Act thanks = Assert.Single(acted.Acts, a => a.Kind == "Thanked");
        FeelingOptions watch = Returns();
        watch.GiftCost = 0.4;
        watch.Acts.Watch = true;
        SimResult watched = Scene(watch, 3, bobAge: 4, bobBold: 0.15, seed: 3);
        Assert.Contains(watched.CatalogWatch, w => w is { Holder: "Bob", Subject: "Ann", Motive: DesireKind.Return, Kind: "Thanked" });
    }
}
