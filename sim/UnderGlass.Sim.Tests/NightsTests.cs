using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The nights record (D54): one row per sleep, with the habitual bedtime and its source, when and why
/// they set out, onset, waking, energy and the next choice. The simulation only writes to it, so a run
/// is the same with it on or off; its rows agree with the run's own sleeps.
/// </summary>
public class NightsTests
{
    private const string Catalog = "returns,company,welcome,repair,sides,invitations,errands";

    private static KeptHour Kept(int hour, int min, int[] weekdays, string? source = null)
        => new(Clock.At(hour, min), weekdays, Array.Empty<int>(), source);

    /// <summary>Habits on three people, so the record's every path runs: Penny early every day (sourced in
    /// the test's terms), Shane, who keeps the Saloon's evenings, at 22:00 on weekdays (ours), and
    /// Sebastian at 00:30 after every evening, written as canon writes it (24:30; ours).</summary>
    private static TownData WithHabits(TownData town) => town with
    {
        Cast = town.Cast.Select(v => v.Name switch
        {
            "Penny" => v with
            {
                HubWeights = new Dictionary<string, OwnWeight> { ["Evening"] = new(0, "a test", "every year") },
                Bedtimes = new[] { Kept(18, 30, Array.Empty<int>(), "a test") },
            },
            "Shane" => v with { Bedtimes = new[] { Kept(22, 0, new[] { 1, 2, 3, 4, 5 }) } },
            "Sebastian" => v with { Bedtimes = new[] { Kept(24, 30, Array.Empty<int>()) } },
            _ => v,
        }).ToList(),
    };

    private static (SimResult R, NightList? Notes, TownData Town) Run(long seed, Func<TownData> make, int days, bool record)
    {
        TownData town = make();
        FeelingOptions f = town.Feelings;
        (f.Steer, f.Desire, f.DesireActs) = (true, true, true);
        f.Acts.With(Catalog);
        town = town with { Feelings = f, Acts = ActCatalog.Kinds(f.Acts, town.Acts), Cast = ActCatalog.Cards(town.Cast, f.Acts) };
        var sim = new Simulation(seed, town);
        NightList? notes = record ? new NightList() : null;
        sim.ConfigureNights(notes);
        return (sim.Run(days), notes, town);
    }

    private static readonly Lazy<(SimResult R, NightList? Notes, TownData Town)> Month = new(() => Run(6, () => WithHabits(TownData.Default()), 28, record: true));

    public static IEnumerable<object[]> Towns() => new[]
    {
        new object[] { 1L, "pelican" }, new object[] { 6L, "pelican" }, new object[] { 1L, "pelican:60@1" },
    };

    [Theory]
    [MemberData(nameof(Towns))]
    public void TheRecordIsWriteOnly_TheRunIsTheSameWithItOnOrOff(long seed, string name)
    {
        TownData Make() => WithHabits(UnderGlass.Sim.Towns.Named(name));
        var (off, _, _) = Run(seed, Make, 21, record: false);
        var (on, notes, _) = Run(seed, Make, 21, record: true);
        Assert.NotEmpty(notes!.Notes);
        Assert.Contains(notes.Notes, n => n.What == NightEvent.SetOut && n.Why == "habit");
        Assert.Equal(Metrics.LogHash(off), Metrics.LogHash(on));
        Assert.Equal(off.Sleeps, on.Sleeps);
        Assert.Equal(off.Acts, on.Acts);
        Assert.Equal(off.Invitations, on.Invitations);
        Assert.Equal(off.Favours, on.Favours);
    }

    [Fact]
    public void TheRecordingIsTheSameRunWithOrWithoutItsNights()
    {
        TownData town = WithHabits(TownData.Default());
        JsonObject with = JsonNode.Parse(Replay.Json(new ReplayOptions { Seed = 2, Days = 7, Town = town }))!.AsObject();
        JsonObject without = JsonNode.Parse(Replay.Json(new ReplayOptions { Seed = 2, Days = 7, Town = town, Nights = false }))!.AsObject();
        Assert.False(without.ContainsKey("nights"));
        JsonObject nights = with["nights"]!.AsObject();
        Assert.Equal(NightTable.Version, nights["version"]!.GetValue<int>());
        with.Remove("nights");
        Assert.Equal(without.ToJsonString(), with.ToJsonString());

        // The cards' routines: every person in name order, each datum marked sourced, ours, or sourced
        // and applied by an assumption of ours.
        var routines = nights["routines"]!.AsArray();
        Assert.Equal(town.Cast.Select(v => v.Name).Order(StringComparer.Ordinal), routines.Select(x => x!["person"]!.GetValue<string>()));
        JsonNode penny = routines.Single(x => x!["person"]!.GetValue<string>() == "Penny")!;
        JsonNode evening = Assert.Single(penny["hubWeights"]!.AsArray())!;
        Assert.Equal(("Evening", 0.0, "a test", "every year", "sourced+ours"), (evening["hub"]!.GetValue<string>(), evening["weight"]!.GetValue<double>(),
            evening["source"]!.GetValue<string>(), evening["assumption"]!.GetValue<string>(), evening["standing"]!.GetValue<string>()));
        Assert.Equal("sourced", penny["bedtimes"]![0]!["standing"]!.GetValue<string>());
        JsonNode shane = routines.Single(x => x!["person"]!.GetValue<string>() == "Shane")!;
        Assert.Equal("ours", shane["bedtimes"]![0]!["standing"]!.GetValue<string>());
        Assert.Null(routines.Single(x => x!["person"]!.GetValue<string>() == "Pam")!["bedtimes"]);
        // A row as the viewer reads it.
        JsonNode row = nights["rows"]!.AsArray().First(x => x!["person"]!.GetValue<string>() == "Penny" && x["setOutWhy"]?.GetValue<string>() == "habit")!;
        foreach (string field in new[] { "night", "habitAt", "habit", "setOutAt", "checkpointAt", "checkpointPick", "sleptAt", "sleptPlace", "sleptHow",
                     "sleptEnergy", "maxEnergy", "wokeAt", "wokeWhy", "wokeEnergy", "next", "checkpointEnergy", "checkpointOptions",
                     "setOutEnergy", "setOutOptions" })
            Assert.True(row.AsObject().ContainsKey(field), field);
    }

    [Fact]
    public void TheSinkOnlyTakes()
    {
        foreach (MethodInfo method in typeof(INightSink).GetMethods())
            Assert.Equal(typeof(void), method.ReturnType);
        Assert.Empty(typeof(INightSink).GetProperties());
        Assert.Throws<InvalidOperationException>(() =>
        {
            var sim = new Simulation(1);
            sim.Run(1);
            sim.ConfigureNights(new NightList());
        });
    }

    [Fact]
    public void TheRowsAgreeWithTheRunsSleeps()
    {
        var (r, notes, town) = Month.Value;
        var rows = NightTable.Rows(notes!.Notes, town.Cast);
        var sleeps = r.Sleeps.OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.SleptAt).ToList();
        Assert.Equal(sleeps.Count, rows.Count);
        foreach (var (s, x) in sleeps.Zip(rows))
        {
            Assert.Equal((s.Name, s.SleptAt, s.WokeAt), (x.Person, x.SleptAt, x.WokeAt));
            Assert.Equal(Math.Round(s.EnergyAtSleep, 3), x.SleptEnergy);
            Assert.Equal(s.Collapsed ? "collapse" : s.SleptAt == 0 ? "start" : "bed", x.SleptHow);
            Assert.Equal(NightTable.NightOf(s.SleptAt), x.Night);
            Villager v = town.Cast.Single(c => c.Name == s.Name);
            Assert.Equal(v.Body.MaxEnergy, x.MaxEnergy);
            // The night's habitual bedtime comes from the card, whoever sent them to bed: the kept hour of
            // the night's own day, which falls in that night, past midnight too.
            var kept = KeptHour.For(v.Bedtimes, x.Night);
            Assert.Equal(kept is { } k ? x.Night * Clock.MinutesPerDay + k.Hour.At : null, x.HabitAt);
            if (x.HabitAt is { } habitAt)
                Assert.Equal(x.Night, NightTable.NightOf(habitAt));
            // A habit's set-out names the hour the simulation found due, and the row the same one.
            if (x.SetOutWhy == "habit")
                Assert.Equal(x.HabitAt, notes.Notes.Single(n => n.Person == x.Person && n.What == NightEvent.SetOut && n.Tick == x.SetOutAt).HabitAt);
            Assert.Equal(x.CheckpointAt is null, x.CheckpointEnergy is null);
            Assert.Equal(x.CheckpointAt is null, x.CheckpointOptions is null);
            Assert.Equal(x.SetOutAt is null, x.SetOutEnergy is null);
            if (x.SetOutWhy is "body" or "held")
                Assert.Null(x.SetOutOptions); // no free-time draw sent them
            if (x.WokeAt is { } woke)
            {
                Assert.Contains(x.WokeWhy, new[] { "rested", "alarm" });
                Assert.Equal(woke + 1, x.Next!.At); // the first decision after waking is the next minute's
            }
            if (x.SetOutWhy == "habit")
                Assert.True(x.SetOutAt >= x.HabitAt);
        }
        Assert.Contains(rows, x => x.Person == "Penny" && x.SetOutWhy == "habit" && x.CheckpointPick == "bed");
        Assert.Contains(rows, x => x.Person == "Pam" && x.HabitAt is null && x.SetOutWhy == "body");
        Assert.Contains(rows, x => x.Person == "Sebastian" && x.SetOutWhy == "habit" && x.SetOutAt == x.HabitAt && Clock.OfDay(x.HabitAt!.Value) == Clock.At(0, 30));
        Assert.Contains(rows, x => x.SetOutWhy == "habit" && x.SetOutOptions!.Any(o => o.Option == "bed"));
        Assert.Contains(rows, x => x.Next?.Options is { Count: > 0 });
    }

    /// <summary>The table from notes made by hand: the run's first sleep, a habit night with its
    /// checkpoint, a waking and the choice after it, and a night still asleep when the run ended.</summary>
    [Fact]
    public void TheTableIsWorkedOutFromTheNotes()
    {
        var ann = new Villager("Ann", "Ann", "villager", new Temperament(0.5, 0.5, 0.5, 0.5), new Body(90, 0.25), null,
            Array.Empty<Haunt>(), new Dictionary<string, double>(), Array.Empty<string>(),
            Bedtimes: new[] { Kept(18, 30, new[] { 1 }, "src"), Kept(20, 0, Array.Empty<int>()) });
        int day1 = Clock.MinutesPerDay, day2 = 2 * Clock.MinutesPerDay;
        var options = new[] { new NightOption("home", 1), new NightOption("bed", 9) };
        var notes = new List<NightNote>
        {
            new(0, "Ann", NightEvent.Slept, "start", "Home:Ann", Energy: 36),
            new(400, "Ann", NightEvent.Woke, "rested", "Home:Ann", Energy: 90),
            new(401, "Ann", NightEvent.Chose, "home", "Home:Ann", Options: new[] { new NightOption("home", 1) }),
            new(day1 + Clock.At(18, 30), "Ann", NightEvent.Checkpoint, "bed", "Home:Ann", Energy: 30, HabitAt: day1 + Clock.At(18, 30), Options: options),
            new(day1 + Clock.At(18, 30), "Ann", NightEvent.SetOut, "habit", "Home:Ann", Energy: 30, HabitAt: day1 + Clock.At(18, 30), Options: options),
            new(day1 + Clock.At(18, 31), "Ann", NightEvent.Slept, "bed", "Home:Ann", Energy: 29.98765),
            new(day2 + Clock.At(3), "Ann", NightEvent.Woke, "rested", "Home:Ann", Energy: 90),
            new(day2 + Clock.At(3, 1), "Ann", NightEvent.Chose, "home", "Home:Ann", Options: new[] { new NightOption("home", 1) }),
            new(day2 + Clock.At(4), "Ann", NightEvent.Chose, "haunt", "Yard"),    // not the first after waking
            new(day2 + Clock.At(21), "Ann", NightEvent.SetOut, "body", "Home:Ann", Energy: 20, HabitAt: day2 + Clock.At(20)),
            new(day2 + Clock.At(21, 5), "Ann", NightEvent.Slept, "bed", "Home:Ann", Energy: 19.5),
        };
        var rows = NightTable.Rows(notes, new[] { ann });
        Assert.Equal(3, rows.Count);
        NightRow start = rows[0], tuesday = rows[1], wednesday = rows[2]; // day 0 is a Monday
        Assert.Equal((-1, (int?)null, "start", (string?)null), (start.Night, start.HabitAt, start.SleptHow, start.SetOutWhy));
        Assert.Equal((400, "rested", 90.0), (start.WokeAt, start.WokeWhy, start.WokeEnergy));
        Assert.Equal(new NightChoice(401, "home", "Home:Ann", null, notes[2].Options), start.Next);

        Assert.Equal(1, tuesday.Night);
        Assert.Equal((day1 + Clock.At(18, 30), (int?)0), (tuesday.HabitAt, tuesday.Habit)); // Tuesday's own hour, the first covering it
        Assert.Equal((day1 + Clock.At(18, 30), "habit"), (tuesday.SetOutAt, tuesday.SetOutWhy));
        Assert.Equal((day1 + Clock.At(18, 30), "bed"), (tuesday.CheckpointAt, tuesday.CheckpointPick));
        Assert.Equal(((double?)30.0, options, (double?)30.0, options), (tuesday.CheckpointEnergy, tuesday.CheckpointOptions, tuesday.SetOutEnergy, tuesday.SetOutOptions));
        Assert.Equal((29.988, 90.0), (tuesday.SleptEnergy, tuesday.MaxEnergy));
        Assert.Equal(day2 + Clock.At(3, 1), tuesday.Next!.At);
        Assert.Equal("home", tuesday.Next.Why);

        Assert.Equal(2, wednesday.Night);
        Assert.Equal((day2 + Clock.At(20), (int?)1, "body"), (wednesday.HabitAt, wednesday.Habit, wednesday.SetOutWhy));
        Assert.Null(wednesday.CheckpointAt);
        Assert.Equal(((double?)null, (IReadOnlyList<NightOption>?)null, (double?)20.0, (IReadOnlyList<NightOption>?)null),
            (wednesday.CheckpointEnergy, wednesday.CheckpointOptions, wednesday.SetOutEnergy, wednesday.SetOutOptions));
        Assert.Equal(((int?)null, (string?)null, (double?)null, (NightChoice?)null), (wednesday.WokeAt, wednesday.WokeWhy, wednesday.WokeEnergy, wednesday.Next)); // still asleep
    }
}
