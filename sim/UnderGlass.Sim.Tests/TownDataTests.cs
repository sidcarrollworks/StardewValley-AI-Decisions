using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// A whole town as one value (town spec, T0): the shipped town run through <see cref="TownData"/>
/// is the shipped town, pin for pin, and one town shared by several runs is read, never written.
/// </summary>
public class TownDataTests
{
    [Fact]
    public void TheShippedTownIsTheSameThroughTownData()
    {
        // P3, the town as it ships (PinnedTests.TheGateIsPinned, "shipped").
        Assert.Equal("e9fd83b284f5c1b6", Metrics.LogHash(new Simulation(1, TownData.Default()).Run(112)));
        // The runs from before feelings (PinnedTests.FeelingsOffReproducesTheRunsFromBeforeFeelings).
        Assert.Equal("e7f6653087ff7e18", Metrics.LogHash(new Simulation(42, TownData.Default() with { Feelings = FeelingOptions.Off }).Run(7)));
    }

    [Fact]
    public void APlacedScandalRunsTheSameThroughTownData()
    {
        var scandal = new[] { Harness.ScandalFor(2, DefaultTown.Acts()) };
        string direct = Metrics.LogHash(new Simulation(2, scheduled: scandal, feelings: DefaultTown.Feelings()).Run(7));
        Assert.Equal(direct, Metrics.LogHash(new Simulation(2, TownData.Default(), scandal).Run(7)));
    }

    /// <summary>One town given to four runs at once: each gives the hash it gives alone, so no run
    /// writes into the shared options or lists.</summary>
    [Fact]
    public void OneTownCanBeSharedByRuns()
    {
        TownData town = TownData.Default();
        string alone = Metrics.LogHash(new Simulation(5, TownData.Default()).Run(3));
        var hashes = new string[4];
        Parallel.For(0, hashes.Length, k => hashes[k] = Metrics.LogHash(new Simulation(5, town).Run(3)));
        Assert.All(hashes, h => Assert.Equal(alone, h));
        Assert.Equal(alone, Metrics.LogHash(new Simulation(5, town).Run(3)));
    }

    [Fact]
    public void TheRecorderRecordsTheTownItIsGiven()
    {
        string plain = Replay.Json(new ReplayOptions { Seed = 3, Days = 2 });
        Assert.Equal(plain, Replay.Json(new ReplayOptions { Seed = 3, Days = 2, Town = TownData.Default() }));
        // A town of its own is recorded as itself: here, the default town with feelings off.
        string off = Replay.Json(new ReplayOptions { Seed = 3, Days = 2, Town = TownData.Default() with { Feelings = FeelingOptions.Off } });
        Assert.Equal(Replay.Json(new ReplayOptions { Seed = 3, Days = 2, Feelings = FeelingOptions.Off }), off);
    }
}
