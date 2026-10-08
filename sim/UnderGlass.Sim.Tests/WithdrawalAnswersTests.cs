using UnderGlass.Sim;
using Xunit;
using static UnderGlass.Sim.Tests.WithdrawalSceneTests;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Sid's answers of 2026-10-08 to 0d.6's questions (design section 12, question 9), in small scenes
/// with WithdrawalSceneTests' conventions: A4, a mishap weighs on the shy (step i); A9, a child is
/// never a hermit; A10, a combative stance takes a stranger's kindness only in part (step j).
/// </summary>
public class WithdrawalAnswersTests
{
    private const int D = Clock.MinutesPerDay;

    private static readonly Affect StumbleRow = new(Patient.Actor, -0.2, 0, 0);

    /// <summary>Ann stumbles once, at 10:00 on day 0, with Bob beside her.</summary>
    private static SimResult Stumbles(FeelingOptions o, double annBold)
    {
        var kinds = new List<ActKind> { new("Stumbled", 1.0, 0, 1, 1, 0, Array.Empty<string>(), Affect: StumbleRow) };
        return new Simulation(1, new[] { V("Ann", "A", 3, 2, bold: annBold), V("Bob", "B", 6, 2) }, new[] { Room() }, kinds,
            feelings: o, body: Awake(), scheduled: new[] { (Clock.At(10), "Ann", "Stumbled") }, wander: 0).Run(2);
    }

    /// <summary>A4: with step i, a stumble moves a shy person's stance toward withdrawn, as a hurt
    /// undergone does; a bold person's not at all; and with i off, nobody's, as before.</summary>
    [Fact]
    public void AMishapWeighsOnTheShyAlone()
    {
        SimResult shy = Stumbles(Steps("i"), annBold: 0.1);
        Assert.Single(shy.Acts, a => a is { Actor: "Ann", Kind: "Stumbled" });
        Assert.True(shy.Stances["Ann"][0] < 0, $"stance {shy.Stances["Ann"][0]:0.000}");
        Assert.Equal(1, shy.Rules["mishap"].Count);

        Assert.All(Stumbles(Steps(""), annBold: 0.1).Stances["Ann"], s => Assert.Equal(0, s));
        SimResult bold = Stumbles(Steps("i"), annBold: 0.9);
        Assert.All(bold.Stances["Ann"], s => Assert.Equal(0, s));
        Assert.False(bold.Rules.ContainsKey("mishap"));
    }

    /// <summary>A9: a run records each person's stage of life, and a child's long withdrawn spell
    /// with the hours out fallen is withdrawn, never a hermit's.</summary>
    [Fact]
    public void AChildIsNeverAHermit()
    {
        SimResult r = new Simulation(1).Run(1);
        Assert.Equal(Stage.Child, r.Stages["Jas"]);
        Assert.Equal(Stage.Adult, r.Stages["Penny"]);

        double[] stance = Enumerable.Range(0, 112).Select(d => d >= 30 ? -0.6 : 0.0).ToArray();
        int[] minutes = Enumerable.Range(0, 112).Select(d => d >= 30 ? 30 : 300).ToArray();
        var days = new PersonDays(new int[112], new bool[112], new int[112], new int[112], minutes, new double[112]);
        Assert.True(Assert.Single(WithdrawalMetrics.SpellsOf("Kid", stance, days, brawlers: false)).Hermit);
        Assert.False(Assert.Single(WithdrawalMetrics.SpellsOf("Kid", stance, days, brawlers: false, hermitAge: false)).Hermit);
    }

    /// <summary>A8: a hermit is back when their stance rises above -0.3 within a season after the
    /// spell counts (56 days from its first day); a spell the run ends inside is not counted.</summary>
    [Fact]
    public void AHermitIsBackWithinASeasonAfterTheSpellCounts()
    {
        double[] Stance(int from, int backOn) => Enumerable.Range(0, 112).Select(d => d >= from && d < backOn ? -0.6 : -0.2).ToArray();
        Spell Spell(int from, int to) => new("Kid", from, to, true, 0.9, true);
        Assert.True(WithdrawalMetrics.BackInASeason(Spell(10, 59), Stance(10, 60), 112));
        Assert.True(WithdrawalMetrics.BackInASeason(Spell(10, 64), Stance(10, 65), 112)); // day 65, the last of the 56
        Assert.False(WithdrawalMetrics.BackInASeason(Spell(10, 65), Stance(10, 66), 112));
        Assert.Null(WithdrawalMetrics.BackInASeason(Spell(70, 111), Stance(70, 112), 112));
    }

    /// <summary>Bo, her housemate, argues at Ann (bold) on days 0, 3, 6 and 9, so she grows
    /// combative (step b). On day 11, while Bo is at the room's far end, Gus, from another household,
    /// comes over and gives her a gift.</summary>
    private static SimResult Combative(FeelingOptions o, bool gift, double annForGus = 0)
    {
        Villager Mover(string name, string household, Dictionary<string, double> acts, params Haunt[] haunts)
            => new(name, household, "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null, haunts, acts, Array.Empty<string>());
        int away = Clock.At(10), back = Clock.At(11);
        var bo = Mover("Bo", "H", Does("Argued"), At(5, 2, 0, away), At(38, 4, away, back), At(5, 2, back, D));
        var gus = Mover("Gus", "G", new Dictionary<string, double>(), At(30, 2, 0, away), At(4, 3, away, back), At(30, 2, back, D));
        o.Start = new Dictionary<(string, string), double> { [("Ann", "Bo")] = 0.6, [("Ann", "Gus")] = annForGus };
        var scheduled = new[] { 0, 3, 6, 9 }.Select(d => (d * D + Clock.At(9), "Bo", "Argued")).ToList();
        if (gift)
            scheduled.Add((11 * D + Clock.At(10, 40), "Gus", "GaveGift"));
        return new Simulation(1, new[] { V("Ann", "H", 3, 2, bold: 0.9), bo, gus }, new[] { Room() }, Kinds(), feelings: o, body: Awake(),
            scheduled: scheduled, wander: 0).Run(12);
    }

    /// <summary>A10: with step j, a stranger's gift eases a combative stance by CombativeShare (0.3)
    /// of what it eases with j off; a friend's eases it in full. Before the gift, j changes nothing.</summary>
    [Fact]
    public void ACombativeStanceTakesAStrangersKindnessOnlyInPart()
    {
        SimResult none = Combative(Steps("b"), gift: false);
        SimResult full = Combative(Steps("b"), gift: true);
        SimResult part = Combative(Steps("bj"), gift: true);
        SimResult friend = Combative(Steps("bj"), gift: true, annForGus: 0.6);

        Assert.Single(full.Acts, a => a is { Actor: "Gus", Kind: "GaveGift", Target: "Ann" });
        Assert.True(none.Stances["Ann"][10] > 0.3, $"combative by day 10: {none.Stances["Ann"][10]:0.000}");
        Assert.Equal(none.Stances["Ann"].Take(11), part.Stances["Ann"].Take(11));
        double easedFull = none.Stances["Ann"][11] - full.Stances["Ann"][11];
        double easedPart = none.Stances["Ann"][11] - part.Stances["Ann"][11];
        Assert.True(easedFull > 0, $"eased {easedFull:0.000}");
        Assert.InRange(easedPart / easedFull, 0.25, 0.35);
        Assert.Equal(1, part.Rules["kindness discounted"].Count);
        Assert.False(friend.Rules.ContainsKey("kindness discounted"));
    }
}
