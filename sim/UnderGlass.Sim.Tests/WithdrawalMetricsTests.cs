using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Phase 0d.6's measures (spec X0 and section 9): a sustained spell needs 28 days in a row;
/// a hermit also needs the free hours out to fall below 60% of the person's first season; a spell
/// cut off by the run's end is a spell but not ended; the shyest third; and the measures agree
/// with what a year of the town records.</summary>
public class WithdrawalMetricsTests
{
    private static PersonDays Days(int length, Func<int, int> outMinutes)
        => new(new int[length], new bool[length], new int[length], new int[length],
            Enumerable.Range(0, length).Select(outMinutes).ToArray(), new double[length]);

    private static double[] Stance(int length, params (int From, int To, double Value)[] spans)
    {
        var s = new double[length];
        foreach (var (from, to, v) in spans)
            for (int k = from; k <= to; k++)
                s[k] = v;
        return s;
    }

    [Fact]
    public void ASpellNeedsTwentyEightDaysInARow()
    {
        var home = Days(112, d => d < 28 ? 600 : 200);   // 10 h out a day, then 3.3 h: a fall of two thirds
        Assert.Empty(WithdrawalMetrics.SpellsOf("Ann", Stance(112, (30, 56, -0.6)), home, brawlers: false)); // 27 days
        Spell s = Assert.Single(WithdrawalMetrics.SpellsOf("Ann", Stance(112, (30, 57, -0.6)), home, brawlers: false));
        Assert.Equal((30, 57, true, true), (s.From, s.To, s.Hermit, s.Ended));
        Assert.Equal(2 / 3.0, s.HoursFall, 9);
        // A day above -0.5 breaks it.
        Assert.Empty(WithdrawalMetrics.SpellsOf("Ann", Stance(112, (30, 50, -0.6), (52, 70, -0.6)), home, brawlers: false));
        // Brawlers the other way.
        Spell b = Assert.Single(WithdrawalMetrics.SpellsOf("Bob", Stance(112, (40, 111, 0.7)), home, brawlers: true));
        Assert.Equal((40, 111, false, false), (b.From, b.To, b.Hermit, b.Ended)); // runs to the end: not ended
    }

    [Fact]
    public void AHermitIsWithdrawnAndStaysIn()
    {
        var stays = Days(112, _ => 300);                       // no fall in hours out
        var fewer = Days(112, d => d < 28 ? 300 : 200);         // a third less: 67% of the first season
        var most = Days(112, d => d < 28 ? 300 : 150);          // half: under 60%
        double[] st = Stance(112, (40, 80, -0.7));
        Assert.False(Assert.Single(WithdrawalMetrics.SpellsOf("A", st, stays, false)).Hermit);
        Assert.False(Assert.Single(WithdrawalMetrics.SpellsOf("A", st, fewer, false)).Hermit);
        Assert.True(Assert.Single(WithdrawalMetrics.SpellsOf("A", st, most, false)).Hermit);
        // Only the spell's last 28 days count: out a lot early in the spell, then in.
        var late = Days(112, d => d < 28 ? 300 : d <= 52 ? 400 : 100);
        Assert.True(Assert.Single(WithdrawalMetrics.SpellsOf("A", st, late, false)).Hermit);
    }

    [Fact]
    public void TheShyestThird()
    {
        var town = DefaultTown.Cast().ToDictionary(v => v.Name, v => v.Temperament);
        var shy = WithdrawalMetrics.ShyestThird(town);
        Assert.Equal(9, shy.Count);
        Assert.Equal("Penny", shy[0]);
        Assert.Contains("Harvey", shy);
        Assert.DoesNotContain("Alex", shy);
    }

    /// <summary>Four weeks of the town: what the metrics report agrees with the record, and with
    /// every 0d.6 switch off nothing is withdrawn and nothing is watched.</summary>
    [Fact]
    public void TheTownsRecordAndItsMeasures()
    {
        SimResult r = new Simulation(1).Run(28);
        Assert.Equal(26, r.Daily.Count);
        Assert.All(r.Daily.Values, d => Assert.Equal(28, d.LeftOut.Length));
        WithdrawalStats w = WithdrawalMetrics.Summarise(new[] { r });
        Assert.Single(w.BySeason);
        double kind = r.Daily.Values.Sum(d => d.KindIn.Sum()) / 26.0;
        Assert.Equal(kind, w.BySeason[0].KindIn, 9);
        Assert.Equal(r.Daily.Values.Sum(d => d.LeftOut.Sum()) / (26.0 * 28), w.BySeason[0].MeanE, 9);
        Assert.True(kind > 0);
        Assert.True(w.BySeason[0].MetDays > 0);
        Assert.Equal(0, w.WithdrawnPerYear);
        Assert.Empty(w.Rules);
        Assert.Equal(5, w.Shyest.Count);
        Assert.Equal("Penny", w.Shyest[0].Name);
        // Kindness received is counted where it is felt, from outside kin and household.
        var byName = DefaultTown.Cast().ToDictionary(v => v.Name);
        int fromOutside = r.LifeEvents.Count(e => e.Role == LifeRole.Undergone && e.Kind is "GaveGift" or "HelpedSomeone"
            && byName.ContainsKey(e.Other) && byName[e.Person].Household != byName[e.Other].Household && byName[e.Person].KinOf(e.Other) is null);
        Assert.Equal(fromOutside, r.Daily.Values.Sum(d => d.KindIn.Sum()));
    }
}
