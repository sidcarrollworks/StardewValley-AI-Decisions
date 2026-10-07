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
    /// him a gift on day 4 and he returns it. Run for 12 days, so every window ends inside the run.
    /// Across households, Ann's argument is answered and Bob's is not (Ann's close calls say no), and
    /// one kindness of two is returned. Ann's argument was scheduled: neither the gate's nor the rates'.</summary>
    [Fact]
    public void AnsweringIsCountedFromTheActs()
    {
        var cast = new[] { V("Ann", "A", 3, 2, 0.5, "Argued"), V("Bob", "B", 5, 2, 0.9) };
        FeelingOptions o = FeelingOptions.WithDesire();
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: new[] { (Ten, "Ann", "Argued"), (4 * D + Ten, "Ann", "GaveGift") }, wander: 0).Run(12);
        Act argue = Assert.Single(r.Acts, a => a is { Actor: "Bob", Kind: "Argued" });
        Assert.True(argue.Tick - Ten <= 60);
        Act gift = Assert.Single(r.Acts, a => a is { Actor: "Bob", Kind: "GaveGift" });

        DesireStats s = DesireMetrics.Summarise(new[] { r }, cast, o);
        Assert.Equal(new[] { 0.5, 0.5, 0.5 }, s.AnsweredWithin);
        Assert.Equal(0.5, s.ReturnedWithin7);
        double years = 12 / 112.0;
        Assert.Equal(1 / years, s.GateActsPerYear["Argued"], 9);
        Assert.Equal(1 / years, s.GateActsPerYear["GaveGift"], 9);
        Assert.Equal(0, s.RateActsPerYear["Argued"]); // Ann's argument was scheduled (injected)
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
        // The seeded pairs across households (Pierre and Shane, both ways) are all that separates the two.
        int n = r.Names.Count, seededUnder = 0;
        foreach (var (from, to) in o.Start.Keys)
            if (DefaultTown.Cast().First(v => v.Name == from).Household != DefaultTown.Cast().First(v => v.Name == to).Household
                && r.RegardSnapshots[0].Regard[r.Names.ToList().IndexOf(from) * n + r.Names.ToList().IndexOf(to)] < -0.2)
                seededUnder++;
        Assert.Equal(2, seededUnder);
        Assert.Equal(d27.Across - seededUnder / (double)(n * (n - 1)), d27.AcrossUnseeded, 12);

        StanceAt st = Assert.Single(s.Stance);
        Assert.Equal(27, st.Day);
        Assert.True(st.P10 <= st.P50 && st.P50 <= st.P90);
        // Hermits and brawlers at the season's end, and ever, counted straight from the stances.
        int hermits = r.Stances.Values.Count(v => v[27] <= DesireMetrics.HermitAt);
        int brawlers = r.Stances.Values.Count(v => v[27] >= DesireMetrics.BrawlerAt);
        Assert.Equal((hermits, brawlers), ((int)st.Hermits, (int)st.Brawlers));
        Assert.Equal((hermits, brawlers), ((int)s.HermitsPerRun, (int)s.BrawlersPerRun)); // one run, one season end
        // The fringe's hours out are its season's free minutes out, a day.
        Assert.Equal(r.OutMinutes["Penny"][0] / 60.0 / 28, s.Boldest.Season1HoursOut, 12);
        Assert.Equal(s.Boldest.Season1HoursOut, s.Boldest.Season4HoursOut, 12); // a run of one season

        Assert.Equal(("Penny", 0.99), (s.Boldest!.Name, s.Boldest.Boldness));
        Assert.Equal(r.Stances["Penny"][^1], s.Boldest.EndStance, 12);
        // Free time out is at most the day's, and the town spends some: the haunts, the hubs.
        Assert.All(r.OutMinutes.Values, m => Assert.InRange(m[0], 0, 28 * D));
        Assert.True(r.OutMinutes.Values.Count(m => m[0] > 28 * 60) > r.OutMinutes.Count / 2, "most people are out an hour a day or more");
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

    /// <summary>A pair that starts in a feud is never counted as a new one (spec section 10); a pair
    /// that falls into one is, even from a cool start (SteeringTests T34). Everyone argues every three
    /// days. Ann and Bob start at -0.4 both ways; Bob and Cal at -0.25, and fall under -0.3.</summary>
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
        Assert.True(r.Regard[("Bob", "Cal")] <= -0.3 && r.Regard[("Cal", "Bob")] <= -0.3);
        Assert.Contains(r.Ties, t => t.What == "feud" && (t.A, t.B) == ("Bob", "Cal"));
    }

    /// <summary>The answering windows told apart, with the gate off so only scheduled acts happen:
    /// Ann argues on day 0, Bob back on day 2 (answered within 3 days, not 1), Ann back on day 9 at
    /// the same minute (exactly 7 days: answered within 7 only), and nobody answers that. Ann gives a
    /// gift on day 0 and Bob one on day 3 (returned within 7 days); nobody returns his. Two people,
    /// two households, 20 days: every window ends inside the run.</summary>
    [Fact]
    public void TheWindowsAreToldApart()
    {
        var cast = new[] { V("Ann", "A", 3, 2, 0.5, "Argued", "GaveGift"), V("Bob", "B", 5, 2, 0.5, "Argued", "GaveGift") };
        FeelingOptions o = FeelingOptions.WithDesire();
        o.Desire = false;
        var scheduled = new[]
        {
            (Ten, "Ann", "Argued"), (2 * D + Ten, "Bob", "Argued"), (9 * D + Ten, "Ann", "Argued"),
            (Ten + 60, "Ann", "GaveGift"), (3 * D + Ten + 60, "Bob", "GaveGift"),
        };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: scheduled, wander: 0).Run(20);
        Assert.Equal(5, r.Acts.Count);
        Assert.All(r.Acts, a => Assert.Equal(a.Actor == "Ann" ? "Bob" : "Ann", a.Target));

        DesireStats s = DesireMetrics.Summarise(new[] { r }, cast, o);
        Assert.Equal(new[] { 0.0, 1 / 3.0, 2 / 3.0 }, s.AnsweredWithin);
        Assert.Equal(0.5, s.ReturnedWithin7);
        Assert.Equal(0, s.PairsTwoEachWayPerRun); // Ann argued twice, Bob once
        Assert.Empty(s.GateActsPerYear);
    }

    /// <summary>Pairs that keep arguing count across households only: housemates who argue twice
    /// each way are not one.</summary>
    [Fact]
    public void PairsThatKeepArguingAreAcrossHouseholds()
    {
        foreach (string bobsHome in new[] { "B", "A" })
        {
            var cast = new[] { V("Ann", "A", 3, 2, 0.5, "Argued"), V("Bob", bobsHome, 5, 2, 0.5, "Argued") };
            FeelingOptions o = FeelingOptions.WithDesire();
            o.Desire = false;
            var scheduled = new[] { (Ten, "Ann", "Argued"), (Ten + 30, "Bob", "Argued"), (4 * D + Ten, "Ann", "Argued"), (4 * D + Ten + 30, "Bob", "Argued") };
            SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
                scheduled: scheduled, wander: 0).Run(6);
            Assert.Equal(4, r.Acts.Count(a => a.Kind == "Argued"));
            Assert.Equal(bobsHome == "B" ? 1 : 0, DesireMetrics.Summarise(new[] { r }, cast, o).PairsTwoEachWayPerRun);
        }
    }
}
