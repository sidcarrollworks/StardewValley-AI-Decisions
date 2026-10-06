using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The feeling metrics (phase 0c; FeelingMetrics.Summarise) read on small scenes where every
/// number can be worked out from the run's own feeling events: how far a holder turns on a
/// culprit, classed by the strongest way they came to know; war and dead towns, judged only at
/// season ends and at the end of the year; and means over nothing, which are NaN, not 0.
/// </summary>
public class FeelingStatsTests
{
    private static Haunt At(int x, int y, int from = 0, int to = Clock.MinutesPerDay, string place = "Room")
        => new(place, new Tile(x, y), from, to, 1);

    /// <summary>Someone whose day moves between spots, or rooms, at set hours; never tires.</summary>
    private static Villager Moving(string name, string household, params Haunt[] haunts)
        => new(name, household, "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null, haunts,
            new Dictionary<string, double>(), Array.Empty<string>());

    /// <summary>Someone who stands at one spot in the room all day.</summary>
    private static Villager V(string name, string household, int x, int y) => Moving(name, household, At(x, y));

    private static Location Hall(string name = "Room") => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    private static readonly int Ten = Clock.At(10);

    private static readonly ActKind Stole = new(Simulation.Stole, 4.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper));
    private static readonly ActKind Warned = new(Authority.Warned, 3.0, -1, 1, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Actor, -0.3, 0.4, 0.4, TargetIs.Given));
    private static readonly ActKind[] Kinds = { Stole, Warned };

    private static FeelingStats Stats(IReadOnlyList<Villager> cast, params SimResult[] runs)
        => FeelingMetrics.Summarise(runs, Kinds, cast, new Dictionary<string, string>(), new FeelingOptions());

    /// <summary>
    /// Tom steals from Kim (far off, she sees nothing) at 10:00. Ann and Bea see it close up and
    /// both tell Cal, who stands 9 tiles from it: hearsay, then a second, independent teller. May,
    /// the mayor, steps in from 14:00 to 14:10 (too short to chat); Ann and Bea report at once and
    /// May warns Tom. Cal walks over at 14:00 and sees the warning, unless he stays where he is,
    /// out of sight of it. Up to two tellings a day, and no confrontation.
    /// </summary>
    private static (IReadOnlyList<Villager> Cast, SimResult R) Hearsay(bool seesWarning = true)
    {
        Haunt[] cal = seesWarning ? new[] { At(12, 2, 0, Clock.At(14)), At(6, 2, Clock.At(14)) } : new[] { At(12, 2) };
        var cast = new[]
        {
            V("Tom", "T", 3, 2), V("Ann", "A", 5, 2), V("Bea", "B", 4, 4), V("Kim", "K", 38, 2), Moving("Cal", "C", cal),
            Moving("May", "M", At(2, 2, 0, Clock.At(14), "Away"), At(8, 2, Clock.At(14), Clock.At(14, 10)),
                At(2, 2, Clock.At(14, 10), Clock.MinutesPerDay, "Away")),
        };
        var authority = new AuthorityOptions
        {
            Mayor = "May", ElectConstable = false, ReportBase = 1,
            Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
        };
        var gossip = new GossipOptions { ChatChance = 10, TellsPerDay = 2, ConfrontMin = 10 };
        SimResult r = new Simulation(1, cast, new[] { Hall(), Hall("Away") }, Kinds, gossip: gossip, authority: authority,
            scheduled: new[] { (Ten, "Tom", Simulation.Stole) }, wander: 0, feelings: new FeelingOptions()).Run(1);
        return (cast, r);
    }

    /// <summary>Cal's regard for Tom moves twice: when Bea agrees with Ann (corroborated), and
    /// again when he sees the warning (confirmed). He is one holder: he counts once, under the
    /// strongest basis he reached, with all he moved. Ann and Bea saw it, and nobody else holds
    /// anything against Tom, so nobody is left under corroborated. Had Cal not seen the warning,
    /// he would count there.</summary>
    [Fact]
    public void AHolderCountsOnce_UnderTheStrongestBasis_WithTheirWholeChange()
    {
        var (cast, r) = Hearsay();
        Act warning = Assert.Single(r.Acts, a => a.Kind == Authority.Warned);
        Assert.Equal(("Tom", 0), (warning.Actor, warning.About));

        var towardTom = r.Feelings.Where(f => f.Toward == "Tom" && f.ActId == 0 && f.Change != 0).ToList();
        Assert.Equal(new[] { "Ann", "Bea", "Cal" }, towardTom.Select(f => f.Holder).Distinct().OrderBy(h => h, StringComparer.Ordinal));
        var cal = towardTom.Where(f => f.Holder == "Cal").ToList();
        Assert.Equal(new[] { "Corroborated", "Confirmed" }, cal.Select(f => f.Basis));
        Assert.True(cal[0].Tick < warning.Tick && warning.Tick < cal[1].Tick);
        Assert.All(towardTom.Where(f => f.Holder != "Cal"), f => Assert.Equal("Witnessed", f.Basis));
        double net = cal.Sum(f => f.Change);
        Assert.True(net < cal[0].Change && net < cal[1].Change);    // so the whole is told from either move

        FeelingStats s = Stats(cast, r);
        Assert.Equal(net, s.DropConfirmed, 12);
        Assert.True(double.IsNaN(s.DropCorroborated));
        Assert.True(double.IsNaN(s.DropHeardName));
        Assert.Equal(-0.05625 * 0.5, s.DropWitnessed, 12);          // Ann and Bea, close up: f0 x 1 x plastic

        var (stayed, unseen) = Hearsay(seesWarning: false);
        Felt only = Assert.Single(unseen.Feelings, f => f.Holder == "Cal" && f.Toward == "Tom" && f.ActId == 0);
        Assert.Equal("Corroborated", only.Basis);
        FeelingStats u = Stats(stayed, unseen);
        Assert.Equal(only.Change, u.DropCorroborated, 12);
        Assert.True(double.IsNaN(u.DropConfirmed));
    }

    /// <summary>A quiet room where Ann has held a grudge against Bob since before the run (-0.5,
    /// one of six pairs under -0.2): a war town, and a dead one, since nobody's regard moves. A
    /// war town is judged only at a season's end and a dead town at the end of the year (day 111),
    /// so runs too short to reach them say NaN, not 0.</summary>
    [Fact]
    public void WarAndDeadTownsAreJudgedOnlyAtTheirSnapshots()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2), V("Cal", "C", 7, 2) };
        var feelings = new FeelingOptions { Start = new Dictionary<(string, string), double> { [("Ann", "Bob")] = -0.5 } };
        SimResult Run(int days) => new Simulation(1, cast, new[] { Hall() }, Kinds, wander: 0, feelings: feelings).Run(days);

        FeelingStats month = Stats(cast, Run(27));
        Assert.True(double.IsNaN(month.WarTowns));
        Assert.True(double.IsNaN(month.DeadTowns));

        FeelingStats season = Stats(cast, Run(28));
        Assert.Equal(1, season.WarTowns);
        Assert.True(double.IsNaN(season.DeadTowns));

        SimResult year = Run(4 * Clock.DaysPerSeason);
        Assert.Contains(year.RegardSnapshots, x => x.Day == 111);
        FeelingStats y = Stats(cast, year);
        Assert.Equal(1, y.WarTowns);
        Assert.Equal(1, y.DeadTowns);

        // A year with a theft on its last day but one: Kim's regard for Tom has moved 0.1 or more,
        // and one pair of six is over the 5% share, so that town is not dead.
        var lively = new[] { V("Tom", "T", 3, 2), V("Kim", "K", 5, 2), V("Cal", "C", 7, 2) };
        SimResult busy = new Simulation(1, lively, new[] { Hall() }, Kinds, wander: 0,
                authority: new AuthorityOptions { Keepers = new Dictionary<string, string> { ["Room"] = "Kim" } },
                scheduled: new[] { (110 * Clock.MinutesPerDay + Ten, "Tom", Simulation.Stole) }, feelings: new FeelingOptions())
            .Run(4 * Clock.DaysPerSeason);
        Assert.Equal(110, Clock.Day(Assert.Single(busy.Acts).Tick));
        Assert.Equal(0, Stats(lively, busy).DeadTowns);
    }

    /// <summary>In the hearsay scene, where Cal sees the warning, nobody was confronted, questioned
    /// or named after seeing, there is no constable, no newcomer, and no gift or argument: each of
    /// those means is over nothing, so NaN. What did happen has a number.</summary>
    [Fact]
    public void AMeanOverNothingIsNaN()
    {
        var (cast, r) = Hearsay();
        Assert.Empty(r.Confrontations);
        Assert.Empty(r.Interviews);
        Assert.Null(r.Constable);

        FeelingStats s = Stats(cast, r);
        Assert.True(double.IsNaN(s.WrongConfrontDrop));
        Assert.True(double.IsNaN(s.InnocentsResentingNamer));
        Assert.True(double.IsNaN(s.BystanderRegardForConstable));
        Assert.True(double.IsNaN(s.GiftsToLoved));
        Assert.True(double.IsNaN(s.ArgumentsToDisliked));
        Assert.True(double.IsNaN(s.RegardTowardNewcomer));
        Assert.True(double.IsNaN(s.RegardFromNewcomer));
        Assert.False(double.IsNaN(s.DropWitnessed));
        Assert.False(double.IsNaN(s.MeanPower));
    }
}
