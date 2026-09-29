using Xunit;

namespace NpcMemory.Tests;

public class ProximityTests
{
    [Fact]
    public void WithinRadius_SameTile_IsTrue()
        => Assert.True(Proximity.WithinRadius(10, 20, 10, 20));

    [Fact]
    public void WithinRadius_ExactlyRadius_IsTrue()
    {
        Assert.True(Proximity.WithinRadius(0, 0, 8, 0));
        Assert.True(Proximity.WithinRadius(0, 0, 0, 8));
        Assert.True(Proximity.WithinRadius(0, 0, 8, 8)); // diagonal corner of the square
    }

    [Fact]
    public void WithinRadius_JustOutside_IsFalse()
    {
        Assert.False(Proximity.WithinRadius(0, 0, 9, 0));
        Assert.False(Proximity.WithinRadius(0, 0, 0, 9));
        Assert.False(Proximity.WithinRadius(0, 0, 9, 9));
    }

    [Fact]
    public void WithinRadius_CustomRadius_IsHonoured()
    {
        Assert.True(Proximity.WithinRadius(0, 0, 5, 5, radius: 6));
        Assert.False(Proximity.WithinRadius(0, 0, 5, 5, radius: 4));
    }

    [Fact]
    public void WithinRadius_NegativeOffsets_Symmetric()
    {
        Assert.True(Proximity.WithinRadius(100, 100, 92, 100)); // dx = -8
        Assert.False(Proximity.WithinRadius(100, 100, 91, 100)); // dx = -9
    }
}
