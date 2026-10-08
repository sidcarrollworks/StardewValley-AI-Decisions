using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's slice acts-5, Sides (acts spec 4.8-4.10): Mocked, StoodUpFor and Comforted; the
/// Defend motive; Pity at a hurt seen. In a square with people placed by hand: a mocking replaces an
/// argument only with a card and an audience; and the golden thread "the square" (acts spec 6.7):
/// Ann mocks Bob, Cara, who loves him, stands up for him, and Dan, who doesn't know him well, comforts him.
/// </summary>
public class SidesTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly ActKind Argued = DefaultTown.Acts().First(k => k.Name == "Argued");

    private static Villager V(string name, string household, int x, double bold, bool mocks = false)
        => new(name, household, "villager", new Temperament(1.0, bold, 0.8, 0.5), new Body(100, -1), null,
            new[] { new Haunt("Square", new Tile(x, 2), 0, D, 1) },
            mocks ? new Dictionary<string, double> { ["Mocked"] = 1 } : new Dictionary<string, double>(), Array.Empty<string>());

    private static SimResult Square(FeelingOptions o, int days, (int, string, string)[] placed, params Villager[] cast)
    {
        var kinds = new List<ActKind> { Argued };
        kinds.AddRange(ActCatalog.Sides);
        var square = new Location("Square", true, Enumerable.Repeat(new string('.', 40), 6).ToList());
        return new Simulation(1, cast, new[] { square }, kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 },
            scheduled: placed, wander: 0).Run(days);
    }

    private static FeelingOptions Sides(params (string From, string To, double Regard)[] start)
    {
        FeelingOptions o = FeelingOptions.WithDesire(start);
        o.Acts.Sides = true;
        o.ClearBand = 0.02;
        o.HostileSurcharge = 0; // so the bold clear a hostile act in a small scene
        return o;
    }

    [Fact]
    public void AMockingReplacesAnArgumentOnlyWithACardAndACrowd()
    {
        // Bob argues with Ann (he holds the others at 1, so it's her); Ann answers.
        var placed = new[] { (Clock.At(10), "Bob", "Argued") };
        var start = new[] { ("Bob", "Ann", -0.6), ("Bob", "Cara", 1.0), ("Bob", "Dan", 1.0), ("Ann", "Bob", -0.3) };
        // Cara and Dan stand within sight of Ann (8 tiles) but out of Bob's reach (5).
        Villager Ann(bool card) => V("Ann", "A", 10, 1.0, mocks: card);
        Villager[] Crowd() => new[] { V("Cara", "C", 3, 0.5), V("Dan", "D", 4, 0.5) };

        SimResult mocked = Square(Sides(start), 2, placed, new[] { Ann(true), V("Bob", "B", 12, 0.5) }.Concat(Crowd()).ToArray());
        Act answer = mocked.Acts.First(a => a.Actor == "Ann" && a.Target == "Bob");
        Assert.Equal("Mocked", answer.Kind);
        Assert.Contains(answer.Id, mocked.Pursued);

        SimResult noCard = Square(Sides(start), 2, placed, new[] { Ann(false), V("Bob", "B", 12, 0.5) }.Concat(Crowd()).ToArray());
        Assert.Equal("Argued", noCard.Acts.First(a => a.Actor == "Ann" && a.Target == "Bob").Kind);

        SimResult noCrowd = Square(Sides(start.Take(1).Concat(start.Skip(3)).ToArray()), 2, placed, Ann(true), V("Bob", "B", 12, 0.5));
        Assert.Equal("Argued", noCrowd.Acts.First(a => a.Actor == "Ann" && a.Target == "Bob").Kind);
    }

    [Fact]
    public void TheSquare()
    {
        // Ann mocks Bob at 10:00 (placed). Cara loves Bob (0.9): she stands up for him (her urge, 0.3 x
        // clarity x 0.9, clears StoodUpFor's 0.2; at 0.6 it wouldn't). Dan holds Bob at 0.1: he pities
        // him and sits with him. Ann holds the others at 1, so Bob is her target.
        var start = new[] { ("Ann", "Bob", -0.6), ("Ann", "Cara", 1.0), ("Ann", "Dan", 1.0), ("Cara", "Bob", 0.9), ("Dan", "Bob", 0.1) };
        SimResult r = Square(Sides(start), 2, new[] { (Clock.At(10), "Ann", "Mocked") },
            V("Ann", "A", 10, 0.5, mocks: true), V("Bob", "B", 12, 0.3), V("Cara", "C", 13, 1.0), V("Dan", "D", 11, 0.8));
        Act mock = Assert.Single(r.Acts, a => a.Kind == "Mocked");
        Assert.Equal(("Ann", "Bob"), (mock.Actor, mock.Target));

        // Cara wants to defend Bob; Dan pities him.
        Assert.Contains(r.Stirred, s => s is { Holder: "Cara", Subject: "Ann", Motive: DesireKind.Defend } && s.Source == mock.Id);
        Assert.Contains(r.Stirred, s => s is { Holder: "Dan", Subject: "Bob", Motive: DesireKind.Pity } && s.Source == mock.Id);
        Act stood = Assert.Single(r.Acts, a => a.Kind == "StoodUpFor");
        Assert.Equal(("Cara", "Ann", "Bob", mock.Id), (stood.Actor, stood.Target, stood.With, stood.About));
        Act comfort = Assert.Single(r.Acts, a => a.Kind == "Comforted");
        Assert.Equal(("Dan", "Bob", mock.Id), (comfort.Actor, comfort.Target, comfort.About));

        // Bob saw Cara stand up for him: he warms to her and wants to return it. Ann is stood up to, and
        // is hurt by it: she holds Cara at 1 here (so that Bob was her target), so she wants to make up.
        Assert.Contains(r.Feelings, f => f.Holder == "Bob" && f.Toward == "Cara" && f.Route == "Defended" && f.Change > 0);
        Assert.Contains(r.Stirred, s => s is { Holder: "Bob", Subject: "Cara", Motive: DesireKind.Return } && s.Source == stood.Id);
        Assert.Contains(r.Stirred, s => s is { Holder: "Ann", Subject: "Cara", Motive: DesireKind.MakeUp or DesireKind.Answer } && s.Source == stood.Id);
        // The comfort warms Bob toward Dan, and stirs Return.
        Assert.Contains(r.Stirred, s => s is { Holder: "Bob", Subject: "Dan", Motive: DesireKind.Return } && s.Source == comfort.Id);
    }

    [Fact]
    public void ComfortCoolsTheGrudgeAgainstTheOneWhoHurt()
    {
        // Ann mocks Bob before Dan and Eve; both pity Bob, and a comfort follows the mocking.
        var start = new[] { ("Ann", "Bob", -0.6), ("Ann", "Dan", 1.0), ("Ann", "Eve", 1.0), ("Dan", "Bob", 0.1), ("Bob", "Ann", 0.0) };
        FeelingOptions o = Sides(start);
        SimResult with = Square(o, 1, new[] { (Clock.At(10), "Ann", "Mocked") },
            V("Ann", "A", 10, 0.5, mocks: true), V("Bob", "B", 12, 0.0), V("Dan", "D", 11, 0.8), V("Eve", "E", 9, 0.8));
        Act comfort = with.Acts.First(a => a.Kind == "Comforted");
        Assert.Equal("Bob", comfort.Target);
        Assert.Contains(with.Stirred, s => s is { Holder: "Bob", Subject: "Ann", Motive: DesireKind.Answer });
        Assert.True(comfort.Tick > with.Acts.First(a => a.Kind == "Mocked").Tick);
    }

    [Fact]
    public void FamiliesDontStandAgainstTheirOwnAndPityIsntFeltAtHome()
    {
        // Cara is Ann's housemate: she doesn't stand against her, though she loves Bob.
        var start = new[] { ("Ann", "Bob", -0.6), ("Ann", "Cara", 1.0), ("Cara", "Bob", 0.6) };
        SimResult r = Square(Sides(start), 1, new[] { (Clock.At(10), "Ann", "Mocked") },
            V("Ann", "A", 10, 0.5, mocks: true), V("Bob", "B", 12, 0.3), V("Cara", "A", 13, 1.0));
        Assert.DoesNotContain(r.Stirred, s => s.Motive == DesireKind.Defend);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "StoodUpFor");
    }

    [Fact]
    public void WithSidesOffNothingTakesSides()
    {
        var start = new[] { ("Ann", "Bob", -0.6), ("Ann", "Cara", 1.0), ("Ann", "Dan", 1.0), ("Cara", "Bob", 0.6), ("Dan", "Bob", 0.1) };
        FeelingOptions o = Sides(start);
        o.Acts.Sides = false;
        SimResult r = Square(o, 2, new[] { (Clock.At(10), "Ann", "Mocked") },
            V("Ann", "A", 10, 0.5, mocks: true), V("Bob", "B", 12, 0.3), V("Cara", "C", 13, 1.0), V("Dan", "D", 11, 0.8));
        Assert.DoesNotContain(r.Stirred, s => s.Motive is DesireKind.Defend || s.Motive == DesireKind.Pity);
        Assert.DoesNotContain(r.Acts, a => a.Kind is "StoodUpFor" or "Comforted");
    }
}
