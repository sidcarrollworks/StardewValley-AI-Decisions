using System.Globalization;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Forked runs and V7, the open future (batch 2 spec 6.3 and 7.4; slice m-2).</summary>
public class ForkTests
{
    private static int TickOf(string line) => int.Parse(line[..line.IndexOf(' ')], CultureInfo.InvariantCulture);

    private static SimResult Forked(long seed, int day, long salt, int days)
    {
        var sim = new Simulation(seed, TownData.Default());
        sim.Fork(day, salt);
        return sim.Run(days);
    }

    [Fact]
    public void AForkIsItsBaseUntilItsDay_ThenDrawsAnew()
    {
        const int days = 29, at = 28 * Clock.MinutesPerDay; // minute 40320
        SimResult based = new Simulation(1, TownData.Default()).Run(days);
        SimResult fork = Forked(1, 28, 1, days);

        var before = based.Log.TakeWhile(l => TickOf(l) < at).ToList();
        Assert.True(before.Count > 1000);
        Assert.Equal(before, fork.Log.TakeWhile(l => TickOf(l) < at));
        Assert.NotEqual(based.Log.Skip(before.Count), fork.Log.Skip(before.Count));

        // One seed, day and salt make one run; another salt makes another.
        Assert.Equal(Metrics.LogHash(fork), Metrics.LogHash(Forked(1, 28, 1, days)));
        Assert.NotEqual(Metrics.LogHash(fork), Metrics.LogHash(Forked(1, 28, 2, days)));
    }

    [Fact]
    public void AForkAfterTheRunsLastDayChangesNothing()
    {
        // P3, the town as it ships (PinnedTests.TheGateIsPinned, "shipped"): the swap is the fork's only effect.
        Assert.Equal("e9fd83b284f5c1b6", Metrics.LogHash(Forked(1, 112, 7, 112)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Simulation(1, TownData.Default()).Fork(-1, 1));
    }

    private static StoryEvent E(string kind, int day, params string[] people) => new(kind, day, people);

    [Fact]
    public void V7CountsTheForksWhoseMajorStoriesMoved()
    {
        IReadOnlyList<StoryEvent> based = new[] { E("Hermit", 10, "Penny"), E("Scandal", 40, "Pam"), E("Feud", 50, "Sam", "Shane") };
        // The same major story on another day, and another feud (a feud isn't major): the same year.
        IReadOnlyList<StoryEvent> same = new[] { E("Hermit", 10, "Penny"), E("Scandal", 60, "Pam"), E("Feud", 70, "Alex", "Haley") };
        // Pam's scandal gone and a secret out instead: another year, under another headline.
        IReadOnlyList<StoryEvent> moved = new[] { E("Hermit", 10, "Penny"), E("SecretOut", 45, "Pierre") };
        // A wrong verdict after the year: not counted.
        IReadOnlyList<StoryEvent> late = new[] { E("Hermit", 10, "Penny"), E("Scandal", 40, "Pam"), E("WrongVerdict", 115, "Shane") };
        var runs = new[] { (based, (IReadOnlyList<IReadOnlyList<StoryEvent>>)new[] { same, moved, late }) };

        var (v7, headline) = Variety.Open(runs);
        Assert.Equal(1 / 3.0, v7, 9);
        Assert.Equal(1 / 3.0, headline, 9);
        Assert.Equal((v7, headline), Variety.Open(runs)); // the same every time
        Assert.Equal(new[] { "Scandal Pam" }, Variety.MajorStories(based));
        Assert.Equal("Hermit", Variety.Kinds[Variety.MajorRanks - 1]);

        var none = Variety.Open(new[] { (based, (IReadOnlyList<IReadOnlyList<StoryEvent>>)Array.Empty<IReadOnlyList<StoryEvent>>()) });
        Assert.True(double.IsNaN(none.V7) && double.IsNaN(none.Headline));
    }
}
