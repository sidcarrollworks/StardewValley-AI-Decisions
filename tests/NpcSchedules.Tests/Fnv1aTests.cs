using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

public class Fnv1aTests
{
    [Fact]
    public void MatchesKnownVectors()
    {
        // standard FNV-1a 32-bit test vectors
        Assert.Equal(2166136261, unchecked((uint)Fnv1a.Hash32("")));
        Assert.Equal(0xE40C292C, unchecked((uint)Fnv1a.Hash32("a")));
        Assert.Equal(0xBF9CF968, unchecked((uint)Fnv1a.Hash32("foobar")));
    }

    [Fact]
    public void IsStableAcrossProcessesByConstruction()
    {
        // string.GetHashCode is randomized per process; FNV-1a is a pure function
        Assert.Equal(Fnv1a.Hash32("Pierre|save1"), Fnv1a.Hash32("Pierre|save1"));
        Assert.NotEqual(Fnv1a.Seed("a", "b"), Fnv1a.Seed("b", "a"));
    }
}
