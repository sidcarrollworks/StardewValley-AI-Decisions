using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The desire gate's metrics (phase 0d.5; spec section 10) and the town's starting tensions
/// (0c question 1, Sid 2026-10-07). The metrics are checked against what the runs record: in a
/// small scene where every answer is known, and in four weeks of the town, where they must agree
/// with the result and with the feeling metrics.
/// </summary>
public class DesireMetricsTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly int Ten = Clock.At(10);

    private static Villager V(string name, string household, int x, int y, double bold, params string[] acts)
        => new(name, household, "villager", new Temperament(1.0, bold, 0.5, 0.6), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, y), 0, D, 1) }, acts.ToDictionary(k => k, _ => 1.0), Array.Empty<string>(), 30, null);

    private static Location Room() => new("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    private static readonly List<ActKind> Kinds = new()
    {
        new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(), Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1)),
        new("Argued", 3.0, -1, 2, 10, 0, Array.Empty<string>(), MinAge: 13, Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen)),
        new("HelpedSomeone", 2.0, 1, 2, 5, 0, Array.Empty<string>(), MinAge: 10, Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1)),
    };

    /// <summary>DesireSceneTests' D13: Ann argues at Bob, who answers within the half hour; she gives
    /// him a gift on day 4 and he returns it. Across households, one argument of two answered within
    /// a day (Bob's own is never answered: Ann's close call says no), and one kindness of two returned.</summary>
    [Fact]
    public void AnsweringIsCountedFromTheActs()
    {
        var cast = new[] { V("Ann", "A", 3, 2, 0.5, "Argued"), V("Bob", "B", 5, 2, 0.9) };
        FeelingOptions o = FeelingOptions.WithDesire();
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: new[] { (Ten, "Ann", "Argued"), (4 * D + Ten, "Ann", "GaveGift") }, wander: 0).Run(7);
        Act argue = Assert.Single(r.Acts, a => a is { Actor: "Bob", Kind: "Argued" });
        Assert.True(argue.Tick - Ten <= 60);
        Act gift = Assert.Single(r.Acts, a => a is { Actor: "Bob", Kind: "GaveGift" });

        DesireStats s = DesireMetrics.Summarise(new[] { r }, cast, o);
        Assert.Equal(new[] { 0.5, 0.5, 0.5 }, s.AnsweredWithin);
        Assert.Equal(0.5, s.ReturnedWithin7);
        double years = 7 / 112.0;
        Assert.Equal(1 / years, s.GateActsPerYear["Argued"], 9);
        Assert.Equal(1 / years, s.GateActsPerYear["GaveGift"], 9);
        Assert.Equal(1 / years, s.RateActsPerYear["Argued"], 9); // Ann's scheduled argument
        Assert.Equal(2 / years, s.CrossHouseholdArgumentsPerYear, 9);
        Assert.Equal(new[] { ("Bob", 1 / years) }, s.TopGateArguers);
        Assert.Contains(gift.Id, r.Pursued);
    }

    /// <summary>Four weeks of the town, seed 1: the metrics agree with the result they summarise.</summary>
    [Fact]
    public void TheTownsMetricsAgreeWithItsRecord()
    {
        FeelingOptions o = DefaultTown.Feelings();
        var sim = new Simulation(1, feelings: o);
        sim.SetTrait("Penny", Trait.Boldness, 0.99); // the fringe line reads character as the run began
        SimResult r = sim.Run(28);
        double years = 28 / 112.0;
        DesireStats s = DesireMetrics.Summarise(new[] { r }, DefaultTown.Cast(), o);

        Assert.Equal(r.Pursued.Count / years, s.GateActsPerYear.Values.Sum(), 9);
        Assert.Equal(r.Stirred.Count / years, s.StirredPerYear.Values.Sum(), 9);
        Assert.Equal(r.Pursuits.Count / years, s.WeighedPerYear.Values.Sum(), 9);
        Assert.Equal(r.Avoids.Count / years, s.AvoidsPerYear, 9);
        Assert.Equal(r.Withdrawals / years, s.WithdrawalsPerYear, 9);
        Assert.All(s.AnsweredWithin.Zip(s.AnsweredWithin.Skip(1)), p => Assert.True(p.First <= p.Second));
        Assert.All(s.Outcomes.GroupBy(p => p.Key.Role), g => Assert.Equal(1.0, g.Sum(p => p.Value), 9));

        // Dislike across households plus inside them is the feeling metrics' share under -0.2.
        FeelingStats f = FeelingMetrics.Summarise(new[] { r }, DefaultTown.Acts(), DefaultTown.Cast(), DefaultTown.TownEconomy().GroceriesAt, o);
        DislikeAt d27 = Assert.Single(s.Dislike);
        Assert.Equal(27, d27.Day);
        Assert.Equal(f.BySnapshot[0].UnderMinus02, d27.Across + d27.KinOrHome, 9);
        Assert.True(d27.AcrossUnseeded <= d27.Across);

        StanceAt st = Assert.Single(s.Stance);
        Assert.Equal(27, st.Day);
        Assert.True(st.P10 <= st.P50 && st.P50 <= st.P90);

        Assert.Equal(("Penny", 0.99), (s.Boldest!.Name, s.Boldest.Boldness));
        Assert.Equal(r.Stances["Penny"][^1], s.Boldest.EndStance, 12);
        // Hours out are at most the day's, and someone with a job is out every day.
        Assert.All(r.OutMinutes.Values, m => Assert.InRange(m[0], 0, 28 * D));
        Assert.True(r.OutMinutes["Pierre"][0] > 28 * 8 * 60 / 2);
    }

    /// <summary>The starting tensions: Pierre and Shane both ways, Sebastian toward Demetrius,
    /// Abigail toward Pierre, at -0.3, as the baseline each heals back to. Depth 0 gives none.</summary>
    [Fact]
    public void TheTownStartsWithItsTensions()
    {
        var start = DefaultTown.Feelings().Start;
        Assert.Equal(new[] { ("Abigail", "Pierre"), ("Pierre", "Shane"), ("Sebastian", "Demetrius"), ("Shane", "Pierre") },
            start.Keys.OrderBy(k => k.From, StringComparer.Ordinal).ToArray());
        Assert.All(start.Values, v => Assert.Equal(-0.3, v));
        Assert.Empty(DefaultTown.Tensions(0));
        Assert.All(DefaultTown.Tensions(0.4).Values, v => Assert.Equal(-0.4, v));

        SimResult r = new Simulation(1, feelings: DefaultTown.Feelings()).Run(1);
        foreach (var (pair, v) in start)
            Assert.Equal(v, r.Baseline[pair]);
        Assert.Equal(0.6, r.Baseline[("Demetrius", "Sebastian")]); // one way only: he keeps a housemate's regard
    }

    /// <summary>A pair that starts in a feud is never counted as a new one (spec section 10); a
    /// pair that falls into one is. Everyone argues every three days. Ann and Bob start at -0.4 both
    /// ways, already a feud; Bob and Cal at -0.25, not yet one, and fall under -0.3.</summary>
    [Fact]
    public void ASeededFeudIsNotANewOne()
    {
        var cast = new[] { V("Ann", "A", 3, 2, 0.9, "Argued"), V("Bob", "B", 5, 2, 0.9, "Argued"), V("Cal", "C", 7, 2, 0.9, "Argued") };
        FeelingOptions o = FeelingOptions.WithDesire(("Ann", "Bob", -0.4), ("Bob", "Ann", -0.4), ("Bob", "Cal", -0.25), ("Cal", "Bob", -0.25));
        var scheduled = Enumerable.Range(0, 6).SelectMany(d => new[] { (d * 3 * D + Ten, "Ann", "Argued"), (d * 3 * D + Ten + 60, "Cal", "Argued") }).ToList();
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: scheduled, wander: 0).Run(18);
        Assert.True(r.Regard[("Ann", "Bob")] <= -0.3 && r.Regard[("Bob", "Ann")] <= -0.3);
        Assert.DoesNotContain(r.Ties, t => t.What == "feud" && (t.A, t.B) == ("Ann", "Bob"));
        var feuds = r.Ties.Where(t => t.What == "feud").ToList();
        Assert.Contains(feuds, t => (t.A, t.B) == ("Bob", "Cal"));
    }
}
