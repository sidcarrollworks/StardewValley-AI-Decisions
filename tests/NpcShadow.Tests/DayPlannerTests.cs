using NpcSchedules;
using Xunit;

namespace NpcShadow.Tests;

/// <summary>
/// DayPlanner.Resolve: one NPC's region/location for every tick of one day, mirroring the
/// game's key pick plus script semantics (home fallback, point times, unmapped locations).
/// </summary>
public class DayPlannerTests
{
    private const int TicksPerDay = TimeUtils.TicksPerDay; // 120

    private static DayPlanner Planner() => new(TestHelpers.Regions());

    private static Dictionary<string, string> Schedules(params (string Key, string Script)[] entries)
    {
        var schedules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string script) in entries)
            schedules[key] = script;
        return schedules;
    }

    private static void AssertAllTicks(DayPlan plan, string region, string location)
    {
        Assert.Equal(TicksPerDay, plan.RegionByTick.Length);
        Assert.Equal(TicksPerDay, plan.LocationByTick.Length);
        Assert.All(plan.RegionByTick, r => Assert.Equal(region, r));
        Assert.All(plan.LocationByTick, l => Assert.Equal(location, l));
    }

    [Fact]
    public void Resolve_EmptySchedules_AllTicksAtHomeAndNoScheduleKey()
    {
        DayPlan plan = Planner().Resolve("Abby", new Dictionary<string, string>(), "spring", 1,
            TestHelpers.NoFriends(), 1234);

        // no key, no home to infer: the NPC stands at "" in the Other region all day
        Assert.Equal("<no schedule>", plan.KeyChain);
        Assert.Equal("", plan.HomeLocation);
        Assert.Equal(RegionMap.OtherRegion, plan.HomeRegion);
        Assert.Equal("Abby", plan.Npc);
        AssertAllTicks(plan, RegionMap.OtherRegion, "");
    }

    [Fact]
    public void Resolve_SchedulesForAnotherSeasonOnly_IsNoScheduleButKeepsInferredHome()
    {
        Dictionary<string, string> schedules = Schedules(("summer_1", "900 Town 10 10 2/1800 SeedShop 6 6 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("<no schedule>", plan.KeyChain);
        Assert.Equal("SeedShop", plan.HomeLocation); // inferred from the only key present
        Assert.Equal("Town", plan.HomeRegion);
        AssertAllTicks(plan, "Town", "SeedShop");
    }

    [Fact]
    public void Resolve_SpringDayKey_TakesEffectAtThePointsTick()
    {
        // home is Mountain (the last stop), so the 0900 Town point is a region change at tick 18
        Dictionary<string, string> schedules = Schedules(("spring_1", "900 Town 10 10 2/1800 Mountain 6 6 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("spring_1", plan.KeyChain);
        Assert.Equal("Mountain", plan.HomeLocation);
        Assert.Equal("Mountain", plan.HomeRegion);
        Assert.Equal(18, TimeUtils.TickIndex(900));
        Assert.Equal(72, TimeUtils.TickIndex(1800));

        for (int tick = 0; tick < 18; tick++)
        {
            Assert.Equal("Mountain", plan.RegionByTick[tick]);
            Assert.Equal("Mountain", plan.LocationByTick[tick]);
        }
        for (int tick = 18; tick < 72; tick++)
        {
            Assert.Equal("Town", plan.RegionByTick[tick]);
            Assert.Equal("Town", plan.LocationByTick[tick]);
        }
        for (int tick = 72; tick < TicksPerDay; tick++)
        {
            Assert.Equal("Mountain", plan.RegionByTick[tick]);
            Assert.Equal("Mountain", plan.LocationByTick[tick]);
        }
    }

    [Fact]
    public void Resolve_HomeIsInferredFromTheSpringLastStop()
    {
        Dictionary<string, string> schedules = Schedules(("spring", "1000 Mountain 5 5 2/2000 SeedShop 6 6 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("SeedShop", RoutineExtractor.InferHomeLocation(schedules)); // cross-check the helper
        Assert.Equal("SeedShop", plan.HomeLocation);
        Assert.Equal("Town", plan.HomeRegion);
        Assert.Equal("spring", plan.KeyChain);

        // before the first point the NPC is at home (SeedShop, Town); 1000 -> Mountain, 2000 -> SeedShop
        Assert.Equal("Town", plan.RegionByTick[0]);
        Assert.Equal("SeedShop", plan.LocationByTick[0]);
        Assert.Equal("Town", plan.RegionByTick[TimeUtils.TickIndex(1000) - 1]);
        Assert.Equal("Mountain", plan.RegionByTick[TimeUtils.TickIndex(1000)]);
        Assert.Equal("Town", plan.RegionByTick[TimeUtils.TickIndex(2000)]);
        Assert.Equal("SeedShop", plan.LocationByTick[TicksPerDay - 1]);
    }

    [Fact]
    public void Resolve_UnmappedLocationPoint_ResolvesToOtherRegion()
    {
        Dictionary<string, string> schedules = Schedules(("spring_1", "900 NowhereVille 1 1 2/1800 SeedShop 6 6 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("Town", plan.RegionByTick[0]);            // start: inferred home SeedShop
        Assert.Equal(RegionMap.OtherRegion, plan.RegionByTick[18]);
        Assert.Equal("NowhereVille", plan.LocationByTick[18]);
        Assert.Equal(RegionMap.OtherRegion, plan.RegionByTick[71]);
        Assert.Equal("Town", plan.RegionByTick[72]);           // back to SeedShop
    }

    [Fact]
    public void Resolve_UnmappedHomeLocation_ResolvesToOtherRegion()
    {
        Dictionary<string, string> schedules = Schedules(("spring_1", "900 Town 1 1 2/1800 NowhereVille 6 6 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("NowhereVille", plan.HomeLocation);
        Assert.Equal(RegionMap.OtherRegion, plan.HomeRegion);
        Assert.Equal(RegionMap.OtherRegion, plan.RegionByTick[0]);
        Assert.Equal("NowhereVille", plan.LocationByTick[17]);
        Assert.Equal("Town", plan.RegionByTick[18]);
        Assert.Equal(RegionMap.OtherRegion, plan.RegionByTick[72]);
    }

    [Fact]
    public void Resolve_HomeOverrideInRegionMap_WinsOverInference()
    {
        RegionMap regions = TestHelpers.Regions();
        regions.Homes["Abby"] = "Beach";

        DayPlan plan = new DayPlanner(regions).Resolve("Abby", new Dictionary<string, string>(), "spring", 1,
            TestHelpers.NoFriends(), 1234);

        Assert.Equal("Beach", plan.HomeLocation);
        Assert.Equal("Beach", plan.HomeRegion);
        Assert.Equal("<no schedule>", plan.KeyChain);
        AssertAllTicks(plan, "Beach", "Beach");
    }

    [Fact]
    public void Resolve_RainyDay_SelectsTheRainKey()
    {
        RegionMap regions = TestHelpers.Regions();
        regions.RainChance["spring"] = 1.0; // a certain-rain day
        Dictionary<string, string> schedules = Schedules(("rain", "900 Forest 5 5 2/1800 SeedShop 6 6 0"));

        DayPlan plan = new DayPlanner(regions).Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("rain", plan.KeyChain);
        Assert.Equal("Town", plan.RegionByTick[17]);
        Assert.Equal("Forest", plan.RegionByTick[18]);
        Assert.Equal("Forest", plan.RegionByTick[71]);
        Assert.Equal("Town", plan.RegionByTick[72]);
    }

    [Fact]
    public void Resolve_NoRainChance_IgnoresTheRainKey()
    {
        Dictionary<string, string> schedules = Schedules(("rain", "900 Forest 5 5 2/1800 SeedShop 6 6 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        // TestHelpers.Regions() has rain chance 0, so no key matches at all
        Assert.Equal("<no schedule>", plan.KeyChain);
        AssertAllTicks(plan, "Town", "SeedShop"); // inferred home from the (unused) rain script
    }

    [Fact]
    public void Resolve_GotoNoSchedule_StandsAtHomeWithTheKeyChain()
    {
        Dictionary<string, string> schedules = Schedules(("spring_1", "GOTO NO_SCHEDULE"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        // the key exists, so the label is the chain rather than "<no schedule>"
        Assert.Equal("spring_1", plan.KeyChain);
        Assert.Equal("", plan.HomeLocation); // nothing to infer from a command-only script
        AssertAllTicks(plan, RegionMap.OtherRegion, "");
    }

    [Fact]
    public void Resolve_UnparseableScript_StandsAtHomeWithTheKeyChain()
    {
        Dictionary<string, string> schedules = Schedules(("spring_1", "900 Town"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("spring_1", plan.KeyChain);
        Assert.Equal("Town", plan.HomeLocation);
        AssertAllTicks(plan, "Town", "Town");
    }

    [Fact]
    public void Resolve_PointOutsideTheDay_NeverApplies()
    {
        // 2600 is the end of the day (TimeUtils.TickIndex -> -1): the game never reaches it
        Dictionary<string, string> schedules = Schedules(("spring_1", "900 Mountain 1 1 2/2600 Town 1 1 2"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("spring_1", plan.KeyChain);
        Assert.Equal("Town", plan.RegionByTick[17]);   // inferred home (the unreachable last stop)
        Assert.Equal("Mountain", plan.RegionByTick[18]);
        Assert.Equal("Mountain", plan.RegionByTick[TicksPerDay - 1]);
    }

    [Fact]
    public void Resolve_FirstPointBefore610_IsClampedToTick1SoTick0StaysAtHome()
    {
        // the schedule simulator keeps the game's arrival clamp (previousTime starts at 610), so a
        // 600 point lands on tick 1 and tick 0 is still the inferred home
        Dictionary<string, string> schedules = Schedules(("spring_1", "600 Mountain 5 5 2/900 Town 40 20 0"));

        DayPlan plan = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);

        Assert.Equal("Town", plan.HomeLocation);
        Assert.Equal("Town", plan.RegionByTick[0]);
        Assert.Equal("Mountain", plan.RegionByTick[1]);
        Assert.Equal("Mountain", plan.RegionByTick[17]);
        Assert.Equal("Town", plan.RegionByTick[18]);
    }

    [Fact]
    public void Resolve_DayHeartsKey_NeedsTheFriendshipPoints()
    {
        Dictionary<string, string> schedules = Schedules(("1_2", "900 Forest 5 5 2/1800 Mountain 6 6 0"));

        DayPlan withHearts = Planner().Resolve("Abby", schedules, "spring", 1,
            new ExtractorOptions { Hearts = 2 }, 1234);
        Assert.Equal("1_2", withHearts.KeyChain);
        Assert.Equal("Forest", withHearts.RegionByTick[18]);

        DayPlan withoutHearts = Planner().Resolve("Abby", schedules, "spring", 1, TestHelpers.NoFriends(), 1234);
        Assert.Equal("<no schedule>", withoutHearts.KeyChain);
        AssertAllTicks(withoutHearts, "Mountain", "Mountain"); // inferred home from the unused key
    }
}
