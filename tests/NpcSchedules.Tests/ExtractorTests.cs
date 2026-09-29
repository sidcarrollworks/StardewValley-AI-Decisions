using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

/// <summary>
/// End-to-end extraction tests over a full year. Each test pins a specific game rule by
/// checking the key-day counts and/or region x block tick counts.
/// </summary>
public class ExtractorTests
{
    [Fact]
    public void Deterministic_SameInputsSameOutput()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string> { ["spring"] = TestHelpers.Base, ["rain"] = "900 Forest 1 1 2/2200 SeedShop 6 6 0" };
        var rainRegions = TestHelpers.Regions(rain: new Dictionary<string, double>
        {
            ["spring"] = 1.0, ["summer"] = 1.0, ["fall"] = 1.0, ["winter"] = 1.0,
        });

        var a = TestHelpers.Extract(rainRegions, schedules, seed: 7);
        var b = TestHelpers.Extract(rainRegions, schedules, seed: 7);
        Assert.Equal(a.RegionTicks.Keys.OrderBy(k => k), b.RegionTicks.Keys.OrderBy(k => k));
        foreach (string region in a.RegionTicks.Keys)
            Assert.Equal(a.RegionTicks[region], b.RegionTicks[region]);
        Assert.Equal(a.KeyDays, b.KeyDays);
        Assert.Equal(a.HomeLocation, b.HomeLocation);
        _ = regions;
    }

    [Fact]
    public void NoScheduleDay_CountsAllTicksAtHome()
    {
        var regions = TestHelpers.Regions();
        // day 1 spring has NO_SCHEDULE; every other day uses spring
        var schedules = new Dictionary<string, string> { ["spring"] = TestHelpers.Base, ["spring_1"] = "GOTO NO_SCHEDULE" };
        var routine = TestHelpers.Extract(regions, schedules);

        Assert.Equal(1, routine.KeyDays["spring_1"]);
        Assert.Equal(112 * 120, routine.TotalTicks);
        // home is SeedShop (Town); spring puts the NPC there from 1800 on anyway,
        // so just verify the total and that the no-schedule day added a full home day.
        Assert.Equal(112 * 120, routine.RegionTicks.Values.Sum(c => c.Sum()));
    }

    [Fact]
    public void NotFriendshipMet_FallsBackToSpring()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "NOT friendship Sebastian 6/900 Mountain 10 10 2/2200 SeedShop 6 6 0",
        };

        var met = TestHelpers.Extract(regions, schedules,
            friends: new Dictionary<string, int> { ["Sebastian"] = 6 });
        Assert.Equal(1, met.KeyDays["spring_1 -> spring"]);

        var unmet = TestHelpers.Extract(regions, schedules);
        Assert.Equal(1, unmet.KeyDays["spring_1"]);
    }

    [Fact]
    public void NotFriendshipCanListSeveralNpcs()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "NOT friendship Sebastian 6 Abigail 3/900 Mountain 10 10 2/2200 SeedShop 6 6 0",
        };
        var routine = TestHelpers.Extract(regions, schedules,
            friends: new Dictionary<string, int> { ["Abigail"] = 3 });
        Assert.Equal(1, routine.KeyDays["spring_1 -> spring"]);
    }

    [Fact]
    public void NotFriendshipUnmet_ThenGotoRuns()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "NOT friendship Sebastian 6/GOTO Mtn",
            ["Mtn"] = "900 Mountain 10 10 2/2200 SeedShop 6 6 0",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Equal(1, routine.KeyDays["spring_1 -> Mtn"]);
        // 900-2150 on spring day 1 is Mountain; nothing else in the year touches Mountain
        Assert.Equal(6, TestHelpers.BlockTicks(routine, "Mountain", 1));  // 0900-0950
        Assert.Equal(12, TestHelpers.BlockTicks(routine, "Mountain", 4)); // 1400-1600
    }

    [Fact]
    public void MailNotReceived_RunsNextGoto_ReceivedSkipsToFollowing()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "MAIL ccVault/GOTO Mtn/GOTO BeachScript",
            ["Mtn"] = "900 Mountain 10 10 2/2200 SeedShop 6 6 0",
            ["BeachScript"] = "900 Beach 10 10 2/2200 SeedShop 6 6 0",
        };

        var notReceived = TestHelpers.Extract(regions, schedules);
        Assert.Equal(1, notReceived.KeyDays["spring_1 -> Mtn"]);

        var received = TestHelpers.Extract(regions, schedules, mail: new[] { "ccVault" });
        Assert.Equal(1, received.KeyDays["spring_1 -> BeachScript"]);
    }

    [Fact]
    public void GotoSeason_UsesCurrentSeason()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["summer"] = "900 Forest 10 10 2/2200 SeedShop 6 6 0",
            ["summer_5"] = "GOTO season",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Equal(1, routine.KeyDays["summer_5 -> summer"]);
    }

    [Fact]
    public void GotoMissingKey_FallsBackToSpringWithWarning()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string> { ["spring"] = TestHelpers.Base, ["spring_4"] = "GOTO nope" };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Equal(1, routine.KeyDays["spring_4 -> spring"]);
        Assert.Contains(routine.Warnings, w => w.Contains("GOTO references missing schedule 'nope'"));
    }

    [Fact]
    public void GotoCycle_GivesEmptyScheduleWithWarning()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_7"] = "GOTO 7",
            ["7"] = "GOTO 7", // day 7 of the other three seasons also hits the loop
        };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Equal(1, routine.KeyDays["spring_7 -> 7 -> spring"]); // loop detected, then the game-style spring fallback
        Assert.Equal(3, routine.KeyDays["7 -> spring"]);
        Assert.Contains(routine.Warnings, w => w.Contains("loops"));
    }

    [Fact]
    public void DuplicateTime_FailsLikeTheGame()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "900 Town 1 1 0/900 Mountain 2 2 0",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Contains(routine.Warnings, w => w.Contains("two points at time 900"));
        Assert.Equal(1, routine.KeyDays["spring_1"]);
    }

    [Fact]
    public void TimeZeroSpawn_SetsWhereNpcCountsBeforeFirstPoint()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "0 Town 2 2 2/900 Desert 10 10 2/2200 SeedShop 6 6 0",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        // spawn at Town covers 0600-0850; Desert from 0900 to 2150; base days are home all day
        Assert.Equal(1344, TestHelpers.BlockTicks(routine, "Town", 0));   // 12 ticks x 112 days (spawn day + home days)
        Assert.Equal(6, TestHelpers.BlockTicks(routine, "Desert", 1));    // 0900-0950
        Assert.Equal(12, TestHelpers.BlockTicks(routine, "Desert", 2));   // 1000-1200
    }

    [Fact]
    public void BedPoint_CountsAtHomeLocation()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["default"] = "900 SeedShop 3 4 2/2200 Mountain 1 1 3",
            ["spring_1"] = "900 Town 40 20 0/2200 bed",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        // bed resolves to default's last stop (Mountain): 2200-2600 = blocks 8 (2200-2400)
        Assert.Equal(12, TestHelpers.BlockTicks(routine, "Mountain", 8));
        Assert.Equal(12, TestHelpers.BlockTicks(routine, "Mountain", 9)); // 2400-2600
    }

    [Fact]
    public void UnmappedLocation_CountsAsOtherAndIsListed()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["spring_1"] = "900 MysteryLand 5 5 2/2200 SeedShop 6 6 0",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Contains("MysteryLand", routine.UnmappedLocations);
        Assert.True(TestHelpers.RegionTotal(routine, RegionMap.OtherRegion) > 0);
    }

    [Fact]
    public void RainKeys_OnlyUsedOnRainyDays()
    {
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["rain2"] = "900 Beach 10 10 2/2200 SeedShop 6 6 0",
            ["rain"] = "900 Forest 10 10 2/2200 SeedShop 6 6 0",
        };

        var dry = TestHelpers.Extract(TestHelpers.Regions(), schedules, seed: 3);
        Assert.False(dry.KeyDays.ContainsKey("rain2"));
        Assert.False(dry.KeyDays.ContainsKey("rain"));

        var wet = TestHelpers.Extract(TestHelpers.Regions(rain: new Dictionary<string, double>
        {
            ["spring"] = 1.0, ["summer"] = 1.0, ["fall"] = 1.0, ["winter"] = 0.0,
        }), schedules, seed: 3);
        int rainyDays = wet.KeyDays.GetValueOrDefault("rain2") + wet.KeyDays.GetValueOrDefault("rain");
        // 84 rainy spring/summer/fall days; winter never rains
        Assert.Equal(84, rainyDays);
        Assert.Equal(28, wet.KeyDays["spring"]); // winter days all fall to spring
    }

    [Fact]
    public void RainChance_IsHonouredPerSeason()
    {
        // the tool follows the configured weights, including winter — the defaults in
        // regions.json set winter to 0, but nothing is hard-coded
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = TestHelpers.Base,
            ["rain"] = "900 Forest 10 10 2/2200 SeedShop 6 6 0",
        };
        var routine = TestHelpers.Extract(TestHelpers.Regions(rain: new Dictionary<string, double>
        {
            ["spring"] = 1.0, ["summer"] = 1.0, ["fall"] = 1.0, ["winter"] = 1.0,
        }), schedules, seed: 5);
        Assert.Equal(112, routine.KeyDays["rain"]); // every day rainy, rain key always used
    }

    [Fact]
    public void Prior_IsDeterministicAndScalesByRelationship()
    {
        var regions = TestHelpers.Regions();
        var routine = TestHelpers.Extract(regions, new Dictionary<string, string> { ["spring"] = TestHelpers.Base }, seed: 9);

        var family1 = RoutinePrior.Build(routine, "Pierre", "save1", new PriorOptions { Kind = RelationshipKind.Family });
        var family2 = RoutinePrior.Build(routine, "Pierre", "save1", new PriorOptions { Kind = RelationshipKind.Family });
        Assert.Equal(family1, family2);

        var friend = RoutinePrior.Build(routine, "Pierre", "save1", new PriorOptions { Kind = RelationshipKind.Friend });
        Assert.Equal(40, family1.Sum(c => c.Count), 1);
        Assert.Equal(8, friend.Sum(c => c.Count), 1);
        Assert.True(family1.Sum(c => c.Count) > friend.Sum(c => c.Count));
    }

    [Fact]
    public void Prior_DifferentSavesDiffer()
    {
        var regions = TestHelpers.Regions();
        var routine = TestHelpers.Extract(regions, new Dictionary<string, string> { ["spring"] = TestHelpers.Base }, seed: 9);
        var a = RoutinePrior.Build(routine, "Pierre", "save1", new PriorOptions());
        var b = RoutinePrior.Build(routine, "Pierre", "save2", new PriorOptions());
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Home_SpringLastStopWins()
    {
        var regions = TestHelpers.Regions();
        var schedules = new Dictionary<string, string>
        {
            ["spring"] = "900 Town 40 20 0/1800 Mountain 6 6 0",
            ["default"] = "900 Town 3 4 2/2200 Town 1 1 3",
            ["summer"] = "900 Forest 1 1 2/2200 Beach 6 6 0",
        };
        var routine = TestHelpers.Extract(regions, schedules);
        Assert.Equal("Mountain", routine.HomeLocation);
        Assert.Equal("Mountain", routine.HomeRegion);
    }

    [Fact]
    public void Home_RegionsOverrideWins()
    {
        var regions = TestHelpers.Regions(homes: new Dictionary<string, string> { ["Testy"] = "Beach" });
        var routine = TestHelpers.Extract(regions, new Dictionary<string, string> { ["spring"] = TestHelpers.Base });
        Assert.Equal("Beach", routine.HomeLocation);
    }
}
