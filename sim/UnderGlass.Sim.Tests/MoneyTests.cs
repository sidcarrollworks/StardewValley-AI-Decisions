using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Money, wants and needs, temptation, and the ladder's fines (phase 0b).</summary>
public class MoneyTests
{
    private static Location Room(string name) => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    private static Villager V(string name, string household, int x, int y, string place = "Store", double bold = 0.5,
        Dictionary<string, double>? acts = null, Job? job = null, int age = 30)
        => new(name, household, "villager", new Temperament(0.5, bold, 0.5, 0.5), new Body(100, -1), job,
            new[] { new Haunt(place, new Tile(x, y), 0, Clock.MinutesPerDay, 1) },
            acts ?? new Dictionary<string, double>(), Array.Empty<string>(), age);

    private static Economy Eco(Dictionary<string, double> start, (string, double, string?)[]? incomes = null,
        Dictionary<string, double>? allowances = null, Dictionary<string, (double, double)>? wants = null)
        => new(start, incomes ?? Array.Empty<(string, double, string?)>(), allowances ?? new(),
            new Dictionary<string, string>(), wants ?? new(), TownStipend: 0, TownStart: 0);

    private static readonly ActKind Stole = new(Simulation.Stole, 4.5, -1, 1, 1, 0, new[] { "Store" }, MinAge: 13);

    [Fact]
    public void TheTownsCashChangesOnlyByWhatCrossesItsEdge()
    {
        SimResult r = new Simulation(7).Run(28);
        Assert.Equal(29, r.TownCash.Count);
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        Assert.True(r.OutsideIn > 0 && r.OutsideOut > 0);
    }

    [Fact]
    public void Payday_WagesAllowancesAndGroceries()
    {
        // Ann earns 500 a week from away; her son Kid gets 50 a week; they buy groceries from Kim's store.
        var cast = new[]
        {
            V("Kim", "Shop", 5, 2, job: new Job("Store", new Tile(5, 2), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1)),
            V("Ann", "H", 8, 2), V("Kid", "H", 9, 2, age: 10),
        };
        var eco = Eco(new() { ["Shop"] = 500, ["H"] = 1000 }, new (string, double, string?)[] { ("Ann", 500, null) },
            new() { ["Kid"] = 50 });
        var authority = new AuthorityOptions { Keepers = new Dictionary<string, string> { ["Store"] = "Kim" } };
        SimResult r = new Simulation(1, cast, new[] { Room("Store") }, Array.Empty<ActKind>(), authority: authority,
            economy: eco, money: new MoneyOptions { SpendShare = 0, WantChancePerDay = 0 }, wander: 0).Run(8); // paydays: day 0 and day 7

        Assert.Equal(1000 + 2 * (500 - 75) - 2 * 50 - 2 * 200, r.Purses["H"], 6);   // 1350
        Assert.Equal(500 + 2 * 200 * 0.4 - 2 * 100 * 0.6, r.Purses["Shop"], 6);       // margin on H, own food at cost
        Assert.Equal(150, r.Pockets["Ann"], 6);
        Assert.Equal(100, r.Pockets["Kid"], 6);
    }

    /// <summary>Abi wants something she can't afford. She is home until 10:00, then in the store,
    /// alone or watched by people who are up by then; nobody tires during the run.</summary>
    private static SimResult Tempted(int watchers)
    {
        var abi = V("Abi", "A", 5, 2, acts: new() { [Simulation.Stole] = 1 }, age: 20) with
        {
            Haunts = new[] { new Haunt("Store", new Tile(5, 2), Clock.At(10), Clock.At(20), 1000) },
        };
        var cast = new List<Villager> { abi };
        for (int i = 0; i < watchers; i++)
            cast.Add(V($"W{i}", $"W{i}", 7 + i, 3));
        var eco = Eco(cast.ToDictionary(v => v.Household, _ => 1000.0), wants: new() { ["Abi"] = (500, 500) });
        var money = new MoneyOptions { WantChancePerDay = 1, WantPatienceDays = 1, StealBase = 1, SpendShare = 0 };
        return new Simulation(2, cast, new[] { Room("Store"), Room("Home:A") }, new[] { Stole }, economy: eco, money: money,
            body: new BodyOptions { AwakeHoursAtRest = 100_000 }, wander: 0).Run(3);
    }

    [Fact]
    public void AnUnmetWantPullsTowardTheft_WhenNobodyIsWatching()
    {
        SimResult alone = Tempted(watchers: 0);
        var (actId, who, motive) = Assert.Single(alone.Motives.Take(1));
        Assert.Equal("Abi", who);
        Assert.Equal("want 500g", motive);
        Assert.Equal(Simulation.Stole, alone.Acts[actId].Kind);
        Assert.True(alone.Pockets["Abi"] > 0);                 // she has the goods' worth

        SimResult watched = Tempted(watchers: 4);
        Assert.Empty(watched.Motives);                         // four people in sight: too risky
    }

    [Fact]
    public void AHouseholdInNeedRummagesTheBins()
    {
        var bin = new ActKind(Simulation.RummagedInBin, 4.0, -1, 1, 3, 0, new[] { "Square" }, MinAge: 13);
        var cast = new[] { V("Pam", "T", 5, 2, place: "Square", acts: new() { [Simulation.RummagedInBin] = 1 }) };
        var eco = Eco(new() { ["T"] = -200 });
        SimResult r = new Simulation(3, cast, new[] { Room("Square") }, new[] { bin }, economy: eco,
            money: new MoneyOptions { StealBase = 1, WantChancePerDay = 0 }, wander: 0).Run(1);
        var (_, who, motive) = r.Motives[0];
        Assert.Equal(("Pam", "need"), (who, motive));
    }

    /// <summary>Tom, with one verdict already, steals beside Kim the keeper; May is mayor.</summary>
    private static SimResult Fined(double tomsPurse)
    {
        var cast = new[]
        {
            V("Tom", "T", 3, 2, place: "Store"),
            V("Kim", "K", 5, 2, job: new Job("Store", new Tile(5, 2), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1)),
            V("May", "M", 11, 2),
        };
        var authority = new AuthorityOptions
        {
            Mayor = "May", ElectConstable = false, Record = new Dictionary<string, int> { ["Tom"] = 1 },
            Keepers = new Dictionary<string, string> { ["Store"] = "Kim" },
        };
        var eco = Eco(new() { ["T"] = tomsPurse, ["K"] = 1000, ["M"] = 1000 });
        var kinds = new[] { Stole, new ActKind(Simulation.Service, 3.0, -1, 2, 30, 0, Array.Empty<string>()) };
        return new Simulation(4, cast, new[] { Room("Store") }, kinds, authority: authority, economy: eco,
            money: new MoneyOptions { WantChancePerDay = 0, SpendShare = 0 },
            scheduled: new[] { (Clock.At(10), "Tom", Simulation.Stole) }, wander: 0).Run(1);
    }

    [Fact]
    public void TheSecondVerdictIsRestitutionAndAFine_PaidOrServed()
    {
        SimResult paid = Fined(tomsPurse: 1000);
        Assert.Equal(Consequence.RestitutionAndFine, Assert.Single(paid.Verdicts).Step);
        Assert.Contains(paid.Log, l => l.Contains(" paid Tom ") && !l.Contains(" paid Tom 0 "));
        Assert.DoesNotContain(paid.Acts, a => a.Kind == Simulation.Service);
        Assert.Equal(100, paid.Purses[Simulation.Town], 6);     // the fine goes to the town

        SimResult broke = Fined(tomsPurse: -50);
        Assert.Contains(broke.Acts, a => a.Kind == Simulation.Service && a.Actor == "Tom");
    }
}
