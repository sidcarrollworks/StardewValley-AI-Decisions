using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Families and age (design rule 17; Sid, 2026-10-06).</summary>
public class FamilyTests
{
    private static Haunt AllDay(int x, int y) => new("Room", new Tile(x, y), 0, Clock.MinutesPerDay, 1);
    private static Location Room() => new("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    private static Villager V(string name, string household, int age, Haunt haunt, Job? job = null,
        Dictionary<string, Kin>? family = null, Dictionary<string, double>? acts = null)
        => new(name, household, "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), job, new[] { haunt },
            acts ?? new Dictionary<string, double>(), Array.Empty<string>(), age, family);

    private static readonly int Ten = Clock.At(10);
    private static readonly ActKind Stole = new("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(), MinAge: 13);
    private static readonly ActKind Squabbled = new("Squabbled", 1.5, -1, 1, 5, 300, Array.Empty<string>(), MaxAge: 12, WithKin: Kin.Sibling);
    private static readonly ActKind Row = new(Simulation.FamilyRow, 3.0, -1, 2, 15, 0, Array.Empty<string>());

    [Fact]
    public void TheCastIsFamilies_TiesGoBothWays()
    {
        var cast = DefaultTown.Cast().ToDictionary(v => v.Name);
        Assert.Equal(Kin.Parent, cast["Abigail"].KinOf("Pierre"));
        Assert.Equal(Kin.Child, cast["Pierre"].KinOf("Abigail"));
        Assert.Equal(Kin.Sibling, cast["Vincent"].KinOf("Sam"));
        Assert.Equal(Kin.Stepparent, cast["Sebastian"].KinOf("Demetrius"));
        Assert.Equal(Kin.Ward, cast["Marnie"].KinOf("Jas"));
        Assert.Null(cast["Marnie"].KinOf("Shane"));           // he rents a room; not family
        Assert.Equal(Stage.Child, cast["Vincent"].Stage);
        Assert.Equal(Stage.Elder, cast["George"].Stage);
        Assert.Equal("SeedShop", cast["Caroline"].Household);
    }

    [Fact]
    public void AChildWouldNotStealFromTheTill_ButSquabblesWithHisBrother()
    {
        var sibs = new Dictionary<string, Kin> { ["Sam"] = Kin.Sibling };
        var cast = new[]
        {
            V("Vincent", "S", 7, AllDay(3, 2), family: sibs, acts: new() { ["Squabbled"] = 1, ["Stole"] = 1 }),
            V("Sam", "S", 21, AllDay(5, 2), family: new() { ["Vincent"] = Kin.Sibling }),
        };
        SimResult r = new Simulation(1, cast, new[] { Room() }, new[] { Stole, Squabbled },
            scheduled: new[] { (Ten, Harness.Anyone, "Stole") }, wander: 0).Run(1);

        Assert.Equal("Sam", Assert.Single(r.Acts, a => a.Kind == "Stole").Actor);
        Assert.Contains(r.Acts, a => a.Kind == "Squabbled" && a.Actor == "Vincent");

        // Without his brother there, no squabble.
        var alone = new[] { cast[0], V("Ann", "A", 30, AllDay(5, 2)) };
        SimResult r2 = new Simulation(1, alone, new[] { Room() }, new[] { Squabbled }, wander: 0).Run(1);
        Assert.DoesNotContain(r2.Acts, a => a.Kind == "Squabbled");
    }

    [Fact]
    public void AKeeperWhoseOwnChildStole_HasItOutAtHome_AndNeverReportsOrRetellsIt()
    {
        // Kim keeps the shop; her son Kid steals beside her. Ann, a neighbour, sees it too. May is mayor.
        var cast = new[]
        {
            V("Kim", "K", 45, AllDay(5, 2), new Job("Room", new Tile(5, 2), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1),
                family: new() { ["Kid"] = Kin.Child }),
            V("Kid", "K", 20, AllDay(3, 2), family: new() { ["Kim"] = Kin.Parent }),
            V("Ann", "A", 30, AllDay(4, 3)),
            V("May", "M", 60, AllDay(9, 2)),
        };
        var authority = new AuthorityOptions
        {
            Mayor = "May", ElectConstable = false, ReportBase = 1,
            Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
        };
        SimResult r = new Simulation(2, cast, new[] { Room() }, new[] { Stole, Row }, authority: authority,
            scheduled: new[] { (Ten, "Kid", "Stole") }, wander: 0).Run(1);

        Assert.Equal("Kid", r.Beliefs["Kim"][0].Actor);         // she knows who
        Assert.DoesNotContain(r.Accounts, a => a.From == "Kim"); // and never tells the mayor
        Assert.DoesNotContain(r.Log, l => l.Contains(" told Kim ") && l.EndsWith(" 0"));
        Assert.Contains(r.Acts, a => a.Kind == Simulation.FamilyRow && a.Actor == "Kid");
        Assert.Contains(r.Accounts, a => a.From == "Ann" && a.Actor == "Kid"); // a neighbour does
    }

    [Fact]
    public void AFamilyAlibiOffsetsBeingSeenNearby_NeverASighting()
    {
        var o = new AuthorityOptions();
        static double Trust(string _) => 1.0;
        var heard = new Account(0, "Bob", "Tom", 0.8, false, 0);                   // hearsay: 0.4
        var near = new Account(0, "Ann", null, 0, true, 0, new[] { "Tom" });       // nearby: 0.25
        var alibi = new Account(0, "Ma", null, 0, true, 0, Alibi: new[] { "Tom" }); // takes back 0.125

        Assert.Equal("Tom", Authority.Weigh(new[] { heard, near }, Trust, o).Accused); // 0.65: decided
        Assert.Null(Authority.Weigh(new[] { heard, near, alibi }, Trust, o).Accused);  // 0.525: not
        var saw = new Account(0, "Cal", "Tom", 1, true, 0);
        Assert.Equal("Tom", Authority.Weigh(new[] { saw, alibi, alibi with { From = "Pa" } }, Trust, o).Accused);
    }

    [Fact]
    public void QuestionedFamilyLeaveTheirKinOut_AndVouchForThem()
    {
        // Tom steals; Wes and May (the mayor) see "someone" and suspect him. His mother Ma, who
        // lives with him, is questioned too.
        var cast = new[]
        {
            V("Tom", "T", 25, AllDay(3, 2), family: new() { ["Ma"] = Kin.Parent }),
            V("Ma", "T", 50, AllDay(12, 4), family: new() { ["Tom"] = Kin.Child }),
            V("Wes", "W", 30, AllDay(10, 2)),
            V("May", "M", 60, AllDay(11, 2)),
        };
        var authority = new AuthorityOptions { Mayor = "May", ElectConstable = false, ConfessBase = 0, ConfessPerTimidity = 0 };
        var questioned = new ActKind(Authority.Questioned, 2.5, -1, 1, 10, 0, Array.Empty<string>());
        SimResult r = new Simulation(3, cast, new[] { Room() }, new[] { Stole, questioned }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(1);

        Assert.Contains(r.Interviews, i => i.Who == "Ma");
        Account ma = r.Accounts.Last(a => a.From == "Ma");
        Assert.Contains("Tom", ma.Alibi!);
        Assert.DoesNotContain("Tom", ma.Nearby ?? Array.Empty<string>());
        Assert.Empty(r.Verdicts);
    }
}
