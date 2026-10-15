using System.Globalization;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Runs pinned to their log hashes (experiment E0): the log reads the same on every
/// machine, and turning feelings off reproduces the runs from before them.</summary>
public class PinnedTests
{
    [Fact]
    public void TheLogReadsTheSameInEveryCulture()
    {
        CultureInfo german;
        try { german = CultureInfo.GetCultureInfo("de-DE"); }
        catch (CultureNotFoundException) { return; } // no culture data on this machine
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = german;
            string inGerman = Metrics.LogHash(new Simulation(42).Run(1)); // feelings on: their lines print numbers too
            Assert.Equal(german, CultureInfo.CurrentCulture); // restored after the run
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(Metrics.LogHash(new Simulation(42).Run(1)), inGerman);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>Update these only when a deliberate change to phase 0a or 0b lands.</summary>
    [Fact]
    public void FeelingsOffReproducesTheRunsFromBeforeFeelings()
    {
        Assert.Equal("d1a5fd6ac8e82bcd", Metrics.LogHash(new Simulation(42, feelings: FeelingOptions.Off).Run(7)));
        Assert.Equal("6b2b062d09ec0258", Metrics.LogHash(new Simulation(7,
            scheduled: new[] { Harness.ScandalFor(7, DefaultTown.Acts()) }, feelings: FeelingOptions.Off).Run(14)));
    }

    /// <summary>Acts and beliefs compared by their 0a and 0b fields (records compare lists by reference).</summary>
    private static string Canon(Act a) => $"{a.Id} {a.Tick} {a.Actor} {a.Kind} {a.Location} {a.At} {a.Injected}";
    private static string Canon(Belief b) => $"{b.ActId} {b.Kind} {b.Actor} {b.Confidence:R} {b.Clarity:R} {b.Source} {b.Juiciness:R} {b.GotTick} "
        + $"[{string.Join(",", b.Chain)}] [{(b.Suspects is null ? "null" : string.Join(",", b.Suspects))}]";

    /// <summary>T12: feelings that only watch change nothing the town does or believes.</summary>
    [Fact]
    public void ObservingFeelingsChangesNoActOrBelief()
    {
        for (long seed = 1; seed <= 10; seed++)
        {
            SimResult off = new Simulation(seed, feelings: FeelingOptions.Off).Run(14);
            SimResult on = new Simulation(seed, feelings: FeelingOptions.Observe).Run(14);
            Assert.Equal(off.Acts.Select(Canon), on.Acts.Select(Canon));
            foreach (string n in off.Beliefs.Keys)
                Assert.Equal(off.Beliefs[n].OrderBy(p => p.Key).Select(p => Canon(p.Value)),
                    on.Beliefs[n].OrderBy(p => p.Key).Select(p => Canon(p.Value)));
            Assert.Equal(off.Interviews, on.Interviews);
            Assert.Equal(off.Verdicts, on.Verdicts);
            Assert.Equal(off.Confrontations, on.Confrontations);
            Assert.Equal(off.Motives, on.Motives);
            Assert.Equal(off.TownCash, on.TownCash);
            Assert.NotEmpty(on.Feelings);
            Assert.Empty(off.Feelings);
        }
    }

    /// <summary>T12b: watching with the desire gate and every added rule switched on still changes
    /// nothing, and the gate keeps no record while feelings only watch.</summary>
    [Fact]
    public void ObservingWithTheGateOnChangesNothing()
    {
        for (long seed = 1; seed <= 3; seed++)
        {
            FeelingOptions o = FeelingOptions.Observe;
            o.Desire = true;
            o.LightActsOn = o.StanceOn = o.FondOn = o.PityOn = o.ToneOn = true;
            o.PowerWeight = 1;
            SimResult off = new Simulation(seed, feelings: FeelingOptions.Off).Run(14);
            SimResult on = new Simulation(seed, feelings: o).Run(14);
            Assert.Equal(off.Acts.Select(Canon), on.Acts.Select(Canon));
            foreach (string n in off.Beliefs.Keys)
                Assert.Equal(off.Beliefs[n].OrderBy(p => p.Key).Select(p => Canon(p.Value)),
                    on.Beliefs[n].OrderBy(p => p.Key).Select(p => Canon(p.Value)));
            Assert.Equal(off.Confrontations, on.Confrontations);
            Assert.Equal(off.TownCash, on.TownCash);
            Assert.Empty(on.Stirred);
            Assert.Empty(on.Pursuits);
            Assert.Empty(on.Pursued);
            Assert.Empty(on.LifeEvents);
            Assert.Empty(on.MotiveLog);
            Assert.All(on.Stances.Values.SelectMany(v => v), v => Assert.Equal(0, v));
        }
    }

    /// <summary>D34: the desire gate pinned on a year of seed 1: the base gate (every added rule off,
    /// power weight 0), the gate with stance and the power of acting, the town as 0d.4 shipped it,
    /// with its starting tensions at Fond's first 10 days, and as it ships now (P3: the tensions, Fond
    /// at 14 days after the three-year check). The first three start with no tensions.
    /// Update only for a deliberate change to the gate.</summary>
    [Theory]
    [InlineData("base", "f436d9f4bde4804b")]
    [InlineData("stance+power", "5d63a0c57937ac68")]
    [InlineData("shipped without tensions", "1de67ee905a330b6")]
    [InlineData("tensions, Fond at 10 days", "4df36deae7f5f0a3")]
    [InlineData("shipped", "8a002fea26b541fe")]
    public void TheGateIsPinned(string which, string hash)
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.Start = DefaultTown.Tensions(0);
        o.LightActsOn = o.StanceOn = o.FondOn = o.PityOn = o.ToneOn = false;
        o.PowerWeight = 0;
        if (which == "stance+power")
        {
            o.StanceOn = true;
            o.PowerWeight = 1;
        }
        else if (which == "shipped without tensions")
        {
            o = DefaultTown.Feelings();
            o.Start = DefaultTown.Tensions(0);
            o.FondDays = 10; // as 0d.4 shipped it
        }
        else if (which == "tensions, Fond at 10 days")
        {
            o = DefaultTown.Feelings();
            o.FondDays = 10; // before the three-year check (0d.5)
        }
        else if (which == "shipped")
            o = DefaultTown.Feelings(); // P3: with the starting tensions (0d.5)
        Assert.Equal(hash, Metrics.LogHash(new Simulation(1, feelings: o).Run(112)));
    }

    /// <summary>P1: the town with feelings steering as 0c built it, before the desire gate. A
    /// year of seed 1. Update only for a deliberate change to 0a-0c.</summary>
    [Fact]
    public void TheTownOf0cIsPinned()
        => Assert.Equal("84e6479eafeb666d", Metrics.LogHash(new Simulation(1, feelings: Town0c()).Run(112)));

    /// <summary>The town's feelings with the desire gate and the starting tensions off: phase 0c's town.</summary>
    internal static FeelingOptions Town0c() { FeelingOptions o = DefaultTown.Feelings(); o.Desire = false; o.Start = DefaultTown.Tensions(0); return o; }

    /// <summary>0d.6: each measured configuration pinned on a year of seed 1, with the constants the
    /// town carries (DefaultTown.Feelings): hurts at home and households arguing (b); with being
    /// left out and the dials (bde); missing people alone (m); the candidate town, every step but
    /// contagion and the tone (bdefghm); and it with the tone (bdefghmt). Update only for a
    /// deliberate change to a 0d.6 rule or constant.</summary>
    [Theory]
    [InlineData("b", "c2a608182a967c1d")]
    [InlineData("bde", "ec533fd249c57f55")]
    [InlineData("m", "15fec38bf3f8c4df")]
    [InlineData("bdefghm", "34259a77e3f9a70a")]
    [InlineData("bdefghmt", "a2b7b3e655519016")]
    [InlineData("bcdefghmt", "884a53de9b68fcb8")] // the review's set: every step, contagion and the tone
    public void The0d6StepsArePinned(string steps, string hash)
        => Assert.Equal(hash, Metrics.LogHash(new Simulation(1, feelings: DefaultTown.Feelings().With0d6(steps)).Run(112)));

    /// <summary>0d.6 (X13): with every 0d.6 rule switched on and only watched, the town is P3's to the
    /// byte, and the rules record what they would have done. (The tone is 0d's own rule, so it stays off.)</summary>
    [Fact]
    public void WatchingEvery0d6RuleChangesNothing()
    {
        FeelingOptions o = DefaultTown.Feelings().With0d6("bcdefghm");
        o.WithdrawalWatch = true;
        o.LeftOutRate = 0.05; // strong enough to want to act
        SimResult r = new Simulation(1, feelings: o).Run(112);
        Assert.Equal("8a002fea26b541fe", Metrics.LogHash(r));
        foreach (string rule in new[] { "hurt at home", "contagion", "left out", "dial: chat", "recovery: keep", "missing", "held", "shown" })
            Assert.True(r.Rules.ContainsKey(rule), rule);
    }

    /// <summary>The act catalog: each slice pinned on a year of seed 1, on the shipped town and on the
    /// 0d.6 review's set (bcdefghmt): acts-1, Returns; acts-2, Company, alone and with Returns; acts-3, Welcome, acts-4, Repair, acts-5, Sides, and acts-6, Late, each alone and with the slices before it.
    /// Update only for a deliberate change to a catalog rule or row.</summary>
    [Theory]
    [InlineData("returns", "", "7862e06e743c4f25")]
    [InlineData("returns", "bcdefghmt", "3b7d1fa03c0d927b")]
    [InlineData("company", "", "7ce8b5cb510e2fee")]
    [InlineData("returns,company", "", "505432fd74a8c7d0")]
    [InlineData("returns,company", "bcdefghmt", "acd9ec30a746fd4e")]
    [InlineData("welcome", "", "fd7a6a2ef016e46b")]
    [InlineData("returns,company,welcome", "", "cce41a2382937ae9")]
    [InlineData("returns,company,welcome", "bcdefghmt", "61a136dda52b2460")]
    [InlineData("repair", "", "e3fd85b2da650214")]
    [InlineData("returns,company,welcome,repair", "", "b7e9d63b891b554a")]
    [InlineData("returns,company,welcome,repair", "bcdefghmt", "841fbf09dd316f42")]
    [InlineData("sides", "", "4191fa33668ae70d")]
    [InlineData("returns,company,welcome,repair,sides", "", "432b156173b8a2fd")]
    [InlineData("returns,company,welcome,repair,sides", "bcdefghmt", "0a8acad9d8aa29cd")]
    [InlineData("late", "", "8fed40e1ccd3d0c0")]
    [InlineData("returns,company,welcome,repair,sides,late", "", "74a5628dd45ee0df")]
    [InlineData("returns,company,welcome,repair,sides,late", "bcdefghmt", "14c8baefe84cf610")]
    public void TheCatalogsSlicesArePinned(string slices, string steps, string hash)
    {
        FeelingOptions o = DefaultTown.Feelings().With0d6(steps);
        o.Acts.With(slices);
        Assert.Equal(hash, Metrics.LogHash(new Simulation(1, cast: ActCatalog.Cards(DefaultTown.Cast(), o.Acts),
            kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(112)));
    }

    /// <summary>P2: motives watched but not acted on change nothing: the same year as P1.</summary>
    [Fact]
    public void WatchedMotivesChangeNothing()
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.DesireActs = false;
        o.Start = DefaultTown.Tensions(0);
        SimResult r = new Simulation(1, feelings: o).Run(112);
        Assert.Equal("84e6479eafeb666d", Metrics.LogHash(r));
        Assert.NotEmpty(r.MotiveLog);
        Assert.NotEmpty(r.Stirred);
    }
}
