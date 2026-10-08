using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The variety measures (actions-and-twists section 3): story events come from a run's
/// results only and the same runs give the same story; and V1-V8 count what they say on seed-years
/// made up to have a known answer.</summary>
public class VarietyTests
{
    private static StoryEvent E(string kind, int day, params string[] people) => new(kind, day, people);
    private static SeedYear Y(params StoryEvent[] events) => new(events);

    [Fact]
    public void ATownThatRepeatsItselfScoresAsOne()
    {
        // Ten seed-years, each the same feud: one story everywhere.
        var years = Enumerable.Range(0, 10).Select(_ => Y(E("Feud", 5, "Sam", "Shane"))).ToList();
        VarietyStats v = Variety.From(years, new int[40]);
        Assert.Equal(10, v.SeedYears);
        Assert.Equal(1.0, v.V1);
        Assert.Equal(("Feud Sam-Shane", 1.0), v.Commonest[0]);
        Assert.Equal(1.0, v.V2, 9); // one headline
        Assert.Equal("Feud Sam-Shane", v.TopHeadline);
        Assert.Equal(1.0, v.V3); // every pair alike
        Assert.True(double.IsNaN(v.V4)); // under 50 seed-years no story can be rare
        Assert.Equal(0.0, Variety.From(Enumerable.Range(0, 50).Select(_ => Y(E("Feud", 5, "Sam", "Shane"))).ToList(), new int[200]).V4);
        Assert.Equal(1.0, v.V5);
        Assert.Equal(("Sam", "Feud"), (v.V5Person, v.V5Arc)); // Sam before Shane on a tie
        Assert.Equal(0.0, v.V6);
    }

    [Fact]
    public void ATownWhereEverySeedDiffersScoresHigh()
    {
        // A hundred seed-years, each a different feud: every story is rare, every headline different.
        var years = Enumerable.Range(0, 100).Select(i => Y(E("Feud", 5, $"A{i:000}", $"B{i:000}"))).ToList();
        VarietyStats v = Variety.From(years, new int[400]);
        Assert.Equal(0.01, v.V1, 9);
        Assert.Equal(100.0, v.V2, 6);
        Assert.Equal(0.0, v.V3);
        Assert.Equal(1.0, v.V4);
        Assert.Equal(0.01, v.V5, 9);
    }

    [Fact]
    public void TheHeadlineIsTheBiggestStory_AndArcsAreKindsOfStory()
    {
        var year = new[] { E("Friendship", 1, "Emily", "Haley"), E("Feud", 9, "Alex", "Sam"), E("Scandal", 20, "Pam"), E("Feud", 3, "Gus", "Pierre") };
        Assert.Equal("Scandal Pam", Variety.Headline(year)!.Id);
        Assert.Equal("Feud Gus-Pierre", Variety.Headline(year.Where(e => e.Kind != "Scandal"))!.Id); // the earlier feud
        Assert.Null(Variety.Headline(Array.Empty<StoryEvent>()));

        // Sam feuds and is a brawler in 6 of 10 years, and only feuds in the others: his commonest
        // arc is Brawler+Feud, in 60% of seed-years.
        var years = Enumerable.Range(0, 10).Select(i => i < 6
            ? Y(E("Feud", 1, "Sam", $"X{i}"), E("Brawler", 2, "Sam"))
            : Y(E("Feud", 1, "Sam", $"X{i}"))).ToList();
        VarietyStats v = Variety.From(years, new int[40]);
        Assert.Equal(("Sam", "Brawler+Feud", 0.6), (v.V5Person, v.V5Arc, v.V5));
        Assert.Equal(0.6, v.V1, 9); // "Brawler Sam", the commonest named story
    }

    [Fact]
    public void JaccardTwistsAndKindsOfTown()
    {
        // Two years sharing 4 of 5 distinct stories are alike (4/5 = 0.8); a third shares none.
        StoryEvent a = E("Feud", 1, "A", "B"), b = E("Feud", 1, "C", "D"), c = E("Friendship", 1, "E", "F"), d = E("Scandal", 1, "G"), x = E("Scandal", 1, "H");
        var years = new List<SeedYear> { Y(a, b, c, d), Y(a, b, c, d, x), Y(E("Feud", 1, "P", "Q")) };
        VarietyStats v = Variety.From(years, new[] { 0, 1, 2, 3, 5 });
        Assert.Equal(1 / 3.0, v.V3, 9);
        Assert.Equal(2.0, v.V6); // the median of 0, 1, 2, 3, 5
        Assert.Equal(2.2, v.TwistsPerSeason, 9);

        // Kinds of town: four quiet years, four at war, two warm.
        var towns = Enumerable.Range(0, 4).Select(_ => new SeedYear(Array.Empty<StoryEvent>(), 1, 1))
            .Concat(Enumerable.Range(0, 4).Select(_ => new SeedYear(Array.Empty<StoryEvent>(), 3, 1)))
            .Concat(Enumerable.Range(0, 2).Select(_ => new SeedYear(Array.Empty<StoryEvent>(), 1, 3))).ToList();
        VarietyStats k = Variety.From(towns, new int[40]);
        // Medians: conflict 1 (six 1s and four 3s), warmth 1 (eight 1s and two 3s); 3 is past 1.2 times.
        Assert.Equal(3, k.V8);
        Assert.Equal(new[] { ("conflict high, warmth mid", 0.4), ("conflict mid, warmth mid", 0.4), ("conflict mid, warmth high", 0.2) }, k.Kinds);
    }

    [Fact]
    public void StoryEventsComeFromTheRunsResults_TheSameEveryTime()
    {
        var kinds = TownData.Default().Acts;
        SimResult r = new Simulation(4, TownData.Default()).Run(Variety.Year);
        var events = Variety.Of(r, kinds);
        Assert.Equal(events.Select(e => e.Id), Variety.Of(new Simulation(4, TownData.Default()).Run(Variety.Year), kinds).Select(e => e.Id));
        Assert.Equal(events.OrderBy(e => e.Day).Select(e => e.Day), events.Select(e => e.Day)); // in day order
        Assert.All(events, e => Assert.Contains(e.Kind, Variety.Kinds));

        // Every feud and friendship tie is a story; every scandal is the town's own and has an actor.
        Assert.Equal(r.Ties.Count(t => t.What is "feud" or "kin-feud"), events.Count(e => e.Kind is "Feud" or "KinFeud" or "FellOut"));
        Assert.Equal(r.Ties.Count(t => t.What == "friendship"), events.Count(e => e.Kind == "Friendship"));
        var byName = kinds.ToDictionary(k => k.Name);
        Assert.Equal(r.Acts.Count(a => !a.Injected && byName[a.Kind].IsScandal), events.Count(e => e.Kind == "Scandal"));
        // A secret out is a scandal nobody saw, later named by someone other than the culprit.
        foreach (StoryEvent s in events.Where(e => e.Kind == "SecretOut"))
            Assert.Contains(r.Acts, a => a.Actor == s.People[0] && a.Kind == s.Detail && r.Witnesses.GetValueOrDefault(a.Id) == 0);
        Assert.Single(events, e => e.Kind == "Elected"); // the opening vote, once a run
    }

    [Fact]
    public void MeasuringRunsCutsThemIntoSeedYears()
    {
        var kinds = TownData.Default().Acts;
        var runs = new[] { 1L, 2L }.Select(s => new Simulation(s, TownData.Default()).Run(Variety.Year)).ToList();
        VarietyStats v = Variety.Measure(runs, kinds);
        Assert.Equal(2, v.SeedYears);
        Assert.Equal(v, Variety.Measure(runs, kinds) with { Commonest = v.Commonest, Kinds = v.Kinds }); // the same runs, the same measures
        Assert.Equal(v.Commonest, Variety.Measure(runs, kinds).Commonest);
        var (years, twists) = Variety.Split(new Simulation(1, TownData.Default()).Run(2 * Variety.Year), kinds);
        Assert.Equal(2, years.Count);
        Assert.Equal(8, twists.Count);
        Assert.All(years, y => Assert.True(y.Conflict > 0 && y.Warmth > 0));
    }
}
