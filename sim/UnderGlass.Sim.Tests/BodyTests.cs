using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The clock, energy and sleep, jobs and the map (design rule 1; Sid, 2026-10-05).</summary>
public class BodyTests
{
    private static Location Room() => new("Room", false, Enumerable.Repeat(new string('.', 12), 6).ToList());

    private static Villager Lone(string name, Body body, Job? job = null)
        => new(name, name, "villager", new Temperament(0.5, 0.5, 0.5, 0.5), body, job,
            new[] { new Haunt("Room", new Tile(3, 3), 0, Clock.MinutesPerDay, 1) },
            new Dictionary<string, double>(), Array.Empty<string>());

    [Fact]
    public void SleepLastsOnlyUntilTheBarIsFull()
    {
        var o = new BodyOptions();
        SimResult r = new Simulation(1, new[] { Lone("Ann", new Body(100, 0.2)) }, new[] { Room() }, DefaultTown.Acts()).Run(4);
        var nights = r.Sleeps.Where(s => s.SleptAt > 0 && s.WokeAt is not null).ToList();
        Assert.NotEmpty(nights);
        double perMinute = 100 / (o.SleepHoursToFill * 60);
        foreach (Sleep s in nights)
        {
            int expected = (int)Math.Ceiling((100 - s.EnergyAtSleep) / perMinute);
            Assert.InRange(s.WokeAt!.Value - s.SleptAt, expected - 1, expected + 1);
            Assert.True(s.WokeAt!.Value - s.SleptAt <= o.SleepHoursToFill * 60 + 1);
        }
    }

    [Fact]
    public void BedtimeComesFromTheBody_NotTheClock()
    {
        var cast = new[] { Lone("Owl", new Body(100, 0.05)), Lone("Lark", new Body(100, 0.35)), Lone("Big", new Body(130, 0.2)) };
        SimResult r = new Simulation(2, cast, new[] { Room() }, DefaultTown.Acts(), wander: 0).Run(3);
        int FirstBed(string n) => r.Sleeps.First(s => s.Name == n && s.SleptAt > 0).SleptAt;
        Assert.True(FirstBed("Owl") > FirstBed("Lark") + 60);
        Assert.True(FirstBed("Big") > FirstBed("Lark") + 60); // a bigger bar keeps you up longer
    }

    [Fact]
    public void AlarmsWakeTheRestedMoreOften_AndSometimesNotAtAll()
    {
        var o = new BodyOptions();
        Assert.True(o.WakeChance(0.9) > o.WakeChance(0.3));
        Assert.True(o.WakeChance(1.0) < 1);

        var runs = Enumerable.Range(1, 10).Select(s => new Simulation(s).Run(7)).ToList();
        var woken = runs.SelectMany(r => r.Sleeps).Where(s => s.WokeAt is not null).ToList();
        int missed = woken.Count(s => s.MissedAlarm);
        Assert.InRange(missed, 1, woken.Count / 2);
        Assert.Contains(runs, r => r.Late.Count > 0); // sleeping through makes someone late for work
    }

    [Fact]
    public void SomeoneWhoNeverRestsCollapses_IsSeen_AndWakesAtHome()
    {
        var cast = new[] { Lone("Ann", new Body(100, -1)), Lone("Bob", new Body(100, 0.05)) };
        SimResult r = new Simulation(3, cast, new[] { Room() }, DefaultTown.Acts(), wander: 0).Run(2);
        Sleep fall = Assert.Single(r.Sleeps, s => s.Collapsed);
        Assert.Equal("Ann", fall.Name);
        Act act = Assert.Single(r.Acts, a => a.Kind == Simulation.Collapsed);
        Assert.Equal("Ann", act.Actor);
        Assert.Equal(Tier.News, DefaultTown.Acts().First(k => k.Name == Simulation.Collapsed).Tier);
    }

    [Fact]
    public void TheTownIsConnected_AndEverySpotCanBeStoodOn()
    {
        var places = DefaultTown.Locations().ToDictionary(p => p.Name);
        var links = DefaultTown.Links();
        foreach (Link l in links)
        {
            Assert.True(places[l.A].Walkable(l.DoorA), $"{l.A} door to {l.B}");
            Assert.True(places[l.B].Walkable(l.DoorB), $"{l.B} door to {l.A}");
        }
        foreach (Villager v in DefaultTown.Cast())
        {
            Assert.True(places[v.Home].Walkable(DefaultTown.Bed));
            if (v.Job is { } j)
                Assert.True(places[j.Place].Walkable(j.Spot), $"{v.Name}'s job");
            foreach (Haunt h in v.Haunts)
                Assert.True(places[h.Place].Walkable(h.Spot), $"{v.Name} at {h.Place}");
        }
        var reached = new HashSet<string> { "Square" };
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (Link l in links)
                if (reached.Contains(l.A) != reached.Contains(l.B))
                    grew |= reached.Add(l.A) | reached.Add(l.B);
        }
        Assert.Equal(places.Keys.OrderBy(k => k), reached.OrderBy(k => k));
    }

    [Fact]
    public void PeopleWalkTheRoads_AndKeepTheirOwnHours()
    {
        var roads = new[] { "TownLane", "BeachPath", "ForestPath", "FarmRoad" };
        var onRoads = new HashSet<string>();
        int upLate = 0, asleepEarly = 0;
        new Simulation(5).Run(2, (m, sim) =>
        {
            foreach (Villager v in DefaultTown.Cast())
            {
                var w = sim.Where(v.Name);
                if (roads.Contains(w.Place))
                    onRoads.Add(v.Name);
                if (m == Clock.MinutesPerDay + Clock.At(23, 30))
                {
                    if (w.Asleep) asleepEarly++;
                    else upLate++;
                }
            }
        });
        Assert.True(onRoads.Count >= 6);
        Assert.True(upLate > 0 && asleepEarly > 0); // no shared bedtime
    }

    [Fact]
    public void EveryActKindHasATier()
    {
        var kinds = DefaultTown.Acts().ToDictionary(k => k.Name);
        Assert.Equal(Tier.Trivia, kinds["GaveGift"].Tier);
        Assert.Equal(Tier.News, kinds["Argued"].Tier);
        Assert.Equal(Tier.Scandal, kinds["Stole"].Tier);
        Assert.Equal(Tier.Upheaval, new ActKind("ShopClosed", 6, -1, 1, 1, 0, Array.Empty<string>(), Upheaval: true).Tier);
        Assert.False(new ActKind("ShopClosed", 6, -1, 1, 1, 0, Array.Empty<string>(), Upheaval: true).IsScandal);
    }

    [Fact]
    public void TheClockReadsAsADateAndATime()
    {
        Assert.Equal("d1 06:05", Clock.Format(Clock.MinutesPerDay + Clock.At(6, 5)));
        Assert.Equal(2, Clock.Weekday(9 * Clock.MinutesPerDay + 1));
        Assert.Equal(20 * 60, Clock.MinutesPerDay * Clock.RealSecondsPerGameMinute, 6);
    }

    [Fact]
    public void MarketDayGathersTheTownInTheSquare()
    {
        // Over five seeds, people in the square at 11:00 on Saturday against Friday.
        int friday = 0, saturday = 0;
        foreach (long seed in Enumerable.Range(1, 5))
            new Simulation(seed).Run(6, (m, sim) =>
            {
                if (m != 4 * Clock.MinutesPerDay + Clock.At(11) && m != 5 * Clock.MinutesPerDay + Clock.At(11))
                    return;
                int n = DefaultTown.Cast().Count(v => sim.Where(v.Name).Place == "Square");
                if (Clock.Weekday(m) == 5) saturday += n; else friday += n;
            });
        Assert.True(saturday >= 5 * 4, $"saturday {saturday}");
        Assert.True(saturday > friday, $"saturday {saturday}, friday {friday}");
    }

    [Fact]
    public void EveryActRecordsWhoWasAround_AndNobodyRobsTheirOwnShop()
    {
        var kinds = DefaultTown.Acts();
        foreach (long seed in Enumerable.Range(1, 30))
        {
            var placed = (1500, Harness.Anyone, "Stole");
            SimResult r = new Simulation(seed, kinds: kinds, scheduled: new[] { placed }).Run(4);
            Assert.Equal(r.Acts.Count, r.Scenes.Count);
            foreach (Act a in r.Acts)
            {
                Scene sc = r.Scenes[a.Id];
                Assert.Equal(r.CastSize - 1, sc.InRange + sc.SamePlace + sc.Elsewhere + sc.Asleep);
            }
            if (r.Acts.FirstOrDefault(a => a.Injected) is { } stole)
                Assert.NotEqual(stole.Location, DefaultTown.Cast().First(v => v.Name == stole.Actor).Job?.Place);
        }
    }
}
