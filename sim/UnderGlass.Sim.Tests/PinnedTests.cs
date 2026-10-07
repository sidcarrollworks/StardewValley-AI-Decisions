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
        Assert.Equal("e7f6653087ff7e18", Metrics.LogHash(new Simulation(42, feelings: FeelingOptions.Off).Run(7)));
        Assert.Equal("388d128d7fd4cdf2", Metrics.LogHash(new Simulation(7,
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
    /// power weight 0), the gate with stance and the power of acting, and the town as shipped.
    /// Update only for a deliberate change to the gate.</summary>
    [Theory]
    [InlineData("base", "a123526358094b43")]
    [InlineData("stance+power", "07505a08bf1a9f78")]
    [InlineData("shipped", "f2af5b9f5b1fb4c4")]
    public void TheGateIsPinned(string which, string hash)
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.LightActsOn = o.StanceOn = o.FondOn = o.PityOn = o.ToneOn = false;
        o.PowerWeight = 0;
        if (which == "stance+power")
        {
            o.StanceOn = true;
            o.PowerWeight = 1;
        }
        else if (which == "shipped")
            o = DefaultTown.Feelings();
        Assert.Equal(hash, Metrics.LogHash(new Simulation(1, feelings: o).Run(112)));
    }

    /// <summary>P1: the town with feelings steering as 0c built it, before the desire gate. A
    /// year of seed 1. Update only for a deliberate change to 0a-0c.</summary>
    [Fact]
    public void TheTownOf0cIsPinned()
        => Assert.Equal("c0488cc6bf81e64f", Metrics.LogHash(new Simulation(1, feelings: Town0c()).Run(112)));

    /// <summary>The town's feelings with the desire gate off: phase 0c's town.</summary>
    internal static FeelingOptions Town0c() { FeelingOptions o = DefaultTown.Feelings(); o.Desire = false; return o; }

    /// <summary>P2: motives watched but not acted on change nothing: the same year as P1.</summary>
    [Fact]
    public void WatchedMotivesChangeNothing()
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.DesireActs = false;
        SimResult r = new Simulation(1, feelings: o).Run(112);
        Assert.Equal("c0488cc6bf81e64f", Metrics.LogHash(r));
        Assert.NotEmpty(r.MotiveLog);
        Assert.NotEmpty(r.Stirred);
    }
}
