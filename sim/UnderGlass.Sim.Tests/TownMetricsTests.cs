using UnderGlass.Sim;
using UnderGlass.Sim.Generation;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>TownMetrics (town spec 6.3): districts, tellings inside them against chance, and how a
/// neighbourhood's stories travel.</summary>
public class TownMetricsTests
{
    [Fact]
    public void EveryoneHasADistrict_TheCoreAndEachNeighbourhood()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var d = TownMetrics.Districts(town);
        Assert.Equal(60, d.Count);
        Assert.Equal(31, d.Values.Count(x => x == "core"));
        Assert.Equal(15, d.Values.Count(x => x == "EastGreen"));
        Assert.Equal(14, d.Values.Count(x => x == "NorthLane"));
        Assert.All(TownMetrics.Districts(TownData.Default()).Values, x => Assert.Equal("core", x));
    }

    /// <summary>The shares are what the log says: counted again here from the "told" lines.</summary>
    [Fact]
    public void TellingsInsideADistrictAreCountedFromTheLog()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var runs = new[] { new Simulation(1, town).Run(3), new Simulation(2, town).Run(3) };
        TownStats s = TownMetrics.Summarise(runs, town);
        var d = TownMetrics.Districts(town);
        var told = runs.SelectMany(r => r.Log).Select(l => l.Split(' ')).Where(w => w.Length >= 5 && w[1] == "told").ToList();
        Assert.NotEmpty(told);
        Assert.Equal(told.Count(w => d[w[2]] == d[w[3]]) / (double)told.Count, s.SameDistrict, 9);
        Assert.Equal((31 * 30 + 15 * 14 + 14 * 13) / (60.0 * 59), s.SameByChance, 9);
        Assert.Equal(s.SameDistrict / s.SameByChance, s.LocalityRatio, 9);
        Assert.InRange(s.FirstDayLocal, 0, 1);
        Assert.InRange(s.CrossedInTwoDays, 0, 1);
        Assert.InRange(s.KnownWellMedian, 0, 59);
        Assert.Equal(3, s.Districts.Count);
        Assert.Equal("core", s.Districts[0].District);
    }
}
