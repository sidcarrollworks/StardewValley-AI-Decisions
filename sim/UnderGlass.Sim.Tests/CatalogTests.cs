using System.Globalization;
using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's seams (acts spec 2 and 6, slice acts-0): the desire gate reads its costs and
/// act lists from the rows (ActGate), and the shipped rows rebuild the lists the gate used before,
/// so nothing the town does changes. Tests 2 and 3 hold the pinned hashes with every row appended
/// (acts-1's Thanked, Complimented and Joked so far) and every switch off, and with every switch
/// on in watch mode. The tests of a row with its own gate use a scene: SteeringTests' room, as
/// DesireSceneTests has it; acts-1's rows have their own (ReturnsTests).
/// </summary>
public class CatalogTests
{
    private const int D = Clock.MinutesPerDay;

    private static readonly DesireKind[] Shipped =
        { DesireKind.Answer, DesireKind.Return, DesireKind.MakeUp, DesireKind.Retaliate, DesireKind.Fond, DesireKind.Pity };

    // ---- today's gate, as the code had it before acts-0 (claude/checkpoint-4, 5fdd647) --------

    /// <summary>Simulation.ActsFor's name table before acts-0, word for word.</summary>
    private static string[] TodaysNames(DesireKind k, string act, FeelingOptions o) => k switch
    {
        DesireKind.Answer or DesireKind.Retaliate => o.LightActsOn ? new[] { "Argued", Simulation.Snubbed } : new[] { "Argued" },
        DesireKind.Return when act == "HelpedSomeone" => new[] { "HelpedSomeone", "GaveGift" },
        DesireKind.Pity => new[] { "HelpedSomeone" },
        _ => new[] { "GaveGift" },
    };

    /// <summary>Simulation.Form before acts-0.</summary>
    private static double TodaysForm(string kind, FeelingOptions o) => kind switch
    {
        "GaveGift" => o.GiftCost, "HelpedSomeone" => o.HelpCost, Simulation.Snubbed => o.SnubForm, _ => o.ArgueForm,
    };

    /// <summary>Simulation.Min before acts-0.</summary>
    private static double TodaysMin(string kind, FeelingOptions o) => kind switch
    {
        "GaveGift" => o.GiftMin, "HelpedSomeone" => o.HelpMin, Simulation.Snubbed => o.SnubMin, _ => o.ArgueMin,
    };

    /// <summary>Simulation.IsLight before acts-0.</summary>
    private static bool TodaysLight(string kind) => kind is Simulation.Snubbed or Simulation.TurnedAway;

    /// <summary>The act lists the gate is tested on: the shipped town's, the grown towns', and a test
    /// world's with no snub and no help.</summary>
    private static IEnumerable<(string Name, IReadOnlyList<ActKind> Kinds)> KindLists()
    {
        yield return ("shipped", DefaultTown.Acts());
        yield return ("pelican31", Towns.Named("pelican31").Acts);
        yield return ("pelican:60@1", Towns.Named("pelican:60@1").Acts);
        yield return ("no snub or help", DefaultTown.Acts().Where(k => k.Name is not (Simulation.Snubbed or "HelpedSomeone")).ToList());
    }

    /// <summary>The switch settings: the town's, the test worlds' (WithDesire), the 0d.6 set, and
    /// the town's with a swept cost (a gift as dear as a help or dearer, a help as cheap as a gift,
    /// a snub dearer than an argument, an argument cheaper than a snub), each with light acts off
    /// and on (the only switch the gate's lists read). A sweep changes what Weigh charges, never
    /// the lists.</summary>
    private static IEnumerable<(string Name, FeelingOptions O)> Settings()
    {
        foreach (bool light in new[] { false, true })
        {
            FeelingOptions town = DefaultTown.Feelings(), tests = FeelingOptions.WithDesire(), all = DefaultTown.Feelings().With0d6("bcdefghmt");
            town.LightActsOn = tests.LightActsOn = all.LightActsOn = light;
            all.PityOn = all.ToneOn = true;
            yield return ($"town, light {light}", town);
            yield return ($"WithDesire, light {light}", tests);
            yield return ($"0d.6 and every 0d rule, light {light}", all);
            foreach (var (name, set) in Sweeps)
            {
                FeelingOptions swept = DefaultTown.Feelings();
                swept.LightActsOn = light;
                set(swept);
                yield return ($"town, {name}, light {light}", swept);
            }
        }
    }

    private static readonly (string Name, Action<FeelingOptions> Set)[] Sweeps =
    {
        ("GiftCost 0.5", o => o.GiftCost = 0.5),
        ("GiftCost 0.6", o => o.GiftCost = 0.6),
        ("HelpCost 0.4", o => o.HelpCost = 0.4),
        ("SnubForm 0.6", o => o.SnubForm = 0.6),
        ("ArgueForm 0.1", o => o.ArgueForm = 0.1),
    };

    private static ActOptions EveryOn() => new ActOptions().With(string.Join(",", ActOptions.Switches));

    /// <summary>Test 1: for every motive, source act kind and switch setting, the list built from the
    /// rows is the one the gate used before, in the same order, with the same costs and minimums;
    /// and every kind is light exactly when it was.</summary>
    [Fact]
    public void TheGateBuiltFromTheRowsIsTodays()
    {
        int compared = 0;
        foreach (var (listName, kinds) in KindLists())
            foreach (var (setting, o) in Settings())
            {
                string[] sources = kinds.Select(k => k.Name).Append("Hugged").Distinct().ToArray(); // and a kindness no town has
                foreach (DesireKind k in Shipped)
                    foreach (string act in sources)
                    {
                        string where = $"{listName}, {setting}, {k} from {act}";
                        string[] today = TodaysNames(k, act, o).Where(n => kinds.Any(x => x.Name == n)).ToArray();
                        IReadOnlyList<ActKind> served = Simulation.Served(kinds, o, k, act);
                        Assert.True(today.SequenceEqual(served.Select(x => x.Name)),
                            $"{where}: today {string.Join(",", today)}, from the rows {string.Join(",", served.Select(x => x.Name))}");
                        foreach (ActKind kind in served)
                        {
                            ActGate g = Simulation.GateOf(kind, o)!;
                            Assert.Equal((TodaysForm(kind.Name, o), TodaysMin(kind.Name, o)), (g.Form, g.Min));
                            Assert.Equal(0, g.PrideWeight);
                            Assert.Equal((false, 0.0, -1.0, 0), (g.NeedsCard, g.MinFamiliarity, g.MinRegard, g.MinAudience));
                        }
                        compared++;
                    }
                foreach (ActKind kind in kinds)
                {
                    Assert.Equal(TodaysLight(kind.Name), Simulation.GateOf(kind, o)?.Light ?? false);
                    Assert.Null(kind.Gate);
                    Assert.Equal((false, 0, D), (kind.PerHead, kind.FromMinute, kind.ToMinute));
                }
                // The new motives have nothing to serve them until their slices add rows.
                foreach (DesireKind k in new[] { DesireKind.Remorse, DesireKind.Defend, DesireKind.Curious })
                    Assert.Empty(Simulation.Served(kinds, o, k, "Argued"));
            }
        Assert.True(compared > 1000, $"only {compared} lists compared");
    }

    /// <summary>A row's own gate takes the place of the legacy table: it is served by cost, ties by
    /// name, Return in kind or less (a gift is returned with a gift or something cheaper, a wave
    /// only with a wave), and its Light flag makes it light.</summary>
    [Fact]
    public void ARowsOwnGateIsRead()
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        var kinds = DefaultTown.Acts().Concat(new[] { Wave(), Wave("Nodded") }).ToList();
        string[] Names(DesireKind k, string act) => Simulation.Served(kinds, o, k, act).Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "HelpedSomeone", "GaveGift", "Nodded", "Waved" }, Names(DesireKind.Return, "HelpedSomeone"));
        Assert.Equal(new[] { "GaveGift", "Nodded", "Waved" }, Names(DesireKind.Return, "GaveGift"));
        Assert.Equal(new[] { "Nodded", "Waved" }, Names(DesireKind.Return, "Waved"));
        Assert.Equal(new[] { "GaveGift", "Nodded", "Waved" }, Names(DesireKind.Fond, ""));
        Assert.Equal(new[] { "GaveGift" }, Names(DesireKind.MakeUp, ""));
        Assert.True(Simulation.GateOf(kinds[^1], o)!.Light);
    }

    /// <summary>Test 2: with every catalog row appended and every switch off, the act
    /// list is the shipped one, the cast is the same cast, and the runs give the shipped hash (P3)
    /// and the feelings-off hashes, as PinnedTests pins them.</summary>
    [Fact]
    public void EveryRowAppendedAndEverySwitchOffIsTheShippedTown()
    {
        Assert.Empty(ActCatalog.Batch1(new ActOptions()));
        Assert.Equal(DefaultTown.Acts().Select(Canon), ActCatalog.Kinds(new ActOptions()).Select(Canon));
        Assert.Equal(DefaultTown.Acts().Select(Canon), ActCatalog.Kinds(EveryOn()).Take(DefaultTown.Acts().Count).Select(Canon)); // shipped indices kept
        IReadOnlyList<Villager> cast = DefaultTown.Cast();
        Assert.Same(cast, ActCatalog.Cards(cast, new ActOptions()));

        IReadOnlyList<ActKind> kinds = ActCatalog.Kinds(EveryOn());
        IReadOnlyList<Villager> carded = ActCatalog.Cards(DefaultTown.Cast(), EveryOn());
        FeelingOptions shipped = DefaultTown.Feelings();
        Assert.False(shipped.Acts.Watch || shipped.Acts.Returns || shipped.Acts.Company || shipped.Acts.Welcome
                     || shipped.Acts.Repair || shipped.Acts.Sides || shipped.Acts.Late);
        Assert.Equal("e9fd83b284f5c1b6", Metrics.LogHash(new Simulation(1, cast: carded, kinds: kinds, feelings: shipped).Run(112)));
        AssertFeelingsOffHashes(carded, kinds, FeelingOptions.Off);
    }

    /// <summary>Test 3: with watch and every slice on, the run still gives the shipped hash and the
    /// feelings-off hashes, and the watch record holds what the catalog would have started.</summary>
    [Fact]
    public void WatchingEverySliceChangesNothing()
    {
        IReadOnlyList<ActKind> kinds = ActCatalog.Kinds(EveryOn());
        IReadOnlyList<Villager> cast = ActCatalog.Cards(DefaultTown.Cast(), EveryOn());
        FeelingOptions o = DefaultTown.Feelings();
        o.Acts = EveryOn();
        SimResult watched = new Simulation(1, cast: cast, kinds: kinds, feelings: o).Run(112);
        Assert.Equal("e9fd83b284f5c1b6", Metrics.LogHash(watched));
        Assert.DoesNotContain(watched.Acts, a => ActCatalog.SliceOf(a.Kind) is not null);
        Assert.Contains(watched.CatalogWatch, w => w.Motive == DesireKind.Return);
        Assert.Contains(watched.CatalogWatch, w => w.Motive == DesireKind.Fond);
        FeelingOptions off = FeelingOptions.Off;
        off.Acts = EveryOn();
        AssertFeelingsOffHashes(cast, kinds, off);
    }

    private static void AssertFeelingsOffHashes(IReadOnlyList<Villager> cast, IReadOnlyList<ActKind> kinds, FeelingOptions off)
    {
        Assert.Equal("e7f6653087ff7e18", Metrics.LogHash(new Simulation(42, cast: cast, kinds: kinds, feelings: off).Run(7)));
        Assert.Equal("388d128d7fd4cdf2", Metrics.LogHash(new Simulation(7, cast: cast, kinds: kinds,
            scheduled: new[] { Harness.ScandalFor(7, kinds) }, feelings: off).Run(14)));
    }

    /// <summary>Determinism: two runs with every slice on (watch off, so the slices act) give one
    /// hash, in any culture.</summary>
    [Fact]
    public void TheCatalogIsDeterministic()
    {
        string Run()
        {
            FeelingOptions o = DefaultTown.Feelings();
            o.Acts = EveryOn();
            o.Acts.Watch = false;
            return Metrics.LogHash(new Simulation(3, cast: ActCatalog.Cards(DefaultTown.Cast(), o.Acts), kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(14));
        }
        string first = Run();
        Assert.Equal(first, Run());
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(first, Run());
        }
        catch (CultureNotFoundException)
        {
            // no culture data on this machine
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>The replay builds its run as the runner does, with the catalog's rows after the
    /// town's own kinds (none yet in acts-0): a replay with every slice on records the shipped run,
    /// and its settings record the switches it was made with.</summary>
    [Fact]
    public void TheReplayAddsTheCatalogAsTheRunnerDoes()
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.Acts = EveryOn();
        JsonElement on = JsonDocument.Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 2, Feelings = o })).RootElement;
        JsonElement off = JsonDocument.Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 2 })).RootElement;
        Assert.Equal(ActCatalog.Kinds(o.Acts).Select(k => k.Name), on.GetProperty("kinds").EnumerateArray().Select(k => k.GetProperty("name").GetString()));
        Assert.Equal(off.GetProperty("hash").GetString(), on.GetProperty("hash").GetString());
        Assert.True(on.GetProperty("settings").GetProperty("ActOptions.Returns").GetBoolean());
        Assert.False(off.GetProperty("settings").GetProperty("ActOptions.Returns").GetBoolean());
    }

    /// <summary>The runner's --catalog list: switch names in any case; anything else names the switches.</summary>
    [Fact]
    public void SwitchesAreNamedInAnyCase()
    {
        ActOptions o = new ActOptions().With("returns, COMPANY,Late");
        Assert.Equal((false, true, true, false, false, false, true), (o.Watch, o.Returns, o.Company, o.Welcome, o.Repair, o.Sides, o.Late));
        var e = Assert.Throws<ArgumentException>(() => new ActOptions().With("acts"));
        Assert.Contains("Watch, Returns, Company, Welcome, Repair, Sides, Late", e.Message);
        ActOptions copy = o.Copy();
        copy.Returns = false;
        Assert.True(o.Returns);
    }

    /// <summary>An act's hours: the whole day by default; a window that runs past midnight.</summary>
    [Fact]
    public void HoursMayRunPastMidnight()
    {
        ActKind always = Wave();
        Assert.True(always.OpenAt(0) && always.OpenAt(D - 1));
        ActKind evening = Wave() with { FromMinute = Clock.At(18), ToMinute = Clock.At(1) };
        Assert.True(evening.OpenAt(Clock.At(18)) && evening.OpenAt(Clock.At(23, 59)) && evening.OpenAt(Clock.At(0, 30)));
        Assert.False(evening.OpenAt(Clock.At(1)) || evening.OpenAt(Clock.At(12)) || evening.OpenAt(Clock.At(17, 59)));
    }

    // ---- a row with its own gate, in a scene ----------------------------------------------

    private static ActKind Wave(string name = "Waved", int from = 0, int to = D, double pride = 0)
        => new(name, 0.5, 1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, 0.05, 0.1, 1, TargetIs.Chosen),
            Gate: new ActGate(0, 0.02, new[] { DesireKind.Return, DesireKind.Fond }, Light: true, PrideWeight: pride),
            FromMinute: from, ToMinute: to);

    private static Villager V(string name, string household, int x, double bold)
        => new(name, household, "villager", new Temperament(1.0, bold, 0.5, 0.6), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, 2), 0, D, 1) }, new Dictionary<string, double>(), Array.Empty<string>());

    /// <summary>Ann (bold 0.5) gives Bob (bold 0) a gift at 10:00. Bob can't clear a gift, which
    /// costs 0.6 here (margin about -0.38), but clears a wave, which costs nothing (about +0.23), so
    /// he returns the gift with a wave: a row with its own gate goes through the gate as a shipped
    /// one does.</summary>
    private static SimResult GiftScene(ActKind wave)
    {
        var kinds = new List<ActKind>
        {
            new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(), Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1)),
            wave,
        };
        var room = new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        FeelingOptions o = FeelingOptions.WithDesire();
        o.GiftCost = 0.6;
        return new Simulation(1, new[] { V("Ann", "A", 3, 0.5), V("Bob", "B", 5, 0) }, new[] { room }, kinds,
            feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: new[] { (Clock.At(10), "Ann", "GaveGift") }, wander: 0).Run(2);
    }

    [Fact]
    public void ARowWithItsOwnGateAnswersAMotive()
    {
        SimResult r = GiftScene(Wave());
        Act wave = Assert.Single(r.Acts, a => a.Kind == "Waved");
        Assert.Equal(("Bob", "Ann", 0), (wave.Actor, wave.Target, wave.About));
        Assert.True(wave.Tick - Clock.At(10) <= 30);
        Assert.Contains(wave.Id, r.Pursued);
        Assert.Contains(r.Pursuits, p => p is { Holder: "Bob", ActKind: "GaveGift", Call: "no" });
        Pursuit why = Assert.Single(r.Pursuits, p => p is { Holder: "Bob", ActKind: "Waved" });
        Assert.Equal((DesireKind.Return, "clear", 0.0), (why.Motive, why.Call, why.Cost));
        Assert.Single(r.Acts, a => a.Kind == "GaveGift"); // the gift is not returned twice
        LifeEvent did = Assert.Single(r.LifeEvents, e => e is { Person: "Ann", Role: LifeRole.Did, ActId: 0 });
        Assert.Equal(Outcome.Returned, did.Outcome);
        Assert.True(Assert.Single(r.LifeEvents, e => e is { Person: "Bob", Role: LifeRole.Did }).Light);
    }

    /// <summary>Fits reads a row's hours: a wave open only from noon waits for noon.</summary>
    [Fact]
    public void ARowWaitsForItsHours()
    {
        SimResult r = GiftScene(Wave(from: Clock.At(12), to: Clock.At(13)));
        Act wave = Assert.Single(r.Acts, a => a.Kind == "Waved");
        Assert.InRange(wave.Tick, Clock.At(12), Clock.At(12, 5));
    }

    /// <summary>Pride: a row with a pride weight costs PrideWeight x (self-regard - 0.5) more; Bob's
    /// self-regard is 0.6.</summary>
    [Fact]
    public void PrideRaisesTheCost()
    {
        SimResult r = GiftScene(Wave(pride: 0.5));
        Pursuit why = Assert.Single(r.Pursuits, p => p is { Holder: "Bob", ActKind: "Waved" });
        Assert.Equal(0.05, why.Cost, 12);
    }

    /// <summary>An act kind compared by value (records compare lists by reference).</summary>
    private static string Canon(ActKind k) => (k with { Allowed = Array.Empty<string>() }) + " " + string.Join(",", k.Allowed);
}
