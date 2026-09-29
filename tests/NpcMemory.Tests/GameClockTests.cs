using Xunit;

namespace NpcMemory.Tests;

/// <summary>Year-aware absolute time: ticks keep increasing across years.</summary>
public sealed class GameClockTests
{
    [Fact]
    public void YearOneMatchesTheOldYearlessTicks()
    {
        Assert.Equal(0, GameClock.AbsoluteTick(new GameTime(0, 1, 0)));
        Assert.Equal(3 * GameClock.TicksPerSeason + 27 * GameClock.TicksPerDay + 119,
            GameClock.AbsoluteTick(new GameTime(3, 28, 119, Year: 1)));
    }

    [Fact]
    public void TheNewYearComesAfterWinter28()
    {
        int lastOfYear1 = GameClock.AbsoluteTick(new GameTime(3, 28, 119, Year: 1));
        int firstOfYear2 = GameClock.AbsoluteTick(new GameTime(0, 1, 0, Year: 2));

        Assert.Equal(lastOfYear1 + 1, firstOfYear2);
        Assert.Equal(GameClock.TicksPerYear, firstOfYear2);
        Assert.Equal(1, GameClock.AgeTicks(lastOfYear1, firstOfYear2));
        Assert.Equal(1, GameClock.DaysBetween(lastOfYear1, firstOfYear2));
    }

    [Theory]
    [InlineData(0, 1, 0, 1)]
    [InlineData(2, 14, 77, 1)]
    [InlineData(3, 28, 119, 2)]
    [InlineData(1, 5, 3, 7)]
    public void FromAbsoluteTickRoundTripsAcrossYears(int season, int day, int tick, int year)
    {
        var time = new GameTime(season, day, tick, year);

        Assert.Equal(time, GameClock.FromAbsoluteTick(GameClock.AbsoluteTick(time)));
    }

    [Fact]
    public void DayIndexChangesAtSixAmNotAtAFixedAge()
    {
        int lateNight = GameClock.AbsoluteTick(new GameTime(0, 1, 115)); // 1:30am, still day 1
        int nextMorning = GameClock.AbsoluteTick(new GameTime(0, 2, 0));

        Assert.Equal(0, GameClock.DayIndex(lateNight));
        Assert.Equal(1, GameClock.DayIndex(nextMorning));
        Assert.Equal(nextMorning, GameClock.DayStartTick(1));
        Assert.Equal(GameClock.SeasonsPerYear * GameClock.DaysPerSeason, GameClock.DayIndex(GameClock.TicksPerYear));
    }
}
