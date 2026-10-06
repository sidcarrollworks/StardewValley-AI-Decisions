using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Traces, reports, the mayor's verdicts, the ladder and the constable (design rule 16;
/// Sid, 2026-10-06).</summary>
public class AuthorityTests
{
    private static Haunt AllDay(string place, int x, int y) => new(place, new Tile(x, y), 0, Clock.MinutesPerDay, 1);

    /// <summary>Someone who spends the day in the haunts given, never tires, and has no home (so
    /// they sleep where they stand).</summary>
    private static Villager P(string name, Job? job, params Haunt[] haunts)
        => new(name, name, "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), job, haunts,
            new Dictionary<string, double>(), Array.Empty<string>());

    private static Job WorksAllDay(string place, int x, int y)
        => new(place, new Tile(x, y), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1);

    private static readonly int Ten = Clock.At(10);

    private static ActKind Stole(TraceKind? trace = null) => new("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(), Trace: trace);
    private static readonly ActKind Warned = new(Authority.Warned, 3.0, -1, 1, 5, 0, Array.Empty<string>());
    private static readonly ActKind TakenIn = new(Authority.TakenIn, 3.5, -1, 1, 5, 0, Array.Empty<string>());

    private static Location Room(string name, int w = 40, int h = 6) => new(name, false, Enumerable.Repeat(new string('.', w), h).ToList());

    [Fact]
    public void AnUnseenTheftIsFoundByTheKeeper_AsSomeone()
    {
        // A wall splits the shop: Tom steals at the front; Kim (the keeper) and Ann work at the back.
        var shop = new Location("Shop", false, new[]
        {
            "............", "............", "............", "############", "............", "............", "............",
        });
        var cast = new[] { P("Tom", null, AllDay("Shop", 5, 1)), P("Kim", WorksAllDay("Shop", 5, 5)), P("Ann", null, AllDay("Shop", 8, 5)) };
        var count = new TraceKind("MissingStock", KeeperOnly: true, LastsMinutes: 3 * Clock.MinutesPerDay, NoticePerHour: 0.9);
        var authority = new AuthorityOptions { Keepers = new Dictionary<string, string> { ["Shop"] = "Kim" } };
        SimResult r = new Simulation(1, cast, new[] { shop }, new[] { Stole(count) }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(1);

        Assert.Equal(0, r.Witnesses[0]);
        Belief kim = r.Beliefs["Kim"][0];
        Assert.Equal(Source.Found, kim.Source);
        Assert.Null(kim.Actor);                       // she knows it happened, not who
        Assert.Equal(4.5 * Simulation.FoundFactor, kim.Juiciness, 6);
        if (r.Beliefs["Ann"].TryGetValue(0, out Belief? ann))
            Assert.Equal(Source.Told, ann.Source);    // only the keeper counts the stock
        Assert.False(r.Beliefs["Tom"].ContainsKey(0));
    }

    [Fact]
    public void AScatteredBinIsNoticedByPassersBy()
    {
        // Tom rummages at 10:00 while Pat is away; Pat comes to the yard at noon and sees the mess.
        var places = new[] { new Location("Yard", true, Enumerable.Repeat(new string('.', 12), 6).ToList()), Room("Away", 6, 6) };
        var cast = new[]
        {
            P("Tom", null, AllDay("Yard", 2, 2)),
            P("Pat", null, new Haunt("Away", new Tile(2, 2), 0, Clock.At(12), 1), new Haunt("Yard", new Tile(4, 3), Clock.At(12), Clock.MinutesPerDay, 1)),
        };
        var mess = new TraceKind("ScatteredBin", KeeperOnly: false, LastsMinutes: 12 * 60, NoticePerHour: 0.9);
        var bin = new ActKind("RummagedInBin", 4.0, -1, 1, 3, 0, Array.Empty<string>(), Trace: mess);
        SimResult r = new Simulation(2, cast, places, new[] { bin }, scheduled: new[] { (Ten, "Tom", "RummagedInBin") }, wander: 0).Run(1);

        Assert.Equal(0, r.Witnesses[0]);
        Belief pat = r.Beliefs["Pat"][0];
        Assert.Equal(Source.Found, pat.Source);
        Assert.Null(pat.Actor);
        Assert.True(pat.GotTick >= Clock.At(12));
    }

    /// <summary>Tom steals two tiles from Kim, the keeper; May, the mayor, stands eight tiles from
    /// Tom and six from Kim.</summary>
    private static (Villager[] Cast, AuthorityOptions Authority) Theft(IReadOnlyDictionary<string, int>? record = null, string? lockup = null)
    {
        var cast = new[] { P("Tom", null, AllDay("Room", 3, 2)), P("Kim", WorksAllDay("Room", 5, 2)), P("May", null, AllDay("Room", 11, 2)) };
        var authority = new AuthorityOptions
        {
            Mayor = "May",
            ElectConstable = false,
            Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
            Record = record ?? new Dictionary<string, int>(),
            LockupPlace = lockup ?? "",
            LockupSpot = new Tile(2, 2),
            DetainMinutes = 600,
        };
        return (cast, authority);
    }

    [Fact]
    public void AClearReportFromTheVictimLeadsToAWarningInPerson()
    {
        var (cast, authority) = Theft();
        SimResult r = new Simulation(3, cast, new[] { Room("Room") }, new[] { Stole(), Warned }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(1);

        Assert.Contains(r.Accounts, a => a.From == "Kim" && a.Actor == "Tom" && a.FirstHand);
        Verdict v = Assert.Single(r.Verdicts);
        Assert.Equal("Tom", v.Accused);
        Assert.True(v.Correct);
        Assert.False(v.LetOff);
        Assert.Equal(Consequence.Warning, v.Step);
        Act warned = Assert.Single(r.Acts, a => a.Kind == Authority.Warned);
        Assert.Equal("Tom", warned.Actor);            // the story is about the person warned
        Assert.True(warned.Tick >= v.Tick);
    }

    [Fact]
    public void AfterThreeVerdictsTheNextIsDetention_HeldForTheTime()
    {
        var (cast, authority) = Theft(new Dictionary<string, int> { ["Tom"] = 3 }, lockup: "Cell");
        var places = new[] { Room("Room"), Room("Cell", 6, 6) };
        var where = new Dictionary<int, string>();
        SimResult r = new Simulation(4, cast, places, new[] { Stole(), TakenIn }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(2, (m, sim) => where[m] = sim.Where("Tom").Place);

        Verdict v = Assert.Single(r.Verdicts);
        Assert.Equal(Consequence.Detained, v.Step);
        Act taken = Assert.Single(r.Acts, a => a.Kind == Authority.TakenIn);
        Assert.Equal("Cell", where[taken.Tick + 60]);
        Assert.Equal("Cell", where[taken.Tick + 500]);
        Assert.Equal("Room", where[taken.Tick + 700]); // released after 600 minutes
    }

    [Fact]
    public void TheConstableCarriesAReportToTheMayor()
    {
        // Kim reports to Con, the constable, in the room; Con meets May in the hall at noon.
        var cast = new[]
        {
            P("Tom", null, AllDay("Room", 3, 2)),
            P("Kim", WorksAllDay("Room", 5, 2)),
            P("Con", null, new Haunt("Room", new Tile(9, 2), 0, Clock.At(12), 1), new Haunt("Hall", new Tile(3, 3), Clock.At(12), Clock.MinutesPerDay, 1)),
            P("May", null, AllDay("Hall", 4, 3)),
        };
        var authority = new AuthorityOptions
        {
            Mayor = "May", Constable = "Con",
            Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
        };
        SimResult r = new Simulation(5, cast, new[] { Room("Room"), Room("Hall", 10, 6) }, new[] { Stole(), Warned }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(1);

        Assert.Contains(r.Log, l => l.Contains(" report Kim Con 0 Tom"));
        Assert.DoesNotContain(r.Log, l => l.Contains(" report Kim May "));
        Assert.Contains(r.Log, l => l.Contains(" handed-on Con May"));
        Account kim = Assert.Single(r.Accounts, a => a.From == "Kim");
        Assert.True(kim.Tick >= Clock.At(12));        // it reached the mayor only when Con did
        Assert.Equal("Tom", Assert.Single(r.Verdicts).Accused);
    }

    [Fact]
    public void TheMayorWeighsWhatHeIsTold()
    {
        var o = new AuthorityOptions();
        static double Trust(string _) => 1.0;
        Account Saw(string from, string? actor, double confidence = 1) => new(0, from, actor, confidence, true, 0);
        Account Heard(string from, string? actor) => new(0, from, actor, 1, false, 0);

        Assert.Equal("Tom", Authority.Weigh(new[] { Saw("Ann", "Tom") }, Trust, o).Accused);
        Assert.Null(Authority.Weigh(new[] { Heard("Ann", "Tom") }, Trust, o).Accused);          // hearsay alone: no
        Assert.Equal("Tom", Authority.Weigh(new[] { Heard("Ann", "Tom"), Heard("Bob", "Tom") }, Trust, o).Accused);
        Assert.Null(Authority.Weigh(new[] { Saw("Ann", "Tom"), Saw("Bob", "Sam", 0.8) }, Trust, o).Accused); // accounts split
        Assert.Null(Authority.Weigh(new[] { Saw("Ann", null) }, Trust, o).Accused);             // "someone"
        Assert.Null(Authority.Weigh(new[] { Saw("Ann", "Tom") }, _ => 0.5, o).Accused);         // a teller he barely trusts
    }

    [Fact]
    public void LewisIsRarelySwayed_AndOnlyBySomeoneClose()
    {
        var o = new AuthorityOptions();
        int close = Enumerable.Range(0, 5000).Count(i => Authority.Swayed(7, i, close: true, o));
        Assert.InRange(close, 50, 160); // about 2%
        Assert.Equal(0, Enumerable.Range(0, 5000).Count(i => Authority.Swayed(7, i, close: false, o)));
    }

    [Fact]
    public void TheLadderClimbsWithEachVerdict()
    {
        Assert.Equal(Consequence.Warning, Authority.StepFor(0));
        Assert.Equal(Consequence.RestitutionAndFine, Authority.StepFor(1));
        Assert.Equal(Consequence.Service, Authority.StepFor(2));
        Assert.Equal(Consequence.Detained, Authority.StepFor(3));
        Assert.Equal(Consequence.Detained, Authority.StepFor(9));
    }

    [Fact]
    public void EachRunOpensWithAVoteForConstable_NotAlwaysTheSameOne()
    {
        var bold = DefaultTown.Cast().ToDictionary(v => v.Name, v => v.Temperament.Boldness);
        int voters = DefaultTown.Cast().Count(v => v.Name != DefaultTown.Newcomer && v.Age >= Simulation.VotingAge);
        var winners = new List<string>();
        foreach (long seed in Enumerable.Range(1, 40))
        {
            SimResult r = new Simulation(seed).Run(1);
            Election e = Assert.Single(r.Elections);
            Assert.Equal("Constable", e.Office);
            Assert.Equal(Clock.At(12), e.Tick);
            Assert.NotEqual(DefaultTown.Mayor, e.Winner);
            Assert.NotEqual(DefaultTown.Newcomer, e.Winner);
            Assert.True(bold[e.Winner] >= 0.5);
            Assert.Equal(voters, e.Votes.Values.Sum()); // everyone 16 or over but the newcomer
            Assert.True(DefaultTown.Cast().First(v => v.Name == e.Winner).Age >= 20);
            winners.Add(e.Winner);
        }
        Assert.True(winners.Distinct().Count() >= 3);
        Assert.Equal(new Simulation(5).Run(1).Constable, new Simulation(5).Run(1).Constable);
    }

    [Fact]
    public void TheConstablePatrolsThePublicPlaces()
    {
        var authority = DefaultTown.TownAuthority();
        authority.Constable = "Haley";
        var visited = new HashSet<string>();
        new Simulation(6, authority: authority).Run(3, (m, sim) =>
        {
            int t = Clock.OfDay(m);
            if (Clock.Day(m) >= 1 && t >= Clock.At(10) && t < Clock.At(12))
                visited.Add(sim.Where("Haley").Place);
        });
        Assert.True(visited.Intersect(authority.Patrol.Keys).Count() >= 3, string.Join(",", visited));
    }
}
