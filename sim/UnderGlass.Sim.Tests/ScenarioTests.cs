using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The scenario harness (batch 2 spec 5.2-5.3, slice m-0): a scenario's act begins only in its
/// scene, a scene that never comes leaves the run as it was but for one log line, scenarios with one
/// key place one act, the town refuses what it can't stage, and a scandal's circle is everyone who
/// knew its actor as its day began.
/// </summary>
public class ScenarioTests
{
    private static SimResult RunWith(long seed, int days, params Scenario[] scenes)
    {
        var sim = new Simulation(seed, TownData.Default());
        sim.Place(scenes);
        return sim.Run(days);
    }

    private static readonly IReadOnlyDictionary<string, Villager> Cast = DefaultTown.Cast().ToDictionary(v => v.Name);

    /// <summary>A scene that never comes (30 people within 8 tiles) is dropped once its days are up,
    /// with a log line, and the rest of the log is the plain run's: the harness draws only when
    /// someone is able.</summary>
    [Fact]
    public void ASceneThatNeverComesChangesNothingButItsLogLine()
    {
        SimResult plain = new Simulation(4, TownData.Default()).Run(7);
        SimResult never = RunWith(4, 7, new Scenario("never", "RummagedInBin", MinInRange: 30));
        Assert.Contains(never.Log, l => l.EndsWith(" scene dropped never", StringComparison.Ordinal));
        Assert.Equal(plain.Log, never.Log.Where(l => !l.EndsWith(" scene dropped never", StringComparison.Ordinal)).ToList());
        Assert.Empty(never.Scenarios);
        Assert.Equal(Metrics.LogHash(plain), Metrics.LogHash(RunWith(4, 7)));
    }

    /// <summary>C2's scene: a bin in the Square while Noon or the Market is on, the actor inside the
    /// gathering, five or more within 8 tiles, in the window, placed and marked.</summary>
    [Fact]
    public void AnActBeginsOnlyInItsScene()
    {
        var gatherings = DefaultTown.Gatherings();
        int placed = 0;
        for (long seed = 1; seed <= 4; seed++)
        {
            SimResult r = RunWith(seed, 6, new Scenario("C2", "RummagedInBin", Places: new[] { "Square" }, AtHub: true, MinInRange: 5));
            foreach (Act a in r.Acts.Where(a => r.Scenarios.ContainsKey(a.Id)))
            {
                placed++;
                Assert.Equal("C2", r.Scenarios[a.Id]);
                Assert.True(a.Injected);
                Assert.Equal(("RummagedInBin", "Square"), (a.Kind, a.Location));
                Assert.True(r.Scenes[a.Id].InRange >= 5);
                Assert.InRange(Clock.OfDay(a.Tick), 480, 1259);
                Assert.InRange(Clock.Day(a.Tick), 1, 3);
                Assert.Contains(gatherings, g => g.Place == "Square" && g.On(a.Tick) && a.At.Chebyshev(g.Center) <= g.Radius);
            }
        }
        Assert.True(placed >= 3, $"placed in {placed} of 4 runs");
    }

    /// <summary>C3's scene has one loner within 8 tiles as it begins, and leaves no trace; C6's has
    /// only kin and housemates there. (Read from where everyone stood at the act's minute.)</summary>
    [Fact]
    public void OnlookersAreLonersOrFamilyAsAsked()
    {
        string[] names = DefaultTown.Cast().Select(v => v.Name).ToArray();
        int lone = 0, family = 0;
        for (long seed = 1; seed <= 6; seed++)
        {
            var sim = new Simulation(seed, TownData.Default());
            sim.Place(new[]
            {
                new Scenario("C3", "RummagedInBin", Places: new[] { "ClinicYard" }, MinInRange: 1, MaxInRange: 1, Onlookers: Onlookers.Loners, NoTrace: true),
                new Scenario("C6", "Stole", MinInRange: 1, Onlookers: Onlookers.KinOnly),
            });
            var where = new Dictionary<int, (string Place, Tile At, bool Asleep)[]>();
            SimResult r = sim.Run(5, (m, s) => where[m] = names.Select(n => { var w = s.Where(n); return (w.Place, w.At, w.Asleep); }).ToArray());
            foreach (Act a in r.Acts.Where(a => r.Scenarios.ContainsKey(a.Id)))
            {
                var near = names.Where((n, i) => n != a.Actor && !where[a.Tick][i].Asleep && where[a.Tick][i].Place == a.Location
                                                 && where[a.Tick][i].At.Chebyshev(a.At) <= 8).ToList();
                Assert.Equal(r.Scenes[a.Id].InRange, near.Count);
                if (r.Scenarios[a.Id] == "C3")
                {
                    lone++;
                    string only = Assert.Single(near);
                    Assert.True(Cast[only].Temperament.Chattiness <= Scenario.LonerChattiness, only);
                    Assert.DoesNotContain(r.Log, l => l.Contains($" trace {a.Id} ", StringComparison.Ordinal));
                }
                else
                {
                    family++;
                    Villager actor = Cast[a.Actor];
                    Assert.NotEmpty(near);
                    Assert.All(near, n => Assert.True(Cast[n].Household == actor.Household || actor.KinOf(n) is not null || Cast[n].KinOf(a.Actor) is not null, n));
                }
            }
        }
        Assert.True(lone >= 3, $"C3 placed {lone} times");
        Assert.True(family >= 1, $"C6 placed {family} times");
    }

    /// <summary>A named actor acts in the window and nobody else does; scenarios that share a key
    /// place the same act at the same minute, whatever their names.</summary>
    [Fact]
    public void ANamedActorActsAndOneKeyPlacesOneAct()
    {
        SimResult pam = RunWith(2, 6, new Scenario("pam", "RummagedInBin", Actor: "Pam", Day: 0, From: 0, To: Clock.MinutesPerDay, GiveUpDays: 6));
        Act bin = Assert.Single(pam.Acts, a => pam.Scenarios.ContainsKey(a.Id));
        Assert.Equal("Pam", bin.Actor);

        Scenario a = new("A", "RummagedInBin", Places: new[] { "Square" }, Key: "k"), b = a with { Name = "B" };
        SimResult ra = RunWith(3, 5, a), rb = RunWith(3, 5, b);
        Act actA = Assert.Single(ra.Acts, x => ra.Scenarios.ContainsKey(x.Id)), actB = Assert.Single(rb.Acts, x => rb.Scenarios.ContainsKey(x.Id));
        Assert.Equal((actA.Tick, actA.Actor), (actB.Tick, actB.Actor));
        Assert.Equal(("A", "B"), (ra.Scenarios[actA.Id], rb.Scenarios[actB.Id]));
    }

    /// <summary>A scenario the town can't stage is refused, and so are a debt and a follow-up,
    /// which come with later slices.</summary>
    [Fact]
    public void WhatTheTownCannotStageIsRefused()
    {
        var sim = new Simulation(1, TownData.Default());
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Juggled") }));
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Stole", Actor: "Nobody") }));
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Stole", Target: "Nobody") }));
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Stole", Places: new[] { "Moon" }) }));
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Stole", From: 600, To: 600) }));
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Stole", MinInRange: 3, MaxInRange: 2) }));
        Assert.Throws<NotSupportedException>(() => sim.Place(new[] { new Scenario("x", "Stole", Amount: 150) }));
        Assert.Throws<NotSupportedException>(() => sim.Place(new[] { new Scenario("x", "Stole", Then: new[] { new FollowUp(60, "settle") }) }));
        Assert.Throws<ArgumentException>(() => sim.Place(new[] { new Scenario("x", "Stole", Then: new[] { new FollowUp(-1, Simulation.Sway) }) }));
        sim.Place(new[] { new Scenario("x", "Stole", Then: new[] { new FollowUp(0, Simulation.Sway) }) }); // built
    }

    /// <summary>"sway": when the mayor decides the case of a swayed act he lets the accused off, and
    /// the same act without it gets its consequence. The scene is C2's crowd, which reports.</summary>
    [Fact]
    public void ASwayedCaseIsLetOff()
    {
        Scenario crowd = new("crowd", "RummagedInBin", Places: new[] { "Square" }, AtHub: true, MinInRange: 5);
        int decided = 0;
        for (long seed = 1; seed <= 6; seed++)
        {
            SimResult plain = RunWith(seed, 14, crowd), swayed = RunWith(seed, 14, crowd with { Then = new[] { new FollowUp(0, Simulation.Sway) } });
            foreach (Verdict v in swayed.Verdicts.Where(v => swayed.Scenarios.ContainsKey(v.ActId)))
            {
                decided++;
                Assert.True(v.LetOff);
                Assert.Contains(swayed.Log, l => l.EndsWith($" scene crowd {Simulation.Sway} {v.ActId}", StringComparison.Ordinal));
                Verdict before = Assert.Single(plain.Verdicts, p => p.ActId == v.ActId); // the same act, decided the same way up to then
                Assert.False(before.LetOff);
            }
        }
        Assert.True(decided >= 2, $"{decided} placed cases decided");
    }

    /// <summary>Each scandal's circle, and each scenario act's, is everyone else whose familiarity
    /// with its actor was 0.2 or more as its day began (read here at the end of the day before), in
    /// name order; other acts have none.</summary>
    [Fact]
    public void TheCircleIsWhoKnewTheActorAsTheDayBegan()
    {
        var sim = new Simulation(5, TownData.Default());
        sim.Place(new[] { new Scenario("gift", "GaveGift", Places: new[] { "Square" }) });
        string[] names = DefaultTown.Cast().Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray(); // the run's order
        var known = new Dictionary<int, Dictionary<(string, string), double>>();
        SimResult r = sim.Run(14, (m, s) =>
        {
            if (Clock.OfDay(m) == Clock.MinutesPerDay - 1) // after the day closed: the next day's start
                known[Clock.Day(m) + 1] = names.SelectMany(a => names.Select(b => (a, b))).ToDictionary(p => p, p => s.Familiarity(p.a, p.b));
        });
        var kinds = DefaultTown.Acts().ToDictionary(k => k.Name);
        Assert.NotEmpty(r.Circles);
        foreach (Act a in r.Acts)
        {
            bool measured = kinds[a.Kind].IsScandal || r.Scenarios.ContainsKey(a.Id);
            Assert.Equal(measured, r.Circles.ContainsKey(a.Id));
            if (!measured || Clock.Day(a.Tick) == 0)
                continue;
            var expected = names.Where(n => n != a.Actor && known[Clock.Day(a.Tick)][(n, a.Actor)] >= ReachMetrics.CircleAt).ToList();
            Assert.Equal(expected, r.Circles[a.Id]);
        }
        Assert.Contains(r.Acts, a => r.Scenarios.ContainsKey(a.Id) && !kinds[a.Kind].IsScandal); // the gift, measured as a scenario
    }
}
