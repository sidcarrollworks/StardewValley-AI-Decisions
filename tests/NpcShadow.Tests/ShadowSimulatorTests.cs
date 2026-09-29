using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcShadow.Tests;

/// <summary>
/// ShadowSimulator.Run: a span of simulated days, one observer, a set of subjects. Co-location
/// drives the memory layer (ledger + diary + routine belief) and the shadow log records what
/// the mod would have written. Memory persists across days, so decay ("gone the next day") and
/// pair unlock both manifest. Everything is deterministic given the seed.
/// </summary>
public class ShadowSimulatorTests
{
    private const int TicksPerDay = TimeUtils.TicksPerDay; // 120

    /// <summary>Observer schedule: spawn in the Mountain, then Town 0900-1750, home at SeedShop
    /// (Town). A first point written as 600 is clamped to 610 by the game, so co-location with
    /// Town subjects starts exactly at tick 18.</summary>
    private const string ObserverDay = "0 Mountain 5 5 2/900 Town 40 20 0/1800 SeedShop 6 6 0";

    /// <summary>Subject in Town 0610-1150 then Mountain for the rest of the day.</summary>
    private const string TownMorningThenMountain = "600 Town 40 20 0/1200 Mountain 5 5 2";

    /// <summary>Subject with two separate Town spans: 0610-1150 and 1400-2500.</summary>
    private const string TownTwoSpans = "600 Town 40 20 0/1200 Mountain 5 5 2/2000 Town 40 20 0";

    /// <summary>Subject standing in the Town location all day (time-0 spawn in Town).</summary>
    private const string TownAllDay = "0 Town 40 20 0/2000 Town 41 20 0";

    /// <summary>Subject who never leaves the Mountain: never co-located with a Town observer.</summary>
    private const string MountainAllDay = "600 Mountain 5 5 2/1800 Mountain 6 6 0";

    /// <summary>Day 1 in Town (morning), every other day in the Mountain: lets a memory decay
    /// to "gone" on day 2.</summary>
    private static Dictionary<string, string> TownOnlyOnDay1() => new()
    {
        ["spring_1"] = "600 Town 40 20 0/1200 Mountain 5 5 2",
        ["spring"] = "600 Mountain 5 5 2/1800 Mountain 6 6 0",
    };

    private static ShadowSimulator Sim() => new(TestHelpers.Regions(), "Player");

    private static ExtractorOptions Options() => TestHelpers.NoFriends();

    private static Dictionary<string, string> Spring(string script) => new() { ["spring"] = script };

    private static List<ShadowEvent> Of(ShadowLog log, string kind)
        => log.Events.Where(e => e.Kind == kind).ToList();

    private static List<ShadowEvent> SawEvents(ShadowLog log, string subject)
        => log.Events.Where(e => e.Kind == "Saw" && e.Message.StartsWith(subject + " at ", StringComparison.Ordinal)).ToList();

    private static List<string> Lines(ShadowLog log)
        => log.Events.Select(e => $"{e.Day}|{e.Tick}|{e.TimeOfDay}|{e.Actor}|{e.Kind}|{e.Message}").ToList();

    // ---- the "Saw" line: one per contiguous co-located span, at the span's start --------------

    [Fact]
    public void Run_CoLocatedSpan_LogsExactlyOneSawAtTheSpanStart()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Player", Spring(ObserverDay)); // the observer has a real schedule
        sim.AddSubject("Abby", TestHelpers.AbbySchedules());

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 1234);

        ShadowEvent saw = Assert.Single(SawEvents(log, "Abby"));
        Assert.Equal(18, saw.Tick);
        Assert.Equal(900, saw.TimeOfDay);
        Assert.Equal(1, saw.Day);
        Assert.Equal("Player", saw.Actor);
        Assert.Equal("Abby at Town (Town)", saw.Message);

        Assert.DoesNotContain(log.Events, e => e.Kind == "Saw" && e.Tick == 17);
        Assert.DoesNotContain(log.Events, e => e.Kind == "Saw" && e.Tick == 24);
        Assert.Equal(TicksPerDay - 18, sim.Beliefs["Abby"].CoPresenceTicks);
    }

    [Fact]
    public void Run_SameRegionButADifferentLocation_IsNotCoPresence()
    {
        // The audit repro: an observer standing in Town must not "see" Abby inside the SeedShop,
        // even though both are in the Town region. Only the Town span (0900-1750) counts.
        ShadowSimulator sim = Sim(); // stationary observer at the Town location
        sim.AddSubject("Abby", TestHelpers.AbbySchedules());

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 1234);

        ShadowEvent saw = Assert.Single(SawEvents(log, "Abby"));
        Assert.Equal(18, saw.Tick);
        Assert.Equal(900, saw.TimeOfDay);
        Assert.Equal("Abby at Town (Town)", saw.Message);
        Assert.Equal(72 - 18, sim.Beliefs["Abby"].CoPresenceTicks);

        // After she goes home at 1800 the memory is of Town, not the SeedShop she is really in.
        int evening = GameClock.AbsoluteTick(new GameTime(0, 1, 80));
        LedgerView view = sim.Ledger.View("Player", "Abby", evening)!;
        Assert.Equal("Town", view.Place);
        Assert.Equal(71, view.AbsoluteTick);
    }

    [Fact]
    public void Run_SeparateSpans_LogOneSawPerSpan()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Caroline", Spring(TownTwoSpans));

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 1234);

        List<ShadowEvent> saws = SawEvents(log, "Caroline");
        Assert.Equal(2, saws.Count);
        Assert.Equal(0, saws[0].Tick);   // home is Town, so co-located from tick 0
        Assert.Equal(84, saws[1].Tick);  // 1400
    }

    // ---- ledger decay and belief unlock (now multi-day) ----------------------------------------

    [Fact]
    public void Run_WithinOneDay_MemoryDecaysToLocationAndRegion()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Caroline", Spring(TownMorningThenMountain)); // co-located 0610-1150 only

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 1234);

        // last sighting at tick 35 (1150); age 12 at tick 47, age 48 at tick 83
        Assert.Contains(log.Events, e => e.Kind == "Decayed" && e.Message == "memory of Caroline decayed to Location" && e.Tick == 47);
        Assert.Contains(log.Events, e => e.Kind == "Decayed" && e.Message == "memory of Caroline decayed to Region" && e.Tick == 83);

        int last = GameClock.AbsoluteTick(new GameTime(0, 1, TicksPerDay - 1));
        Assert.Equal(LedgerDetail.Region, sim.Ledger.View("Player", "Caroline", last)!.Detail);
    }

    [Fact]
    public void Run_SecondDay_MemoryIsGoneAtTheNextMorning()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Caroline", TownOnlyOnDay1()); // Town on day 1 only, last seen at 1150

        ShadowLog log = sim.Run("spring", 1, 2, Options(), 1234);

        // Calendar-day decay: Gone at 6:00 on day 2, not 120 ticks after the sighting, and no
        // "EarlierToday" carried into the next day.
        ShadowEvent gone = Assert.Single(log.Events, e => e.Kind == "Decayed" && e.Message == "memory of Caroline decayed to Gone");
        Assert.Equal(2, gone.Day);
        Assert.Equal(0, gone.Tick);
        Assert.DoesNotContain(log.Events, e => e.Kind == "Decayed" && e.Message.EndsWith("EarlierToday") && e.Day == 2);

        int day2First = GameClock.AbsoluteTick(new GameTime(0, 2, 0));
        Assert.Equal(LedgerDetail.Gone, sim.Ledger.View("Player", "Caroline", day2First)!.Detail);
    }

    [Fact]
    public void Run_AcrossTheYearEnd_TimeKeepsMovingForward()
    {
        // The review repro: the season names wrap from winter 28 to spring 1, but the harness's
        // clock must carry on into year 2. Otherwise spring 1 reads as earlier than winter 27, the
        // old sighting looks fresh again, and new sightings are filed in year 1.
        ShadowSimulator sim = Sim();
        sim.AddSubject("Caroline", new Dictionary<string, string>
        {
            ["winter_27"] = "600 Town 40 20 0/1200 Mountain 5 5 2",
            ["spring_1"] = "600 Town 40 20 0/1200 Mountain 5 5 2",
            ["winter"] = "600 Mountain 5 5 2/1800 Mountain 6 6 0",
            ["spring"] = "600 Mountain 5 5 2/1800 Mountain 6 6 0",
        });

        ShadowLog log = sim.Run("winter", 27, 3, Options(), 1234);

        Assert.Equal(new[] { "winter day 27", "winter day 28", "spring day 1" },
            Of(log, "DayStart").Select(e => e.Message).ToArray());

        // Winter 27's sighting went Gone once, on winter 28, and the spring 1 sighting is a new one.
        Assert.Single(log.Events, e => e.Kind == "Decayed" && e.Message == "memory of Caroline decayed to Gone");
        Assert.Equal(2, SawEvents(log, "Caroline").Count);

        int lastSpring1Sighting = GameClock.AbsoluteTick(new GameTime(0, 1, 35, Year: 2)); // 1150 on spring 1, year 2
        Assert.Equal(lastSpring1Sighting, sim.Ledger.View("Player", "Caroline", lastSpring1Sighting)!.AbsoluteTick);
        Assert.All(sim.Diary.About("Caroline").Skip(1), e => Assert.True(e.AbsoluteTick >= GameClock.TicksPerYear));
    }

    [Fact]
    public void Run_TwoDaysOfCoLocation_UnlocksThePair()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Abby", Spring(TownAllDay)); // the Town location all day, every day

        ShadowLog log = sim.Run("spring", 1, 2, Options(), 42);

        // 120 co-located ticks/day, 240 total -> unlock on the last tick of day 2
        Assert.Equal(240, sim.Beliefs["Abby"].CoPresenceTicks);
        Assert.True(sim.Beliefs["Abby"].Unlocked);
        Assert.Contains(log.Events, e => e.Kind == "Unlocked" && e.Day == 2);
    }

    [Fact]
    public void Run_OneDayOfCoLocation_DoesNotUnlock()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Abby", Spring(TownAllDay));

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 42);

        Assert.Equal(TicksPerDay, sim.Beliefs["Abby"].CoPresenceTicks);
        Assert.False(sim.Beliefs["Abby"].Unlocked);
        Assert.DoesNotContain(log.Events, e => e.Kind == "Unlocked");
    }

    [Fact]
    public void Run_ResightingRefreshesButNeverLogsDecayToAFinerDetail()
    {
        // Lewis leaves Town, comes back, leaves again: the re-sighting is a "Saw", not a
        // "Decayed to NamedSpot". Decay only ever logs a COARSENING.
        ShadowSimulator sim = Sim();
        sim.AddSubject("Lewis", Spring("600 Town 40 20 0/1200 Mountain 5 5 2/1600 Town 40 20 0/2000 Mountain 5 5 2"));

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 1234);

        Assert.Equal(2, SawEvents(log, "Lewis").Count); // 0610 and 1600
        List<ShadowEvent> decays = Of(log, "Decayed").Where(e => e.Message.Contains("Lewis")).ToList();
        Assert.Equal(2, decays.Count);
        Assert.All(decays, e => Assert.Equal("memory of Lewis decayed to Location", e.Message));
        Assert.DoesNotContain(log.Events, e => e.Kind == "Decayed" && e.Message.Contains("NamedSpot"));
    }

    // ---- observer memory state after a run ---------------------------------------------------

    [Fact]
    public void Run_PopulatesTheObserversLedgerDiaryAndBeliefs()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Player", Spring(ObserverDay));
        sim.AddSubject("Abby", TestHelpers.AbbySchedules());

        sim.Run("spring", 1, 1, Options(), 1234);

        int last = GameClock.AbsoluteTick(new GameTime(0, 1, TicksPerDay - 1));
        LedgerView? view = sim.Ledger.View("Player", "Abby", last);
        Assert.NotNull(view);
        Assert.Equal("Abby", view!.Subject);
        Assert.Equal(LedgerDetail.NamedSpot, view.Detail); // still co-located at the last tick
        Assert.Equal("SeedShop", view.Place);
        Assert.Equal("6,6", view.Spot);                     // the schedule tile she stands on

        // One diary entry per co-located span (0900 in Town, still together in the SeedShop after),
        // not one per tick.
        DiaryEntry entry = Assert.Single(sim.Diary.Entries);
        Assert.Equal("Saw", entry.Kind);
        Assert.Equal("Town", entry.Detail);
        Assert.Single(sim.Diary.About("abby"));

        Assert.True(sim.Beliefs.ContainsKey("abby"));
        Assert.Equal("Abby", sim.Beliefs["Abby"].Subject);
        Assert.False(sim.Beliefs.ContainsKey("Player")); // the observer never watches itself
    }

    [Fact]
    public void Run_SubjectNeverCoLocated_LogsNoSawAndLeavesNoLedgerView()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Miner", Spring(MountainAllDay));

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 99);

        Assert.Empty(SawEvents(log, "Miner"));
        Assert.Null(sim.Ledger.View("Player", "Miner", TicksPerDay - 1));
        Assert.Empty(sim.Diary.Entries);
        Assert.True(sim.Beliefs.ContainsKey("Miner"));
        Assert.Equal(0, sim.Beliefs["Miner"].CoPresenceTicks);
    }

    // ---- the log's bookkeeping lines ----------------------------------------------------------

    [Fact]
    public void Run_LogsAStartAndADayStartPerDay()
    {
        ShadowSimulator sim = Sim();

        ShadowLog one = sim.Run("spring", 3, 1, Options(), 4242);
        Assert.Single(Of(one, "Start"));
        Assert.Single(Of(one, "DayStart"));
        Assert.Equal("spring day 3", Of(one, "DayStart")[0].Message);

        ShadowSimulator sim2 = Sim();
        ShadowLog three = sim2.Run("spring", 3, 3, Options(), 4242);
        Assert.Equal(3, Of(three, "DayStart").Count);
        Assert.Equal(new[] { "spring day 3", "spring day 4", "spring day 5" },
            Of(three, "DayStart").Select(e => e.Message).ToArray());
    }

    [Fact]
    public void Run_WrapsAcrossSeasonBoundaries()
    {
        ShadowSimulator sim = Sim();

        ShadowLog log = sim.Run("spring", 28, 2, Options(), 4242);

        Assert.Equal(new[] { "spring day 28", "summer day 1" },
            Of(log, "DayStart").Select(e => e.Message).ToArray());
    }

    [Fact]
    public void Run_ObserverHomeRegion_IsUsedWhenTheObserverHasNoSchedule()
    {
        ShadowSimulator sim = Sim();
        sim.ObserverHomeRegion = "Mountain";
        sim.ObserverHomeLocation = "Mountain";
        sim.AddSubject("Abby", TestHelpers.AbbySchedules()); // Abby stays in Town all day

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 7);

        Assert.Empty(SawEvents(log, "Abby"));
        Assert.Equal(0, sim.Beliefs["Abby"].CoPresenceTicks);
    }

    // ---- determinism and ordering -------------------------------------------------------------

    [Fact]
    public void Run_SameSeed_ProducesAnIdenticalEventSequence()
    {
        static List<string> RunOnce()
        {
            ShadowSimulator sim = Sim();
            sim.AddSubject("Player", Spring(ObserverDay));
            sim.AddSubject("Abby", TestHelpers.AbbySchedules());
            sim.AddSubject("Caroline", Spring(TownTwoSpans));
            sim.AddSubject("Miner", Spring(MountainAllDay));
            return Lines(sim.Run("spring", 1, 2, Options(), 4242));
        }

        List<string> first = RunOnce();
        List<string> second = RunOnce();
        Assert.Equal(first, second);
        Assert.NotEmpty(first);
    }

    [Fact]
    public void Run_LogIsOrderedByDayThenTick()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("Caroline", Spring(TownMorningThenMountain));

        ShadowLog log = sim.Run("spring", 1, 2, Options(), 8);

        for (int i = 1; i < log.Events.Count; i++)
        {
            var prev = log.Events[i - 1];
            var curr = log.Events[i];
            Assert.True(prev.Day < curr.Day || (prev.Day == curr.Day && prev.Tick <= curr.Tick),
                $"event {i} out of order: {prev.Day}:{prev.Tick} before {curr.Day}:{curr.Tick}");
        }
        Assert.Equal(log.RenderText().Split('\n').Length, log.Events.Count);
    }

    [Fact]
    public void Run_LookupsAndRegistrationAreCaseInsensitive()
    {
        ShadowSimulator sim = Sim();
        sim.AddSubject("player", Spring(ObserverDay)); // registered as the observer
        sim.AddSubject("ABBY", TestHelpers.AbbySchedules());

        ShadowLog log = sim.Run("spring", 1, 1, Options(), 1234);

        Assert.False(sim.Beliefs.ContainsKey("Player"));
        Assert.True(sim.Beliefs.ContainsKey("abby"));
        Assert.NotNull(sim.Ledger.View("PLAYER", "abby", TicksPerDay - 1));
        Assert.Single(SawEvents(log, "ABBY"));
    }
}
