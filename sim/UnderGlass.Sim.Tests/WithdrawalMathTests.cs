using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Phase 0d.6's pure arithmetic (spec section 8): being left out against the town,
/// content loners, W1's push and W2's repetition, contagion's pull, its tie and its asymmetry at
/// home, patience, expression's split, fading by retention, proneness and the wish to give,
/// adaptation, and the calendar. Pure, like DesireMathTests.</summary>
public class WithdrawalMathTests
{
    private static readonly FeelingOptions O = new();

    /// <summary>X0: each part is relative to the town's median and clamped at -1; with a median of
    /// 0 a part counts nothing; the share of one's kindness unanswered counts above the town's
    /// median share; the whole is clamped to [0, 1].</summary>
    [Fact]
    public void LeftOutIsRelativeToTheTown()
    {
        Assert.Equal(1, WithdrawalMath.Below(0, 4));
        Assert.Equal(0, WithdrawalMath.Below(4, 4));
        Assert.Equal(-1, WithdrawalMath.Below(12, 4));
        Assert.Equal(0, WithdrawalMath.Below(0, 0));
        Assert.Equal(1, WithdrawalMath.Above(1, 0.4));
        Assert.Equal(0, WithdrawalMath.Above(0.4, 0.4));
        Assert.Equal(0.5, WithdrawalMath.Above(0.7, 0.4), 12);
        Assert.Equal(-2 / 3.0, WithdrawalMath.Above(0, 0.4), 12);
        Assert.Equal(-1, WithdrawalMath.Above(0, 0.6)); // clamped
        Assert.Equal(0, WithdrawalMath.Above(double.NaN, 0.4)); // gave nothing
        Assert.Equal(0, WithdrawalMath.Above(1, 1));

        // Nothing received and no company where the town has both: 0.4 + 0.3.
        Assert.Equal(0.7, WithdrawalMath.LeftOut(0, 3, 0, 10, double.NaN, 0.4, 0, O), 12);
        // ... and every kindness of one's own unanswered, where the town's usual share is 40%: everything.
        Assert.Equal(1.0, WithdrawalMath.LeftOut(0, 3, 0, 10, 1, 0.4, 0, O), 12);
        // At the median: nothing. Unanswered as often as everyone is: nothing.
        Assert.Equal(0, WithdrawalMath.LeftOut(3, 3, 10, 10, 0.4, 0.4, 0, O));
        // Plenty of kindness offsets missing company, but never below 0.
        Assert.Equal(0, WithdrawalMath.LeftOut(9, 3, 0, 10, double.NaN, 0.4, 0, O));
        // A quiet town (every median 0) leaves nobody out.
        Assert.Equal(0, WithdrawalMath.LeftOut(0, 0, 0, 0, double.NaN, double.NaN, 0, O));
    }

    /// <summary>Sid's answer 3: the quiet who are not shy (content loners) are not harmed by time
    /// alone; the shy who are alone are.</summary>
    [Fact]
    public void AContentLonerCountsCompanyLess()
    {
        Assert.Equal(2 / 3.0, WithdrawalMath.Solitary(0.3, 0.6, O), 12);   // Shane
        Assert.Equal(0, WithdrawalMath.Solitary(0.4, 0.2, O));              // Penny: shy
        Assert.Equal(0, WithdrawalMath.Solitary(0.8, 0.7, O));              // Pam: talkative
        double loner = WithdrawalMath.LeftOut(3, 3, 0, 10, double.NaN, 0.4, WithdrawalMath.Solitary(0.2, 0.7, O), O);
        double shy = WithdrawalMath.LeftOut(3, 3, 0, 10, double.NaN, 0.4, WithdrawalMath.Solitary(0.2, 0.1, O), O);
        Assert.Equal(0, loner);
        Assert.Equal(0.3, shy, 12);
    }

    /// <summary>W1 (X4): shy squared is the interaction, so the bold barely move; sensitivity and
    /// low self-regard deepen it; the buffer cuts it.</summary>
    [Fact]
    public void BeingLeftOutWeighsOnTheShy()
    {
        double Push(double bold, double sens = 1, double self = 0.5, double buffer = 1) => WithdrawalMath.LeftOutPush(0.7, bold, sens, self, buffer, O);
        Assert.Equal(0.01 * 0.7 * 0.98 * 0.98, Push(0.02), 12);
        Assert.Equal(Push(0.02) * 0.04 / 0.9604, Push(0.8), 12);
        Assert.True(Push(0.8) < Push(0.02) / 20);
        Assert.True(Push(0.2, sens: 1.2) > Push(0.2, sens: 0.8));
        Assert.True(Push(0.2, self: 0.1) > Push(0.2, self: 0.9));
        Assert.Equal(Push(0.2) * 0.3, Push(0.2, buffer: 0.3), 12);
        // A content loner is not harmed by being alone.
        Assert.Equal(0, WithdrawalMath.LeftOutPush(0.7, 0.7, 1, 0.5, 1, O, solitary: 1));
        Assert.Equal(Push(0.7) * 0.5, WithdrawalMath.LeftOutPush(0.7, 0.7, 1, 0.5, 1, O, solitary: 0.5), 12);
        // Shy to a higher power narrows the push onto the shyest.
        var cubed = new FeelingOptions { ShyPower = 3 };
        Assert.Equal(Push(0.2) * 0.8, WithdrawalMath.LeftOutPush(0.7, 0.2, 1, 0.5, 1, cubed), 12);
    }

    /// <summary>X4's included side: only the shy who are not left out, only toward 0 and never
    /// past it, and faster for the sensitive (differential susceptibility: test 13's other half).</summary>
    [Fact]
    public void TheIncludedShyComeCloser()
    {
        Assert.Equal(0.01 * 1.2, WithdrawalMath.IncludedPull(-0.4, 0.1, 0.2, 1.2, O), 12);
        Assert.Equal(0.01 * 0.6, WithdrawalMath.IncludedPull(-0.4, 0.1, 0.2, 0.6, O), 12);
        Assert.Equal(0.005, WithdrawalMath.IncludedPull(-0.005, 0.1, 0.2, 1.2, O), 12); // never past 0
        Assert.Equal(0, WithdrawalMath.IncludedPull(-0.4, 0.3, 0.2, 1.2, O));           // left out
        Assert.Equal(0, WithdrawalMath.IncludedPull(-0.4, 0.1, 0.6, 1.2, O));           // not shy
        Assert.Equal(0, WithdrawalMath.IncludedPull(0.4, 0.1, 0.2, 1.2, O));            // combative: not this rule
    }

    /// <summary>W2 (X1): each earlier hurt in the window adds a quarter, up to double.</summary>
    [Fact]
    public void RepeatedHurtsCountMoreUpToDouble()
    {
        Assert.Equal(new[] { 1.0, 1.25, 1.5, 1.75, 2.0, 2.0, 2.0 }, Enumerable.Range(0, 7).Select(n => WithdrawalMath.Repeated(n, O)));
    }

    /// <summary>C1 (X3): toward the other, never past; nothing between equals; the tie; bad moods
    /// weigh double at home, softened by love; good ones do not.</summary>
    [Fact]
    public void ContagionPullsTowardTheOther()
    {
        Assert.Equal(0, WithdrawalMath.Contagion(0.2, 0.2, 1, true, 0, O));
        Assert.Equal(0.01 * 0.3 * 0.4, WithdrawalMath.Contagion(0.4, 0, 1, false, 0, O), 12);          // a stranger
        Assert.Equal(0.01 * 0.6 * 0.4, WithdrawalMath.Contagion(0.4, 0, 1, false, 0.5, O), 12);        // a friend
        Assert.Equal(0.01 * 1.0 * 0.4, WithdrawalMath.Contagion(0.4, 0, 1, true, 0.6, O), 12);         // at home, glad: symmetric
        Assert.Equal(-0.01 * 2 * 0.4, WithdrawalMath.Contagion(-0.4, 0, 1, true, 0, O), 12);           // at home, low, no love
        Assert.Equal(-0.01 * 1 * 0.4, WithdrawalMath.Contagion(-0.4, 0, 1, true, 1.0, O), 12);         // loved: halved
        Assert.Equal(-0.01 * 0.3 * 0.4, WithdrawalMath.Contagion(-0.4, 0, 1, false, 0, O), 12);        // elsewhere: symmetric
        // Never past the other: the entry is a share of the gap below 1.
        var hot = new FeelingOptions { ContagionK = 0.4 };
        Assert.True(Math.Abs(WithdrawalMath.Contagion(-0.3, 0.1, 1.2, true, 0, hot)) < 0.4);
    }

    /// <summary>X3's cap: a day's entries stay within the cap either way.</summary>
    [Fact]
    public void TheDailyCapCuts()
    {
        Assert.Equal(-0.01, WithdrawalMath.Capped(-0.03, -0.04, O), 12);
        Assert.Equal(0.02, WithdrawalMath.Capped(0.02, -0.04, O), 12);
        Assert.Equal(0, WithdrawalMath.Capped(0.02, 0.05, O), 12);
    }

    /// <summary>X8: patience is a couple of rounds, more for the understanding and fewer for the
    /// sensitive; it refills by a round in PatienceRefillDays; kind daring falls only once less
    /// than a round is left.</summary>
    [Fact]
    public void PatienceLastsACoupleOfRounds()
    {
        Assert.Equal(2.5, WithdrawalMath.Patience(0.5, 0.5, O), 12);
        Assert.InRange(WithdrawalMath.Patience(0.7, 0.67, O), 2.4, 2.6);   // Penny
        Assert.InRange(WithdrawalMath.Patience(0.3, 0.56, O), 1.8, 2.0);   // Pam
        Assert.True(WithdrawalMath.Patience(0.9, 0.5, O) > WithdrawalMath.Patience(0.1, 0.5, O));
        Assert.True(WithdrawalMath.Patience(0.5, 0.9, O) < WithdrawalMath.Patience(0.5, 0.1, O));
        Assert.Equal(1.5, WithdrawalMath.Refilled(2, 5, O), 12);
        Assert.Equal(0, WithdrawalMath.Refilled(1, 30, O));
        Assert.Equal(0, WithdrawalMath.Impatience(1.5, O));
        Assert.Equal(0.125, WithdrawalMath.Impatience(0.5, O), 12);
        Assert.Equal(0.25, WithdrawalMath.Impatience(-1, O), 12);
    }

    /// <summary>X10: what shows falls a little when low; the shown part of a hurt moves stance by
    /// boldness and the held part pulls toward withdrawn whatever the boldness; showing everything
    /// is the old rule exactly.</summary>
    [Fact]
    public void AHurtSplitsIntoShownAndHeld()
    {
        Assert.Equal(0.8, WithdrawalMath.Show(0.8, 0.3, O), 12);
        Assert.Equal(0.8 * 0.9, WithdrawalMath.Show(0.8, -0.5, O), 12);
        Assert.Equal(O.ShowMin, WithdrawalMath.Show(0, 0, O));
        var typical = new FeelingOptions { ShowReference = 0.75 };
        Assert.Equal(1, WithdrawalMath.Show(0.95, 0.3, typical));         // above the reference: in full
        Assert.Equal(1, WithdrawalMath.Show(0.75, 0.3, typical), 12);
        Assert.Equal(1 / 3.0, WithdrawalMath.Show(0.25, 0.3, typical), 12); // Penny
        foreach (double bold in new[] { 0.02, 0.5, 0.98 })
            Assert.Equal(DesireMath.StanceAfterHurt(0.1, 0.3, bold), WithdrawalMath.StanceAfterHeldHurt(0.1, 0.3, bold, 1, O), 12);
        // Held entirely: withdrawn, even for the bold.
        Assert.Equal(-0.09, WithdrawalMath.StanceAfterHeldHurt(0, 0.3, 0.98, 0, O), 12);
        // A bold masker (Pam's boldness, Penny's mask) is pulled toward withdrawal.
        Assert.True(WithdrawalMath.StanceAfterHeldHurt(0, 0.3, 0.7, 0.25, O) < 0);
        Assert.True(WithdrawalMath.StanceAfterHeldHurt(0, 0.3, 0.7, 0.85, O) > 0);
    }

    /// <summary>X7: retention sets how long a stance lasts: 0.5 keeps the old 0.975.</summary>
    [Fact]
    public void RetentionSetsHowLongAStanceLasts()
    {
        Assert.Equal(0.975, WithdrawalMath.StanceKeep(0.5, O), 12);
        Assert.Equal(0.9685, WithdrawalMath.StanceKeep(0, O), 12);
        Assert.Equal(0.9815, WithdrawalMath.StanceKeep(1, O), 12);
    }

    /// <summary>X12: the prone miss sooner; a steady tie misses less; an occasion gives on its
    /// own; nobody misses someone they do not love.</summary>
    [Fact]
    public void MissingPeople()
    {
        Assert.Equal(1, WithdrawalMath.Prone(new Temperament(0.5, 0.5, 0.5, 0.5), O), 12);
        Assert.Equal(1.5, WithdrawalMath.Prone(new Temperament(1, 0.5, 0.5, 0, 1, 1), O), 12);
        Assert.Equal(0.5, WithdrawalMath.Prone(new Temperament(0, 0.5, 0.5, 1, 0, 0), O), 12);

        Assert.Equal(0.4 * 0.5, WithdrawalMath.Wish(0.6, 5, 1, 0, false, O), 12);
        Assert.Equal(0.4 * 1.0, WithdrawalMath.Wish(0.6, 5, 2, 0, false, O), 12);     // the prone miss sooner
        Assert.Equal(0.4 * 1.0 * 0.2, WithdrawalMath.Wish(0.6, 20, 1, 1, false, O), 12); // steady: missing goes down
        Assert.Equal(0.4 * 0.5, WithdrawalMath.Wish(0.6, 0, 1, 1, true, O), 12);       // an occasion, in a steady tie
        Assert.Equal(0, WithdrawalMath.Wish(0.6, 0, 1, 0, true, O));                    // ... and not otherwise
        Assert.Equal(0.4 * 0.5 * 0.5, WithdrawalMath.OccasionPart(0.6, 0.5, O), 12);
        Assert.Equal(0, WithdrawalMath.Wish(0.2, 30, 1.5, 0, true, O));                 // not loved
        Assert.Equal(1, WithdrawalMath.Steady(9, O));
        Assert.Equal(0.5, WithdrawalMath.Steady(4, O), 12);
    }

    /// <summary>X12: each like kindness from the same person lately counts AdaptFactor less.</summary>
    [Fact]
    public void RepeatedGiftsCountForLess()
    {
        Assert.Equal(1, WithdrawalMath.Adapted(0, O));
        Assert.Equal(0.85, WithdrawalMath.Adapted(1, O), 12);
        Assert.Equal(0.85 * 0.85 * 0.85 * 0.85, WithdrawalMath.Adapted(4, O), 12);
    }

    [Fact]
    public void TheMedian()
    {
        Assert.Equal(0, WithdrawalMath.Median(Array.Empty<double>()));
        Assert.Equal(2, WithdrawalMath.Median(new double[] { 1, 2, 9 }));
        Assert.Equal(2.5, WithdrawalMath.Median(new double[] { 1, 2, 3, 9 }));
    }

    /// <summary>Day 0 is spring 1, a Monday; the festivals and birthdays fall on the game's days
    /// (Data/Festivals/FestivalDates and Data/Characters, 1.6.15).</summary>
    [Fact]
    public void TheCalendar()
    {
        Assert.Equal(new YearDay(0, 1), Calendar.DateOf(0));
        Assert.Equal(0, Clock.Weekday(0));
        Assert.Equal(new YearDay(0, 28), Calendar.DateOf(27));
        Assert.Equal(new YearDay(1, 1), Calendar.DateOf(28));
        Assert.Equal(new YearDay(3, 28), Calendar.DateOf(111));
        Assert.Equal(new YearDay(0, 1), Calendar.DateOf(112));
        Assert.Equal("Egg Festival", Calendar.FestivalOn(12));
        Assert.Equal("Feast of the Winter Star", Calendar.FestivalOn(3 * 28 + 24));
        Assert.Null(Calendar.FestivalOn(13));
        Assert.Equal(8, Enumerable.Range(0, 112).Count(d => Calendar.FestivalOn(d) is not null));

        var cast = DefaultTown.Cast().ToDictionary(v => v.Name);
        Assert.Equal(new YearDay(2, 2), cast["Penny"].Birthday);
        Assert.Equal(new YearDay(0, 18), cast["Pam"].Birthday);
        Assert.Equal(new YearDay(3, 10), cast["Sebastian"].Birthday);
        Assert.Null(cast[DefaultTown.Newcomer].Birthday);
        Assert.Equal(25, cast.Values.Count(v => v.Birthday is not null));
        Assert.True(Calendar.IsBirthday(cast["Penny"].Birthday, 2 * 28 + 1));
        Assert.False(Calendar.IsBirthday(cast["Penny"].Birthday, 2 * 28));
    }

    /// <summary>The town ships with every 0d.6 switch off, and with the constants the sweeps tuned
    /// (sim/README.md), so a step switched on runs as it was measured.</summary>
    [Fact]
    public void TheTownShipsWithEverySwitchOff()
    {
        FeelingOptions town = DefaultTown.Feelings();
        Assert.False(town.WithdrawalWatch || town.HomeHurtOn || town.HouseholdGateOn || town.ContagionOn || town.LeftOutOn
                     || town.InclusionDiscountOn || town.DialsOn || town.RecoveryOn || town.PatienceOn || town.CoercionOn
                     || town.ShowOn || town.MissingOn || town.ToneOn);
        Assert.Equal((0.8, 4, 6.0), (town.LeftOutRate, town.ShyPower, town.StanceHomeDial));
        Assert.Equal((0.01, 2, 3.0), (O.LeftOutRate, O.ShyPower, O.StanceHomeDial)); // the research's first guesses
    }

    /// <summary>The seventh trait reads and sets like the other six, and the cast carries Sid's
    /// reading: Penny masks, Pam lets it out.</summary>
    [Fact]
    public void ExpressionIsTheSeventhTrait()
    {
        var sim = new Simulation(1);
        Assert.Equal(0.25, sim.TraitOf("Penny", Trait.Expression));
        Assert.Equal(0.85, sim.TraitOf("Pam", Trait.Expression));
        sim.SetTrait("Penny", Trait.Expression, 0.6);
        Assert.Equal(0.6, sim.TraitOf("Penny", Trait.Expression));
        Assert.Equal(0.2, sim.TraitOf("Penny", Trait.Boldness));
        Assert.Equal(0.75, new Temperament(0.5, 0.5, 0.5, 0.5).Expression);
    }
}
