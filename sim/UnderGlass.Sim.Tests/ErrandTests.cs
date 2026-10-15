using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Errands (needs spec): a need for one named thing, asked of someone in sight through the
/// general Ask, promised, fetched from a counter during its sign's hours, and handed over face to face
/// (kept, late) or never (broken, and the one let down knows); looking for the person reads memory and
/// public knowledge, never where they are; money moves as groceries' does and nothing is lost; speech
/// names the thing; with the switch off nothing runs.</summary>
public class ErrandTests
{
    private const int Day = Clock.MinutesPerDay;
    private const string Slices = "returns,company,welcome,repair,sides";
    private static readonly Lazy<(SimResult R, Dictionary<int, Dictionary<string, Spot>> Where)> Month = new(() => Watched(7, 28));
    private static readonly IReadOnlyDictionary<string, Location> TownPlaces = DefaultTown.Locations().ToDictionary(p => p.Name);

    private sealed record Spot(string Place, Tile At, bool Asleep);

    private static FeelingOptions Options(bool errands)
    {
        FeelingOptions f = DefaultTown.Feelings();
        (f.Steer, f.Desire, f.DesireActs) = (true, true, true);
        f.Acts.With(errands ? Slices + ",errands" : Slices);
        return f;
    }

    private static Simulation Town(long seed, bool errands, bool speech)
    {
        FeelingOptions f = Options(errands);
        var sim = new Simulation(seed, cast: ActCatalog.Cards(DefaultTown.Cast(), f.Acts), kinds: ActCatalog.Kinds(f.Acts), feelings: f);
        sim.ConfigureSpeech(new SpeechOptions { Enabled = speech });
        return sim;
    }

    private static SimResult Run(long seed, bool errands, bool speech, int days) => Town(seed, errands, speech).Run(days);

    /// <summary>A month of the shipped town with errands and speech on, with everyone's place and tile
    /// at the end of each tick. Nothing moves between an errand's rules and the end of the minute
    /// (only Live walks, and it runs first), so these are where people stood when each act began.</summary>
    private static (SimResult, Dictionary<int, Dictionary<string, Spot>>) Watched(long seed, int days)
    {
        var where = new Dictionary<int, Dictionary<string, Spot>>();
        string[] names = DefaultTown.Cast().Select(v => v.Name).ToArray();
        SimResult r = Town(seed, errands: true, speech: true).Run(days, (m, s) =>
        {
            if (m % Clock.TickMinutes != 0)
                return;
            where[m] = names.ToDictionary(n => n, n => { var (place, at, asleep, _) = s.Where(n); return new Spot(place, at, asleep); });
        });
        return (r, where);
    }

    /// <summary>The shop world. Ann needs tea; Bob (who likes her well, so a plain asking is a clear
    /// yes) stands near her in the room and is the only one to ask; Cal keeps the shop, open all day
    /// every day, at work there all day. Nobody sleeps, chats or wanders, and without a road whoever
    /// sets out arrives at once. Every household starts with 1000. Each field changes one thing.</summary>
    private sealed record World
    {
        public Action<ActOptions>? Tune { get; init; }
        public int[] Shut { get; init; } = Array.Empty<int>();
        public string[] BobWont { get; init; } = Array.Empty<string>();
        public double Regard { get; init; } = 0.9;
        public string ShopPlace { get; init; } = "Shop";
        public int CounterTo { get; init; } = Day;
        public Job? CalJob { get; init; }
        public string AnnHome { get; init; } = "Ann";
        public string BobHome { get; init; } = "Bob";
        public int BobAge { get; init; } = 30;
        public Job? BobJob { get; init; }
        public int MinAge { get; init; }
        public double BobPurse { get; init; } = 1000;
        /// <summary>A door between the room and the shop, so the trip takes a few minutes.</summary>
        public bool Road { get; init; }
        /// <summary>A wall across the room between Ann and Bob, still within reach.</summary>
        public bool Wall { get; init; }
        /// <summary>From this minute of the day until midnight Ann is in the park, where Bob never sees her.</summary>
        public int? AnnLeavesAt { get; init; }
        /// <summary>Ann works from this minute of the day at the clinic, a home (Home:Clinic) that is
        /// also a workplace, as Harvey's is.</summary>
        public int? AnnWorksAtClinicFrom { get; init; }
        /// <summary>Dee is in the room too: she likes Bob, and she and Ann dislike each other.</summary>
        public bool Dee { get; init; }
        public string[] UseTea { get; init; } = { "Ann" };
    }

    private static TownData Shop(World w)
    {
        static Villager Person(string name, string home, Haunt[] haunts, Job? job = null, int age = 30) => new(name, home, "villager",
            new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), job, haunts, new Dictionary<string, double>(), Array.Empty<string>(), age);
        static Haunt At(string place, int x, int from = 0, int to = Day) => new(place, new Tile(x, 2), from, to, 1);
        var start = new List<(string, string, double)> { ("Ann", "Bob", w.Regard), ("Bob", "Ann", w.Regard) };
        if (w.Dee)
            start.AddRange(new[] { ("Dee", "Bob", 0.9), ("Bob", "Dee", 0.9), ("Dee", "Ann", -0.5), ("Ann", "Dee", -0.5) });
        FeelingOptions f = FeelingOptions.WithDesire(start.ToArray());
        (f.Steer, f.DesireActs) = (true, true);
        f.AnswerOn = f.ReturnOn = f.MakeUpOn = f.RetaliateOn = f.AvoidOn = f.WithdrawOn = false;
        f.Acts.Errands = true;
        f.Acts.NeedChancePerDay = 1;
        w.Tune?.Invoke(f.Acts);

        Haunt[] annHaunts = w.AnnLeavesAt is { } leaves ? new[] { At("Room", 3, 0, leaves), At("Park", 3, leaves, Day) } : new[] { At("Room", 3) };
        Job? annJob = w.AnnWorksAtClinicFrom is { } from ? new Job("Home:Clinic", new Tile(3, 2), from, Day, Array.Empty<int>(), 1) : null;
        var cast = new List<Villager>
        {
            Person("Ann", w.AnnHome, annHaunts, annJob),
            Person("Bob", w.BobHome, new[] { At("Room", w.Wall ? 8 : 5) }, w.BobJob, w.BobAge),
            Person("Cal", "Cal", new[] { At(w.ShopPlace, 2) }, w.CalJob ?? new Job(w.ShopPlace, new Tile(2, 2), 0, Day, Array.Empty<int>(), 1)),
        };
        if (w.Dee)
            cast.Add(Person("Dee", "Dee", new[] { At("Room", 7) }));
        static Location Open(string name) => new(name, false, Enumerable.Repeat(new string('.', 12), 6).ToList());
        var places = new List<Location>
        {
            new("Room", false, Enumerable.Repeat(w.Wall ? "......#....." : new string('.', 12), 6).ToList()),
            Open(w.ShopPlace),
        };
        if (w.AnnLeavesAt is not null)
            places.Add(Open("Park"));
        if (w.AnnWorksAtClinicFrom is not null)
            places.Add(Open("Home:Clinic"));
        var book = new ErrandBook(new[] { new Good("tea", "some tea", w.ShopPlace, 5, w.MinAge) },
            new[] { new Counter(w.ShopPlace, new Tile(2, 3), 0, w.CounterTo, w.Shut) },
            w.UseTea.ToDictionary(n => n, n => (IReadOnlyList<string>)new[] { "tea" }),
            new Dictionary<string, IReadOnlyList<string>> { ["Bob"] = w.BobWont });
        var purses = cast.Select(v => v.Household).Distinct().ToDictionary(h => h, h => h == "Bob" ? w.BobPurse : 1000.0);
        return new TownData
        {
            Cast = cast, Places = places,
            Links = w.Road ? new[] { new Link("Room", new Tile(11, 2), w.ShopPlace, new Tile(0, 2)) } : Array.Empty<Link>(),
            Gatherings = Array.Empty<Gathering>(),
            Acts = ActCatalog.Errands.Concat(ActCatalog.Invitations).ToArray(),
            Feelings = f, Authority = new AuthorityOptions { ElectConstable = false, Keepers = new Dictionary<string, string> { [w.ShopPlace] = "Cal" } },
            Economy = new Economy(purses, Array.Empty<(string, double, string?)>(), new Dictionary<string, double>(),
                new Dictionary<string, string>(), new Dictionary<string, (double, double)>(), 0, 0, book),
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static SimResult RunShop(World w, int days, long seed = 1) => new Simulation(seed, Shop(w)).Run(days);

    /// <summary>The purses after the same days with nobody needing anything, to read an errand's money as a change.</summary>
    private static IReadOnlyDictionary<string, double> Without(World w, int days)
        => RunShop(w with { Tune = o => { w.Tune?.Invoke(o); o.NeedChancePerDay = 0; } }, days).Purses;

    [Fact]
    public void ErrandsAddTheirRowsAndTheSwitch()
    {
        string[] added = ActCatalog.Kinds(Options(true).Acts).Select(k => k.Name).Except(ActCatalog.Kinds(Options(false).Acts).Select(k => k.Name)).ToArray();
        Assert.Equal(new[] { "AskedFavour", "Fetched", "HandedOver", "LetDown", "Refused" }, added.Order(StringComparer.Ordinal));
        foreach (string kind in new[] { "AskedFavour", "Fetched", "HandedOver", "LetDown" })
            Assert.Equal("Errands", ActCatalog.SliceOf(kind));
        Assert.Contains("Errands", ActOptions.Switches);
        // With invitations on too, the refusal's row is there once.
        FeelingOptions both = Options(true);
        both.Acts.With("invitations");
        Assert.Single(ActCatalog.Kinds(both.Acts), k => k.Name == "Refused");
        Assert.All(ActCatalog.Errands, k => Assert.Equal(0, k.PerDay)); // only the errand's own rules begin them
    }

    [Fact]
    public void WithTheSwitchOffInWatchModeOrWithoutABookNothingRuns()
    {
        SimResult off = Run(7, errands: false, speech: false, days: 7);
        Assert.Empty(off.Needs);
        Assert.Empty(off.Favours);
        Assert.Empty(off.Promises);
        Assert.DoesNotContain(off.Acts, a => a.Kind is "AskedFavour" or "Fetched" or "HandedOver" or "LetDown");

        Assert.Empty(RunShop(new World { Tune = o => o.Watch = true }, 3).Needs);
        TownData bookless = Shop(new World()) with { Economy = Shop(new World()).Economy! with { Errands = null } };
        Assert.Empty(new Simulation(1, bookless).Run(3).Needs);
    }

    [Fact]
    public void ErrandsNeedTheirRows()
    {
        TownData town = Shop(new World());
        Assert.Throws<ArgumentException>(() => new Simulation(1, town with { Acts = ActCatalog.Errands.ToArray() })); // no refusal row
    }

    [Fact]
    public void ANeedIsAskedPromisedFetchedAndHandedOver()
    {
        SimResult r = RunShop(new World(), 1);
        Need need = Assert.Single(r.Needs);
        Assert.Equal(("Ann", "tea", NeedState.Met), (need.Holder, need.Item, need.State));
        Assert.InRange(Clock.OfDay(need.Tick), Clock.At(8), Clock.At(20)); // noticed in waking hours
        Favour asked = Assert.Single(r.Favours);
        Assert.Equal(("Ann", "Bob", "tea", true), (asked.Asker, asked.Asked, asked.Item, asked.Accepted));
        Promise p = Assert.Single(r.Promises);
        Assert.Equal((PromiseKind.Errand, "Bob", "Ann", "tea", PromiseState.Kept, false), (p.Kind, p.By, p.To, p.Item, p.State, p.Late));
        Assert.Equal(need.Promise, p.Id);
        Assert.Equal(("AskedFavour", "Ann", "Bob"), (r.Acts[p.MadeAct].Kind, r.Acts[p.MadeAct].Actor, r.Acts[p.MadeAct].Target));
        Assert.Equal(("Fetched", "Bob", "Cal", "Shop"), (r.Acts[p.FetchedAct].Kind, r.Acts[p.FetchedAct].Actor, r.Acts[p.FetchedAct].Target, r.Acts[p.FetchedAct].Location));
        Act handed = r.Acts[p.SettledAct];
        Assert.Equal(("HandedOver", "Bob", "Ann", p.MadeAct), (handed.Kind, handed.Actor, handed.Target, handed.About));
        Assert.True(p.MadeAct < p.FetchedAct && p.FetchedAct < p.SettledAct);
        // Bob paid Cal and Ann paid Bob back: Bob is even, Ann down the price, and Cal up by it less what
        // restocking sent out of town; the town's money changed only by what left it.
        IReadOnlyDictionary<string, double> before = Without(new World(), 1);
        Assert.Equal(before["Bob"], r.Purses["Bob"], 6);
        Assert.Equal(before["Ann"] - 5, r.Purses["Ann"], 6);
        Assert.Equal(before["Cal"] + 5 - 5 * new MoneyOptions().StoreRestock, r.Purses["Cal"], 6);
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        // Ann was handed it face to face, and felt the kindness toward Bob.
        Assert.Contains(r.Feelings, x => x.Holder == "Ann" && x.ActId == handed.Id && x.Toward == "Bob" && x.Change > 0);
    }

    [Fact]
    public void TheMoneyGoesWhereGroceriesMoneyGoes()
    {
        // The chain's takings leave town: Ann pays Bob back, and the keeper's household gets nothing.
        var mart = new World { ShopPlace = "Mart" };
        SimResult r = RunShop(mart, 1);
        IReadOnlyDictionary<string, double> before = Without(mart, 1);
        Assert.Equal(PromiseState.Kept, Assert.Single(r.Promises).State);
        Assert.Equal((before["Ann"] - 5, before["Bob"], before["Cal"]), (r.Purses["Ann"], r.Purses["Bob"], r.Purses["Cal"]));
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        // The keeper's own household takes it off the shelf: nothing is paid at the counter, and Ann's
        // paying it back is the shop's gain.
        var shelf = new World { BobHome = "Cal" };
        r = RunShop(shelf, 1);
        before = Without(shelf, 1);
        Assert.Equal(PromiseState.Kept, Assert.Single(r.Promises).State);
        Assert.Equal((before["Ann"] - 5, before["Cal"] + 5), (r.Purses["Ann"], r.Purses["Cal"]));
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
    }

    [Fact]
    public void ShutTheDayItIsDueItIsHandedOverLate()
    {
        SimResult r = RunShop(new World { Tune = o => o.ErrandDays = 0, Shut = new[] { 0 } }, 2);
        Promise p = r.Promises[0];
        Assert.Equal((PromiseState.Kept, true), (p.State, p.Late));
        Assert.Equal(1, Clock.Day(r.Acts[p.FetchedAct].Tick)); // bought on Tuesday, when it opened
        Assert.True(p.Settled >= p.DueTo && p.Settled < p.DueTo + Day);
    }

    [Fact]
    public void NeverOpenThePromiseBreaksAndTheOneLetDownKnows()
    {
        SimResult r = RunShop(new World { Shut = Enumerable.Range(0, 7).ToArray() }, 5);
        Promise p = r.Promises[0];
        Assert.Equal((PromiseState.Broken, -1), (p.State, p.FetchedAct));
        Assert.Equal(p.DueTo + Day, p.Settled); // ErrandGraceDays after it was due
        Act letDown = r.Acts[p.SettledAct];
        Assert.Equal(("LetDown", "Bob", "Ann", p.MadeAct), (letDown.Kind, letDown.Actor, letDown.Target, letDown.About));
        Assert.Equal(0, r.Witnesses[letDown.Id]); // nobody saw it happen: it is a thing that didn't
        Assert.Contains(r.Log, l => l.Contains($" belief Ann {letDown.Id} Bob Witnessed", StringComparison.Ordinal));
        Assert.Contains(r.Feelings, x => x.Holder == "Ann" && x.ActId == letDown.Id && x.Toward == "Bob" && x.Change < 0);
        Assert.Equal(NeedState.Lapsed, r.Needs[0].State);
        Assert.DoesNotContain(r.Log, l => l.Contains(" errand-bought ", StringComparison.Ordinal));
        Assert.Equal(r.Purses["Ann"], r.Purses["Bob"], 6); // no money moved
    }

    [Fact]
    public void NobodyServingTurnsTheRunnerAwayUntilTomorrow()
    {
        // Cal serves only from midnight to one; Ann's need comes in waking hours.
        SimResult r = RunShop(new World { CalJob = new Job("Shop", new Tile(2, 2), 0, 60, Array.Empty<int>(), 1) }, 2);
        string[] shut = r.Log.Where(l => l.Contains(" errand-shut Bob Shop nobody", StringComparison.Ordinal)).ToArray();
        Assert.Single(shut); // once, and not again that day
        Assert.Equal(0, Clock.Day(int.Parse(shut[0][..shut[0].IndexOf(' ')])));
        Promise p = Assert.Single(r.Promises);
        Act fetched = r.Acts[p.FetchedAct];
        Assert.Equal((1, "Cal"), (Clock.Day(fetched.Tick), fetched.Target));
        Assert.True(Clock.OfDay(fetched.Tick) < 60, $"bought at {Clock.OfDay(fetched.Tick)}, after Cal's hours");
        Assert.Equal(PromiseState.Kept, p.State);
    }

    [Fact]
    public void SomeoneOnTheWayGoesOnWhileTheSignSaysOpen()
    {
        // With a road the trip takes a few minutes. The sign shuts 40 minutes after the asking: worth
        // setting out for (30 to spare), and still open when Bob gets there, so he buys it that day.
        int asked = RunShop(new World { Road = true }, 1).Favours[0].Tick;
        SimResult r = RunShop(new World { Road = true, CounterTo = Clock.OfDay(asked) + 40 }, 2);
        Promise p = r.Promises[0];
        Act fetched = r.Acts[p.FetchedAct];
        Assert.Equal(0, Clock.Day(fetched.Tick));
        Assert.InRange(fetched.Tick, asked + Clock.TickMinutes, asked + 40 - 1);
    }

    [Fact]
    public void WhatSomeoneWontFetchIsANoHoweverObligedTheyAre()
    {
        // Bob likes Ann well enough that asking is a clear yes; told he won't fetch tea, it is a no all
        // the same, and the record keeps how obliged he was (Sid, 2026-10-13: Penny and her mother).
        SimResult willing = RunShop(new World(), 1), unwilling = RunShop(new World { BobWont = new[] { "tea" } }, 1);
        Favour yes = willing.Favours[0], no = unwilling.Favours[0];
        Assert.Equal((true, false), (yes.Accepted, yes.WontFetch));
        Assert.Equal((false, true), (no.Accepted, no.WontFetch));
        Assert.Equal(yes.Margin, no.Margin);
        Assert.True(no.Margin > new FeelingOptions().ClearBand, $"margin {no.Margin}");
        Assert.Equal(("Refused", "Bob", "Ann"), (unwilling.Acts[no.ActId].Kind, unwilling.Acts[no.ActId].Actor, unwilling.Acts[no.ActId].Target!));
        Assert.Contains(unwilling.Log, l => l.Contains($" ask Bob Errand Ann act {no.ActId} no ", StringComparison.Ordinal));
        Assert.Empty(unwilling.Promises);
        Assert.Equal(Math.Round(AskMath.Eagerness(new Temperament(0.5, 0.5, 0.5, 0.5)), 4), no.Eager);
    }

    [Fact]
    public void TheAskWeighsWhatTheRecordSays()
    {
        double Plain() => RunShop(new World(), 1).Favours[0].Margin;
        // A household that can't pay costs 1, and the record says so.
        Favour poor = RunShop(new World { BobPurse = 1 }, 1).Favours[0];
        Assert.True(poor.Short);
        Assert.False(poor.Accepted);
        Assert.Equal(Plain() - 1, poor.Margin, 3);
        // Family or a shared home adds ErrandKinBond to the obligation.
        double Home(double kin) => RunShop(new World { AnnHome = "AB", BobHome = "AB", Tune = o => o.ErrandKinBond = kin }, 1).Favours[0].Margin;
        Assert.Equal(Home(0) + 0.2, Home(0.2), 3);
        // One errand already running costs 1. Ann and Dee both need tea and both ask Bob; the shop never
        // opens, so the first promise stays open, and whoever asks second finds him on an errand.
        var both = new World { Dee = true, UseTea = new[] { "Ann", "Dee" }, Shut = Enumerable.Range(0, 7).ToArray() };
        Favour[] asked = RunShop(both, 1).Favours.OrderBy(f => f.Tick).ThenBy(f => f.ActId).ToArray();
        Assert.Equal(("Bob", true, false), (asked[0].Asked, asked[0].Accepted, asked[0].Running));
        Favour second = asked.First(f => f.Asker != asked[0].Asker);
        Assert.True(second.Running);
        Favour alone = RunShop(both with { UseTea = new[] { second.Asker } }, 1).Favours[0];
        Assert.Equal((second.Asker, false), (alone.Asker, alone.Running));
        Assert.Equal(alone.Margin - 1, second.Margin, 3);
    }

    [Fact]
    public void OnlySomeoneInSightOldEnoughWhoDoesntSellItIsAsked()
    {
        Assert.NotEmpty(RunShop(new World(), 1).Favours);
        Assert.Empty(RunShop(new World { Wall = true }, 1).Favours); // within five tiles, behind a wall
        Assert.Empty(RunShop(new World { BobAge = 10, MinAge = 18 }, 1).Favours);
        Assert.Empty(RunShop(new World { BobJob = new Job("Shop", new Tile(3, 2), 0, 60, Array.Empty<int>(), 1) }, 1).Favours); // he sells it
        Assert.Empty(RunShop(new World { Regard = -0.5 }, 1).Favours);
    }

    [Fact]
    public void AfterANoTheAskerWaitsAndThenAsksAgain()
    {
        // Bob won't fetch tea, so every asking is a no; Ann waits ErrandAskAgainDays.
        SimResult r = RunShop(new World { Tune = o => (o.NeedDays, o.ErrandAskAgainDays) = (9, 3), BobWont = new[] { "tea" } }, 9);
        int[] days = r.Favours.Select(f => Clock.Day(f.Tick)).ToArray();
        Assert.True(days.Length >= 2, $"asked {days.Length} times");
        Assert.All(r.Favours, f => Assert.False(f.Accepted));
        Assert.All(days.Zip(days.Skip(1)), d => Assert.True(d.Second - d.First >= 3, $"asked again after {d.Second - d.First} days"));
        Assert.Equal(r.Favours.Count, r.Acts.Count(a => a.Kind == "Refused" && a.Actor == "Bob" && a.Target == "Ann"));
        Assert.Empty(r.Promises);
    }

    [Fact]
    public void ANeedNobodyTakesOnLapsesAndAnotherCanCome()
    {
        SimResult r = RunShop(new World { Tune = o => (o.NeedDays, o.ErrandAskAgainDays) = (1, 1), BobWont = new[] { "tea" } }, 3);
        Need first = r.Needs[0];
        Assert.Equal((NeedState.Lapsed, -1, first.Until), (first.State, first.Promise, first.Settled));
        Assert.Contains($"{first.Until} need-lapsed Ann tea", r.Log);
        Assert.True(r.Needs.Count >= 2 && r.Needs[1].Holder == "Ann", "a need lapsed makes room for the next");
        Assert.DoesNotContain(r.Favours, f => f.Tick >= first.Until && f.Tick < r.Needs[1].Tick); // nobody asked for a lapsed need
        Assert.Empty(r.Promises);
    }

    [Fact]
    public void LookingReadsWhatTheRunnerKnowsNotWhereSomeoneIs()
    {
        // Ann leaves for the park just after asking, and Bob never sees her there. He looks where he last
        // saw her (the room), not where she is; she is handed it when she comes back at midnight.
        int asked = RunShop(new World(), 1).Favours[0].Tick;
        var away = new World { AnnLeavesAt = Clock.OfDay(asked) + 3 };
        var bobWas = new HashSet<string>();
        SimResult r = new Simulation(1, Shop(away)).Run(2, (m, s) => bobWas.Add(s.Where("Bob").Place));
        Assert.DoesNotContain("Park", bobWas);
        Assert.Contains(r.Log, l => l.EndsWith(" errand-seek Bob Ann at Room", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Log, l => l.Contains(" errand-seek ", StringComparison.Ordinal) && l.EndsWith(" at Park", StringComparison.Ordinal));
        Promise p = Assert.Single(r.Promises);
        Act handed = r.Acts[p.SettledAct];
        Assert.Equal(("HandedOver", "Room", 1), (handed.Kind, handed.Location, Clock.Day(handed.Tick)));

        // Housemates don't go looking: they meet at home, or wherever they next are together.
        SimResult home = new Simulation(1, Shop(away with { AnnHome = "AB", BobHome = "AB" })).Run(2);
        Assert.DoesNotContain(home.Log, l => l.Contains(" errand-seek ", StringComparison.Ordinal));
        Assert.Equal(PromiseState.Kept, Assert.Single(home.Promises).State);

        // Anyone knows where someone works, even in a home (the clinic): once her hours start, Bob looks
        // for Ann there, having first looked where he last saw her.
        int start = Clock.OfDay(asked) + 35;
        SimResult work = new Simulation(1, Shop(new World { AnnWorksAtClinicFrom = start })).Run(1);
        Promise q = work.Promises[0];
        Assert.Equal(("HandedOver", "Home:Clinic"), (work.Acts[q.SettledAct].Kind, work.Acts[q.SettledAct].Location));
        Assert.Contains(work.Log, l => l.EndsWith(" errand-seek Bob Ann at Room", StringComparison.Ordinal));
        Assert.Contains(work.Log, l => l.EndsWith(" errand-seek Bob Ann at Home:Clinic", StringComparison.Ordinal));
        Assert.True(Clock.OfDay(work.Acts[q.SettledAct].Tick) >= start);
    }

    [Fact]
    public void InTheTownEveryChainHoldsTogether()
    {
        var (r, where) = Month.Value;
        Assert.NotEmpty(r.Needs);
        Assert.Contains(r.Favours, f => f.Accepted);
        Assert.Contains(r.Favours, f => !f.Accepted);
        Assert.Contains(r.Promises, p => p.State == PromiseState.Kept);
        var age = DefaultTown.Cast().ToDictionary(v => v.Name, v => v.Age);
        var book = DefaultTown.Errands();
        var po = new PerceptionOptions();
        bool InSight(Spot a, Spot b, int tiles)
            => !a.Asleep && !b.Asleep && a.Place == b.Place && a.At.Chebyshev(b.At) <= tiles
               && Perception.LineOfSight(TownPlaces[a.Place], a.At, b.At, po) > 0;
        foreach (Favour f in r.Favours)
        {
            Act act = r.Acts[f.ActId];
            Assert.Equal(f.Tick, act.Tick);
            Assert.Equal(f.Accepted ? ("AskedFavour", f.Asker, f.Asked) : ("Refused", f.Asked, f.Asker), (act.Kind, act.Actor, act.Target!));
            Assert.True(age[f.Asked] >= book.Good(f.Item).MinAge, $"{f.Asked} asked for {f.Item}");
            Assert.Contains(f.Item, book.Uses[f.Asker]);
            Assert.True(InSight(where[f.Tick][f.Asker], where[f.Tick][f.Asked], po.NearTiles), $"{f.Asker} asked {f.Asked} out of sight at {f.Tick}");
        }
        foreach (Act handed in r.Acts.Where(a => a.Kind == "HandedOver"))
            Assert.True(InSight(where[handed.Tick][handed.Actor], where[handed.Tick][handed.Target!], po.FarTiles),
                $"{handed.Actor} handed {handed.Target} something out of reach at {handed.Tick}");
        foreach (Promise p in r.Promises)
        {
            Need need = Assert.Single(r.Needs, n => n.Promise == p.Id);
            Assert.Equal((need.Holder, need.Item), (p.To, p.Item));
            Assert.Equal(p.Amount, book.Good(p.Item!).Price);
            if (p.FetchedAct >= 0)
            {
                Act fetched = r.Acts[p.FetchedAct];
                Assert.Equal(("Fetched", p.By, p.Place), (fetched.Kind, fetched.Actor, fetched.Location));
                Assert.True(book.CounterFor(p.Place).OpenAt(fetched.Tick), $"bought at {fetched.Tick} with the sign saying shut");
            }
            switch (p.State)
            {
                case PromiseState.Kept:
                    Assert.Equal(("HandedOver", p.By, p.To, p.MadeAct), (r.Acts[p.SettledAct].Kind, r.Acts[p.SettledAct].Actor, r.Acts[p.SettledAct].Target!, r.Acts[p.SettledAct].About));
                    Assert.Equal(p.Settled >= p.DueTo, p.Late);
                    Assert.True(p.FetchedAct >= 0);
                    Assert.Equal(NeedState.Met, need.State);
                    break;
                case PromiseState.Broken:
                    Assert.Equal(("LetDown", p.By, p.To), (r.Acts[p.SettledAct].Kind, r.Acts[p.SettledAct].Actor, r.Acts[p.SettledAct].Target!));
                    Assert.Equal(NeedState.Lapsed, need.State);
                    break;
                default:
                    Assert.Equal(PromiseState.Open, p.State);
                    Assert.Equal(NeedState.Promised, need.State);
                    break;
            }
        }
        Assert.Equal(r.Promises.Count, r.Favours.Count(f => f.Accepted));
        Assert.Equal(r.Promises.Count(p => p.State == PromiseState.Kept), r.Acts.Count(a => a.Kind == "HandedOver"));
        Assert.Equal(r.Promises.Count(p => p.State == PromiseState.Broken), r.Acts.Count(a => a.Kind == "LetDown"));
        // Nobody looks inside a home but where its person works.
        var jobs = DefaultTown.Cast().Where(v => v.Job is not null).Select(v => v.Job!.Place).ToHashSet();
        foreach (string line in r.Log.Where(l => l.Contains(" errand-seek ", StringComparison.Ordinal)))
        {
            string place = line[(line.LastIndexOf(" at ", StringComparison.Ordinal) + 4)..];
            Assert.True(!place.StartsWith("Home:", StringComparison.Ordinal) || jobs.Contains(place), line);
        }
        // Money stays in town: what the town holds changed by exactly what came in and went out.
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        Assert.DoesNotContain(r.Pursued, id => r.Acts[id].Kind is "AskedFavour" or "Fetched" or "HandedOver" or "LetDown"); // no gate begins them
    }

    [Fact]
    public void PennyNeverFetchesBeerNotEvenForHerMother()
    {
        Favour[] all = new long[] { 1, 2, 3, 5, 6, 7 }.SelectMany(seed => Run(seed, errands: true, speech: false, days: 28).Favours).ToArray();
        foreach (Favour f in all)
            Assert.Equal(f.Asked == "Penny" && f.Item == "beer", f.WontFetch);
        Favour[] beer = all.Where(f => f.WontFetch).ToArray();
        Assert.Contains(beer, f => f.Asker == "Pam"); // her mother does ask
        Assert.All(beer, f => Assert.False(f.Accepted));
        Assert.Contains(beer, f => f.Margin > new FeelingOptions().ClearBand); // and would have had a yes for anything else
        Assert.Contains(all, f => f.Asked == "Penny" && f.Accepted); // anything else, she does (Pam's only need is beer)
        Assert.Equal(new[] { "beer" }, DefaultTown.Errands().WontFetch["Penny"]);
    }

    [Fact]
    public void SpeechNamesTheThingAndAnswers()
    {
        SimResult r = Month.Value.R;
        var kindOf = SpeechCatalog.Default.Lines.ToDictionary(l => l.Id, l => l.Kind);
        var phrase = DefaultTown.Errands().Goods.ToDictionary(g => g.Name, g => g.Phrase);
        foreach (Favour f in r.Favours)
        {
            Speech[] said = r.Speech.Where(s => s.ActId == f.ActId).ToArray();
            Assert.Equal(2, said.Length);
            Assert.Equal((f.Asker, false, "AskedFavour"), (said[0].Speaker, said[0].Reply, kindOf[said[0].LineId]));
            Assert.Equal((f.Asked, true, f.Accepted ? "AskedFavour.Accepted" : "AskedFavour.Refused"), (said[1].Speaker, said[1].Reply, kindOf[said[1].LineId]));
            if (SpeechCatalog.Default.Lines.Single(l => l.Id == said[0].LineId).Text.Contains("{item}", StringComparison.Ordinal))
                Assert.Contains(phrase[f.Item], said[0].Line, StringComparison.Ordinal);
        }
        foreach (Promise p in r.Promises.Where(p => p.State == PromiseState.Kept))
        {
            Speech[] said = r.Speech.Where(s => s.ActId == p.SettledAct).ToArray();
            Assert.Equal(2, said.Length);
            Assert.Equal((p.By, p.Late ? "HandedOver.Late" : "HandedOver"), (said[0].Speaker, kindOf[said[0].LineId]));
            Assert.Equal((p.To, true, "HandedOver.Thanks"), (said[1].Speaker, said[1].Reply, kindOf[said[1].LineId]));
        }
        Assert.DoesNotContain(r.Speech, s => s.Line.Contains('{') || s.Line.Contains('}'));
        Assert.DoesNotContain(r.Speech, s => kindOf[s.LineId] == "Fetched");
    }

    [Fact]
    public void OnlyErrandLinesMayNameTheThingAndNeverToStartASentence()
    {
        string Pack(string kind, string text = "Here is {item} for you, friend.")
            => $$"""{"schema":1,"provenance":"test","lines":[{"id":"a","kind":"{{kind}}","archetypes":[],"relationship":[],"mood":[],"text":"{{text}}","tags":[]}]}""";
        Assert.Single(SpeechCatalog.Read(Pack("HandedOver.Late")).Lines);
        Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read(Pack("GaveGift")));
        var line = SpeechCatalog.Read(Pack("AskedFavour")).Lines[0];
        Assert.Equal("Here is some tea for you, friend.", SpeechCatalog.Render(line, "Ann", "some tea"));
        // The thing is said in lower case ("some tea"), so it can't open a sentence.
        Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read(Pack("HandedOver", "{item}, as you asked. Enjoy it.")));
        Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read(Pack("HandedOver", "There. {item}, as you asked.")));
        Assert.Single(SpeechCatalog.Read(Pack("HandedOver", "There you go: {item}, as you asked.")).Lines);
        Assert.DoesNotContain(SpeechCatalog.Default.Lines, l => SpeechCatalog.StartsASentence(l.Text, "{item}"));
        // Every errand kind has plain lines, so a speaker without an archetype always has something to say.
        foreach (string kind in new[] { "AskedFavour", "AskedFavour.Accepted", "AskedFavour.Refused", "HandedOver", "HandedOver.Late", "HandedOver.Thanks" })
            Assert.True(SpeechCatalog.Default.Lines.Count(l => l.Kind == kind && l.Archetypes.Count == 0) >= 3, kind);
    }

    [Fact]
    public void ARunIsTheSameTwiceAndSpeechChangesNothing()
    {
        SimResult a = Run(11, errands: true, speech: true, days: 7), b = Run(11, errands: true, speech: true, days: 7);
        Assert.Equal(a.Favours, b.Favours);
        Assert.Equal(a.Needs, b.Needs);
        Assert.Equal(a.Speech, b.Speech);
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(Run(11, errands: true, speech: false, days: 7)));
    }

    [Fact]
    public void TheRecordingCarriesErrandsOnlyWhenTheyAreOn()
    {
        static System.Text.Json.JsonElement Record(bool on) => System.Text.Json.JsonDocument.Parse(Replay.Json(new ReplayOptions
        { Seed = 7, Days = 7, Feelings = Options(on), Speech = new SpeechOptions { Enabled = true } })).RootElement;
        System.Text.Json.JsonElement with = Record(true), without = Record(false);
        foreach (string table in new[] { "needs", "favours", "promises" })
            Assert.False(without.TryGetProperty(table, out _), table);
        Assert.Equal(new[] { "accepted", "actId", "asked", "asker", "bond", "busy", "eager", "item", "low", "margin", "running", "short", "tick", "tired", "wontFetch" },
            with.GetProperty("favours")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        // The fields the viewer reads (index.html: favourByAct, promiseByAct, errandNote).
        Assert.Equal(new[] { "amount", "by", "dueFrom", "dueTo", "fetchedAct", "id", "item", "kind", "late", "made", "madeAct", "place", "settled", "settledAct", "spot", "state", "to" },
            with.GetProperty("promises")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Errand", with.GetProperty("promises")[0].GetProperty("kind").GetString());
        Assert.Equal(new[] { "holder", "id", "item", "promise", "settled", "state", "tick", "until" },
            with.GetProperty("needs")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ATownFileWithABrokenBookIsRefused()
    {
        Assert.DoesNotContain(TownCheck.Problems(TownData.Default()), p => p.StartsWith("errands", StringComparison.Ordinal) || p.StartsWith("counter", StringComparison.Ordinal));
        Economy e = TownData.Default().Economy!;
        ErrandBook book = e.Errands!;
        IReadOnlyList<string> Problems(ErrandBook b) => TownCheck.Problems(TownData.Default() with { Economy = e with { Errands = b } });
        var wine = new Dictionary<string, IReadOnlyList<string>>(book.Uses) { ["Pam"] = new[] { "wine" } };
        Assert.Contains(Problems(book with { Uses = wine }), p => p.Contains("Pam uses wine, which nobody sells", StringComparison.Ordinal));
        Assert.Contains(Problems(book with { Counters = book.Counters.Where(c => c.Place != "Saloon").ToArray() }),
            p => p.Contains("beer is sold at Saloon, which needs exactly one counter", StringComparison.Ordinal));
        Assert.Contains(Problems(book with { Counters = book.Counters.Select(c => c.Place == "Saloon" ? c with { Spot = new Tile(5, 7) } : c).ToArray() }),
            p => p.StartsWith("counter Saloon", StringComparison.Ordinal));
        Assert.Contains(Problems(book with { Counters = book.Counters.Select(c => c.Place == "Store" ? c with { From = 900, To = 600 } : c).ToArray() }),
            p => p.Contains("counter Store opens 900 and shuts 600", StringComparison.Ordinal));
    }
}
