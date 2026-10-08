using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The act catalog's story measures (acts spec 7.3): threads, feud lengths and the
/// per-person view count what they say on acts and spells made up to have a known answer, and on a
/// real year they agree with the engine's own counts and with the measures that already existed.</summary>
public class StoryMetricsTests
{
    private static Act A(int id, int day, string actor, string? target, string kind = "Argued", int about = -1, bool injected = false)
        => new(id, day * Clock.MinutesPerDay + 600, actor, kind, "Square", new Tile(0, 0), injected, target, about);

    private static readonly Dictionary<string, string> Homes = new()
    {
        ["Sam"] = "SamHouse", ["Jodi"] = "SamHouse", ["Shane"] = "Ranch", ["Marnie"] = "Ranch", ["Pam"] = "Trailer",
    };

    private static IReadOnlyList<StoryThread> Threads(params Act[] acts) => StoryMetrics.Threads(acts, n => Homes.GetValueOrDefault(n));

    [Fact]
    public void AChainThreeDeepAcrossTwoHouseholdsIsAThread()
    {
        // Sam argues with Shane, Shane answers, Sam answers back: a thread, landed on day 4.
        var t = Assert.Single(Threads(A(0, 1, "Sam", "Shane"), A(1, 2, "Shane", "Sam", about: 0), A(2, 4, "Sam", "Shane", about: 1)));
        Assert.Equal((0, 3, 3, 2, 4), (t.Root, t.Acts, t.Depth, t.Households, t.Day));
        Assert.Equal("Argued > Argued > Argued", t.Shape);

        // Two deep is not a thread, and two answers to one act are not a chain.
        Assert.Empty(Threads(A(0, 1, "Sam", "Shane"), A(1, 2, "Shane", "Sam", about: 0)));
        Assert.Empty(Threads(A(0, 1, "Sam", "Shane"), A(1, 2, "Shane", "Sam", about: 0), A(2, 2, "Marnie", "Sam", about: 0)));

        // One household is a family's story, not the town's.
        Assert.Empty(Threads(A(0, 1, "Sam", "Jodi"), A(1, 2, "Jodi", "Sam", about: 0), A(2, 3, "Sam", "Jodi", about: 1)));

        // The harness's placed act and what follows it are left out.
        Assert.Empty(Threads(A(0, 1, "Pam", null, "Stole", injected: true), A(1, 2, "Shane", "Pam", about: 0), A(2, 3, "Pam", "Shane", about: 1)));
    }

    [Fact]
    public void AThreadIsOneTreeWithItsDepthAndSize()
    {
        // A scandal, a warning for it, and two chains of answers: one thread of six acts, five deep,
        // landed when its first chain reached three.
        var t = Assert.Single(Threads(
            A(0, 1, "Pam", null, "Stole"),
            A(1, 2, "Shane", "Pam", about: 0),
            A(2, 3, "Pam", "Shane", about: 1),
            A(3, 3, "Marnie", "Pam", about: 0),
            A(4, 5, "Shane", "Pam", about: 2),
            A(5, 6, "Pam", "Shane", about: 4),
            A(6, 9, "Sam", "Jodi"))); // apart, and alone
        Assert.Equal((0, 6, 5, 2, 3), (t.Root, t.Acts, t.Depth, t.Households, t.Day));
        Assert.Equal("Stole > Argued > Argued", t.Shape);
    }

    [Fact]
    public void TheMedianFeudCountsFeudsStillOnAsLastingAtLeastThatLong()
    {
        Assert.Equal(3, StoryMetrics.MedianDays(new[] { (1, true), (2, true), (3, true), (4, true), (5, true) }));
        // After day 5 three of four are on; after day 10, half.
        Assert.Equal(10, StoryMetrics.MedianDays(new[] { (5, true), (10, true), (15, false), (20, true) }));
        // Two of three were still on when the runs ended: past every ended one.
        Assert.True(double.IsPositiveInfinity(StoryMetrics.MedianDays(new[] { (10, true), (20, false), (30, false) })));
        Assert.True(double.IsNaN(StoryMetrics.MedianDays(Array.Empty<(int, bool)>())));
    }

    [Fact]
    public void SpellsAWeekApartOrLessAreOneFeud()
    {
        var spells = new[]
        {
            new FeudSpell("Sam", "Shane", 10, 15, false, false),
            new FeudSpell("Sam", "Shane", 18, 40, false, false), // three days' pause: the same feud
            new FeudSpell("Sam", "Shane", 60, -1, false, false), // twenty days: a new one, still on
            new FeudSpell("Pam", "Shane", 5, 6, false, false),
        };
        Assert.Equal(new[] { (1, true), (30, true), (52, false) }, StoryMetrics.Feuds(spells, 112));
        Assert.Equal(52, spells[2].Days(112));
    }

    [Fact]
    public void AYearsStoryAgreesWithTheEngineAndTheOtherMeasures()
    {
        FeelingOptions o = DefaultTown.Feelings();
        var runs = new[] { 1L, 2L }.Select(s => new Simulation(s, feelings: o).Run(112)).ToList();
        IReadOnlyList<ActKind> kinds = DefaultTown.Acts();
        IReadOnlyList<Villager> cast = DefaultTown.Cast();
        StoryStats s = StoryMetrics.Summarise(runs, kinds, cast, o);

        foreach (SimResult r in runs)
        {
            // Every new feud the engine noted starts a spell that night, and spells are in order.
            foreach (var t in r.Ties.Where(t => t.What is "feud" or "kin-feud"))
                Assert.Contains(r.FeudSpells, x => x.A == t.A && x.B == t.B && x.From == t.Day && !x.Seeded);
            Assert.All(r.FeudSpells, x => Assert.True(x.To < 0 || x.To > x.From));
            Assert.Equal(r.FeudSpells.OrderBy(x => x.From).ThenBy(x => x.A, StringComparer.Ordinal).ThenBy(x => x.B, StringComparer.Ordinal), r.FeudSpells);
        }

        // Feuds and friendships per person are the variety measures' story events, which are the ties.
        double personYears = runs.Sum(r => r.CastSize) * 1.0;
        Assert.Equal(runs.Sum(r => r.Ties.Count(t => t.What == "feud")), s.FeudsPer100 * personYears / 100, 6);
        Assert.Equal(runs.Sum(r => r.Ties.Count(t => t.What == "friendship")), s.FriendshipsPer100 * personYears / 100, 6);
        Assert.Equal(2 * runs.Sum(r => r.Ties.Count(t => t.What == "feud")), s.People.Sum(p => p.Feuds) * s.SeedYears, 6); // two people a feud

        // Acts done add up to the town's acts; kindness returned is the gate's measure on the shipped rows.
        Assert.Equal(runs.Sum(r => r.Acts.Count(a => !a.Injected)) / 2.0, s.People.Sum(p => p.Did), 6);
        Assert.Equal(DesireMetrics.Summarise(runs, cast, o).ReturnedWithin7, s.ReturnedWithin7, 9);
        Assert.Equal(0, s.WarmthPerPerson); // no light kind act ships
        Assert.Equal(new[] { "Argued", "GaveGift" }, s.Outcomes.Where(k => k.Settled > 100).Select(k => k.Kind).Take(2));
        Assert.InRange(s.IgnoredKindness, 0.05, 0.95);
        Assert.True(s.ThreadsPerSeason > 0);
        Assert.InRange(s.MedianFeudDays, 1, 112);

        // The same runs give the same story.
        StoryStats again = StoryMetrics.Summarise(runs, kinds, cast, o);
        Assert.Equal((s.ThreadsPerSeason, s.MedianFeudDays, s.IgnoredKindness, s.FeudsPer100), (again.ThreadsPerSeason, again.MedianFeudDays, again.IgnoredKindness, again.FeudsPer100));
        Assert.Equal(s.ThreadShapes, again.ThreadShapes);
    }

    [Fact]
    public void WithFeelingsOffThereIsNoStoryButNoFailure()
    {
        SimResult r = new Simulation(3, feelings: FeelingOptions.Off).Run(14);
        Assert.Empty(r.FeudSpells);
        StoryStats s = StoryMetrics.Summarise(new[] { r }, DefaultTown.Acts(), DefaultTown.Cast(), FeelingOptions.Off);
        Assert.True(double.IsNaN(s.IgnoredKindness));
        Assert.True(double.IsNaN(s.MedianFeudDays));
        Assert.Equal(0, s.ThreadsPerSeason);
        Assert.True(s.ActsPerPerson > 0);
    }
}
