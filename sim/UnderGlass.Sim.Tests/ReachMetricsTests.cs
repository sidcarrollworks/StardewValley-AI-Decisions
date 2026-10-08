using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The reach measures and the checks that read them (batch 2 spec 5.4-5.5, slice m-0): Spearman
/// with ties; on real runs, circle reach only grows, hearing is part of holding, the town's shares
/// are the counts the run keeps, and died and grew agree with them; a festival day is the town's
/// exposure; and each of today's checks runs and reports.
/// </summary>
public class ReachMetricsTests
{
    [Fact]
    public void SpearmanGivesTiesTheirAverageRank()
    {
        Assert.Equal(1, ReachMetrics.Spearman(new double[] { 1, 2, 3, 4 }, new double[] { 3, 5, 8, 20 }), 10);
        Assert.Equal(-1, ReachMetrics.Spearman(new double[] { 1, 2, 3, 4 }, new double[] { 9, 7, 5, 1 }), 10);
        // x's ranks are 1, 2.5, 2.5, 4 against 1, 2, 3, 4: 4.5 / sqrt(4.5 x 5).
        Assert.Equal(4.5 / Math.Sqrt(4.5 * 5), ReachMetrics.Spearman(new double[] { 1, 2, 2, 3 }, new double[] { 10, 20, 30, 40 }), 10);
        Assert.True(double.IsNaN(ReachMetrics.Spearman(new double[] { 1, 2 }, new double[] { 1, 2 })));
        Assert.True(double.IsNaN(ReachMetrics.Spearman(new double[] { 1, 1, 1 }, new double[] { 1, 2, 3 })));
        Assert.Equal(2.5, ReachMetrics.Median(new double[] { 4, 1, double.NaN, 2, 3 }));
        Assert.True(double.IsNaN(ReachMetrics.Median(Array.Empty<double>())));
    }

    /// <summary>A placed scandal over two weeks, on several seeds: the measures agree with each other
    /// and with what the run counted.</summary>
    [Fact]
    public void OnRunsTheMeasuresAgree()
    {
        var kinds = DefaultTown.Acts();
        int measured = 0;
        for (long seed = 1; seed <= 5; seed++)
        {
            SimResult r = new Simulation(seed, TownData.Default(), new[] { Harness.ScandalFor(seed, kinds) }).Run(14);
            foreach (Act a in r.Acts.Where(a => a.Injected))
            {
                measured++;
                int d0 = Clock.Day(a.Tick), last = r.Days - 1;
                // An empty circle (the newcomer, whom nobody knows yet) has no circle reach.
                Assert.Equal(r.Circles[a.Id].Count == 0, double.IsNaN(ReachMetrics.CircleReach(r, a, last)));
                double before = 0;
                for (int d = d0; d <= last && r.Circles[a.Id].Count > 0; d++)
                {
                    double reach = ReachMetrics.CircleReach(r, a, d), heard = ReachMetrics.CircleHeard(r, a, d);
                    Assert.True(reach >= before && heard <= reach, $"day {d}");
                    before = reach;
                }
                Assert.Equal(r.HoldersByDay[a.Id][last] / (double)(r.CastSize - 1), ReachMetrics.TownReach(r, a, last));
                Assert.True(ReachMetrics.TownHeard(r, a, last) <= ReachMetrics.TownReach(r, a, last));
                Assert.Equal(r.HeardByDay[a.Id][last] <= r.Witnesses.GetValueOrDefault(a.Id), ReachMetrics.Died(r, a));
                int grew = ReachMetrics.Grew(r, a);
                Assert.InRange(grew, 0, last - d0);
                if (grew > 0)
                    Assert.True(r.HeardByDay[a.Id][d0 + grew] > r.HeardByDay[a.Id][d0 + grew - 1]);
                Assert.All(Enumerable.Range(d0 + grew + 1, last - d0 - grew), d => Assert.True(r.HeardByDay[a.Id][d] <= r.HeardByDay[a.Id][d - 1]));
            }
        }
        Assert.True(measured >= 4, $"{measured} placed");
    }

    /// <summary>An act on a festival day is the town's (the Egg Festival, day 12), and one with
    /// nobody near it, on an ordinary day, is private.</summary>
    [Fact]
    public void AFestivalDayIsTheTownsExposure()
    {
        var sim = new Simulation(2, TownData.Default());
        sim.Place(new[]
        {
            new Scenario("fest", "RummagedInBin", Places: new[] { "Square" }, Day: 12, AtFestival: true, GiveUpDays: 1),
            new Scenario("alone", "RummagedInBin", Day: 2, MaxInRange: 0),
        });
        SimResult r = sim.Run(14);
        Assert.Equal("Egg Festival", Calendar.FestivalOn(12));
        Act fest = Assert.Single(r.Acts, a => r.Scenarios.TryGetValue(a.Id, out string? s) && s == "fest");
        Assert.Equal(12, Clock.Day(fest.Tick));
        Assert.Equal(Exposure.Town, ReachMetrics.ExposureOf(r, fest, DefaultTown.Gatherings()));
        Act alone = Assert.Single(r.Acts, a => r.Scenarios.TryGetValue(a.Id, out string? s) && s == "alone");
        Assert.Equal(Exposure.Private, ReachMetrics.ExposureOf(r, alone, DefaultTown.Gatherings()));
    }

    /// <summary>Today's checks are found by name in any case, the others are refused, and each runs
    /// on a few seeds to a result with its criterion; C13 is only reported.</summary>
    [Fact]
    public void TodaysChecksRun()
    {
        Assert.Equal("C10", ReachMetrics.Named("c10").Name);
        Assert.Throws<ArgumentException>(() => ReachMetrics.Named("C1")); // festivals come with b2-4
        var kinds = DefaultTown.Acts();
        foreach (string name in ReachMetrics.Today.Where(n => n != "C11"))
        {
            Check check = ReachMetrics.Named(name);
            var runs = Enumerable.Range(1, 4).Select(seed =>
            {
                var sim = new Simulation(seed, TownData.Default(), check.Placement == Placement.PlacedScandal
                    ? new[] { Harness.ScandalFor(seed, kinds) } : Array.Empty<(int, string, string)>());
                if (check.Scenarios is { } scenes)
                    sim.Place(scenes(seed, kinds));
                return sim.Run(10);
            }).ToList();
            CheckResult res = ReachMetrics.Evaluate(check, runs, kinds, DefaultTown.Cast(), DefaultTown.Gatherings());
            Assert.Equal((name, 4), (res.Check, res.Runs));
            Assert.InRange(res.Placed, 1, 4);
            Assert.False(string.IsNullOrWhiteSpace(res.Summary));
            Assert.All(res.Values.Where(v => v.Key != "spearman"), v => Assert.True(double.IsNaN(v.Value) || v.Value is >= 0 and <= 1, $"{name} {v.Key} {v.Value}"));
            Assert.Equal(name == "C13", res.Pass is null);
        }
    }
}
