using UnderGlass.Sim;
using UnderGlass.Sim.Generation;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The town generator (town spec 4; steps T3 and T4 at 60): the same spec builds the same
/// town, every town is sound, the core is untouched, a bigger town keeps a smaller one's people, and
/// the people it makes look like the core's.</summary>
public class TownGenTests
{
    private static readonly IReadOnlyList<string> Core = Towns.Pelican31().Cast.Select(v => v.Name).ToList();

    [Fact]
    public void TheSameSpecBuildsTheSameTown_AndTheHashesArePinned()
    {
        Assert.Equal(Census.Hash(TownGen.Build(new TownSpec(1, 60))), Census.Hash(TownGen.Build(TownSpec.Parse("pelican:60@1"))));
        Assert.NotEqual(Census.Hash(TownGen.Build(new TownSpec(1, 60))), Census.Hash(TownGen.Build(new TownSpec(2, 60))));
        // Update only when a deliberate change to the generator lands (and raise TownGen.Version).
        // Version 2 (2026-10-08, the review's fixes; the hash now covers the whole town file).
        Assert.Equal("9e4cfd730011e798", Census.Hash(TownGen.Build(new TownSpec(1, 60))));
        Assert.Equal("7526ebc824f17303", Census.Hash(TownGen.Build(new TownSpec(2, 60))));
        Assert.Equal("98db2dc5e3e49b06", Census.Hash(TownGen.Build(new TownSpec(3, 60))));
    }

    [Fact]
    public void SpecsReadAndWriteTheirShortForm()
    {
        TownSpec spec = TownSpec.Parse("pelican:60@7");
        Assert.Equal(new TownSpec(7, 60), spec);
        Assert.Equal("pelican:60@7", spec.ToString());
        Assert.Throws<ArgumentException>(() => TownSpec.Parse("pelican60"));
        var e = Assert.Throws<ArgumentException>(() => TownGen.Build(new TownSpec(1, 50)));
        Assert.Contains("31, 46, 60", e.Message);
        Assert.Equal(60, Towns.Named("pelican:60@4").Cast.Count);
    }

    /// <summary>TownCheck (which Build runs) passes for 200 town seeds; nobody is named twice or
    /// takes a reserved name; each slot holds its people exactly.</summary>
    [Fact]
    public void EveryTownOfSixtyIsSound()
    {
        for (long seed = 1; seed <= 200; seed++)
        {
            TownData town = TownGen.Build(new TownSpec(seed, 60));
            Assert.Equal(60, town.Cast.Count);
            Assert.Equal(town.Cast.Count, town.Cast.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            var generated = town.Cast.Where(v => !Core.Contains(v.Name)).ToList();
            Assert.All(generated, v => Assert.DoesNotContain(v.Name, Names.Reserved));
            Assert.Equal(15, generated.Count(v => Hood(town, v) == "EastGreen"));
            Assert.Equal(14, generated.Count(v => Hood(town, v) == "NorthLane"));
        }
    }

    private static string Hood(TownData town, Villager v)
    {
        string home = v.Home;
        Link l = town.Links.First(x => x.A == home || x.B == home);
        return l.A == home ? l.B : l.A;
    }

    [Fact]
    public void TheCoreIsUntouched()
    {
        TownData core = Towns.Pelican31(), grown = TownGen.Build(new TownSpec(5, 60));
        foreach (Villager v in core.Cast)
        {
            Villager g = grown.Cast.Single(x => x.Name == v.Name);
            Assert.Equal(v.Temperament, g.Temperament);
            Assert.Equal(v.Household, g.Household);
            Assert.Equal(v.Haunts, g.Haunts);
            Assert.Equal(v.Friends, g.Friends);
            Assert.Equal(v.Family!.OrderBy(f => f.Key), g.Family!.OrderBy(f => f.Key)); // no kin points into the core
        }
        foreach (Location p in core.Places)
            Assert.Equal(p.Rows, grown.Places.Single(x => x.Name == p.Name).Rows);
        Assert.All(core.Links, l => Assert.Contains(l, grown.Links));
        Assert.DoesNotContain(grown.Familiarity, s => Core.Contains(s.A) && Core.Contains(s.B)); // core pairs keep their seeds
    }

    /// <summary>Kin go both ways; a generated person's parent was 22 to 45 at their birth (22 to 40
    /// at home), whichever of two parents; nobody has more than two parents, and two parents are
    /// each other's spouses.</summary>
    [Fact]
    public void KinAgreeWithEachOtherAndWithAges()
    {
        for (long seed = 1; seed <= 50; seed++)
        {
            TownData town = TownGen.Build(new TownSpec(seed, 60));
            var byName = town.Cast.ToDictionary(v => v.Name);
            foreach (Villager v in town.Cast)
            {
                foreach (var (other, kin) in v.Family!)
                {
                    Assert.Equal(Ages.Reverse(kin), byName[other].KinOf(v.Name));
                    if (kin is Kin.Parent or Kin.Grandparent)
                        Assert.True(byName[other].Age >= v.Age + 18, $"{other} ({byName[other].Age}) is {v.Name}'s ({v.Age}) {kin}");
                }
                if (Core.Contains(v.Name))
                    continue;
                var parents = v.Family!.Where(f => f.Value == Kin.Parent).Select(f => byName[f.Key]).ToList();
                Assert.True(parents.Count <= 2, $"{v.Name} has {parents.Count} parents (seed {seed})");
                Assert.All(parents, p => Assert.InRange(p.Age - v.Age, 22, 45));
                if (parents.Count == 2)
                    Assert.Equal(Kin.Spouse, parents[0].KinOf(parents[1].Name));
            }
        }
    }

    /// <summary>No two generated people share a spot (a haunt or a place of work) in one place, staff
    /// in a core shop work near its keeper (not in the back room), and everyone under 20 with no job
    /// has an allowance.</summary>
    [Fact]
    public void SpotsAreTheirOwn_StaffWorkNearTheKeeper_AndTheYoungHaveAnAllowance()
    {
        for (long seed = 1; seed <= 50; seed++)
        {
            TownData town = TownGen.Build(new TownSpec(seed, 60));
            var generated = town.Cast.Where(v => !Core.Contains(v.Name)).ToList();
            var spots = generated.SelectMany(v => v.Haunts.Select(h => (Name: v.Name, Place: h.Place, Spot: h.Spot))
                .Concat(v.Job is { } j && j.Place != "Square" ? new[] { (Name: v.Name, Place: j.Place, Spot: j.Spot) } : Array.Empty<(string Name, string Place, Tile Spot)>()))
                .Distinct().ToList();
            var shared = spots.GroupBy(x => (x.Place, x.Spot)).Where(g => g.Select(x => x.Name).Distinct().Count() > 1).ToList();
            Assert.True(shared.Count == 0, $"seed {seed}: " + string.Join("; ", shared.Select(g => $"{g.Key.Place} {g.Key.Spot}: {string.Join(", ", g.Select(x => x.Name))}")));
            foreach (Villager v in generated.Where(v => v.Job is { } j && town.Authority.Keepers.ContainsKey(j.Place) && Core.Contains(town.Authority.Keepers[j.Place])
                                                       && !town.Places.Single(p => p.Name == j.Place).Outdoor))
            {
                Job keeper = town.Cast.Single(k => k.Name == town.Authority.Keepers[v.Job!.Place]).Job!;
                if (keeper.Place == v.Job!.Place)
                    Assert.True(v.Job.Spot.Chebyshev(keeper.Spot) <= 6, $"seed {seed}: {v.Name} works at {v.Job.Place} {v.Job.Spot}, the keeper at {keeper.Spot}");
            }
            Assert.All(generated.Where(v => v.Age < 20 && v.Job is null), v => Assert.True(town.Economy!.Allowances.ContainsKey(v.Name), $"seed {seed}: {v.Name} ({v.Age})"));
        }
    }

    /// <summary>Every generated home is within 25 tiles' walk of its own green's linger spot (town
    /// spec 2.4), walking only on open ground inside the neighbourhood.</summary>
    [Fact]
    public void EveryHomeIsNearItsGreen()
    {
        for (long seed = 1; seed <= 5; seed++)
        {
            TownData town = TownGen.Build(new TownSpec(seed, 60));
            foreach (string hood in new[] { "EastGreen", "NorthLane" })
            {
                Location place = town.Places.Single(p => p.Name == hood);
                Tile linger = town.Gatherings.Single(g => g.Name == "Green:" + hood).Center;
                foreach (Link l in town.Links.Where(l => l.A == hood && l.B.StartsWith("Home:", StringComparison.Ordinal)))
                    Assert.InRange(Walk(place, l.DoorA, linger), 0, 25);
            }
        }
    }

    /// <summary>Tiles walked between two open tiles of a place, a step at a time in four directions
    /// (no shorter than the engine's walk); -1 when there is no way.</summary>
    private static int Walk(Location place, Tile from, Tile to)
    {
        var dist = new Dictionary<Tile, int> { [from] = 0 };
        var queue = new Queue<Tile>(new[] { from });
        while (queue.Count > 0)
        {
            Tile t = queue.Dequeue();
            if (t == to)
                return dist[t];
            foreach (Tile n in new[] { new Tile(t.X + 1, t.Y), new Tile(t.X - 1, t.Y), new Tile(t.X, t.Y + 1), new Tile(t.X, t.Y - 1) })
                if (n.X >= 0 && n.Y >= 0 && n.X < place.Width && n.Y < place.Height && place.Walkable(n) && !dist.ContainsKey(n))
                {
                    dist[n] = dist[t] + 1;
                    queue.Enqueue(n);
                }
        }
        return -1;
    }

    /// <summary>The 31 town is the core itself: a spec of 31 people, whatever its seed, builds it
    /// unchanged.</summary>
    [Fact]
    public void ASpecOfThirtyOneIsTheThirtyOneTown()
    {
        string core = Census.Hash(Towns.Pelican31());
        Assert.Equal(core, Census.Hash(TownGen.Build(new TownSpec(1, 31))));
        Assert.Equal(core, Census.Hash(TownGen.Build(new TownSpec(9, 31))));
        Assert.NotEqual(core, Census.Hash(Towns.Pelican31() with { Wander = 0 })); // the hash sees every option
    }

    /// <summary>Generated adults look like the core's (town spec T3): each trait's mean within 0.1 of
    /// the core's, and its spread within half to one and a half times the core's, over 50 towns; and
    /// they keep the core's strongest link between traits (boldness against understanding, -0.70),
    /// wildcards included.</summary>
    [Fact]
    public void GeneratedPeopleKeepTheCoresTraits()
    {
        var core = Towns.Pelican31().Cast.Where(v => v.Age >= 18).Select(v => v.Temperament).ToList();
        var made = Enumerable.Range(1, 50).SelectMany(s => TownGen.Build(new TownSpec(s, 60)).Cast.Where(v => !Core.Contains(v.Name) && v.Age >= 18))
            .Select(v => v.Temperament).ToList();
        foreach (Func<Temperament, double> trait in new Func<Temperament, double>[] { t => t.Chattiness, t => t.Boldness, t => t.Understanding, t => t.SelfRegard, t => t.Sensitivity })
        {
            double cm = core.Average(trait), mm = made.Average(trait);
            double cs = Math.Sqrt(core.Average(t => Math.Pow(trait(t) - cm, 2))), ms = Math.Sqrt(made.Average(t => Math.Pow(trait(t) - mm, 2)));
            Assert.InRange(mm - cm, -0.1, 0.1);
            Assert.InRange(ms / cs, 0.5, 1.5);
        }
        double mb = made.Average(t => t.Boldness), mu = made.Average(t => t.Understanding);
        double r = made.Average(t => (t.Boldness - mb) * (t.Understanding - mu))
                   / Math.Sqrt(made.Average(t => Math.Pow(t.Boldness - mb, 2)) * made.Average(t => Math.Pow(t.Understanding - mu, 2)));
        Assert.InRange(r, -1, -0.4);
    }

    /// <summary>A bigger town keeps a smaller one's people (town spec 4.2): the 46 town's generated
    /// people are found unchanged in the 60, kin entries toward later slots aside.</summary>
    [Fact]
    public void TheSixtyTownKeepsTheFortySixTownsPeople()
    {
        foreach (long seed in new long[] { 1, 2, 3 })
        {
            TownData small = TownGen.Build(new TownSpec(seed, 46)), big = TownGen.Build(new TownSpec(seed, 60));
            var later = big.Cast.Select(v => v.Name).Except(small.Cast.Select(v => v.Name)).ToHashSet();
            foreach (Villager v in small.Cast.Where(v => !Core.Contains(v.Name)))
            {
                Villager w = big.Cast.Single(x => x.Name == v.Name);
                Assert.Equal(v.Temperament, w.Temperament);
                Assert.Equal((v.Household, v.Age, v.Kind, v.Birthday), (w.Household, w.Age, w.Kind, w.Birthday));
                Assert.Equal(v.Haunts, w.Haunts);
                Assert.Equal(v.Friends, w.Friends);
                Assert.Equal(v.Acts.OrderBy(a => a.Key), w.Acts.OrderBy(a => a.Key));
                Assert.Equal((v.Job?.Place, v.Job?.Spot, v.Job?.Start, v.Job?.Commute), (w.Job?.Place, w.Job?.Spot, w.Job?.Start, w.Job?.Commute));
                Assert.Equal(v.Family!.OrderBy(f => f.Key), w.Family!.Where(f => !later.Contains(f.Key)).OrderBy(f => f.Key));
            }
            var smallSeeds = small.Familiarity.ToHashSet();
            Assert.All(big.Familiarity.Where(s => !later.Contains(s.A) && !later.Contains(s.B)), s => Assert.Contains(s, smallSeeds));
        }
    }

    /// <summary>Familiarity starts by circle (town spec 4.3, step 11): housemates and friends keep
    /// the engine's seeds; otherwise kin elsewhere 0.6, coworkers and classmates (never a child with
    /// an adult) 0.4, neighbours 0.25, a public figure 0.2, strangers 0.08. Each pair is worked out
    /// here from the cards, so a dropped or wrong seed fails.</summary>
    [Fact]
    public void FamiliarityStartsByCircle()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var sim = new Simulation(1, town);
        var hood = TownMetrics.Districts(town);
        var figures = town.Authority.Keepers.Values.Append(DefaultTown.Mayor).Append("Harvey").Append("Penny").ToHashSet();
        var people = town.Cast.Where(v => !Core.Contains(v.Name)).ToList();
        var seen = new HashSet<double>();
        foreach (Villager a in people)
            foreach (Villager b in town.Cast.Where(b => b.Name != a.Name && b.Name != DefaultTown.Newcomer))
            {
                double f = sim.Familiarity(a.Name, b.Name);
                if (a.Household == b.Household) { Assert.Equal(0.8, f); continue; }
                if (a.Friends.Contains(b.Name) || b.Friends.Contains(a.Name)) { Assert.Equal(0.5, f); continue; }
                double expected = 0.08;
                if (a.KinOf(b.Name) is not null) expected = Math.Max(expected, 0.6);
                if (a.Job is { } ja && b.Job is { } jb && ja.Place == jb.Place && a.Age >= 18 == b.Age >= 18) expected = Math.Max(expected, 0.4);
                if (hood[a.Name] == hood[b.Name] && hood[a.Name] != "core") expected = Math.Max(expected, 0.25);
                if (figures.Contains(a.Name) || figures.Contains(b.Name)) expected = Math.Max(expected, 0.2);
                Assert.True(expected == f, $"{a.Name}-{b.Name}: {f}, expected {expected}");
                seen.Add(f);
            }
        Assert.Superset(new HashSet<double> { 0.08, 0.2, 0.25, 0.4 }, seen);
        Assert.Equal(0.0, sim.Familiarity(people[0].Name, DefaultTown.Newcomer)); // nobody knows the newcomer
    }

    [Fact]
    public void ARunOfTheSixtyTownIsPinned()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        string hash = Metrics.LogHash(new Simulation(1, town).Run(7));
        Assert.Equal(hash, Metrics.LogHash(new Simulation(1, TownGen.Build(new TownSpec(1, 60))).Run(7)));
        Assert.Equal("c8990fcb4c6537c7", hash); // generator version 2
    }
}
