using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Batch 2's seams, part one (acts-batch2 spec 2.1-2.4, 2.10; slice b2-0): the records, the switches
/// and the five edits that keep the shipped town as it is. No batch 2 row exists yet, so every
/// switch on changes nothing; a scandal that isn't placeable is never placed; a crime is a scandal
/// unless its row says otherwise; a dated gathering is on only on its date.
/// </summary>
public class Batch2SeamsTests
{
    private static readonly string[] Batch2 =
    {
        "Visits", "Dates", "Peace", "Festivals", "PublicConfront", "ReadVerdicts", "Settled", "Amends", "MadeScenes",
        "Bans", "Counter", "Hosting", "Debts", "Loans", "Items", "Blunders", "Contests", "Partiality", "Board",
    };

    [Fact]
    public void EveryBatch2SwitchOnChangesNothingYet()
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.Acts.With(string.Join(",", Batch2));
        Assert.All(Batch2, n => Assert.True(o.Acts.IsOn(n), n));
        Assert.Equal(DefaultTown.Acts().Count, ActCatalog.Kinds(o.Acts).Count); // no rows yet
        Assert.Equal("e9fd83b284f5c1b6", Metrics.LogHash(new Simulation(1, kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(112)));
    }

    [Fact]
    public void TheSwitchesAreNamedForTheRunner()
    {
        Assert.All(Batch2, n => Assert.Contains(n, ActOptions.Switches));
        Assert.True(new ActOptions().With("visits,BOARD").Board);
        Assert.False(new ActOptions().IsOn("Watch")); // watch isn't a slice
    }

    [Fact]
    public void AScandalThatIsntPlaceableIsNeverPlaced()
    {
        // A new scandal-tier row appended with Placeable false leaves the placed scandal, and so the
        // pinned feelings-off run, as they were (acts-batch2 spec 2.2, edit 1).
        var kinds = DefaultTown.Acts().Append(new ActKind("Slandered", 4.5, -1, 1, 1, 0, Array.Empty<string>(), Placeable: false)).ToList();
        for (long seed = 1; seed <= 50; seed++)
            Assert.Equal(Harness.ScandalFor(seed, DefaultTown.Acts()), Harness.ScandalFor(seed, kinds));
        Assert.Equal("388d128d7fd4cdf2", Metrics.LogHash(new Simulation(7, kinds: kinds,
            scheduled: new[] { Harness.ScandalFor(7, kinds) }, feelings: FeelingOptions.Off).Run(14)));
    }

    [Fact]
    public void ACrimeIsAScandalUnlessTheRowSaysOtherwise()
    {
        ActKind stole = DefaultTown.Acts().First(k => k.Name == "Stole");
        ActKind argued = DefaultTown.Acts().First(k => k.Name == "Argued");
        Assert.True(stole.IsCrime);
        Assert.False(argued.IsCrime);
        Assert.False((stole with { Reportable = false }).IsCrime); // a scene: loud, but nobody's business
        Assert.True((argued with { Reportable = true }).IsCrime); // a broken promise of money, say
        Assert.True(DefaultTown.Acts().All(k => k.Placeable && k.IsCrime == k.IsScandal)); // shipped rows as they were
    }

    [Fact]
    public void ADatedGatheringIsOnOnlyOnItsDate()
    {
        var fair = new Gathering("Fair", "Square", new Tile(5, 5), 4, Clock.At(9), Clock.At(15), Array.Empty<int>(), 20,
            Date: new YearDay(2, 16), Holiday: true);
        int fairDay = 2 * Clock.DaysPerSeason + 15; // fall 16 of the first year
        Assert.True(fair.On(fairDay * Clock.MinutesPerDay + Clock.At(10)));
        Assert.True(fair.On((fairDay + 4 * Clock.DaysPerSeason) * Clock.MinutesPerDay + Clock.At(10))); // and of the next
        Assert.False(fair.On((fairDay + 1) * Clock.MinutesPerDay + Clock.At(10)));
        Assert.False(fair.On(fairDay * Clock.MinutesPerDay + Clock.At(16)));
    }

    [Fact]
    public void TheRecordsGrowAtTheEnd()
    {
        // Appended values keep every earlier value's number (they are saved and replayed as integers).
        Assert.Equal(8, (int)DesireKind.Curious);
        Assert.Equal(9, (int)DesireKind.Seek);
        Assert.Equal(8, (int)Outcome.Refused);
        Assert.Equal(9, (int)Outcome.Kept);
        var a = new Act(0, 0, "Ann", "Argued", "Square", new Tile(0, 0));
        Assert.Equal(0, a.Amount);
        Assert.Equal(Going.None, new ActGate(0.3, 0.1, Array.Empty<DesireKind>()).Going);
    }
}
