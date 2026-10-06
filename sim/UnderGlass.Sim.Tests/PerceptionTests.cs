using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Layered perception (design rule 2; Sid, 2026-10-05).</summary>
public class PerceptionTests
{
    private static readonly PerceptionOptions O = new();
    private static Location Open(bool outdoor = false) => new("Room", outdoor, Enumerable.Repeat(new string('.', 20), 20).ToList());

    [Theory]
    [InlineData(2, 1.0)]
    [InlineData(4, 0.6)]
    [InlineData(7, 0.3)]
    [InlineData(9, 0.0)]
    public void ClarityFallsWithDistanceInBands(int distance, double clarity)
        => Assert.Equal(clarity, Perception.Instant(Open(), new Tile(1, 1), new Tile(1 + distance, 1), 600, O), 6);

    [Fact]
    public void WallsBlock_FencesHalve_NightHalvesOutdoorsOnly()
    {
        var walled = new Location("Saloon", false, new[] { "..#..", "....." });
        Assert.Equal(0, Perception.Instant(walled, new Tile(0, 0), new Tile(4, 0), 600, O));
        Assert.Equal(1.0, Perception.Instant(walled, new Tile(0, 1), new Tile(2, 1), 600, O)); // around it

        var fenced = new Location("Square", true, new[] { "..+.." });
        Assert.Equal(0.6 * 0.5, Perception.Instant(fenced, new Tile(0, 0), new Tile(4, 0), 600, O), 6);
        Assert.Equal(0.6 * 0.5 * 0.5, Perception.Instant(fenced, new Tile(0, 0), new Tile(4, 0), 1300, O), 6);
        Assert.Equal(0.6, Perception.Instant(Open(outdoor: false), new Tile(0, 0), new Tile(4, 0), 1300, O), 6);
        Assert.Equal(0.6 * 0.5 * 0.5, Perception.Instant(fenced, new Tile(0, 0), new Tile(4, 0), 3 * 60, O), 6); // 3:00 is night too
    }

    [Fact]
    public void WatchingLongerMakesItClearer_UpToTheActsReadTime()
    {
        Assert.Equal(0.3, Perception.OfAct(new[] { 0.3, 0.3 }, 2), 6);
        Assert.Equal(1.0, Perception.OfAct(new[] { 1.0, 1.0, 1.0 }, 2), 6);
        Assert.Equal(0.5, Perception.OfAct(new[] { 1.0 }, 2), 6); // one minute of an act that takes two to read
    }

    [Theory]
    [InlineData(1.0, 0.0, true)]    // a stranger, close, for a good look
    [InlineData(0.6, 0.0, false)]   // a stranger at 4 tiles: "someone"
    [InlineData(0.6, 0.5, true)]    // a friend at 4 tiles
    [InlineData(0.3, 0.25, false)]  // an acquaintance at 8 tiles: "someone"
    [InlineData(0.3, 0.9, true)]    // family who know them well, at 8 tiles
    public void WhoItWasDependsOnClarityAndHowWellTheyKnowThem(double clarity, double familiarity, bool identifies)
        => Assert.Equal(identifies, Perception.Identifies(clarity, familiarity, O));

    [Fact]
    public void OnlyTheBoldAndInsecureGuessConfidently()
    {
        Assert.True(Perception.GuessesConfidently(new Temperament(0.5, 0.8, 0.5, 0.2)));
        Assert.False(Perception.GuessesConfidently(new Temperament(0.5, 0.8, 0.5, 0.7))); // bold but secure
        Assert.False(Perception.GuessesConfidently(new Temperament(0.5, 0.3, 0.5, 0.2))); // insecure but cautious
    }

    [Fact]
    public void TheSameSeedGivesTheSameNumbers()
    {
        Assert.Equal(Rng.Unit(7, "a", "b"), Rng.Unit(7, "a", "b"));
        Assert.NotEqual(Rng.Unit(7, "a", "b"), Rng.Unit(8, "a", "b"));
        Assert.NotEqual(Rng.Hash("ab", "c"), Rng.Hash("a", "bc"));
    }
}
