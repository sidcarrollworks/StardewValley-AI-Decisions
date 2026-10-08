using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's slice acts-4, Repair (acts spec 4.7 and question 3): Remorse and Apologised, with
/// its answer on the spot. In a room with two people placed by hand, Ann argues with Bob (placed by
/// the test): she feels remorse if she doesn't dislike him, apologises, and he accepts or refuses by
/// how he holds her; an accepted apology gives back half the regard the argument cost, then a
/// quarter, then nothing; a refused one stings; a joke read cold stirs remorse too.
/// </summary>
public class RepairTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly ActKind Argued = DefaultTown.Acts().First(k => k.Name == "Argued");

    private static Villager V(string name, string household, int x, double bold, double understanding = 0.5, string? friend = null)
        => new(name, household, "villager", new Temperament(1.0, bold, understanding, 0.5), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, 2), 0, D, 1) }, new Dictionary<string, double>(),
            friend is null ? Array.Empty<string>() : new[] { friend });

    private static SimResult Scene(FeelingOptions o, int days, (int Tick, string Who, string Kind)[] placed, bool friends = true, IEnumerable<ActKind>? more = null)
    {
        var kinds = new List<ActKind> { Argued };
        kinds.AddRange(ActCatalog.Repair);
        if (more is not null)
            kinds.AddRange(more);
        var room = new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        // Ann is understanding (0.9): her remorse, 0.3 x 0.5 x 1.4 = 0.21, clears the apology's 0.15.
        var cast = new[] { V("Ann", "A", 3, 0.8, 0.9, friend: friends ? "Bob" : null), V("Bob", "B", 5, 0.2, friend: friends ? "Ann" : null) };
        return new Simulation(1, cast, new[] { room }, kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: placed, wander: 0).Run(days);
    }

    private static FeelingOptions Repair(params (string From, string To, double Regard)[] start)
    {
        FeelingOptions o = FeelingOptions.WithDesire(start);
        o.Acts.Repair = true;
        o.AnswerOn = false; // Bob doesn't argue back: the apology is the only thing that follows
        o.ClearBand = 0.02;
        return o;
    }

    private static readonly (int, string, string)[] OneArgument = { (Clock.At(10), "Ann", "Argued") };

    [Fact]
    public void AnAcceptedApologyGivesBackHalfTheHurt()
    {
        SimResult r = Scene(Repair(("Ann", "Bob", 0.4), ("Bob", "Ann", 0.4)), 2, OneArgument);
        Act argument = r.Acts.First(a => a.Kind == "Argued");
        Assert.Contains(r.Stirred, s => s is { Holder: "Ann", Subject: "Bob", Motive: DesireKind.Remorse } && s.Source == argument.Id);
        Act sorry = Assert.Single(r.Acts, a => a.Kind == "Apologised");
        Assert.Equal(("Ann", "Bob", argument.Id), (sorry.Actor, sorry.Target, sorry.About));
        Assert.All(r.LifeEvents.Where(e => e.ActId == sorry.Id && e.Role is LifeRole.Did or LifeRole.Undergone), e => Assert.Equal(Outcome.Accepted, e.Outcome));
        double cost = r.Feelings.Where(f => f.Holder == "Bob" && f.Toward == "Ann" && f.ActId == argument.Id).Sum(f => f.Change);
        Felt back = Assert.Single(r.Feelings, f => f.Holder == "Bob" && f.Toward == "Ann" && f.Route == "Apology");
        Assert.True(cost < 0);
        Assert.Equal(-cost * 0.5, back.Raw, 9); // the first time: half
    }

    [Fact]
    public void ARefusedApologyStingsAndGivesNothingBack()
    {
        // Bob holds Ann at -0.6: the cost of accepting (0.2 + 0.6 x 1) is far above what he owes her.
        SimResult r = Scene(Repair(("Ann", "Bob", 0.2), ("Bob", "Ann", -0.6)), 2, OneArgument, friends: false);
        Act sorry = Assert.Single(r.Acts, a => a.Kind == "Apologised");
        Assert.All(r.LifeEvents.Where(e => e.ActId == sorry.Id && e.Role is LifeRole.Did or LifeRole.Undergone), e => Assert.Equal(Outcome.Refused, e.Outcome));
        Assert.DoesNotContain(r.Feelings, f => f.Route == "Apology");
        Assert.Contains(r.Log, l => l.Contains(" apology Ann Bob ") && l.Contains(" refused "));
    }

    [Fact]
    public void NoRemorseTowardSomeoneDisliked()
    {
        SimResult r = Scene(Repair(("Ann", "Bob", -0.2)), 2, OneArgument);
        Assert.DoesNotContain(r.Stirred, s => s.Motive == DesireKind.Remorse);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Apologised");
    }

    [Fact]
    public void WithRepairOffNothingFollows()
    {
        FeelingOptions o = Repair(("Ann", "Bob", 0.4), ("Bob", "Ann", 0.4));
        o.Acts.Repair = false;
        SimResult r = Scene(o, 2, OneArgument);
        Assert.DoesNotContain(r.Stirred, s => s.Motive == DesireKind.Remorse);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Apologised");
    }

    [Fact]
    public void SorryMeansLessEachTime()
    {
        // Three arguments, four days apart (past the three-day cooldown), each apologised for and
        // accepted: half the hurt back, then a quarter, then nothing (question 3, answer b).
        var placed = new[] { (Clock.At(10), "Ann", "Argued"), (4 * D + Clock.At(10), "Ann", "Argued"), (8 * D + Clock.At(10), "Ann", "Argued") };
        SimResult r = Scene(Repair(("Ann", "Bob", 0.6), ("Bob", "Ann", 0.6)), 10, placed);
        var arguments = r.Acts.Where(a => a.Kind == "Argued").ToList();
        var sorries = r.Acts.Where(a => a.Kind == "Apologised").ToList();
        Assert.Equal(3, arguments.Count);
        Assert.Equal(arguments.Select(a => a.Id), sorries.Select(a => a.About));
        Assert.All(sorries, s => Assert.Contains(r.LifeEvents, e => e.ActId == s.Id && e.Outcome == Outcome.Accepted));
        double[] shares = { 0.5, 0.25, 0 };
        for (int i = 0; i < 3; i++)
        {
            double cost = r.Feelings.Where(f => f.Holder == "Bob" && f.Toward == "Ann" && f.ActId == arguments[i].Id).Sum(f => f.Change);
            double back = r.Feelings.Where(f => f.Holder == "Bob" && f.Toward == "Ann" && f.Route == "Apology" && f.ActId == sorries[i].Id).Sum(f => f.Raw);
            Assert.Equal(-cost * shares[i], back, 9);
        }
    }

    [Fact]
    public void AJokeReadColdStirsRemorse()
    {
        // With Returns on too: Ann teases Bob, who holds her at 0, so it lands badly; she saw it.
        FeelingOptions o = Repair(("Ann", "Bob", 0.6), ("Bob", "Ann", 0));
        o.Acts.Returns = true;
        o.FondOn = true;
        SimResult r = Scene(o, 3, Array.Empty<(int, string, string)>(), more: ActCatalog.Returns.Where(k => k.Name == "Joked"));
        Act joke = r.Acts.First(a => a.Kind == "Joked");
        Assert.Contains(r.Feelings, f => f.Holder == "Bob" && f.ActId == joke.Id && f.Route == "Cold");
        Assert.Contains(r.Stirred, s => s is { Holder: "Ann", Subject: "Bob", Motive: DesireKind.Remorse } && s.Source == joke.Id);
    }

    [Fact]
    public void RemorseNotActedOnLapses()
    {
        // Ann is too shy to walk up and say sorry (boldness 0): her remorse lapses after RemorseDays.
        var room = new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        FeelingOptions o = Repair(("Ann", "Bob", 0.4), ("Bob", "Ann", 0.4));
        o.Acts.RemorseDays = 3;
        var cast = new[] { V("Ann", "A", 3, 0.0, friend: "Bob"), V("Bob", "B", 5, 0.2, friend: "Ann") };
        var kinds = new List<ActKind> { Argued };
        kinds.AddRange(ActCatalog.Repair);
        SimResult r = new Simulation(1, cast, new[] { room }, kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: OneArgument, wander: 0).Run(5);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Apologised");
        Assert.Contains(r.LifeEvents, e => e is { Person: "Ann", Role: LifeRole.Lapsed, Kind: "Apologised" });
    }
}
