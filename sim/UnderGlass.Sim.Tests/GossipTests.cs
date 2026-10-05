using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Gossip, scandal and the run-level checks (design rules 8 and 9; experiment E0).</summary>
public class GossipTests
{
    private static Villager V(string name, string household, int x, int y, double bold = 0.5, double chatty = 1.0, double self = 0.6)
        => new(name, household, "villager", new Temperament(chatty, bold, 0.5, self),
            new[] { new RoutineStep(0, "Room", new Tile(x, y)) },
            new Dictionary<string, double>(), Array.Empty<string>());

    /// <summary>A long open room; only scheduled acts happen (every rate is 0).</summary>
    private static Location Hall() => new("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    private static IReadOnlyList<ActKind> Kinds() => new[]
    {
        new ActKind("RummagedInBin", 4.0, -1, 1, 1, 0, Array.Empty<string>()),
        new ActKind("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>()),
    };

    // These scenes turn wandering off (wander: 0), so distances are exact.
    private static GossipOptions Chatty() => new() { ChatChance = 10 };

    [Fact]
    public void AStoryIsPassedOnWeakerAndNeverBackToItsTeller()
    {
        // Ann sees Bob rummage close up; Cal and Dee stand near Ann but out of sight of the bin.
        var cast = new[] { V("Ann", "A", 5, 2), V("Bob", "B", 3, 2), V("Cal", "C", 12, 2), V("Dee", "D", 19, 2) };
        SimResult r = new Simulation(1, cast, new[] { Hall() }, Kinds(), gossip: Chatty(),
            scheduled: new[] { (10, "Bob", "RummagedInBin") }, wander: 0).Run(1);

        Belief ann = r.Beliefs["Ann"][0];
        Assert.Equal(Source.Witnessed, ann.Source);
        Belief? cal = r.Beliefs["Cal"].GetValueOrDefault(0);
        Assert.NotNull(cal);
        Assert.True(cal!.Juiciness < ann.Juiciness);
        Assert.Equal(Source.Told, cal.Source);
        Assert.Equal("Ann", cal.Chain[0]);
        Assert.DoesNotContain(r.Log, l => l.Contains(" told Cal Ann 0")); // never back to its teller
    }

    [Fact]
    public void ATellerTellsOneStoryToOneListenerADayInASmallTown()
    {
        var cast = new[] { V("Ann", "A", 5, 2), V("Bob", "B", 3, 2), V("Cal", "C", 9, 2), V("Dee", "D", 9, 4) };
        SimResult r = new Simulation(2, cast, new[] { Hall() }, Kinds(), gossip: Chatty(),
            scheduled: new[] { (10, "Bob", "RummagedInBin") }, wander: 0).Run(1);
        int byAnn = r.Log.Count(l => l.Contains(" told Ann ") && l.EndsWith(" 0"));
        Assert.True(byAnn <= 1);
    }

    [Fact]
    public void EveryHeardStoryTracesBackToAWitness_AndTheSameSeedGivesTheSameRun()
    {
        SimResult a = new Simulation(42).Run(7);
        SimResult b = new Simulation(42).Run(7);
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(b));
        Assert.NotEqual(Metrics.LogHash(a), Metrics.LogHash(new Simulation(43).Run(7)));

        foreach (var (holder, beliefs) in a.Beliefs)
            foreach (Belief told in beliefs.Values.Where(x => x.Source == Source.Told))
            {
                string origin = told.Chain[^1];
                Assert.Equal(Source.Witnessed, a.Beliefs[origin][told.ActId].Source);
                Assert.True(told.Juiciness < DefaultTown.Acts().First(k => k.Name == told.Kind).Juiciness);
                Assert.DoesNotContain(holder, told.Chain);
            }
    }

    [Fact]
    public void AScandalIsConfrontedOnceWhenEnoughOfThoseWhoKnowTheActorHoldIt()
    {
        // Four neighbours of one household stand close and see Bob rummage: enough for a confrontation.
        var cast = new[] { V("Ann", "H", 3, 2), V("Bob", "B", 5, 2), V("Cal", "H", 7, 2), V("Dee", "H", 5, 4), V("Eve", "H", 4, 4) };
        // Everyone knows Bob a little (familiarity 0.25 from the seed), so 3 holders are needed.
        SimResult r = new Simulation(3, cast, new[] { Hall() }, Kinds(),
            scheduled: new[] { (10, "Bob", "RummagedInBin") }, wander: 0).Run(2);
        Confrontation c = Assert.Single(r.Confrontations);
        Assert.Equal("Bob", c.Target);
        Assert.True(c.Correct);
        Assert.NotEqual("Bob", c.By);
    }

    [Fact]
    public void AStrangerSeenFromAfarIsSomeone_UpCloseTheyAreKnown()
    {
        // The newcomer is a stranger to everyone (familiarity 0).
        var cast = new[] { V("Ann", "A", 3, 2), V(DefaultTown.Newcomer, "Farm", 5, 2), V("Far", "F", 12, 2) };
        SimResult r = new Simulation(4, cast, new[] { Hall() }, Kinds(),
            scheduled: new[] { (10, DefaultTown.Newcomer, "RummagedInBin") }, wander: 0).Run(1);
        Assert.Equal(DefaultTown.Newcomer, r.Beliefs["Ann"][0].Actor);
        Belief far = r.Beliefs["Far"][0];         // 7 tiles away: saw it, couldn't tell who
        Assert.Equal(Source.Witnessed, far.Source);
        Assert.Null(far.Actor);
    }

    [Fact]
    public void ASeasonRunsAndReportsMetrics()
    {
        var runs = Enumerable.Range(1, 5).Select(s => new Simulation(s).Run(7)).ToList();
        RunStats stats = Metrics.Summarise(runs, DefaultTown.Acts());
        Assert.Equal(5, stats.Runs);
        Assert.NotEmpty(stats.Groups);
        Assert.All(stats.Groups, g => Assert.InRange(g.MeanReach, 0, 1));
    }
}
