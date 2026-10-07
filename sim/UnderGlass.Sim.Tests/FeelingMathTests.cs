using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The rules of feeling that need no world (phase 0c): saturation, the law 5 table,
/// likeness, blame, keeping, credence, love after conquered hate, mood and the power of acting,
/// being named, and the mayor's and witnesses' readings of regard. Pure, like Authority.Weigh.</summary>
public class FeelingMathTests
{
    private static readonly FeelingOptions O = new();

    private static Villager V(string name, string household, string kind, int age, string? workplace)
        => new(name, household, kind, new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1),
            workplace is null ? null : new Job(workplace, new Tile(2, 2), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1),
            Array.Empty<Haunt>(), new Dictionary<string, double>(), Array.Empty<string>(), age);

    /// <summary>What someone feels at an act whose patient is someone else they know.</summary>
    private static (double F0, string Route) FeelFor(double joy, double regard, double likeness = 0, double understanding = 0.5,
        FeelingOptions? o = null)
        => Feelings.Base(joy, 1, self: false, known: true, regard, likeness, understanding, onlookers: false, o ?? O);

    private static IEnumerable<double> Grid(double from, double to, int steps)
        => Enumerable.Range(0, steps + 1).Select(i => from + (to - from) * i / steps);

    [Fact]
    public void Saturate_KeepsRegardInRange_AndDampsOnlyTheWayItLeans()
    {
        Assert.Equal(0.84, Feelings.Saturate(0.8, 0.2), 12);    // deeper love is harder to gain
        Assert.Equal(0.6, Feelings.Saturate(0.8, -0.2), 12);    // but lost at full rate
        Assert.Equal(-0.75, Feelings.Saturate(-0.5, -0.5), 12);
        foreach (double d in new[] { -1, -0.3, 0, 0.3, 1 })
            Assert.Equal(d, Feelings.Saturate(0, d), 12);       // neutral leans no way

        foreach (double r in Grid(-1, 1, 40))
            foreach (double d in Grid(-1, 1, 40))
            {
                double v = Feelings.Saturate(r, d);
                Assert.InRange(v, -1, 1);
                double change = v - r;
                Assert.True(Math.Abs(change) <= Math.Abs(d) + 1e-12, $"r {r} d {d}: moved {change}");
                Assert.True(change == 0 || Math.Sign(change) == Math.Sign(d), $"r {r} d {d}: moved {change}");
                if (r != 0 && Math.Sign(d) != Math.Sign(r))
                    Assert.Equal(r + d, v, 12);                 // against the lean: undamped
            }
    }

    /// <summary>Law 5 at sensitivity 0.5 (sens 1): the patient feels it in full; someone who loves
    /// them shares it; someone who hates them feels the opposite; anyone else a smaller share by
    /// likeness and understanding.</summary>
    [Fact]
    public void TheLawFiveTable()
    {
        var patient = Feelings.Base(-0.5, 1, self: true, known: true, 0, 0, 0.5, onlookers: false, O);
        Assert.Equal(-0.5, patient.F0, 12);
        Assert.Equal("Direct", patient.Route);
        Assert.Equal("Onlooker", Feelings.Base(-0.5, 1, self: true, known: true, 0, 0, 0.5, onlookers: true, O).Route);

        var lover = FeelFor(-0.5, 0.6);
        Assert.Equal(-0.15, lover.F0, 12);
        Assert.Equal("Sympathy", lover.Route);

        var hater = FeelFor(-0.5, -0.6);
        Assert.Equal(0.075, hater.F0, 12);                      // their sadness is joy to a hater (capped at 0.3)
        Assert.Equal("Antipathy", hater.Route);

        var neutral = FeelFor(-0.5, 0, likeness: 0, understanding: 0.5);
        Assert.Equal(-0.0375, neutral.F0, 12);
        Assert.Equal("Imitation", neutral.Route);
        Assert.Equal(-0.075, FeelFor(-0.5, 0, likeness: 1, understanding: 0.5).F0, 12);

        var envy = FeelFor(0.2, -0.6);
        Assert.Equal(-0.03, envy.F0, 12);                       // their joy is sadness to a hater
        Assert.Equal("Antipathy", envy.Route);

        // Someone unknown is felt by imitation only, whatever the regard passed.
        var unknown = Feelings.Base(-0.5, 1, self: false, known: false, 0.6, 0, 0.5, onlookers: false, O);
        Assert.Equal(-0.0375, unknown.F0, 12);
        Assert.Equal("Imitation", unknown.Route);

        // The ablations: without sympathy the lover imitates; without imitation the neutral feel nothing.
        var noSympathy = FeelFor(-0.5, 0.6, o: new FeelingOptions { Sympathy = false });
        Assert.Equal(-0.0375, noSympathy.F0, 12);
        Assert.Equal("Imitation", noSympathy.Route);
        Assert.Equal(0, FeelFor(-0.5, 0, o: new FeelingOptions { Imitation = false }).F0);
        Assert.Equal(-0.15, FeelFor(-0.5, 0.6, o: new FeelingOptions { Imitation = false }).F0, 12);
    }

    [Fact]
    public void ImitationGrowsWithLikeness_AndShrinksWithUnderstanding()
    {
        Villager ann = V("Ann", "Inn", "villager", 30, "Shop");
        Assert.Equal(1, Feelings.Likeness(ann, V("Bea", "Inn", "villager", 40, "Shop")), 12);
        Assert.Equal(0, Feelings.Likeness(ann, V("Old", "Farm", "old woman", 70, null)), 12);
        Assert.Equal(0.25, Feelings.Likeness(ann, V("Cal", "Inn", "old woman", 70, "Mill")), 12);   // household only
        Assert.Equal(0.5, Feelings.Likeness(ann, V("Dee", "Farm", "villager", 50, null)), 12);      // stage and kind
        Assert.Equal(0.75, Feelings.Likeness(V("Eve", "Farm", "villager", 30, null), V("Fay", "Farm", "villager", 30, null)), 12); // no jobs

        double unlike = Math.Abs(FeelFor(-0.5, 0, likeness: 0).F0);
        double alike = Math.Abs(FeelFor(-0.5, 0, likeness: 1).F0);
        Assert.True(alike > unlike, $"alike {alike} unlike {unlike}");

        double understanding = Math.Abs(FeelFor(-0.5, 0, likeness: 0.5, understanding: 0.9).F0);
        double little = Math.Abs(FeelFor(-0.5, 0, likeness: 0.5, understanding: 0.1).F0);
        Assert.True(understanding < little, $"U 0.9 {understanding} U 0.1 {little}");
    }

    [Fact]
    public void BlameFollowsFreedom_AndAKnownHardshipExcuses()
    {
        Assert.Equal(0, Feelings.Phi(0, 0, O));                 // an accident is nobody's fault
        Assert.Equal(1, Feelings.Phi(1, 0, O), 12);
        Assert.Equal(0.4, Feelings.Phi(0.4, 0, O), 12);         // an official doing their job

        Assert.Equal(1, Feelings.Excuse(housemates: true, need: 1, understanding: 1), 12);
        Assert.Equal(0.5, Feelings.Excuse(housemates: true, need: 1, understanding: 0), 12);
        Assert.Equal(0, Feelings.Excuse(housemates: true, need: 0, understanding: 1), 12);
        Assert.Equal(0, Feelings.Excuse(housemates: false, need: 1, understanding: 1), 12); // only the purse's sharers know

        Assert.Equal(0, Feelings.Phi(1, Feelings.Excuse(true, 1, 1), O), 12);
        Assert.Equal(0.5, Feelings.Phi(1, 0.5, O), 12);

        var noFreedom = new FeelingOptions { Freedom = false };
        Assert.Equal(1, Feelings.Phi(0.4, 0.5, noFreedom));     // law 10 off: everyone is blamed in full
    }

    [Fact]
    public void SevereFeelingsIgnoreRetention()
    {
        Assert.Equal(1, Feelings.Keep(-0.8, 0.2, O), 12);
        Assert.Equal(1, Feelings.Keep(-0.7, 0.2, O), 12);       // at SevereAt
        Assert.Equal(1, Feelings.Keep(0.8, 0.8, O), 12);
        Assert.Equal(0.7, Feelings.Keep(-0.3, 0.2, O), 12);     // Pam lets go
        Assert.Equal(1.3, Feelings.Keep(-0.3, 0.8, O), 12);     // Robin keeps
        Assert.Equal(1, Feelings.Keep(0.1, 0.5, O), 12);
    }

    [Fact]
    public void CredenceEqualsTodaysTrustAtNeutralRegard()
    {
        Assert.Equal(0.625, Feelings.Credence(0.25, 0, 0.6, O), 12);
        Assert.Equal(0.705, Feelings.Credence(0.25, 0.8, 0.6, O), 12);
        Assert.Equal(0.545, Feelings.Credence(0.25, -0.8, 0.6, O), 12);

        foreach (double fam in Grid(0, 1, 20))
            foreach (double u in Grid(0, 1, 10))
                Assert.Equal(0.5 + 0.5 * fam, Feelings.Credence(fam, 0, u, O), 12);

        var strong = new FeelingOptions { CredencePerRegard = 1 };
        Assert.Equal(0.25, Feelings.Credence(0, -1, 0, strong), 12);   // the floor
        Assert.Equal(1, Feelings.Credence(1, 1, 0, strong), 12);       // the ceiling
        Assert.Equal(0.25, Feelings.Credence(0, -1, 0, O), 12);
    }

    /// <summary>III P44: the bonus is the depth of the old hate, bounded by the love given since,
    /// and only after a real hate (a trough at or below -0.2).</summary>
    [Fact]
    public void HateConqueredByLove_BonusIsBoundedByTheLoveGiven()
    {
        Assert.Equal(0.3, Feelings.ReconcileBonus(-0.3, 0.315, O), 12);
        Assert.Equal(0.1, Feelings.ReconcileBonus(-0.3, 0.1, O), 12);
        Assert.Equal(0.2, Feelings.ReconcileBonus(-0.2, 0.5, O), 12);  // at ReconcileFrom
        foreach (double q in new[] { 0, 0.05, 0.315, 1 })
            Assert.Equal(0, Feelings.ReconcileBonus(-0.1, q, O));
        Assert.Equal(0, Feelings.ReconcileBonus(-0.3, 0, O));          // no love given, no bonus
        Assert.Equal(0.15, Feelings.ReconcileBonus(-0.3, 0.315, new FeelingOptions { ReconcileShare = 0.5 }), 12);
    }

    [Fact]
    public void MoodFadesLinearlyOverThreeDays_AndPowerStaysInRange()
    {
        int day = Clock.MinutesPerDay;
        Assert.Equal(1, Feelings.MoodWeight(0, O), 12);
        Assert.Equal(2.0 / 3, Feelings.MoodWeight(day, O), 12);
        Assert.Equal(1.0 / 3, Feelings.MoodWeight(2 * day, O), 12);
        Assert.Equal(0, Feelings.MoodWeight(3 * day, O), 12);
        Assert.Equal(0, Feelings.MoodWeight(10 * day, O));
        Assert.Equal(0.5, Feelings.MoodWeight(3 * day / 2, O), 12);

        // One entry of -0.6: -0.6, -0.4, -0.2, then nothing.
        double[] weighs = new[] { 0, 1, 2, 3 }.Select(d => Math.Round(-0.6 * Feelings.MoodWeight(d * day, O), 12)).ToArray();
        Assert.Equal(new[] { -0.6, -0.4, -0.2, 0 }, weighs);
        Assert.Equal(-0.375, Feelings.Squash(-0.6), 12);
        Assert.Equal(0.35, Feelings.PowerOf(0, Feelings.Squash(-0.6), O), 12);

        Assert.Equal(0.5, Feelings.PowerOf(0, 0, O), 12);
        Assert.Equal(0.35, Feelings.PowerOf(-O.NeedPower, 0, O), 12);   // a short purse
        foreach (double x in Grid(-100, 100, 400))
            Assert.InRange(Feelings.Squash(x), -1, 1);
        foreach (double c in Grid(-1, 0, 40))
            foreach (double mood in Grid(-1, 1, 40))
                Assert.InRange(Feelings.PowerOf(c, mood, O), 0, 1);
        Assert.Equal(1, Feelings.PowerOf(0, 1, new FeelingOptions { PowerScale = 2 }));
        Assert.Equal(0, Feelings.PowerOf(-0.3, -1, O));
    }

    /// <summary>F13: an innocent resents whoever named them, split over the namers; the guilty feel
    /// it more, by low self-regard, and resent nobody.</summary>
    [Fact]
    public void AccusedSplitsOverNamers_AndTheGuiltyFeelShameInstead()
    {
        var (mood, per) = Feelings.AccusedSplit(-0.3, 1, 0.6, guilty: false, namers: 2, retention: 0.5, O);
        Assert.Equal(-0.3, mood, 12);
        Assert.Equal(-0.075, per, 12);
        Assert.Equal(-0.15, Feelings.AccusedSplit(-0.3, 1, 0.6, guilty: false, namers: 1, retention: 0.5, O).PerNamer, 12);
        Assert.Equal("Wronged", Feelings.SentimentName("Accused", per));
        Assert.Equal("Wronged", Feelings.SentimentName("Confronted", per));

        var guilty = Feelings.AccusedSplit(-0.3, 1, 0.6, guilty: true, namers: 2, retention: 0.5, O);
        Assert.Equal(-0.3 * (1.5 - 0.6), guilty.Mood, 12);
        Assert.Equal(0, guilty.PerNamer);
        Assert.Equal(-0.3 * (1.5 - 0.2), Feelings.AccusedSplit(-0.3, 1, 0.2, guilty: true, namers: 2, retention: 0.5, O).Mood, 12);
        Assert.Equal("Ashamed", Feelings.SentimentName("Shame", guilty.Mood));

        Assert.Equal(0, Feelings.AccusedSplit(-0.3, 1, 0.6, guilty: false, namers: 0, retention: 0.5, O).PerNamer);
    }

    /// <summary>S4 for the mayor (Lewis, understanding 0.6): close is a housemate, someone he knows
    /// well and doesn't dislike, or someone he loves; his trust leans at most 0.1 either way.</summary>
    [Fact]
    public void TheMayorTrustsTellersHeLikes_AndIsSwayedOnlyByTheClose()
    {
        double at = new AuthorityOptions().SwayCloseAt;
        Assert.True(Feelings.Close(household: true, 0, -1, at, O));
        Assert.True(Feelings.Close(household: true, 0.6, -0.5, at, O));
        Assert.False(Feelings.Close(household: false, 0.6, -0.5, at, O));
        Assert.True(Feelings.Close(household: false, 0.25, 0.5, at, O));
        Assert.True(Feelings.Close(household: false, 0.6, 0, at, O));
        Assert.False(Feelings.Close(household: false, 0.25, 0, at, O));
        foreach (double fam in Grid(0, 1, 20))
            Assert.Equal(fam >= at, Feelings.Close(household: false, fam, 0, at, O));   // as before 0c

        const double lewis = 0.6;
        Assert.Equal(0.725, Feelings.Credence(0.25, 1, lewis, O), 12);
        Assert.Equal(0.525, Feelings.Credence(0.25, -1, lewis, O), 12);
        foreach (double fam in Grid(0, 1, 20))
            foreach (double regard in Grid(-1, 1, 20))
                Assert.True(Math.Abs(Feelings.Credence(fam, regard, lewis, O) - Feelings.Credence(fam, 0, lewis, O)) <= 0.1 + 1e-12);
    }

    /// <summary>S5: love covers and hate reports.</summary>
    [Fact]
    public void LoveCoversAndHateReports()
    {
        var a = new AuthorityOptions();
        double today = a.ReportBase + a.ReportPerBoldness * 0.5;
        Assert.Equal(today, Feelings.ReportChance(a.ReportBase, a.ReportPerBoldness, 0.5, 0, O), 12);
        Assert.Equal(0.4 * today, Feelings.ReportChance(a.ReportBase, a.ReportPerBoldness, 0.5, 0.6, O), 12);
        Assert.Equal(today + 0.15, Feelings.ReportChance(a.ReportBase, a.ReportPerBoldness, 0.5, -0.5, O), 12);
        Assert.Equal(0, Feelings.ReportChance(a.ReportBase, a.ReportPerBoldness, 0.5, 1, O), 12);
    }
}
