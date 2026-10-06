using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Piecing together "someone" from who was around, and the constable's interviews
/// (Sid, 2026-10-06).</summary>
public class SuspicionTests
{
    private static Villager V(string name, string household, params Haunt[] haunts)
        => new(name, household, "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null, haunts,
            new Dictionary<string, double>(), Array.Empty<string>());

    private static Haunt At(string place, int x, int y, int from = 0, int to = Clock.MinutesPerDay) => new(place, new Tile(x, y), from, to, 1);

    private static Location Room(string name = "Room", int w = 40, int h = 6) => new(name, false, Enumerable.Repeat(new string('.', w), h).ToList());

    private static readonly int Ten = Clock.At(10);
    private static readonly ActKind Bin = new("RummagedInBin", 4.0, -1, 1, 1, 0, Array.Empty<string>());
    private static readonly ActKind Stole = new("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>());
    private static readonly ActKind Warned = new(Authority.Warned, 3.0, -1, 1, 5, 0, Array.Empty<string>());
    private static readonly ActKind Questioned = new(Authority.Questioned, 2.5, -1, 1, 10, 0, Array.Empty<string>());

    [Fact]
    public void AWitnessWhoSawSomeone_SuspectsWhoWasAround_StrangersFirst()
    {
        // Far sees Bob rummage from 7 tiles: "someone". Around Far: Kin (family), the newcomer (a
        // stranger), and Bob himself.
        var cast = new[]
        {
            V("Far", "F", At("Room", 12, 2)), V("Kin", "F", At("Room", 10, 2)),
            V(DefaultTown.Newcomer, "Farm", At("Room", 6, 4)), V("Bob", "B", At("Room", 5, 2)),
        };
        SimResult r = new Simulation(1, cast, new[] { Room() }, new[] { Bin },
            scheduled: new[] { (Ten, "Bob", "RummagedInBin") }, wander: 0).Run(1);

        Belief far = r.Beliefs["Far"][0];
        Assert.Null(far.Actor);
        Assert.Equal(new[] { DefaultTown.Newcomer, "Bob", "Kin" }, far.Suspects);
    }

    [Fact]
    public void SuspicionTravelsWithTheStory()
    {
        // Cal comes to the room from noon, after Far has pieced it together, and hears it from Far
        // (that day, or the next if Far has already told it to someone today).
        var cast = new[]
        {
            V("Far", "F", At("Room", 12, 2)), V("Bob", "B", At("Room", 5, 2)),
            V("Cal", "C", At("Away", 2, 2, 0, Clock.At(12)), At("Room", 18, 2, Clock.At(12))),
        };
        SimResult r = new Simulation(2, cast, new[] { Room(), Room("Away", 6, 6) }, new[] { Bin },
            gossip: new GossipOptions { ChatChance = 10 }, scheduled: new[] { (Ten, "Bob", "RummagedInBin") }, wander: 0).Run(2);

        Belief cal = r.Beliefs["Cal"][0];
        Assert.Equal(Source.Told, cal.Source);
        Assert.Null(cal.Actor);
        Assert.Contains("Bob", cal.Suspects!);
    }

    [Fact]
    public void BeingNearbyNeverDecidesACaseAlone_AConfessionDoes()
    {
        var o = new AuthorityOptions();
        static double Trust(string _) => 1.0;
        Account Near(string from, params string[] names) => new(0, from, null, 0, true, 0, names);

        var four = new[] { Near("A", "Tom"), Near("B", "Tom"), Near("C", "Tom"), Near("D", "Tom") };
        var (accused, weight, _) = Authority.Weigh(four, Trust, o);
        Assert.Null(accused);
        Assert.True(weight >= o.VerdictWeight); // heavy enough, but nobody saw him do it

        var withHearsay = four.Append(new Account(0, "E", "Tom", 1, false, 0));
        Assert.Equal("Tom", Authority.Weigh(withHearsay, Trust, o).Accused);

        // A confession counts in full, even from a stranger the mayor barely trusts.
        Assert.Equal("Newc", Authority.Weigh(new[] { new Account(0, "Newc", "Newc", 1, true, 0) }, _ => 0.5, o).Accused);
    }

    /// <summary>Tom steals; Wes and May (the mayor) see "someone" from 7 and 8 tiles and suspect
    /// Tom; May questions him.</summary>
    private static SimResult Questioning(double confess)
    {
        var cast = new[] { V("Tom", "T", At("Room", 3, 2)), V("Wes", "W", At("Room", 10, 2)), V("May", "M", At("Room", 11, 2)) };
        var authority = new AuthorityOptions { Mayor = "May", ElectConstable = false, ConfessBase = confess, ConfessPerTimidity = 0 };
        return new Simulation(3, cast, new[] { Room() }, new[] { Stole, Warned, Questioned }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(1);
    }

    [Fact]
    public void QuestionedTheCulpritCanConfess_AndIsWarned()
    {
        SimResult r = Questioning(confess: 1);
        Assert.Contains(r.Interviews, i => i.ActId == 0 && i.Who == "Tom" && i.Confessed);
        Assert.Contains(r.Acts, a => a.Kind == Authority.Questioned && a.Actor == "Tom");
        Verdict v = Assert.Single(r.Verdicts);
        Assert.Equal("Tom", v.Accused);
        Assert.True(v.Correct);
        Assert.Contains(r.Acts, a => a.Kind == Authority.Warned && a.Actor == "Tom");
    }

    [Fact]
    public void WithoutAConfession_TheQuestionedSayWhoTheySaw_AndTheCaseStaysOpen()
    {
        SimResult r = Questioning(confess: 0);
        Assert.Contains(r.Interviews, i => i.Who == "Tom" && !i.Confessed);
        Assert.Contains(r.Interviews, i => i.Who == "Wes");          // named by Tom, so questioned too
        Assert.Contains(r.Accounts, a => a.From == "Wes" && a.Nearby?.Contains("Tom") == true);
        Assert.Empty(r.Verdicts);                                     // nobody saw who did it
    }
}
