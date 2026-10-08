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
        Assert.Equal("467a0c5864fc3a3b", Census.Hash(TownGen.Build(new TownSpec(1, 60))));
        Assert.Equal("3c8d91a17edf9c2e", Census.Hash(TownGen.Build(new TownSpec(2, 60))));
        Assert.Equal("6a10da8cf3800c04", Census.Hash(TownGen.Build(new TownSpec(3, 60))));
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

    /// <summary>Kin go both ways, and a parent is at least 18 years older than their child.</summary>
    [Fact]
    public void KinAgreeWithEachOtherAndWithAges()
    {
        for (long seed = 1; seed <= 50; seed++)
        {
            TownData town = TownGen.Build(new TownSpec(seed, 60));
            var byName = town.Cast.ToDictionary(v => v.Name);
            foreach (Villager v in town.Cast)
                foreach (var (other, kin) in v.Family!)
                {
                    Assert.Equal(Ages.Reverse(kin), byName[other].KinOf(v.Name));
                    if (kin is Kin.Parent or Kin.Grandparent)
                        Assert.True(byName[other].Age >= v.Age + 18, $"{other} ({byName[other].Age}) is {v.Name}'s ({v.Age}) {kin}");
                }
        }
    }

    /// <summary>Generated adults look like the core's (town spec T3): each trait's mean within 0.1 of
    /// the core's, and its spread within half to one and a half times the core's, over 50 towns.</summary>
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
    /// the engine's seeds, neighbours 0.25, coworkers 0.4, strangers 0.08.</summary>
    [Fact]
    public void FamiliarityStartsByCircle()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        var sim = new Simulation(1, town);
        var people = town.Cast.Where(v => !Core.Contains(v.Name)).ToList();
        foreach (Villager a in people)
            foreach (Villager b in town.Cast.Where(b => b.Name != a.Name && b.Name != DefaultTown.Newcomer))
            {
                double f = sim.Familiarity(a.Name, b.Name);
                if (a.Household == b.Household) Assert.Equal(0.8, f);
                else if (a.Friends.Contains(b.Name) || b.Friends.Contains(a.Name)) Assert.Equal(0.5, f);
                else Assert.Contains(f, new[] { 0.08, 0.2, 0.25, 0.4, 0.6 });
            }
        Assert.Equal(0.0, sim.Familiarity(people[0].Name, DefaultTown.Newcomer)); // nobody knows the newcomer
    }

    [Fact]
    public void ARunOfTheSixtyTownIsPinned()
    {
        TownData town = TownGen.Build(new TownSpec(1, 60));
        string hash = Metrics.LogHash(new Simulation(1, town).Run(7));
        Assert.Equal(hash, Metrics.LogHash(new Simulation(1, TownGen.Build(new TownSpec(1, 60))).Run(7)));
        Assert.Equal("e067cecab875a0dd", hash);
    }
}
