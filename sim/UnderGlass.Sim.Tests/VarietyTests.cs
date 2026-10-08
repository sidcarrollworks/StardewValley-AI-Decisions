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
        Assert.Equal(r.Acts.Count(a => !a.Injected && byName[a.Kind].IsScandal), events.Count(e => e.Kind is "FirstScandal" or "Scandal"));
        // A person's first scandal comes before their others.
        foreach (var g in events.Where(e => e.Kind is "FirstScandal" or "Scandal").GroupBy(e => e.People[0]))
            Assert.Equal("FirstScandal", g.OrderBy(e => e.Tick).First().Kind);
        // A secret out is a scandal nobody saw, later named by someone other than the culprit, on the
        // day of that naming.
        RunLog log = RunLog.Of(r);
        foreach (StoryEvent s in events.Where(e => e.Kind == "SecretOut"))
            Assert.Contains(r.Acts, a => a.Actor == s.People[0] && a.Kind == s.Detail && r.Witnesses.GetValueOrDefault(a.Id) == 0
                                         && Variety.FirstNaming(log, a) == s.Tick && Clock.Day(s.Tick) == s.Day);
        Assert.Single(events, e => e.Kind == "Elected"); // the opening vote, once a run
    }

    [Fact]
    public void MeasuringRunsCutsThemIntoSeedYears()
    {
        var kinds = TownData.Default().Acts;
        var runs = new[] { 1L, 2L }.Select(s => new Simulation(s, TownData.Default()).Run(Variety.Year)).ToList();
        VarietyStats v = Variety.Measure(runs, kinds);
        Assert.Equal(2, v.SeedYears);
        VarietyStats again = Variety.Measure(runs, kinds);
        Assert.Equal(v, again with { Commonest = v.Commonest, Kinds = v.Kinds, Unfair = v.Unfair }); // the same runs, the same measures
        Assert.Equal(v.Unfair, again.Unfair);
        Assert.Equal(v.Commonest, Variety.Measure(runs, kinds).Commonest);
        var (years, twists) = Variety.Split(new Simulation(1, TownData.Default()).Run(2 * Variety.Year), kinds);
        Assert.Equal(2, years.Count);
        Assert.Equal(8, twists.Count);
        Assert.All(years, y => Assert.True(y.Conflict > 0 && y.Warmth > 0));
    }

    /// <summary>V1 pools families (batch 2's m-1): a pair's feud and falling out are one story, and so
    /// are a person's first scandal and later ones; each family's commonest is given.</summary>
    [Fact]
    public void FamiliesArePooledForV1()
    {
        var years = new List<SeedYear>
        {
            Y(E("Feud", 1, "Sam", "Shane"), E("FirstScandal", 2, "Pam")),
            Y(E("FellOut", 1, "Sam", "Shane"), E("Scandal", 2, "Pam")),
            Y(E("KinFeud", 1, "Sam", "Vincent"), E("FirstScandal", 2, "Pam")),
            Y(E("Friendship", 1, "Emily", "Haley")),
        };
        VarietyStats v = Variety.From(years, new int[16]);
        Assert.Equal(0.75, v.V1); // Pam's scandals, in three of four years
        Assert.Equal(("Pam", 0.75), (v.V1CulpritName, v.V1Culprit));
        Assert.Equal(("Sam-Shane", 0.5), (v.V1FeudPair, v.V1Feud)); // a feud, then a falling out
        Assert.Equal("Feud Sam-Shane", Variety.FamilyOf(E("FellOut", 3, "Sam", "Shane")));
        Assert.Equal("Brawler Sam", Variety.FamilyOf(E("Brawler", 3, "Sam")));
    }

    /// <summary>The lead name for an act, day by day: each holder's latest belief counts, the actor's
    /// own doesn't, and a lead needs two holders and a strict winner.</summary>
    [Fact]
    public void TheLeadMovesWithTheHolders()
    {
        int D(int day, int minute = 600) => day * Clock.MinutesPerDay + minute;
        var rows = new (int, string, string?)[]
        {
            (D(0), "Ann", "Pam"), (D(0, 700), "Bob", "Pam"), (D(0, 800), "Pam", "Shane"), // Pam's own belief is left out
            (D(1), "Cal", "Shane"), (D(1, 700), "Dan", "Shane"),                          // a tie: no lead on day 1
            (D(2), "Ann", "Shane"),                                                         // Ann changes her mind
        };
        var leads = Variety.Leads(rows, "Pam", 0, 3).ToList();
        Assert.Equal(new[] { (0, "Pam"), (2, "Shane"), (3, "Shane") }, leads.Select(l => (l.Day, l.Lead)));
        Assert.Equal(D(2), leads[1].Tick);
        Assert.Empty(Variety.Leads(new (int, string, string?)[] { (D(0), "Ann", "Pam") }, "Sam", 0, 2)); // one holder is no lead
    }

    /// <summary>The fair-twist rule: two or more steps before the twist, one of them visible.</summary>
    [Fact]
    public void ATwistIsFairWithTwoStepsAndOneVisible()
    {
        CauseStep Seen(int t) => new(t, "act", 1, true);
        CauseStep Unseen(int t) => new(t, "report", 1, false);
        Assert.True(Variety.Fair(new[] { Seen(10), Unseen(20) }, 30));
        Assert.False(Variety.Fair(new[] { Seen(10) }, 30));                // one step
        Assert.False(Variety.Fair(new[] { Unseen(10), Unseen(20) }, 30));  // nothing to notice
        Assert.False(Variety.Fair(new[] { Seen(10), Unseen(40) }, 30));    // the second step comes after
    }

    /// <summary>The run log's rows, read from its lines.</summary>
    [Fact]
    public void TheRunLogReadsItsRows()
    {
        RunLog log = RunLog.Parse(new[]
        {
            "100 belief Ann 7 Pam Witnessed 4", "110 belief Bob 7 someone Found 2", "120 told Ann Cal 7",
            "130 found Bob 7 MissingStock", "140 suspects Bob 7 Pam,Shane", "150 report Bob Lewis 7 someone nearby Pam",
            "160 questioned Pam by Lewis for 7: confessed", "170 reconciled Ann Pam bonus 0.050 act 9", "180 act 9 GaveGift by Pam at Square",
        });
        Assert.Equal(new (int, string, int, string?, Source)[] { (100, "Ann", 7, "Pam", Source.Witnessed), (110, "Bob", 7, null, Source.Found) }, log.Beliefs);
        Assert.Equal(new[] { (120, "Ann", "Cal", 7) }, log.Told);
        Assert.Equal(new[] { (130, "Bob", 7) }, log.Found);
        Assert.Equal(new[] { (140, "Bob", 7) }, log.Suspected);
        Assert.Equal(new (int, string, int, string?)[] { (150, "Bob", 7, null) }, log.Reports);
        Assert.Equal(new[] { (160, "Pam", 7, true) }, log.Questioned);
        Assert.Equal(new[] { (170, "Ann", "Pam", 9) }, log.Reconciled);
        Assert.Equal(120, log.FirstTold(7));
        Assert.Equal(int.MaxValue, log.FirstTold(8));
    }

    /// <summary>On a year of the shipped town: every twist carries its cause chain and minute, fair
    /// as the rule says; let-offs and upheavals are the run's own; a humiliation had 3 or more
    /// onlookers; and the season counts add up.</summary>
    [Fact]
    public void OnARunEveryTwistCarriesItsChain()
    {
        var kinds = TownData.Default().Acts;
        int twists = 0;
        foreach (long seed in new[] { 2L, 5L })
        {
            SimResult r = new Simulation(seed, TownData.Default()).Run(Variety.Year);
            var events = Variety.Of(r, kinds);
            foreach (StoryEvent e in events.Where(e => Variety.Twists.Contains(e.Kind)))
            {
                twists++;
                Assert.NotNull(e.Chain);
                Assert.True(e.Tick >= 0 && Clock.Day(e.Tick) == e.Day, $"{e.Id} at {e.Tick} on day {e.Day}");
                Assert.True(e.Chain!.Zip(e.Chain.Skip(1)).All(p => p.First.Tick <= p.Second.Tick), e.Id); // earliest first
                if (e.Kind is "FellOut" or "Reconciled" or "WrongVerdict")
                    Assert.Equal(Variety.Fair(e.Chain, e.Tick + 1) && (e.Kind != "WrongVerdict" || e.Chain[0].Visible), e.Fair);
            }
            Assert.Equal(r.Verdicts.Count(v => v.LetOff && !r.Acts[v.ActId].Injected), events.Count(e => e.Kind == "LetOff"));
            Assert.Equal(r.ShopSwitches.Count, events.Count(e => e.Kind == "Upheaval"));
            foreach (StoryEvent h in events.Where(e => e.Kind == "Humiliated"))
                Assert.Contains(r.Acts, a => a.Actor == h.People[0] && a.Tick == h.Tick && r.Scenes[a.Id].InRange - 1 >= 3);
            var (_, fair, all, unfair) = Variety.SplitAll(r, kinds);
            Assert.Equal(events.Count(e => Variety.Twists.Contains(e.Kind)), all.Sum());
            Assert.Equal(events.Count(e => Variety.Twists.Contains(e.Kind) && e.Fair), fair.Sum());
            Assert.Equal(all.Sum() - fair.Sum(), unfair.Values.Sum());
        }
        Assert.True(twists > 0, "no twists in two years");
    }
}
