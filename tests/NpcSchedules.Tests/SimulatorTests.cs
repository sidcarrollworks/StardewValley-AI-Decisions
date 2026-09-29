using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

public class SimulatorTests
{
    [Theory]
    [InlineData(1, "Mon")]
    [InlineData(7, "Sun")]
    [InlineData(8, "Mon")]
    [InlineData(28, "Sun")]
    public void DayName_SeasonStartsOnMonday(int day, string expected)
        => Assert.Equal(expected, ScheduleSimulator.DayName(day));

    private static (string? Key, string? Reason) Pick(Dictionary<string, string> schedules, string season,
        int day, bool rainy, int hearts = 0, int seed = 1)
    {
        var sim = new ScheduleSimulator("Testy", schedules,
            new ExtractorOptions { Hearts = hearts }, new Random(seed), new List<string>());
        return (sim.PickDayKey(season, day, rainy, out string? reason), reason);
    }

    [Fact]
    public void KeyOrder_SeasonDayBeatsDayHeartsAndDay()
    {
        var schedules = new Dictionary<string, string>
        {
            ["spring_4"] = "900 Beach 1 1 2",
            ["4_2"] = "900 Forest 1 1 2",
            ["4"] = "900 Mountain 1 1 2",
            ["spring"] = TestHelpers.Base,
        };
        Assert.Equal("spring_4", Pick(schedules, "spring", 4, rainy: false, hearts: 2).Key);
    }

    [Fact]
    public void KeyOrder_DayHeartsPicksHighestNotExceedingHearts()
    {
        var schedules = new Dictionary<string, string>
        {
            ["4_2"] = "900 Forest 1 1 2",
            ["4_6"] = "900 Beach 1 1 2",
            ["spring"] = TestHelpers.Base,
        };
        Assert.Equal("4_2", Pick(schedules, "spring", 4, rainy: false, hearts: 5).Key);
        Assert.Equal("4_6", Pick(schedules, "spring", 4, rainy: false, hearts: 6).Key);
    }

    [Fact]
    public void KeyOrder_HeartsLoopStopsAtZero()
    {
        // hearts 0 -> no day_hearts match, falls to plain day key
        var schedules = new Dictionary<string, string>
        {
            ["4_2"] = "900 Forest 1 1 2",
            ["4"] = "900 Mountain 1 1 2",
            ["spring"] = TestHelpers.Base,
        };
        Assert.Equal("4", Pick(schedules, "spring", 4, rainy: false, hearts: 0).Key);
    }

    [Fact]
    public void KeyOrder_RainKeysOnlyWhenRainy()
    {
        var schedules = new Dictionary<string, string>
        {
            ["rain2"] = "900 Beach 1 1 2",
            ["rain"] = "900 Forest 1 1 2",
            ["spring"] = TestHelpers.Base,
        };
        Assert.Equal("spring", Pick(schedules, "spring", 4, rainy: false).Key);
        string? key = Pick(schedules, "spring", 4, rainy: true).Key;
        Assert.True(key is "rain2" or "rain", $"expected rain2 or rain, got {key}");
    }

    [Fact]
    public void KeyOrder_SeasonDowHeartsUsesGameDoubleDecrement()
    {
        // game tries heartLevel, heartLevel-2, ... (a `tryHearts--` in both header and body)
        var schedules = new Dictionary<string, string>
        {
            ["spring_Mon_1"] = "900 Forest 1 1 2",
            ["spring_Mon_2"] = "900 Beach 1 1 2",
            ["spring_Mon"] = "900 Mountain 1 1 2",
            ["spring"] = TestHelpers.Base,
        };
        // day 1 of spring is a Monday; hearts=2 -> tries 2 then skips to... 2-1(header).. loop body decrements: 2,0 -> matches _2
        Assert.Equal("spring_Mon_2", Pick(schedules, "spring", 1, rainy: false, hearts: 2).Key);
        // hearts=3 -> tries 3,1 -> matches _1
        Assert.Equal("spring_Mon_1", Pick(schedules, "spring", 1, rainy: false, hearts: 3).Key);
    }

    [Fact]
    public void KeyOrder_FallsThroughDowSeasonSpringDowSpring()
    {
        var schedules = new Dictionary<string, string>
        {
            ["Mon"] = "900 Town 1 1 2",
            ["spring"] = TestHelpers.Base,
        };
        Assert.Equal("Mon", Pick(schedules, "spring", 8, rainy: false).Key); // day 8 = Monday

        var seasonOnly = new Dictionary<string, string> { ["summer"] = "900 Forest 1 1 2", ["spring"] = TestHelpers.Base };
        Assert.Equal("summer", Pick(seasonOnly, "summer", 9, rainy: false).Key);

        var springDow = new Dictionary<string, string> { ["spring_Tue"] = "900 Beach 1 1 2", ["spring"] = TestHelpers.Base };
        Assert.Equal("spring_Tue", Pick(springDow, "summer", 2, rainy: false).Key);

        var onlySpring = new Dictionary<string, string> { ["spring"] = TestHelpers.Base };
        Assert.Equal("spring", Pick(onlySpring, "summer", 2, rainy: false).Key);
    }

    [Fact]
    public void KeyOrder_NoMatchMeansNoSchedule()
    {
        Assert.Null(Pick(new Dictionary<string, string>(), "spring", 4, rainy: false).Key);
    }

    [Fact]
    public void BusKey_OnlyPamWithCcVaultMail()
    {
        var schedules = new Dictionary<string, string> { ["bus"] = "900 Desert 1 1 2", ["spring"] = TestHelpers.Base };
        var sim = new ScheduleSimulator("Pam", schedules,
            new ExtractorOptions(), new Random(1), new List<string>());
        Assert.Equal("spring", sim.PickDayKey("spring", 4, false, out _));
        var withMail = new ScheduleSimulator("Pam", schedules,
            new ExtractorOptions { MailReceived = new HashSet<string> { "ccVault" } }, new Random(1), new List<string>());
        Assert.Equal("bus", withMail.PickDayKey("spring", 4, false, out _));
        // bus is Pam-only: Testy with ccVault does not use it
        var other = new ScheduleSimulator("Testy", schedules,
            new ExtractorOptions { MailReceived = new HashSet<string> { "ccVault" } }, new Random(1), new List<string>());
        Assert.Equal("spring", other.PickDayKey("spring", 4, false, out _));
    }
}
