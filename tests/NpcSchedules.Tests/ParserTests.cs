using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

public class ParserTests
{
    private static (List<SchedulePoint> Points, SchedulePoint? Spawn, string? Error) Parse(string script, string home = "SeedShop")
    {
        string[] fields = ScriptParser.SplitFields(script);
        int first = 0;
        if (fields.Length > 0 && (fields[0].Contains("GOTO") || fields[0].Contains("NOT") || fields[0].Contains("MAIL")))
            first = 1;
        return ScriptParser.ParsePoints(fields, first, home);
    }

    [Fact]
    public void ParsesBasicPoint()
    {
        var (points, _, error) = Parse("900 Town 40 20 0");
        Assert.Null(error);
        var p = Assert.Single(points);
        Assert.Equal(900, p.Time);
        Assert.Equal("Town", p.Location);
        Assert.Equal(40, p.X);
        Assert.Equal(20, p.Y);
        Assert.Equal(0, p.Facing);
    }

    [Fact]
    public void ParsesAnimationAndMessage()
    {
        var (points, _, error) = Parse("2200 SeedShop 1 9 3 abigail_sleep \"Strings\\schedules\\Abigail:Sun.000\"");
        Assert.Null(error);
        var p = Assert.Single(points);
        Assert.Equal("abigail_sleep", p.Animation);
        Assert.Contains("Strings", p.Message); // game keeps the raw substring from the opening quote on
    }

    [Fact]
    public void OmittedLocation_StaysOnPreviousMap()
    {
        var (points, _, error) = Parse("900 Town 40 20 0/1000 5 4 2");
        Assert.Null(error);
        Assert.Equal("Town", points[1].Location);
        Assert.Equal(5, points[1].X);
        Assert.Equal(4, points[1].Y);
    }

    [Fact]
    public void ArriveByTime_IsFlagged()
    {
        var (points, _, error) = Parse("a1200 Town 50 50 2");
        Assert.Null(error);
        Assert.True(points[0].ArriveBy);
        Assert.Equal(1200, points[0].Time);
    }

    [Fact]
    public void TimeZero_IsSpawnNotRoute()
    {
        var (points, spawn, error) = Parse("0 Town 2 2 2/900 Desert 10 10 2");
        Assert.Null(error);
        Assert.NotNull(spawn);
        Assert.Equal("Town", spawn!.Location);
        Assert.Single(points);
    }

    [Fact]
    public void BedPoint_UsesResolvedHome()
    {
        var (points, _, error) = Parse("900 Town 40 20 0/2200 bed", home: "Mountain");
        Assert.Null(error);
        Assert.Equal("Mountain", points[1].Location);
    }

    [Fact]
    public void BedHome_ComesFromDefaultElseSpringLastPoint()
    {
        var schedules = new Dictionary<string, string>
        {
            ["default"] = "900 SeedShop 3 4 2/2200 Mountain 1 1 3",
            ["spring"] = "900 Town 3 4 2/2200 Beach 1 1 3",
        };
        Assert.Equal("Mountain", ScriptParser.ResolveBedHome(schedules));

        var springOnly = new Dictionary<string, string> { ["spring"] = "900 Town 3 4 2/2200 Beach 1 1 3" };
        Assert.Equal("Beach", ScriptParser.ResolveBedHome(springOnly));
    }

    [Fact]
    public void GarbageTime_FailsParse()
    {
        var (_, _, error) = Parse("9x0 Town 40 20 0");
        Assert.NotNull(error);
    }

    [Fact]
    public void MissingCoordinates_FailsParse()
    {
        var (_, _, error) = Parse("900 Town");
        Assert.NotNull(error);
    }

    [Fact]
    public void FacingDefaultsToDownAndBadTokenBecomesAnimation()
    {
        // game: a non-numeric facing token is left in place and then read as the animation
        var (points, _, error) = Parse("900 Town 40 20 look");
        Assert.Null(error);
        Assert.Equal(2, points[0].Facing);
        Assert.Equal("look", points[0].Animation);
    }
}
