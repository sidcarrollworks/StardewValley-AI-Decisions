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

    /// <summary>The tiles where the generated neighbourhoods join the core (town spec 2.3) stay free,
    /// so the 31 town nests inside the 60 and the 120.</summary>
    [Fact]
    public void TheSlotsAttachmentTilesStayFree()
    {
        var used = Towns.Pelican31().Links.SelectMany(l => new[] { (l.A, l.DoorA), (l.B, l.DoorB) }).ToHashSet();
        foreach (var tile in new[] { ("Square", new Tile(29, 10)), ("MountainPath", new Tile(12, 0)), ("ForestPath", new Tile(8, 2)), ("Beach", new Tile(29, 6)) })
            Assert.DoesNotContain(tile, used);
    }

    [Fact]
    public void TownCheckFindsWhatTheEngineWouldForgive()
    {
        TownData t = Towns.Pelican31();
        var bad = t with
        {
            Places = t.Places.Append(new Location("Island", true, new[] { "....", "...." })).ToList(),
            Links = t.Links.Append(new Link("TownLane", new Tile(14, 2), "Beach", new Tile(1, 1))).ToList(),
            Cast = t.Cast.Select(v => v.Name == "Linus" ? v with { Haunts = new[] { new Haunt("Saloon", new Tile(3, 7), 0, 60, 1) } } : v).ToList(),
            Authority = new AuthorityOptions { Mayor = "Lewis", Keepers = new Dictionary<string, string> { ["Store"] = "Nobody" },
                LockupPlace = "Home:Manor", LockupSpot = new Tile(7, 2), ServicePlace = "Square", ServiceSpot = new Tile(12, 16) },
        };
        var problems = TownCheck.Problems(bad);
        Assert.Contains(problems, p => p.Contains("Island") && p.Contains("no door leads there"));
        Assert.Contains(problems, p => p.Contains("TownLane (14,2) is already a door"));
        Assert.Contains(problems, p => p.Contains("Linus's haunt") && p.Contains("can't be stood on"));
        Assert.Contains(problems, p => p.Contains("keeper of Store: Nobody"));
    }

    /// <summary>What a hand-edited town file can get wrong, found without a crash: a place that isn't a
    /// rectangle, a home that doesn't exist, groceries at a shop that sells none, a starting regard
    /// and a hub's household that name nobody, a parent too young, a place too wide and too many
    /// places for the replay.</summary>
    [Fact]
    public void TownCheckFindsWhatAHandEditCanBreak()
    {
        TownData t = Towns.Pelican31();
        var ragged = t.Places.Select(p => p.Name == "Blacksmith" ? p with { Rows = p.Rows.Select((r, i) => i == 6 ? r[..^1] : r).ToList() } : p);
        var bad = t with
        {
            Places = ragged.Append(new Location("Field", true, new[] { new string('.', 300) })).Concat(Enumerable.Range(0, 100).Select(i => new Location($"Shed{i:000}", false, new[] { "..." }))).ToList(),
            Cast = t.Cast.Select(v => v.Name switch
            {
                "Linus" => v with { Household = "Camp" },
                "Penny" => v with { Age = 40 }, // Pam's daughter, now older than her mother
                _ => v,
            }).ToList(),
            Economy = t.Economy! with { GroceriesAt = t.Economy!.GroceriesAt.Concat(new[] { KeyValuePair.Create("FishShop", "FishShop") }).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value) },
            Gatherings = t.Gatherings.Select((g, i) => i == 0 ? g with { Local = new[] { "Nowhere" } } : g).ToList(),
            Feelings = new FeelingOptions { Start = new Dictionary<(string, string), double>(t.Feelings.Start) { [("Clint", "Emilly")] = 0.4 } },
        };
        var problems = TownCheck.Problems(bad);
        Assert.Contains(problems, p => p.Contains("Blacksmith is not a rectangle"));
        Assert.Contains(problems, p => p.Contains("Linus: no home Home:Camp"));
        Assert.Contains(problems, p => p.Contains("groceries of FishShop"));
        Assert.Contains(problems, p => p.Contains("Clint->Emilly"));
        Assert.Contains(problems, p => p.Contains("no household Nowhere"));
        Assert.Contains(problems, p => p.Contains("Penny (40): parent Pam"));
        Assert.Contains(problems, p => p.Contains("Field is 300 x 1"));
        Assert.Contains(problems, p => p.Contains("places: the replay holds up to 127"));
    }

    /// <summary>--tensions sets the shipped tensions' depth and keeps a town's own starting regards.</summary>
    [Fact]
    public void TensionsKeepATownsOwnStartingRegards()
    {
        Assert.Equal(DefaultTown.Tensions(0.2).OrderBy(x => x.Key), Towns.WithTensions(DefaultTown.Feelings().Start, 0.2).OrderBy(x => x.Key));
        var start = Towns.Pelican31().Feelings.Start;
        Assert.Equal(0.4, start[("Clint", "Emily")]);
        var shallow = Towns.WithTensions(start, 0.1);
        Assert.Equal(0.4, shallow[("Clint", "Emily")]);
        Assert.Equal(-0.1, shallow[("Pierre", "Shane")]);
        var none = Towns.WithTensions(start, 0);
        Assert.Equal(0.4, none[("Clint", "Emily")]);
        Assert.DoesNotContain(("Pierre", "Shane"), none.Keys);
    }

    [Fact]
    public void The31TownRunsTheSameEveryTime_AndIsPinned()
    {
        string first = Metrics.LogHash(new Simulation(1, Towns.Pelican31()).Run(7));
        Assert.Equal(first, Metrics.LogHash(new Simulation(1, Towns.Pelican31()).Run(7)));
        Assert.Equal("db268a57b95bada2", Metrics.LogHash(new Simulation(1, Towns.Pelican31()).Run(112)));
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
