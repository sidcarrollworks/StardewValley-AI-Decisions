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
            string inGerman = Metrics.LogHash(new Simulation(42).Run(1));
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
        Assert.Equal("e7f6653087ff7e18", Metrics.LogHash(new Simulation(42).Run(7)));
        Assert.Equal("388d128d7fd4cdf2", Metrics.LogHash(new Simulation(7,
            scheduled: new[] { Harness.ScandalFor(7, DefaultTown.Acts()) }).Run(14)));
    }
}
