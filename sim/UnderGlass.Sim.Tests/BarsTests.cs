using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The needs bars (needs bars spec, step 1) and the law 1 experiment's modes
/// (experiments/law1-bars-2026-10-14.md): kept and recorded, the watch mode changes nothing, children
/// are never pressed, and the modes press differently.</summary>
public class BarsTests
{
    private const string Slices = "returns,company,welcome,repair,sides,invitations";

    private static SimResult Run(long seed, int days, bool bars, int mode = 0, bool amplified = false)
    {
        FeelingOptions f = DefaultTown.Feelings();
        (f.Steer, f.Desire, f.DesireActs) = (true, true, true);
        f.Acts.With(bars ? Slices + ",bars" : Slices);
        f.Acts.BarMode = mode;
        if (amplified)
        {
            (f.Acts.LonelyTimes, f.Acts.LonelyFloorMinutes, f.Acts.BoredTimes, f.Acts.BoredFloorMinutes) = (1.25, 180, 1.25, 1440);
            (f.Acts.BarPullGain, f.Acts.BarCap, f.Acts.BarPowerWeight) = (3, 4, 0.5);
        }
        return new Simulation(seed, cast: ActCatalog.Cards(DefaultTown.Cast(), f.Acts), kinds: ActCatalog.Kinds(f.Acts), feelings: f).Run(days);
    }

    [Fact]
    public void WatchingTheBarsChangesNothingAndRecordsEachDay()
    {
        SimResult off = Run(7, 10, bars: false), watched = Run(7, 10, bars: true);
        Assert.Equal(Metrics.LogHash(off), Metrics.LogHash(watched));
        Assert.Empty(off.BarDays);
        Assert.Equal(9 * DefaultTown.Cast().Count, watched.BarDays.Count); // each closed day, everyone
        Assert.Contains("Bars", ActOptions.Switches);
    }

    [Fact]
    public void ChildrenAreNeverPressed()
    {
        var age = DefaultTown.Cast().ToDictionary(v => v.Name, v => v.Age);
        SimResult r = Run(7, 21, bars: true, amplified: true);
        Assert.All(r.BarDays.Where(d => age[d.Who] < 13), d => Assert.Equal((0, 0), (d.LonelyMinutes, d.BoredMinutes)));
        Assert.Contains(r.BarDays, d => age[d.Who] >= 13 && d.LonelyMinutes > 0);
        Assert.Contains(r.BarDays, d => age[d.Who] >= 13 && d.BoredMinutes > 0);
    }

    [Fact]
    public void TheModesPressDifferently()
    {
        SimResult watch = Run(7, 28, bars: true, 0, true), general = Run(7, 28, bars: true, 1, true),
            particular = Run(7, 28, bars: true, 2, true), spinoza = Run(7, 28, bars: true, 3, true);
        string[] hashes = new[] { watch, general, particular, spinoza }.Select(Metrics.LogHash).ToArray();
        Assert.Equal(4, hashes.Distinct().Count());
        // Spinoza: the bars are a sadness, so power falls on days that press; the lean doesn't exist.
        static int LowPower(SimResult r) => r.BarDays.Count(d => d.Day >= 7 && d.Power < 0.45);
        Assert.True(LowPower(spinoza) > 3 * Math.Max(1, LowPower(watch)), $"{LowPower(spinoza)} against {LowPower(watch)}");
        // General and particular: going out relieves boredom.
        static int Bored(SimResult r) => r.BarDays.Where(d => d.Day >= 7).Sum(d => d.BoredMinutes);
        Assert.True(Bored(general) < Bored(watch) && Bored(particular) < Bored(watch), $"{Bored(general)}, {Bored(particular)} against {Bored(watch)}");
    }
}
