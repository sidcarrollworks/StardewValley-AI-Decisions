using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Town spec step T1: the engine options a grown town needs, each in a small scene. Routing
/// by walking distance (E3), hubs with ages, a crowd limit and local households (E4), commutes (E5),
/// and familiarity seeded by circle and fading by Sid's forgetting model (E6). The shipped town is
/// unchanged by all of them (every pin holds); the forgetting run is pinned here.</summary>
public class TownGrowthTests
{
    private static Location Room(string name, int w, int h, bool outdoor = false)
        => new(name, outdoor, Enumerable.Repeat(new string('.', w), h).ToList());

    private static Villager V(string name, string household, IReadOnlyList<Haunt> haunts, Job? job = null, int age = 30, Body? body = null)
        => new(name, household, "villager", new Temperament(0.5, 0.5, 0.5, 0.5), body ?? new Body(100, -1), job, haunts,
            new Dictionary<string, double>(), Array.Empty<string>(), age);

    private static Haunt At(string place, int x, int y, int from = 0, int to = Clock.MinutesPerDay, double weight = 1)
        => new(place, new Tile(x, y), from, to, weight);

    /// <summary>A small town as one value: no money, no authority, feelings off unless given.</summary>
    private static TownData Town(IReadOnlyList<Villager> cast, IReadOnlyList<Location> places, IReadOnlyList<Link>? links = null,
        IReadOnlyList<Gathering>? hubs = null, FeelingOptions? feelings = null, GossipOptions? gossip = null,
        IReadOnlyList<(string, string, double)>? familiarity = null, BodyOptions? body = null) => new()
    {
        Cast = cast,
        Places = places,
        Links = links ?? Array.Empty<Link>(),
        Gatherings = hubs ?? Array.Empty<Gathering>(),
        Acts = Array.Empty<ActKind>(),
        Feelings = feelings ?? FeelingOptions.Off,
        Authority = new AuthorityOptions(),
        Gossip = gossip ?? new GossipOptions(),
        Body = body ?? new BodyOptions { AwakeHoursAtRest = 100_000 },
        Familiarity = familiarity ?? Array.Empty<(string, string, double)>(),
        Wander = 0,
    };

    // ---- E3: routing --------------------------------------------------------------------------

    /// <summary>Two ways from Start to Goal: two doors through a 60-tile corridor, or three doors
    /// through two small rooms, about 12 tiles in all. Fewest doors took the corridor; the walker now
    /// takes the shorter walk.</summary>
    [Fact]
    public void AWalkerTakesTheShorterWalk_NotTheFewestDoors()
    {
        var places = new[] { Room("Start", 5, 3), Room("Corridor", 60, 3), Room("A", 5, 3), Room("B", 5, 3), Room("Goal", 5, 3) };
        var links = new[]
        {
            new Link("Start", new Tile(4, 1), "Corridor", new Tile(0, 1)),
            new Link("Corridor", new Tile(59, 1), "Goal", new Tile(0, 1)),
            new Link("Start", new Tile(2, 2), "A", new Tile(0, 1)),
            new Link("A", new Tile(4, 1), "B", new Tile(0, 1)),
            new Link("B", new Tile(4, 1), "Goal", new Tile(4, 1)),
        };
        var walker = V("Wal", "W", new[] { At("Start", 2, 1, 0, Clock.At(10)), At("Goal", 2, 1, Clock.At(10)) });
        var sim = new Simulation(1, Town(new[] { walker }, places, links));
        var seen = new List<string>();
        sim.Run(1, (m, s) =>
        {
            string here = s.Where("Wal").Place;
            if (seen.Count == 0 || seen[^1] != here)
                seen.Add(here);
        });
        Assert.Equal(new[] { "Start", "A", "B", "Goal" }, seen);
    }

    // ---- E5: commutes -------------------------------------------------------------------------

    private static Simulation Commuter(int? commute, Body? body = null)
    {
        var places = new[] { Room("Home:W", 5, 3), Room("Road", 40, 3), Room("Work", 5, 3) };
        var links = new[]
        {
            new Link("Home:W", new Tile(4, 1), "Road", new Tile(0, 1)),
            new Link("Road", new Tile(39, 1), "Work", new Tile(0, 1)),
        };
        var job = new Job("Work", new Tile(2, 1), Clock.At(10), Clock.At(18), Array.Empty<int>(), 1, commute);
        return new Simulation(3, Town(new[] { V("Wor", "W", Array.Empty<Haunt>(), job, body: body) }, places, links,
            body: body is null ? null : new BodyOptions()));
    }

    [Fact]
    public void AWorkerWithALongerCommuteLeavesEarlier_AndIsntLate()
    {
        int day1 = Clock.MinutesPerDay;
        string PlaceAt(int? commute, int minute)
        {
            string at = "";
            SimResult r = Commuter(commute).Run(2, (m, s) => { if (m == minute) at = s.Where("Wor").Place; });
            Assert.DoesNotContain(r.Late, l => l.Name == "Wor");
            return at;
        }
        // The walk is about 45 tiles at 2 a minute. With 50 minutes they are on the road by 9:15;
        // with the town's 30 they leave at 9:30.
        Assert.Equal("Road", PlaceAt(50, day1 + Clock.At(9, 15)));
        Assert.Equal("Home:W", PlaceAt(null, day1 + Clock.At(9, 15)));
        Assert.Equal("Home:W", PlaceAt(50, day1 + Clock.At(9, 5)));
    }

    [Fact]
    public void TheAlarmAllowsForTheCommute()
    {
        static int FirstAlarm(int? commute)
        {
            SimResult r = Commuter(commute, new Body(100, 0.2)).Run(2);
            string line = r.Log.First(l => l.Contains(" sleep Wor ") && l.Contains(" alarm "));
            return int.Parse(line[(line.LastIndexOf(' ') + 1)..]) % Clock.MinutesPerDay;
        }
        Assert.Equal(Clock.At(9), FirstAlarm(null));          // 60 minutes before work, as before
        Assert.Equal(Clock.At(8, 40), FirstAlarm(50));        // 20 more for a 50-minute commute
        Assert.Equal(Clock.At(9, 20), FirstAlarm(10));        // and 20 fewer for a 10-minute one
    }

    // ---- E4: hubs -----------------------------------------------------------------------------

    /// <summary>Three locals stand on the green all day. At 10:00 a fete starts there that everyone
    /// would rather be at; three visitors walk over from the café.</summary>
    private static (SimResult R, Dictionary<string, int> InCrowdAfter) Fete(int capacity)
    {
        var places = new[] { Room("Green", 20, 10, outdoor: true), Room("Cafe", 6, 4) };
        var links = new[] { new Link("Green", new Tile(19, 5), "Cafe", new Tile(0, 2)) };
        var locals = Enumerable.Range(1, 3).Select(i => V("L" + i, "L" + i, new[] { At("Green", 9 + i, 5) }));
        var visitors = Enumerable.Range(1, 3).Select(i => V("V" + i, "V" + i, new[] { At("Cafe", 1 + i, 2) }));
        var fete = new Gathering("Fete", "Green", new Tile(10, 5), 3, Clock.At(10), Clock.At(18), Array.Empty<int>(), 1000, Capacity: capacity);
        var sim = new Simulation(4, Town(locals.Concat(visitors).ToList(), places, links, new[] { fete }));
        var inCrowd = new Dictionary<string, int>();
        SimResult r = sim.Run(1, (m, s) =>
        {
            if (m < Clock.At(11) || m >= Clock.At(18))
                return;
            foreach (string n in new[] { "V1", "V2", "V3" })
            {
                var w = s.Where(n);
                if (w.Place == "Green" && w.At.Chebyshev(new Tile(10, 5)) <= 3)
                    inCrowd[n] = inCrowd.GetValueOrDefault(n) + 1;
            }
        });
        return (r, inCrowd);
    }

    [Fact]
    public void AHubTurnsPeopleAwayAtItsLimit()
    {
        var (full, inFull) = Fete(capacity: 3);
        Assert.Equal(3, full.Log.Count(l => l.Contains(" turned-back ") && l.Contains(" Fete ")));
        Assert.Empty(inFull); // they went back to the café and didn't try again that day

        var (open, inOpen) = Fete(capacity: 0);
        Assert.DoesNotContain(open.Log, l => l.Contains(" turned-back "));
        Assert.Equal(3, inOpen.Count); // with no limit they join the crowd
    }

    /// <summary>Two households, each of three, with the same café to go to. The north green is the
    /// north's own; others come at a tenth of the weight.</summary>
    [Fact]
    public void ALocalHubDrawsItsOwnHouseholds()
    {
        var places = new[] { Room("NorthGreen", 12, 8, outdoor: true), Room("Cafe", 8, 4) };
        var links = new[] { new Link("NorthGreen", new Tile(11, 4), "Cafe", new Tile(0, 2)) };
        var cast = new[] { "N1", "N2", "N3", "S1", "S2", "S3" }
            .Select((n, i) => V(n, n[0] == 'N' ? "North" : "South", new[] { At("Cafe", 1 + i, 2) })).ToList();
        var green = new Gathering("Green", "NorthGreen", new Tile(5, 4), 3, Clock.At(8), Clock.At(20), Array.Empty<int>(), 1,
            Local: new[] { "North" }, Visitors: 0.1);
        var minutes = new Dictionary<char, int> { ['N'] = 0, ['S'] = 0 };
        foreach (long seed in new long[] { 5, 6, 7 }) // a week of six people is a small sample: three of them
            new Simulation(seed, Town(cast, places, links, new[] { green })).Run(7, (m, s) =>
            {
                foreach (Villager v in cast)
                    if (s.Where(v.Name).Place == "NorthGreen")
                        minutes[v.Name[0]]++;
            });
        Assert.True(minutes['N'] > 4 * minutes['S'], $"north {minutes['N']} minutes, south {minutes['S']}");
        Assert.True(minutes['S'] > 0, "visitors still come now and then");
    }

    [Fact]
    public void AHubForGrownUpsIsNeverPickedByAChild()
    {
        var places = new[] { Room("Bar", 10, 6), Room("Yard", 8, 4, outdoor: true) };
        var links = new[] { new Link("Bar", new Tile(9, 3), "Yard", new Tile(0, 2)) };
        var cast = new[] { V("Kid", "K", new[] { At("Yard", 2, 2) }, age: 12), V("Ada", "A", new[] { At("Yard", 4, 2) }, age: 40) };
        var evening = new Gathering("Evening", "Bar", new Tile(4, 3), 2, Clock.At(8), Clock.At(22), Array.Empty<int>(), 1000, MinAge: 18);
        var sim = new Simulation(6, Town(cast, places, links, new[] { evening }));
        var inBar = new Dictionary<string, int>();
        sim.Run(3, (m, s) =>
        {
            foreach (Villager v in cast)
                if (s.Where(v.Name).Place == "Bar")
                    inBar[v.Name] = inBar.GetValueOrDefault(v.Name) + 1;
        });
        Assert.False(inBar.ContainsKey("Kid"));
        Assert.True(inBar.GetValueOrDefault("Ada") > 600);
    }

    // ---- E6: familiarity seeds and forgetting -------------------------------------------------

    private static readonly string[] Apart = { "Ann", "Bea", "Cal", "Dee", "Eve", "Fay" };

    /// <summary>Six people, each in a room of their own, so nobody meets anybody.</summary>
    private static Simulation Alone(double fade, IReadOnlyList<(string, string, double)> seeds, IReadOnlyList<string>? households = null)
    {
        var places = Apart.Select(n => Room("Room" + n, 4, 3)).ToArray();
        var cast = Apart.Select((n, i) => V(n, households?[i] ?? n, new[] { At("Room" + n, 1, 1) })).ToList();
        return new Simulation(7, Town(cast, places, gossip: new GossipOptions { Forgetting = new ForgettingOptions { FadePerDay = fade } },
            familiarity: seeds));
    }

    [Fact]
    public void FamiliarityIsSeededFromTheList_BothWays()
    {
        var sim = Alone(0, new[] { ("Ann", "Bea", 0.6), ("Cal", "Dee", 0.05) });
        Assert.Equal(0.6, sim.Familiarity("Ann", "Bea"));
        Assert.Equal(0.6, sim.Familiarity("Bea", "Ann"));
        Assert.Equal(0.05, sim.Familiarity("Dee", "Cal"));
        Assert.Equal(0.25, sim.Familiarity("Ann", "Cal")); // not listed: today's seed for strangers
        Assert.Throws<ArgumentException>(() => Alone(0, new[] { ("Ann", "Nobody", 0.5) }));
    }

    [Fact]
    public void WithFadingOff_FamiliarityNeverFalls()
    {
        var sim = Alone(0, new[] { ("Ann", "Bea", 0.6) });
        sim.Run(10);
        Assert.Equal(0.6, sim.Familiarity("Ann", "Bea"));
        Assert.Equal(0.25, sim.Familiarity("Cal", "Dee"));
    }

    /// <summary>The more a tie has built, the harder it is to forget: apart for 20 days, a tie at 0.6
    /// keeps a larger share than one at 0.25, and housemates forget nothing.</summary>
    [Fact]
    public void ATieThatHasBuiltFadesSlower_AndHousematesNeverFade()
    {
        var sim = Alone(0.02, new[] { ("Ann", "Bea", 0.6), ("Cal", "Dee", 0.25), ("Eve", "Fay", 0.6) },
            new[] { "A", "B", "C", "D", "H", "H" });
        sim.Run(20);
        double strong = sim.Familiarity("Ann", "Bea") / 0.6, weak = sim.Familiarity("Cal", "Dee") / 0.25;
        Assert.True(strong < 1 && weak < 1, $"both fade: {strong:0.000}, {weak:0.000}");
        Assert.True(strong > weak, $"the built tie keeps more: {strong:0.000} against {weak:0.000}");
        Assert.Equal(0.6, sim.Familiarity("Eve", "Fay")); // housemates
    }

    /// <summary>Two strangers spend a day side by side. When one is drawn to the other (regard 0.8 to
    /// start with), they remember them faster than when they feel nothing in particular.</summary>
    [Fact]
    public void AWarmFirstMeetingSticks()
    {
        static double After(double regard)
        {
            var feelings = new FeelingOptions();
            if (regard != 0)
                feelings.Start = new Dictionary<(string, string), double> { [("Ann", "Bea")] = regard };
            var cast = new[] { V("Ann", "A", new[] { At("Room", 2, 1) }), V("Bea", "B", new[] { At("Room", 3, 1) }) };
            var sim = new Simulation(8, Town(cast, new[] { Room("Room", 6, 3) }, feelings: feelings,
                gossip: new GossipOptions { Forgetting = new ForgettingOptions { FadePerDay = 0.01 } },
                familiarity: new[] { ("Ann", "Bea", 0.02) }));
            sim.Run(1);
            return sim.Familiarity("Ann", "Bea");
        }
        double warm = After(0.8), neutral = After(0);
        Assert.True(warm > neutral * 1.3, $"warm {warm:0.000}, neutral {neutral:0.000}");
    }

    /// <summary>Xan spends every day with twelve strangers; Yul spends them alone. Both know Zed a
    /// little (0.3) and never see him. Too many new faces push Xan's weak tie out faster.</summary>
    [Fact]
    public void ManyNewFacesPushWeakTiesOut()
    {
        var crowd = Enumerable.Range(1, 12).Select(i => V("C" + i.ToString("00"), "C" + i, new[] { At("Hall", i % 8, i / 8) })).ToList();
        var cast = crowd.Concat(new[]
        {
            V("Xan", "X", new[] { At("Hall", 4, 3) }),
            V("Yul", "Y", new[] { At("Den", 1, 1) }),
            V("Zed", "Z", new[] { At("Shed", 1, 1) }),
        }).ToList();
        var seeds = crowd.Select(c => ("Xan", c.Name, 0.0)).Concat(new[] { ("Xan", "Zed", 0.3), ("Yul", "Zed", 0.3) }).ToList();
        var sim = new Simulation(9, Town(cast, new[] { Room("Hall", 10, 6), Room("Den", 4, 3), Room("Shed", 4, 3) },
            gossip: new GossipOptions { Forgetting = new ForgettingOptions { FadePerDay = 0.01, NewFacesPerWeek = 5 } },
            familiarity: seeds));
        sim.Run(10);
        double flooded = sim.Familiarity("Xan", "Zed"), calm = sim.Familiarity("Yul", "Zed");
        Assert.True(flooded < calm, $"Xan {flooded:0.0000}, Yul {calm:0.0000}");
        Assert.True(calm < 0.3); // both fade; Xan's faster
    }

    /// <summary>A new face counts once toward a week's new faces, however often it is met. Ann sees
    /// the same two strangers every morning, fewer than the three new faces a week she takes in, so
    /// her tie to Zed fades exactly as Yul's, who sees nobody. Counted by the day, the two strangers
    /// were fourteen new faces a week, and Ann's tie faded faster.</summary>
    [Fact]
    public void ANewFaceCountsOnceAWeek_HoweverOftenItIsMet()
    {
        int ten = Clock.At(10);
        var cast = new[]
        {
            V("Ann", "A", new[] { At("Hall", 2, 1) }),
            V("Sal", "S", new[] { At("Hall", 3, 1, ten, ten + 30), At("RoomS", 1, 1, weight: 0.01) }),
            V("Tod", "T", new[] { At("Hall", 4, 1, ten, ten + 30), At("RoomT", 1, 1, weight: 0.01) }),
            V("Yul", "Y", new[] { At("Den", 1, 1) }),
            V("Zed", "Z", new[] { At("Shed", 1, 1) }),
        };
        var seeds = new[] { ("Ann", "Sal", 0.0), ("Ann", "Tod", 0.0), ("Ann", "Zed", 0.3), ("Yul", "Zed", 0.3) };
        var sim = new Simulation(11, Town(cast, new[] { Room("Hall", 8, 3), Room("RoomS", 4, 3), Room("RoomT", 4, 3), Room("Den", 4, 3), Room("Shed", 4, 3) },
            gossip: new GossipOptions { Forgetting = new ForgettingOptions { FadePerDay = 0.02, NewFacesPerWeek = 3 } }, familiarity: seeds));
        sim.Run(14);
        Assert.True(sim.Familiarity("Ann", "Sal") > 0, "Ann has met Sal");
        Assert.Equal(sim.Familiarity("Yul", "Zed"), sim.Familiarity("Ann", "Zed"));
        Assert.True(sim.Familiarity("Ann", "Zed") < 0.3);
    }

    /// <summary>Both halves of a tie fade alike: each night's fade is worked out from the night's
    /// starting values, not from the half already faded.</summary>
    [Fact]
    public void BothHalvesOfATieFadeAlike()
    {
        var sim = Alone(0.05, new[] { ("Ann", "Bea", 0.3) });
        sim.Run(60);
        Assert.True(sim.Familiarity("Ann", "Bea") < 0.1);
        Assert.Equal(sim.Familiarity("Ann", "Bea"), sim.Familiarity("Bea", "Ann"));
    }

    /// <summary>T12 with forgetting on: feelings that only watch change nothing the town does,
    /// believes or remembers. Regard adds to a tie's strength only while feelings steer.</summary>
    [Fact]
    public void WithForgettingOn_FeelingsThatOnlyWatchChangeNothing()
    {
        static SimResult Run(long seed, FeelingOptions feelings) => new Simulation(seed, TownData.Default() with
        {
            Feelings = feelings,
            Gossip = new GossipOptions { Forgetting = new ForgettingOptions { FadePerDay = 0.01 } },
        }).Run(14);
        static IEnumerable<string> Beliefs(SimResult r) => r.Beliefs.OrderBy(b => b.Key, StringComparer.Ordinal)
            .SelectMany(b => b.Value.OrderBy(x => x.Key).Select(x => $"{b.Key} {x.Key} {x.Value.Actor} {x.Value.Confidence:R} {x.Value.Clarity:R} {x.Value.Source} {x.Value.GotTick}"));
        for (long seed = 1; seed <= 3; seed++)
        {
            SimResult off = Run(seed, FeelingOptions.Off), watched = Run(seed, FeelingOptions.Observe);
            // As T12 compares them (PinnedTests): watched feelings name whom an act was aimed at, off they don't.
            Assert.Equal(off.Acts.Select(a => (a.Id, a.Tick, a.Actor, a.Kind, a.Location, a.At)), watched.Acts.Select(a => (a.Id, a.Tick, a.Actor, a.Kind, a.Location, a.At)));
            Assert.Equal(Beliefs(off), Beliefs(watched));
            Assert.Equal(off.Familiarity.OrderBy(f => f.Key).Select(f => f.Value), watched.Familiarity.OrderBy(f => f.Key).Select(f => f.Value));
            Assert.NotEmpty(watched.Feelings);
        }
    }

    /// <summary>The shipped town with forgetting on at rule 5's 1% a day (update only when a
    /// deliberate change to forgetting lands); with it off, P3 holds (PinnedTests).</summary>
    [Fact]
    public void TheShippedTownWithForgettingOn_IsPinned()
    {
        TownData town = TownData.Default() with { Gossip = new GossipOptions { Forgetting = new ForgettingOptions { FadePerDay = 0.01 } } };
        string hash = Metrics.LogHash(new Simulation(1, town).Run(112));
        Assert.NotEqual("e9fd83b284f5c1b6", hash);
        Assert.Equal("0fb826867e0573b5", hash); // 2026-10-08: a new face counts once a week, and a tie fades from the night's start
    }
}
