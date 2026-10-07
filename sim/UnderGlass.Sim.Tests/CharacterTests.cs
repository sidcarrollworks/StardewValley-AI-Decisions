using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Each person's character (phase 0d; rule 18): every rule reads traits from it, and it
/// can be set, but nothing in the simulator changes it yet.</summary>
public class CharacterTests
{
    /// <summary>D1.</summary>
    [Fact]
    public void CharacterStartsAsTheCastCard_AndStaysUnlessSet()
    {
        var sim = new Simulation(1);
        var cast = DefaultTown.Cast().ToDictionary(v => v.Name);
        foreach (var (name, v) in cast)
            Assert.Equal(v.Temperament, sim.Character(name));
        SimResult r = sim.Run(2);
        Assert.Equal(cast.Count, r.CharactersAtStart.Count);
        foreach (var (name, v) in cast)
        {
            Assert.Equal(v.Temperament, r.CharactersAtStart[name]);
            Assert.Equal(v.Temperament, r.CharactersAtEnd[name]);
        }

        var other = new Simulation(1);
        other.SetTrait("Penny", Trait.Boldness, 1.7);
        Assert.Equal(1.0, other.TraitOf("Penny", Trait.Boldness));
        other.SetTrait("Penny", Trait.Boldness, -3);
        Assert.Equal(0.0, other.TraitOf("Penny", Trait.Boldness));
        foreach (Trait t in Enum.GetValues<Trait>())
        {
            other.SetTrait("Alex", t, 0.123);
            Assert.Equal(0.123, other.TraitOf("Alex", t));
        }
    }

    /// <summary>A trait set before the run is the one the rules read: a bolder newcomer stands for
    /// constable (only the bold stand).</summary>
    [Fact]
    public void TheRulesReadTheCharacterNotTheCard()
    {
        var shy = new Simulation(3);
        shy.SetTrait("Penny", Trait.Boldness, 0.2);
        var bold = new Simulation(3);
        bold.SetTrait("Penny", Trait.Boldness, 1.0);
        Assert.DoesNotContain("Penny", shy.Run(1).Elections.Single().Votes.Keys);
        Assert.Contains("Penny", bold.Run(1).Elections.Single().Votes.Keys);
    }
}
