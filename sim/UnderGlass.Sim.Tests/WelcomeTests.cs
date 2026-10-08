using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's slice acts-3, Welcome (acts spec 4.6): the Curious motive and Welcomed. In a
/// room with people placed by hand: someone barely known is welcomed once, by each who is curious;
/// a welcome makes the two better known and stirs Return; nobody welcomes kin, housemates, someone
/// already known or at home. And on the shipped town, it is the newcomer's arc.
/// </summary>
public class WelcomeTests
{
    private const int D = Clock.MinutesPerDay;

    private static Villager V(string name, string household, int x, double chatty = 1.0, IReadOnlyDictionary<string, Kin>? family = null)
        => new(name, household, "villager", new Temperament(chatty, 0.5, 0.5, 0.6), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, 2), 0, D, 1) }, new Dictionary<string, double>(), Array.Empty<string>(), Family: family);

    private static SimResult Scene(FeelingOptions o, int days, params Villager[] cast)
    {
        var kinds = new List<ActKind>
        {
            new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(), Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1)),
        };
        kinds.AddRange(ActCatalog.Welcome);
        var room = new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        return new Simulation(1, cast, new[] { room }, kinds, feelings: o, body: new BodyOptions { AwakeHoursAtRest = 100_000 }, wander: 0).Run(days);
    }

    private static FeelingOptions Welcome()
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.Acts.Welcome = true;
        return o;
    }

    [Fact]
    public void TheNewcomerIsWelcomedOnceByEach()
    {
        SimResult r = Scene(Welcome(), 4, V("Ann", "A", 3), V(DefaultTown.Newcomer, "N", 5));
        var welcomes = r.Acts.Where(a => a.Kind == "Welcomed").ToList();
        Assert.Single(welcomes, a => a.Actor == "Ann" && a.Target == DefaultTown.Newcomer);
        Assert.True(welcomes.Count <= 2); // and the newcomer may introduce themselves to Ann, once
        Act w = welcomes.First(a => a.Actor == "Ann");
        Assert.Contains(w.Id, r.Pursued);
        Assert.Contains(r.Pursuits, p => p is { Holder: "Ann", Motive: DesireKind.Curious, ActKind: "Welcomed", Acted: true });
        // It stirs Return in the one welcomed.
        Assert.Contains(r.Stirred, s => s.Source == w.Id && s.Holder == DefaultTown.Newcomer && s.Motive == DesireKind.Return);
    }

    [Fact]
    public void AWelcomeMakesTheTwoBetterKnown()
    {
        // Ann is curious and welcomes the newcomer; Bob, as chatty but with Welcome off, can't. Both
        // spend the same days in the room, so the welcome is the difference.
        FeelingOptions on = Welcome();
        FeelingOptions off = Welcome();
        off.Acts.Welcome = false;
        SimResult with = Scene(on, 1, V("Ann", "A", 3), V(DefaultTown.Newcomer, "N", 5));
        SimResult without = Scene(off, 1, V("Ann", "A", 3), V(DefaultTown.Newcomer, "N", 5));
        Assert.Contains(with.Acts, a => a.Kind == "Welcomed");
        Assert.DoesNotContain(without.Acts, a => a.Kind == "Welcomed");
        double gained = with.Familiarity[("Ann", DefaultTown.Newcomer)] - without.Familiarity[("Ann", DefaultTown.Newcomer)];
        Assert.True(gained >= on.Acts.WelcomeFamiliarity - 1e-9, $"{gained}");
        Assert.True(with.Familiarity[(DefaultTown.Newcomer, "Ann")] - without.Familiarity[(DefaultTown.Newcomer, "Ann")] >= on.Acts.WelcomeFamiliarity - 1e-9);
    }

    [Fact]
    public void NobodyWelcomesKinHousematesOrTheKnown()
    {
        // Housemates and kin cover; someone known at 0.25 (a stranger in town) isn't new.
        Assert.DoesNotContain(Scene(Welcome(), 3, V("Ann", "A", 3), V(DefaultTown.Newcomer, "A", 5)).Acts, a => a.Kind == "Welcomed");
        var kin = new Dictionary<string, Kin> { [DefaultTown.Newcomer] = Kin.Sibling };
        var kinBack = new Dictionary<string, Kin> { ["Ann"] = Kin.Sibling };
        Assert.DoesNotContain(Scene(Welcome(), 3, V("Ann", "A", 3, family: kin), V(DefaultTown.Newcomer, "N", 5, family: kinBack)).Acts, a => a.Kind == "Welcomed");
        Assert.DoesNotContain(Scene(Welcome(), 3, V("Ann", "A", 3), V("Bob", "B", 5)).Acts, a => a.Kind == "Welcomed");
    }

    [Fact]
    public void NoWelcomeAtHome()
    {
        var kinds = new List<ActKind>(ActCatalog.Welcome);
        var home = new Location("Home:A", false, Enumerable.Repeat(new string('.', 40), 6).ToList());
        Villager Here(string name, string household, int x) => new(name, household, "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null,
            new[] { new Haunt("Home:A", new Tile(x, 2), 0, D, 1) }, new Dictionary<string, double>(), Array.Empty<string>());
        SimResult r = new Simulation(1, new[] { Here("Ann", "A", 3), Here(DefaultTown.Newcomer, "N", 5) }, new[] { home }, kinds,
            feelings: Welcome(), body: new BodyOptions { AwakeHoursAtRest = 100_000 }, wander: 0).Run(2);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Welcomed");
    }

    [Fact]
    public void OnTheShippedTownItIsTheNewcomersArc()
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.Acts.Welcome = true;
        SimResult r = new Simulation(1, kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(28);
        var welcomes = r.Acts.Where(a => a.Kind == "Welcomed").ToList();
        Assert.NotEmpty(welcomes);
        Assert.All(welcomes, a => Assert.True(a.Actor == DefaultTown.Newcomer || a.Target == DefaultTown.Newcomer, $"{a.Actor} {a.Target}"));
        Assert.All(welcomes.GroupBy(a => (a.Actor, a.Target)), g => Assert.Single(g)); // once for each ordered pair
        // The newcomer is better known by day 28 than without the slice.
        SimResult plain = new Simulation(1, feelings: DefaultTown.Feelings()).Run(28);
        double Known(SimResult x) => x.Names.Where(n => n != DefaultTown.Newcomer).Average(n => x.Familiarity[(n, DefaultTown.Newcomer)]);
        Assert.True(Known(r) > Known(plain), $"{Known(r)} against {Known(plain)}");
    }

    [Fact]
    public void WatchRecordsTheWelcomeAndStartsNothing()
    {
        FeelingOptions o = Welcome();
        o.Acts.Watch = true;
        SimResult r = Scene(o, 3, V("Ann", "A", 3), V(DefaultTown.Newcomer, "N", 5));
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Welcomed");
        Assert.Contains(r.CatalogWatch, w => w is { Holder: "Ann", Motive: DesireKind.Curious, Kind: "Welcomed" });
        Assert.Single(r.CatalogWatch, w => w.Holder == "Ann"); // a pair's curiosity is recorded once
        FeelingOptions off = Welcome();
        off.Acts.Welcome = false;
        Assert.Equal(Metrics.LogHash(Scene(off, 3, V("Ann", "A", 3), V(DefaultTown.Newcomer, "N", 5))), Metrics.LogHash(r));
    }
}
