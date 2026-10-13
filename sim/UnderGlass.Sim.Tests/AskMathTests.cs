using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>What moves an answer to an invitation besides regard (invitation spec, Sid 2026-10-13):
/// being tired, due at work or low makes a no likelier; eagerness to please, in proportion to the bond
/// with the one asking, takes that back, so Penny does things for her mother when she is low.</summary>
public class AskMathTests
{
    private static Temperament Of(string name) => DefaultTown.Cast().Single(v => v.Name == name).Temperament;

    [Fact]
    public void EagernessComesFromLowSelfRegardAndLowBoldness()
    {
        Assert.Equal(0.75, AskMath.Eagerness(Of("Penny")), 6);
        Assert.Equal(0.5, AskMath.Eagerness(Of("Pam")), 6);
        Assert.True(AskMath.Eagerness(Of("Penny")) > AskMath.Eagerness(Of("Abigail")));
        Assert.Equal(1, AskMath.Eagerness(new Temperament(0.5, 0, 0.5, 0)));
        Assert.Equal(0, AskMath.Eagerness(new Temperament(0.5, 1, 0.5, 1)));
    }

    [Fact]
    public void TheBondIsWholeForFamilyAndOtherwiseLikingAndKnowing()
    {
        Assert.Equal(1, AskMath.Bond(family: true, liked: 0, familiarity: 0));
        Assert.Equal(0.5, AskMath.Bond(family: false, liked: 0.4, familiarity: 0.6), 6);
        Assert.Equal(1, AskMath.Bond(family: false, liked: 1, familiarity: 1));
        Assert.Equal(0, AskMath.Bond(family: false, liked: 0, familiarity: 0));
    }

    [Fact]
    public void TiredBusyOrLowRaiseTheCostAndEagernessTimesTheBondTakesItBack()
    {
        var o = new ActOptions();
        var rested = new AskState(0, 0, 0);
        var worn = new AskState(Tired: 0.9, Busy: 1, Low: 0.6);
        Assert.Equal(0, AskMath.StateCost(rested, 0, 0, o));
        Assert.Equal(0.3 * 0.9 + 0.3 + 0.3 * 0.6, AskMath.StateCost(worn, 0, 0, o), 6);
        Assert.True(AskMath.StateCost(worn with { Low = 0.9 }, 0.5, 0.5, o) > AskMath.StateCost(worn, 0.5, 0.5, o));

        // Penny, worn out and low: her mother asking costs her a quarter of what a stranger asking does.
        double penny = AskMath.Eagerness(Of("Penny"));
        double forMother = AskMath.StateCost(worn, penny, AskMath.Bond(true, 0.5, 0.8), o);
        double forStranger = AskMath.StateCost(worn, penny, AskMath.Bond(false, 0, 0.1), o);
        Assert.Equal(0.25 * AskMath.StateCost(worn, 0, 0, o), forMother, 6);
        Assert.True(forStranger > 3 * forMother);
        // Someone who isn't eager to please finds family no easier on a bad day.
        Assert.Equal(AskMath.StateCost(worn, 0, 0, o), AskMath.StateCost(worn, 0, 1, o), 6);
        // With the weights at zero the state counts for nothing, as before.
        var off = new ActOptions { InviteTiredCost = 0, InviteBusyCost = 0, InviteLowCost = 0 };
        Assert.Equal(0, AskMath.StateCost(worn, 0, 0, off));
    }
}
