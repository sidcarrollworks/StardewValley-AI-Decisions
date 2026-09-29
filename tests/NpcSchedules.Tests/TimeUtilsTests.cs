using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

public class TimeUtilsTests
{
    [Theory]
    [InlineData(600, 0)]
    [InlineData(610, 1)]
    [InlineData(700, 6)]
    [InlineData(830, 15)]
    [InlineData(1200, 36)]
    [InlineData(2400, 108)]
    [InlineData(2500, 114)]
    [InlineData(2550, 119)]
    public void TickIndex_MapsGameTimes(int time, int expectedTick)
        => Assert.Equal(expectedTick, TimeUtils.TickIndex(time));

    [Theory]
    [InlineData(550, -1)]
    [InlineData(2600, -1)]
    [InlineData(2800, -1)]
    [InlineData(655, -1)]   // minute 55: never produced by the game clock
    [InlineData(660, -1)]   // minute 60: the clock wraps to the next hour instead
    public void TickIndex_RejectsTimesOutsideDay(int time, int expected)
        => Assert.Equal(expected, TimeUtils.TickIndex(time));

    [Fact]
    public void TickIndex_HasExactly120LiveTicks()
    {
        var live = Enumerable.Range(600, 2000).Where(t => TimeUtils.TickIndex(t) >= 0).ToList();
        Assert.Equal(120, live.Count);
        Assert.Equal(600, live.First());
        Assert.Equal(2550, live.Last());
    }

    [Fact]
    public void BlockLabels_TwoHourBlocksFrom0600To2600()
    {
        for (int b = 0; b < 10; b++)
            Assert.Equal($"{(600 + b * 200):0000}", TimeUtils.BlockLabel(b, 120));
        Assert.Equal(10, TimeUtils.BlockCount(120));
        Assert.Equal(12, TimeUtils.TicksPerBlock(120));
    }

    [Fact]
    public void BlockIndex_PutsTick1200InBlock3()
    {
        // 1200 = tick 36 -> block 36/12 = 3 (10:00-12:00)
        Assert.Equal(3, TimeUtils.BlockIndex(TimeUtils.TickIndex(1200), 120));
    }
}
