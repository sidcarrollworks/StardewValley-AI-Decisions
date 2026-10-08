using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Town spec T2b: the 31-person town (the shipped 26 with Clint, Willy, Elliott, Linus and
/// the Wizard), and TownCheck, which catches what the engine would forgive quietly.</summary>
public class Town31Tests
{
    private static readonly string[] Five = { "Clint", "Willy", "Elliott", "Linus", "Wizard" };

    [Fact]
    public void TheShippedTownAndThe31TownAreSound()
    {
        Assert.Empty(TownCheck.Problems(TownData.Default()));
        Assert.Empty(TownCheck.Problems(Towns.Pelican31()));
    }

    [Fact]
    public void The31TownKeepsTheShippedTownWhole()
    {
        TownData shipped = TownData.Default(), grown = Towns.Pelican31();
        Assert.Equal(31, grown.Cast.Count);
        Assert.Equal(shipped.Cast.Select(v => v.Name), grown.Cast.Take(26).Select(v => v.Name));
        Assert.Equal(Five, grown.Cast.Skip(26).Select(v => v.Name));
        foreach (Villager v in shipped.Cast)
        {
            Villager g = grown.Cast.Single(x => x.Name == v.Name);
            Assert.Equal(v.Temperament, g.Temperament);
            Assert.Equal(v.Household, g.Household);
            static object? Of(Job? j) => j is null ? null : (j.Place, j.Spot, j.Start, j.End, string.Join(",", j.DaysOff), j.Effort, j.Commute);
            Assert.Equal(Of(v.Job), Of(g.Job));
            Assert.Equal(v.Haunts, g.Haunts);
        }
        foreach (Location p in shipped.Places)
            Assert.Equal(p.Rows, grown.Places.Single(x => x.Name == p.Name).Rows);
        Assert.All(shipped.Links, l => Assert.Contains(l, grown.Links));
        // The five live apart: none is kin to anyone, and each has a home of their own.
        Assert.All(grown.Cast.Where(v => Five.Contains(v.Name)), v => Assert.Empty(v.Family!));
        Assert.Equal(5, grown.Cast.Where(v => Five.Contains(v.Name)).Select(v => v.Household).Distinct().Count());
    }

    [Fact]
    public void TownCheckFindsWhatTheEngineWouldForgive()
    {
        TownData t = Towns.Pelican31();
        var bad = t with
        {
            Places = t.Places.Append(new Location("Island", true, new[] { "....", "...." })).ToList(),
            Links = t.Links.Append(new Link("Square", new Tile(29, 10), "Beach", new Tile(1, 1))).ToList(),
            Cast = t.Cast.Select(v => v.Name == "Linus" ? v with { Haunts = new[] { new Haunt("Saloon", new Tile(3, 7), 0, 60, 1) } } : v).ToList(),
            Authority = new AuthorityOptions { Mayor = "Lewis", Keepers = new Dictionary<string, string> { ["Store"] = "Nobody" },
                LockupPlace = "Home:Manor", LockupSpot = new Tile(7, 2), ServicePlace = "Square", ServiceSpot = new Tile(12, 16) },
        };
        var problems = TownCheck.Problems(bad);
        Assert.Contains(problems, p => p.Contains("Island") && p.Contains("no door leads there"));
        Assert.Contains(problems, p => p.Contains("Square (29,10) is already a door"));
        Assert.Contains(problems, p => p.Contains("Linus's haunt") && p.Contains("can't be stood on"));
        Assert.Contains(problems, p => p.Contains("keeper of Store: Nobody"));
    }

    [Fact]
    public void The31TownRunsTheSameEveryTime_AndIsPinned()
    {
        string first = Metrics.LogHash(new Simulation(1, Towns.Pelican31()).Run(7));
        Assert.Equal(first, Metrics.LogHash(new Simulation(1, Towns.Pelican31()).Run(7)));
        Assert.Equal("f0103b3ea9ec7bf3", Metrics.LogHash(new Simulation(1, Towns.Pelican31()).Run(112)));
    }

    /// <summary>The five go about their days where their cards put them: Clint at the anvil on a
    /// workday, Willy in his shop, the Wizard mostly in his tower.</summary>
    [Fact]
    public void TheFiveKeepTheirDays()
    {
        var sim = new Simulation(2, Towns.Pelican31());
        var at = new Dictionary<(string, string), int>();
        int awakeWizard = 0;
        sim.Run(14, (m, s) =>
        {
            foreach (string n in Five)
            {
                var w = s.Where(n);
                if (w.Asleep)
                    continue;
                at[(n, w.Place)] = at.GetValueOrDefault((n, w.Place)) + 1;
                if (n == "Wizard")
                    awakeWizard++;
            }
        });
        Assert.True(at.GetValueOrDefault(("Clint", "Blacksmith")) > 14 * 4 * 60, "Clint works the anvil");
        Assert.True(at.GetValueOrDefault(("Willy", "FishShop")) > 12 * 4 * 60, "Willy keeps his shop");
        int home = at.GetValueOrDefault(("Wizard", "Home:Tower")) + at.GetValueOrDefault(("Wizard", "TowerPath"));
        Assert.True(home > 0.8 * awakeWizard, $"the Wizard keeps to his tower: {home} of {awakeWizard} waking minutes");
    }
}
