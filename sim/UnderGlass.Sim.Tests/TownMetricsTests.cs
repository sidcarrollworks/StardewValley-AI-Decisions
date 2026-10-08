using UnderGlass.Sim;
using UnderGlass.Sim.Generation;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>TownMetrics (town spec 6.3): districts, tellings inside them against chance, and how a
/// neighbourhood's stories travel.</summary>
public class TownMetricsTests
{
    [Fact]
    public void EveryoneHasADistrict_TheCoreAndEachNeighbourhood()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var d = TownMetrics.Districts(town);
        Assert.Equal(60, d.Count);
        Assert.Equal(31, d.Values.Count(x => x == "core"));
        Assert.Equal(15, d.Values.Count(x => x == "EastGreen"));
        Assert.Equal(14, d.Values.Count(x => x == "NorthLane"));
        Assert.All(TownMetrics.Districts(TownData.Default()).Values, x => Assert.Equal("core", x));
    }

    /// <summary>A place belongs to the neighbourhood it hangs from (its road, its shops, its homes),
    /// everyone lives in their own district, and the replay records both for the viewer.</summary>
    [Fact]
    public void PlacesBelongToTheNeighbourhoodTheyHangFrom()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var places = TownMetrics.PlaceDistricts(town);
        Assert.Equal(town.Places.Count, places.Count);
        Assert.Equal(("EastGreen", "EastGreen", "EastGreen"), (places["EastGreen"], places["EastRoad"], places["Cafe"]));
        Assert.Equal(("NorthLane", "NorthLane", "NorthLane"), (places["NorthLane"], places["NorthRoad"], places["Workshop"]));
        Assert.Equal(("core", "core", "core"), (places["Square"], places["MountainPath"], places["Home:Tent"]));
        var people = TownMetrics.Districts(town);
        Assert.All(town.Cast, v => Assert.Equal(people[v.Name], places[v.Home]));
        Assert.All(TownMetrics.PlaceDistricts(Towns.Pelican31()).Values, x => Assert.Equal("core", x));

        using var doc = System.Text.Json.JsonDocument.Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 1, Town = town }));
        var recorded = doc.RootElement.GetProperty("people").EnumerateArray().Select(p => p.GetProperty("district").GetString()).ToList();
        Assert.Equal((31, 15, 14), (recorded.Count(x => x == "core"), recorded.Count(x => x == "EastGreen"), recorded.Count(x => x == "NorthLane")));
        Assert.Contains(doc.RootElement.GetProperty("places").EnumerateArray(), p => p.GetProperty("name").GetString() == "Cafe" && p.GetProperty("district").GetString() == "EastGreen");
    }

    /// <summary>Chance is a crowd, each teller's listeners drawn evenly: when everyone tells everyone once,
    /// the share inside a district is exactly chance (ratio 1), however unequal the districts; when
    /// the core talks twice as much, still 1; when people only talk at home, above 1.</summary>
    [Fact]
    public void ACrowdScoresOne_ANeighbourhoodScoresMore()
    {
        var district = new Dictionary<string, string> { ["a1"] = "A", ["a2"] = "A", ["a3"] = "A", ["a4"] = "A", ["b1"] = "B", ["b2"] = "B" };
        var everyone = district.Keys.SelectMany(t => district.Keys.Where(l => l != t).Select(l => (T: t, L: l))).ToList();
        double Same(List<(string T, string L)> told) => told.Count(x => district[x.T] == district[x.L]) / (double)told.Count;
        Assert.Equal(1.0, Same(everyone) / TownMetrics.ChanceSameDistrict(everyone, district), 9);
        var chattyA = everyone.Concat(everyone.Where(x => district[x.T] == "A")).ToList();
        Assert.Equal(1.0, Same(chattyA) / TownMetrics.ChanceSameDistrict(chattyA, district), 9);
        var home = everyone.Where(x => district[x.T] == district[x.L]).ToList();
        Assert.True(Same(home) / TownMetrics.ChanceSameDistrict(home, district) > 1.5);
    }

    /// <summary>Ties: a feud or a friendship across two districts counts half in each, so the rows,
    /// weighted by their people, add up to the town's; a neighbourhood's stories are its people's
    /// acts at home.</summary>
    [Fact]
    public void RowsAddUpToTheTown_AndStoriesBeginAtHome()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var runs = new[] { new Simulation(1, town).Run(28), new Simulation(2, town).Run(28) };
        TownStats s = TownMetrics.Summarise(runs, town);
        double years = runs.Sum(r => r.Days) / (double)WithdrawalMetrics.Year;
        Assert.Equal(runs.Sum(r => r.Ties.Count(t => t.What is "feud" or "kin-feud")), s.Districts.Sum(d => d.FeudsPerPerson * d.People * years), 6);
        Assert.Equal(runs.Sum(r => r.Ties.Count(t => t.What == "friendship")), s.Districts.Sum(d => d.FriendshipsPerPerson * d.People * years), 6);
        var people = TownMetrics.Districts(town);
        var places = TownMetrics.PlaceDistricts(town);
        int atHome = runs.Sum(r => r.Acts.Count(a => people.TryGetValue(a.Actor, out string? h) && h != "core" && places.GetValueOrDefault(a.Location) == h
            && r.Beliefs.Count(b => b.Key != a.Actor && b.Value.ContainsKey(a.Id)) >= 3));
        Assert.Equal(atHome, s.NeighbourhoodStories);
    }

    /// <summary>The shares are what the log says: counted again here from the "told" lines.</summary>
    [Fact]
    public void TellingsInsideADistrictAreCountedFromTheLog()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var runs = new[] { new Simulation(1, town).Run(3), new Simulation(2, town).Run(3) };
        TownStats s = TownMetrics.Summarise(runs, town);
        var d = TownMetrics.Districts(town);
        var told = runs.SelectMany(r => r.Log).Select(l => l.Split(' ')).Where(w => w.Length >= 5 && w[1] == "told").ToList();
        Assert.NotEmpty(told);
        Assert.Equal(told.Count(w => d[w[2]] == d[w[3]]) / (double)told.Count, s.SameDistrict, 9);
        Assert.Equal(TownMetrics.ChanceSameDistrict(told.Select(w => (w[2], w[3])).ToList(), d), s.SameByChance, 9);
        Assert.Equal(s.SameDistrict / s.SameByChance, s.LocalityRatio, 9);
        Assert.InRange(s.FirstDayLocal, 0, 1);
        Assert.InRange(s.CrossedInTwoDays, 0, 1);
        Assert.InRange(s.KnownWellMedian, 0, 59);
        Assert.Equal(3, s.Districts.Count);
        Assert.Equal("core", s.Districts[0].District);
    }
}
