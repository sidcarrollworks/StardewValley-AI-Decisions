using UnderGlass.Sim;
using UnderGlass.Sim.Generation;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Routines as character data (D54; Sid, 2026-10-14: "bedtimes are habits, not hard rules"): a habitual
/// bedtime is a weighted free-time choice from its hour until they sleep (the weight is ours), not a
/// rule; sleep starts on reaching the bed and ends when the bar is full; they stay free until asleep. A
/// person's own weight for a hub stands in for the hub's, and 0 leaves it out. Toy towns: a yard, a
/// hall, and a home for each household.
/// </summary>
public class RoutineTests
{
    private static readonly Tile YardSpot = new(6, 3), HallSpot = new(5, 3);

    private static KeptHour Kept(int hour, int min = 0, int[]? weekdays = null, int[]? seasons = null, string? source = null)
        => new(Clock.At(hour, min), weekdays ?? Array.Empty<int>(), seasons ?? Array.Empty<int>(), source);

    private static Haunt Yard(int from, int to, double weight = 1) => new("Yard", YardSpot, Clock.At(from), Clock.At(to), weight);

    private static Dictionary<string, OwnWeight> Own(params (string Hub, double Weight)[] weights)
        => weights.ToDictionary(w => w.Hub, w => new OwnWeight(w.Weight));

    private static Villager Person(string name, Body body, IReadOnlyList<Haunt> haunts, IReadOnlyList<KeptHour>? bedtimes = null,
        IReadOnlyDictionary<string, OwnWeight>? hubs = null, string? household = null)
        => new(name, household ?? name, "villager", new Temperament(0.5, 0.5, 0.5, 0.5), body, null, haunts,
            new Dictionary<string, double>(), Array.Empty<string>(), HubWeights: hubs, Bedtimes: bedtimes);

    /// <summary>A night owl: the body asks for bed only near the bar's end, so a habit's hour comes first.</summary>
    private static Body Owl => new(100, 0.05);

    private static (SimResult R, IReadOnlyList<NightRow> Rows) Run(long seed, int days, IReadOnlyList<Villager> cast, double weight = 9,
        IReadOnlyList<Gathering>? hubs = null, Action<int, Simulation>? each = null)
    {
        var households = cast.Select(v => v.Household).Distinct().ToList();
        var places = new List<Location>
        {
            new("Yard", true, Enumerable.Repeat(new string('.', 12), 8).ToList()),
            new("Hall", false, Enumerable.Repeat(new string('.', 10), 6).ToList()),
        };
        var links = new List<Link> { new("Yard", new Tile(11, 3), "Hall", new Tile(0, 3)) };
        for (int i = 0; i < households.Count; i++)
        {
            places.Add(new Location("Home:" + households[i], false, Enumerable.Repeat(new string('.', 8), 8).ToList()));
            links.Add(new Link("Yard", new Tile(0, i), "Home:" + households[i], DefaultTown.HomeDoor));
        }
        var sim = new Simulation(seed, cast, places, DefaultTown.Acts(), links: links, gatherings: hubs ?? Array.Empty<Gathering>(),
            habits: new HabitOptions { BedtimeHabitWeight = weight }, wander: 0);
        var notes = new NightList();
        sim.ConfigureNights(notes);
        SimResult r = sim.Run(days, each);
        return (r, NightTable.Rows(notes.Notes, cast));
    }

    private static IEnumerable<NightRow> Nights(IReadOnlyList<NightRow> rows, string who = "Ann")
        => rows.Where(x => x.Person == who && x.Night >= 0 && x.SleptHow == "bed");

    // ---- the habit's mechanism (a weight so large the pick is certain) --------------------------------

    [Fact]
    public void AHabitSendsThemToBedAtItsHour_OnTheDaysItCovers()
    {
        var ann = Person("Ann", Owl, new[] { Yard(8, 24) }, new[] { Kept(18, weekdays: new[] { 1, 2, 3, 4, 5 }) });
        var (_, rows) = Run(1, 21, new[] { ann }, weight: 1e9);
        var covered = Nights(rows).Where(x => x.HabitAt is not null).ToList();
        Assert.True(covered.Count >= 12, $"{covered.Count} covered nights");
        Assert.All(covered, x =>
        {
            Assert.Equal(("habit", x.HabitAt), (x.SetOutWhy, x.SetOutAt));
            Assert.Equal(Clock.At(18), Clock.OfDay(x.HabitAt!.Value));
            Assert.Contains(x.Night % Clock.DaysPerWeek, new[] { 1, 2, 3, 4, 5 });
        });
        var free = Nights(rows).Where(x => x.HabitAt is null).ToList();
        Assert.NotEmpty(free);
        Assert.All(free, x => Assert.Equal("body", x.SetOutWhy)); // on the other nights the body decides
    }

    [Fact]
    public void TheFirstKeptHourCoveringADayWins()
    {
        var ann = Person("Ann", Owl, new[] { Yard(8, 24) }, new[] { Kept(20, weekdays: new[] { 1 }), Kept(18) });
        var (_, rows) = Run(2, 21, new[] { ann }, weight: 1e9);
        var nights = Nights(rows).ToList();
        Assert.True(nights.Count >= 18);
        Assert.All(nights, x =>
        {
            bool tuesday = x.Night % Clock.DaysPerWeek == 1; // day 0 is a Monday
            Assert.Equal((int?)(tuesday ? 0 : 1), x.Habit);
            Assert.Equal(("habit", Clock.At(tuesday ? 20 : 18)), (x.SetOutWhy, Clock.OfDay(x.SetOutAt!.Value)));
        });
    }

    /// <summary>A bedtime past midnight is written as canon writes it, on its evening's day (24:00 is
    /// 1440): it covers that evening's weekday, falls in that day's night, and the night's row names the
    /// hour the habit acted at.</summary>
    [Theory]
    [InlineData(24, 0, new int[0])]       // every night at midnight
    [InlineData(24, 30, new[] { 1 })]     // Tuesday evenings: 00:30 on the Wednesday
    [InlineData(25, 10, new[] { 5, 6 })]  // Saturday and Sunday evenings: 01:10 after
    public void AHabitPastMidnightBelongsToItsEveningsNight(int hour, int min, int[] weekdays)
    {
        var ann = Person("Ann", Owl, new[] { Yard(8, 24) }, new[] { Kept(hour, min, weekdays) });
        var (_, rows) = Run(10, 28, new[] { ann }, weight: 1e9);
        var habit = Nights(rows).Where(x => x.SetOutWhy == "habit").ToList();
        Assert.True(habit.Count >= (weekdays.Length == 0 ? 20 : 3), $"{habit.Count} nights to bed by habit"); // on some nights the body comes first
        Assert.All(habit, x =>
        {
            Assert.Equal(x.HabitAt, x.SetOutAt); // the row's hour is the one the habit acted at
            Assert.Equal(x.Night * Clock.MinutesPerDay + Clock.At(hour, min), x.HabitAt);
            Assert.Equal(x.Night, NightTable.NightOf(x.HabitAt!.Value));
            if (weekdays.Length > 0)
                Assert.Contains(x.Night % Clock.DaysPerWeek, weekdays);
        });
        Assert.All(Nights(rows).Where(x => x.HabitAt is null), x => Assert.NotEqual("habit", x.SetOutWhy));
    }

    [Fact]
    public void ABodyTiredFirstGoesToBedBeforeTheHabitsHour()
    {
        var ann = Person("Ann", new Body(100, 0.35), new[] { Yard(8, 24) }, new[] { Kept(23, 30) });
        var (_, rows) = Run(3, 14, new[] { ann }, weight: 1e9);
        var nights = Nights(rows).ToList();
        Assert.True(nights.Count >= 12);
        Assert.All(nights, x =>
        {
            Assert.Equal("body", x.SetOutWhy);
            Assert.True(x.SetOutAt < x.HabitAt, $"night {x.Night}: set out {x.SetOutAt}, habit {x.HabitAt}");
            Assert.Null(x.CheckpointAt); // the body's bedtime comes first: no free-time decision after the hour
        });
    }

    /// <summary>The routine checkpoint: an afternoon haunt picked with time to spare is set aside at the
    /// hour, so the habit acts at its hour and not when the stay runs out.</summary>
    [Fact]
    public void APickInProgressIsReconsideredAtTheHour()
    {
        var atYard = new List<int>();
        var ann = Person("Ann", Owl, new[] { Yard(8, 24, 1e6) }, new[] { Kept(18) });
        var (_, rows) = Run(4, 21, new[] { ann }, weight: 1e9, each: (m, s) =>
        {
            if (Clock.OfDay(m) == Clock.At(18) - 1 && s.Where("Ann").Place == "Yard")
                atYard.Add(Clock.Day(m));
        });
        var nights = Nights(rows).ToList();
        Assert.True(nights.Count >= 18);
        Assert.All(nights, x => Assert.Equal((x.HabitAt, "bed", x.HabitAt), (x.CheckpointAt, x.CheckpointPick, x.SetOutAt)));
        Assert.True(atYard.Count >= 15, $"in the yard at 17:59 on {atYard.Count} days"); // and left it at 18:00
    }

    // ---- the energy rule: no hold ---------------------------------------------------------------------

    [Fact]
    public void SleepEndsWhenTheBarIsFull_EvenInTheSmallHours()
    {
        var o = new BodyOptions();
        var asleep = new List<bool>();
        var ann = Person("Ann", Owl, new[] { Yard(8, 24) }, new[] { Kept(18) });
        var (r, rows) = Run(5, 14, new[] { ann }, each: (m, s) => asleep.Add(s.Where("Ann").Asleep));
        double perMinute = 100 / (o.SleepHoursToFill * 60);
        var sleeps = r.Sleeps.Where(s => s.SleptAt > 0 && s.WokeAt is not null).ToList();
        Assert.True(sleeps.Count >= 12);
        foreach (Sleep s in sleeps.Where(s => s.WokeAt + 60 < asleep.Count))
        {
            int expected = (int)Math.Ceiling((100 - s.EnergyAtSleep) / perMinute);
            Assert.InRange(s.WokeAt!.Value - s.SleptAt, expected - 1, expected + 1);
            Assert.All(Enumerable.Range(s.SleptAt, s.WokeAt.Value - s.SleptAt), m => Assert.True(asleep[m]));
            Assert.All(Enumerable.Range(s.WokeAt.Value, 60), m => Assert.False(asleep[m], $"asleep again at {Clock.Format(m)}"));
        }
        Assert.True(sleeps.Count(s => Clock.OfDay(s.WokeAt!.Value) < Clock.At(6)) > sleeps.Count / 2, "most wakings before 06:00");
        Assert.All(Nights(rows).Where(x => x.WokeAt is not null), x => Assert.Equal("rested", x.WokeWhy));
        // Woken in the small hours, the habit isn't due again until the next night's hour.
        Assert.All(Nights(rows).Where(x => x.SetOutWhy == "habit"), x => Assert.True(x.SetOutAt >= x.HabitAt));
        Assert.All(Nights(rows).Where(x => x.Next is not null), x => Assert.NotEqual("bed", x.Next!.Why));
    }

    // ---- a preference, not a rule ---------------------------------------------------------------------

    /// <summary>At the default weight (9), with only home (1) to compete in the evening, most habit
    /// nights set out at the hour, and some nights they stay up a while first.</summary>
    [Fact]
    public void AHabitIsAPreference_NotARule()
    {
        var ann = Person("Ann", Owl, new[] { Yard(8, 17) }, new[] { Kept(18) });
        var (_, rows) = Run(6, 56, new[] { ann });
        var covered = Nights(rows).Where(x => x.HabitAt is not null).ToList();
        Assert.True(covered.Count >= 50, $"{covered.Count} nights");
        Assert.All(covered, x => Assert.True(x.SetOutAt >= x.HabitAt)); // never before the hour (the owl's body is later)
        Assert.All(covered, x => Assert.Equal(x.HabitAt, x.CheckpointAt));
        int onTime = covered.Count(x => x.SetOutWhy == "habit" && x.SetOutAt - x.HabitAt <= 5);
        Assert.True(onTime * 3 >= covered.Count * 2, $"{onTime} of {covered.Count} at the hour");
        Assert.Contains(covered, x => x.CheckpointPick == "home");  // the checkpoint's draw went the other way
        Assert.Contains(covered, x => x.SetOutAt - x.HabitAt > 5);  // and they stayed up past the hour
    }

    /// <summary>The schedule alone never takes anyone out of reach: from the hour until they fall asleep
    /// they are awake and where they chose to be.</summary>
    [Fact]
    public void TheyAreAwakeAndAboutUntilTheyFallAsleep()
    {
        var awake = new Dictionary<int, (bool Asleep, string Place)>();
        var ann = Person("Ann", Owl, new[] { Yard(8, 17) }, new[] { Kept(18) }, household: "Trailer");
        var pam = Person("Pam", Owl, new[] { Yard(8, 24) }, household: "Trailer");
        var (_, rows) = Run(7, 28, new[] { ann, pam }, weight: 1, each: (m, s) => awake[m] = (s.Where("Ann").Asleep, s.Where("Ann").Place));
        var late = Nights(rows).Where(x => x.HabitAt is not null && x.SleptAt - x.HabitAt > 30).ToList();
        Assert.NotEmpty(late);
        foreach (NightRow x in late)
            Assert.All(Enumerable.Range(x.HabitAt!.Value, x.SleptAt - x.HabitAt.Value), m => Assert.False(awake[m].Asleep));
    }

    /// <summary>"Actual sleep can prevent conversation; the schedule alone shouldn't" (Sid, 2026-10-14):
    /// in the shipped town with invitations on and everyone keeping 19:00 as a habit, someone past their
    /// hour and not yet asleep is still asked to have a drink, and answers.</summary>
    [Fact]
    public void AnInvitationAfterTheirHourIsStillAskedAndAnswered()
    {
        TownData town = TownData.Default();
        town = town with { Cast = town.Cast.Select(v => v with { HubWeights = null, Bedtimes = new[] { Kept(19) } }).ToList() };
        FeelingOptions f = town.Feelings;
        (f.Steer, f.Desire, f.DesireActs) = (true, true, true);
        f.Acts.With("returns,company,welcome,repair,sides,invitations");
        town = town with { Feelings = f, Acts = ActCatalog.Kinds(f.Acts, town.Acts), Cast = ActCatalog.Cards(town.Cast, f.Acts) };
        var sim = new Simulation(4, town);
        var notes = new NightList();
        sim.ConfigureNights(notes);
        SimResult r = sim.Run(3);
        var rows = NightTable.Rows(notes.Notes, town.Cast);
        Invitation asked = Assert.Single(r.Invitations, i => rows.Any(x => x.Person == i.Asked && x.HabitAt <= i.Tick && i.Tick < x.SleptAt));
        string line = $"{asked.Tick} ask {asked.Asked} Invitation {asked.Asker} act {asked.ActId} {(asked.Accepted ? "yes" : "no")} margin";
        Assert.Contains(r.Log, l => l.StartsWith(line, StringComparison.Ordinal)); // asked, and answered
        Assert.DoesNotContain(r.Sleeps, s => s.Name == asked.Asked && s.SleptAt <= asked.Tick && (s.WokeAt ?? int.MaxValue) > asked.Tick); // awake
    }

    // ---- a person's own hub weight --------------------------------------------------------------------

    private static Gathering Hub(string name, double weight, IReadOnlyList<string>? local = null, double visitors = 1)
        => new(name, "Hall", HallSpot, 1, Clock.At(10), Clock.At(22), Array.Empty<int>(), weight, Local: local, Visitors: visitors);

    [Fact]
    public void AnOwnWeightOf0LeavesTheHubOut_AndAnyOtherStandsInForTheHubs()
    {
        var inHall = new Dictionary<string, int> { ["Ann"] = 0, ["Bob"] = 0, ["Cat"] = 0 };
        var cast = new[]
        {
            Person("Ann", Owl, new[] { Yard(8, 24) }, hubs: Own(("Evening", 0))),
            Person("Bob", Owl, new[] { Yard(8, 24) }),
            Person("Cat", Owl, new[] { Yard(8, 24) }, hubs: Own(("Party", 5))),
        };
        var hubs = new[] { Hub("Evening", 2), Hub("Party", 100, local: new[] { "Nobody" }, visitors: 0) };
        Run(8, 7, cast, hubs: hubs, each: (m, s) =>
        {
            foreach (string n in inHall.Keys.ToList())
                if (s.Where(n).Place == "Hall")
                    inHall[n]++;
        });
        Assert.Equal(0, inHall["Ann"]);  // nothing else leads to the hall: her 0 kept her out of it
        Assert.True(inHall["Bob"] > 0);  // the evening's own pull (the party draws him at 100 x 0)
        Assert.True(inHall["Cat"] > inHall["Bob"] * 5 / 4, $"Cat {inHall["Cat"]}, Bob {inHall["Bob"]}"); // her own 5 for the party, on top
    }

    [Fact]
    public void WithoutOwnWeightsOrBedtimesNothingChanges()
    {
        var plain = Person("Ann", Owl, new[] { Yard(8, 24) });
        var empty = plain with { HubWeights = Own(), Bedtimes = Array.Empty<KeptHour>() };
        var hubs = new[] { Hub("Evening", 2) };
        Assert.Equal(Metrics.LogHash(Run(9, 7, new[] { plain }, hubs: hubs).R), Metrics.LogHash(Run(9, 7, new[] { empty }, hubs: hubs).R));
    }

    // ---- the town as data: written, read, checked -----------------------------------------------------

    private static TownData WithRoutine(TownData town, string who, IReadOnlyDictionary<string, OwnWeight>? hubs, IReadOnlyList<KeptHour>? bedtimes)
        => town with { Cast = town.Cast.Select(v => v.Name == who ? v with { HubWeights = hubs, Bedtimes = bedtimes } : v).ToList() };

    private static readonly KeptHour[] TwoHours =
    {
        Kept(18, 30, new[] { 1, 2, 4 }, source: "fixtures/game/Penny.json: Tue"),
        Kept(21, seasons: new[] { 1 }),
        Kept(19, seasons: new[] { 3 }, source: "a source") with { Assumption = "every winter" },
    };

    [Fact]
    public void OwnWeightsAndBedtimesAreWrittenAndReadBack()
    {
        var hubs = new Dictionary<string, OwnWeight> { ["Evening"] = new(0, "a fixture", "never"), ["Noon"] = new(2) };
        TownData town = WithRoutine(TownData.Default(), "Penny", hubs, TwoHours);
        string json = TownJson.Write(town);
        TownData back = TownJson.Read(json);
        Assert.Equal(json, TownJson.Write(back));
        Villager penny = back.Cast.Single(v => v.Name == "Penny");
        Assert.Equal(new OwnWeight(0, "a fixture", "never"), penny.HubWeights!["Evening"]);
        Assert.Equal(new OwnWeight(2), penny.HubWeights!["Noon"]);
        Assert.Equal(new[] { "fixtures/game/Penny.json: Tue", null, "a source" }, penny.Bedtimes!.Select(k => k.Source));
        Assert.Equal(new[] { null, null, "every winter" }, penny.Bedtimes!.Select(k => k.Assumption));
        Assert.Equal(new[] { 1, 2, 4 }, penny.Bedtimes![0].Weekdays);
        Assert.Equal(Census.Hash(town), Census.Hash(back));
        Assert.Equal(Metrics.LogHash(new Simulation(3, town).Run(3)), Metrics.LogHash(new Simulation(3, back).Run(3)));
        Assert.NotEqual(Census.Hash(TownData.Default()), Census.Hash(town)); // a routine is part of what a town is
        // A card without them leaves the canonical form (and the hash) as it was.
        string plain = TownJson.Canonical(UnderGlass.Sim.Towns.Named("pelican-as-shipped"));
        Assert.DoesNotContain("\"HubWeights\"", plain);
        Assert.DoesNotContain("\"Bedtimes\"", plain);
        Assert.DoesNotContain("\"BedtimeHabitWeight\"", plain);
    }

    [Fact]
    public void AFileWrittenBeforeRoutinesIsRefused()
    {
        string json = TownJson.Write(WithRoutine(TownData.Default(), "Penny", Own(("Evening", 0)), TwoHours));
        static string Edit(string json, Action<System.Text.Json.Nodes.JsonObject> edit)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            edit(node);
            return node.ToJsonString();
        }
        int penny = TownData.Default().Cast.Select((v, i) => (v, i)).Single(x => x.v.Name == "Penny").i;
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Cast"]![0]!.AsObject().Remove("HubWeights"))));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Cast"]![0]!.AsObject().Remove("Bedtimes"))));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Cast"]![penny]!["Bedtimes"]![1]!.AsObject().Remove("Source"))));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Cast"]![penny]!["Bedtimes"]![1]!.AsObject().Remove("Assumption"))));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Cast"]![penny]!["HubWeights"]!["Evening"]!.AsObject().Remove("Source"))));
        Assert.Throws<System.Text.Json.JsonException>(() => TownJson.Read(Edit(json, o => o["Habits"]!.AsObject().Remove("BedtimeHabitWeight"))));
        TownJson.Read(json); // as written, it reads
    }

    [Fact]
    public void TownCheckFindsABadRoutine()
    {
        IReadOnlyList<string> Problems(IReadOnlyDictionary<string, OwnWeight>? hubs, params KeptHour[] bedtimes)
            => TownCheck.Problems(WithRoutine(TownData.Default(), "Penny", hubs, bedtimes));
        Assert.Empty(Problems(new Dictionary<string, OwnWeight> { ["Evening"] = new(0, "a fixture", "never"), ["Noon"] = new(3) }, TwoHours));
        Assert.Contains("Penny: own weight for hub Ball, which the town doesn't have", Problems(Own(("Ball", 1))));
        Assert.Contains("Penny: own weight -1 for hub Evening is not a number of 0 or more", Problems(Own(("Evening", -1))));
        Assert.Contains("Penny: own weight NaN for hub Evening is not a number of 0 or more", Problems(Own(("Evening", double.NaN))));
        Assert.Contains("Penny: own weight for hub Evening is blank", Problems(new Dictionary<string, OwnWeight> { ["Evening"] = null! }));
        Assert.Contains("Penny: own weight for hub Evening has a blank source", Problems(new Dictionary<string, OwnWeight> { ["Evening"] = new(0, "") }));
        Assert.Contains("Penny: own weight for hub Evening has a blank assumption", Problems(new Dictionary<string, OwnWeight> { ["Evening"] = new(0, "a fixture", " ") }));
        const string range = "is not a minute from noon of its evening's day to the next noon (720-2159)";
        Assert.Contains($"Penny: kept bedtime 0 at 2160 {range}", Problems(null, new KeptHour(2160, Array.Empty<int>(), Array.Empty<int>())));
        Assert.Contains($"Penny: kept bedtime 0 at 30 {range}", Problems(null, new KeptHour(30, Array.Empty<int>(), Array.Empty<int>())));
        Assert.Empty(Problems(null, Kept(24, 40, new[] { 5 }), Kept(12, weekdays: new[] { 0 }), Kept(35, 59, new[] { 1 }))); // canon's 2440; the range's ends
        Assert.Contains("Penny: kept bedtime 0 has no weekday or season list", Problems(null, new KeptHour(Clock.At(20), null!, Array.Empty<int>())));
        Assert.Contains("Penny: kept bedtime 0 on weekday 7, not 0-6", Problems(null, Kept(18, weekdays: new[] { 1, 7 })));
        Assert.Contains("Penny: kept bedtime 0 in season 4, not 0-3", Problems(null, Kept(18, seasons: new[] { 4 })));
        Assert.Contains("Penny: kept bedtime 0 has a blank source", Problems(null, Kept(18, source: " ")));
        Assert.Contains("Penny: kept bedtime 0 has a blank assumption", Problems(null, Kept(18, source: "a fixture") with { Assumption = "" }));
        // The all-seasons hour placed before the summer one hides it.
        Assert.Contains("Penny: kept bedtime 1 is never used: an earlier one covers all its days",
            Problems(null, Kept(18, 40, new[] { 0, 3, 6 }), Kept(21, 0, new[] { 0, 3, 6 }, new[] { 1 })));
        Assert.Empty(Problems(null, Kept(21, 0, new[] { 0, 3, 6 }, new[] { 1 }), Kept(18, 40, new[] { 0, 3, 6 })));
    }

    // ---- Penny's sourced routine in the shipped town (D54) ---------------------------------------------

    [Fact]
    public void PennyCarriesHerSourcedRoutine_AndTheComparisonTownsLeaveItOut()
    {
        Villager penny = TownData.Default().Cast.Single(v => v.Name == "Penny");
        OwnWeight evening = Assert.Single(penny.HubWeights!, x => x.Key == "Evening").Value;
        Assert.Equal(0, evening.Weight);
        Assert.StartsWith("fixtures/game/Penny.json: ", evening.Source);
        Assert.Equal("sourced+ours", NightTable.Standing(evening.Source, evening.Assumption)); // "never" is ours: no green rain
        Assert.Single(penny.HubWeights!);
        Assert.Equal(new[] { Clock.At(18, 30), Clock.At(20), Clock.At(21), Clock.At(18, 40), Clock.At(18, 40) }, penny.Bedtimes!.Select(k => k.At));
        Assert.All(penny.Bedtimes!, k => Assert.StartsWith("fixtures/game/Penny.json: ", k.Source));
        // Sourced, but for 18:40 in every winter, which is ours: the sim has no Community Center to restore.
        Assert.Equal(new[] { "sourced", "sourced", "sourced", "sourced", "sourced+ours" }, penny.Bedtimes!.Select(k => NightTable.Standing(k.Source, k.Assumption)));
        Assert.Equal(new[] { 3 }, penny.Bedtimes![4].Seasons);
        // Built afresh for every cast: no two towns share the objects.
        Villager again = TownData.Default().Cast.Single(v => v.Name == "Penny");
        Assert.NotSame(penny.HubWeights, again.HubWeights);
        Assert.NotSame(penny.Bedtimes, again.Bedtimes);
        Assert.NotSame(penny.Bedtimes![0].Weekdays, again.Bedtimes![0].Weekdays);
        Assert.Equal(Census.Hash(TownData.Default()), Census.Hash(TownData.Default()));
        Assert.All(TownData.Default().Cast.Where(v => v.Name != "Penny"), v => Assert.True(v.HubWeights is null && v.Bedtimes is null, v.Name));
        // Her four hours cover every day of the year, each day by the hour her game gives it.
        int[] expected = { Clock.At(18, 40), Clock.At(18, 30), Clock.At(18, 30), Clock.At(18, 40), Clock.At(18, 30), Clock.At(20), Clock.At(18, 40) };
        for (int day = 0; day < 4 * Clock.DaysPerSeason; day++)
        {
            int weekday = day % Clock.DaysPerWeek;
            bool summerEvening = Calendar.Season(day) == 1 && weekday is 0 or 3 or 6;
            Assert.Equal(summerEvening ? Clock.At(21) : expected[weekday], KeptHour.For(penny.Bedtimes, day)!.Value.Hour.At);
        }

        Villager simulated = UnderGlass.Sim.Towns.Named("pelican-simulated-bedtime").Cast.Single(v => v.Name == "Penny");
        Assert.Equal(penny.HubWeights, simulated.HubWeights);
        Assert.Null(simulated.Bedtimes);
        string shipped = TownJson.Canonical(UnderGlass.Sim.Towns.Named("pelican-as-shipped"));
        Assert.DoesNotContain("\"HubWeights\"", shipped);
        Assert.DoesNotContain("\"Bedtimes\"", shipped);
        foreach (string name in new[] { "pelican", "pelican-simulated-bedtime", "pelican-as-shipped" })
            Assert.Empty(TownCheck.Problems(UnderGlass.Sim.Towns.Named(name)));
    }

    /// <summary>The town as it shipped before D54 is kept, run hash for run hash (census hashes moved
    /// with the generator's version): PinnedTests' values from before D54 on its cast, from phase 0a's
    /// week to the act catalog's widest pin, and the 31 town with every card's routine left out.</summary>
    [Theory]
    [InlineData("P3: a year of seed 1, as shipped", "e9fd83b284f5c1b6")]
    [InlineData("0a: a week of seed 42, feelings off", "e7f6653087ff7e18")]
    [InlineData("0a: seed 7's placed scandal, feelings off", "388d128d7fd4cdf2")]
    [InlineData("P1: phase 0c's town", "c0488cc6bf81e64f")]
    [InlineData("the catalog through late, bcdefghmt", "7701a5a8af63b3f4")]
    [InlineData("pelican31 without routines", "db268a57b95bada2")]
    public void TheTownAsShippedRunsAsItDidBefore(string run, string hash)
    {
        TownData town = UnderGlass.Sim.Towns.Named("pelican-as-shipped");
        IReadOnlyList<Villager> cast = town.Cast;
        SimResult r;
        switch (run)
        {
            case "P3: a year of seed 1, as shipped":
                r = new Simulation(1, town).Run(112);
                break;
            case "0a: a week of seed 42, feelings off":
                r = new Simulation(42, town with { Feelings = FeelingOptions.Off }).Run(7);
                break;
            case "0a: seed 7's placed scandal, feelings off":
                r = new Simulation(7, cast: cast, scheduled: new[] { Harness.ScandalFor(7, DefaultTown.Acts()) }, feelings: FeelingOptions.Off).Run(14);
                break;
            case "P1: phase 0c's town":
                FeelingOptions c = DefaultTown.Feelings();
                c.Desire = false;
                c.Start = DefaultTown.Tensions(0);
                r = new Simulation(1, cast: cast, feelings: c).Run(112);
                break;
            case "the catalog through late, bcdefghmt":
                FeelingOptions o = DefaultTown.Feelings().With0d6("bcdefghmt");
                o.Acts.With("returns,company,welcome,repair,sides,late");
                r = new Simulation(1, cast: ActCatalog.Cards(cast, o.Acts), kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(112);
                break;
            default:
                TownData p31 = UnderGlass.Sim.Towns.Pelican31();
                r = new Simulation(1, p31 with { Cast = p31.Cast.Select(v => v with { HubWeights = null, Bedtimes = null }).ToList() }).Run(112);
                break;
        }
        Assert.Equal(hash, Metrics.LogHash(r));
    }

    /// <summary>A year of the shipped town: the Saloon's evening gathering is never among Penny's options
    /// (how she chooses, not where she ends up: an errand may still take her to the Saloon), she never
    /// sets out by habit before its hour and mostly at it, and wakes rested or to her alarm. Rates beyond
    /// these loose bounds belong in the experiment note.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PennyKeepsHerHabits_AndTheEnergyRuleKeepsHerHours(long seed)
    {
        var sim = new Simulation(seed);
        var notes = new NightList();
        sim.ConfigureNights(notes);
        sim.Run(112);
        var drawn = notes.Notes.Where(n => n.Person == "Penny" && n.Options is not null).ToList();
        Assert.Contains(drawn, n => n.What == NightEvent.Checkpoint && Clock.OfDay(n.Tick) is >= 18 * 60 and < 23 * 60); // drawn while the evening is on
        Assert.DoesNotContain(drawn, n => n.Options!.Any(o => o.Option == "hub:Evening"));
        Assert.DoesNotContain(notes.Notes, n => n.Person == "Penny" && (n.Hub == "Evening" || n.Why == "hub:Evening"));
        var rows = NightTable.Rows(notes.Notes, DefaultTown.Cast()).Where(x => x.Person == "Penny" && x.Night >= 0).ToList();
        var habit = rows.Where(x => x.SetOutWhy == "habit").ToList();
        Assert.True(habit.Count >= 56, $"{habit.Count} nights to bed by habit");
        Assert.All(habit, x => Assert.True(x.SetOutAt >= x.HabitAt, $"night {x.Night}: set out {x.SetOutAt}, habit {x.HabitAt}"));
        int onTime = habit.Count(x => x.SetOutAt - x.HabitAt <= 5);
        Assert.True(onTime * 3 >= habit.Count * 2, $"{onTime} of {habit.Count} at the hour");
        Assert.All(rows.Where(x => x.WokeAt is not null), x => Assert.Contains(x.WokeWhy, new[] { "rested", "alarm" }));
    }

    /// <summary>The check above can fail: with her own weight left out, the same habit checkpoints draw
    /// from the evening gathering too.</summary>
    [Fact]
    public void WithoutHerOwnWeightTheEveningIsAmongHerOptions()
    {
        TownData town = TownData.Default();
        town = town with { Cast = town.Cast.Select(v => v.Name == "Penny" ? v with { HubWeights = null } : v).ToList() };
        var sim = new Simulation(1, town);
        var notes = new NightList();
        sim.ConfigureNights(notes);
        sim.Run(14);
        Assert.Contains(notes.Notes, n => n.Person == "Penny" && n.What == NightEvent.Checkpoint && n.Options!.Any(o => o.Option == "hub:Evening"));
    }
}
