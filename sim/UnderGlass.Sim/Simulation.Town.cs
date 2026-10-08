namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    /// <summary>A run of a whole town (<see cref="TownData"/>), with any acts placed by the harness.
    /// Every part is passed through, so nothing falls back to the default town or switches off.</summary>
    public Simulation(long seed, TownData town, IReadOnlyList<(int Tick, string Actor, string Kind)>? scheduled = null)
        : this(seed, town.Cast, town.Places, town.Acts, town.Perception, town.Gossip, scheduled, town.Wander, town.Links,
            town.Body, town.Gatherings, town.Authority, town.Habits, town.Economy, town.Money, town.Feelings)
    {
        SeedFamiliarity(town.Familiarity);
    }
}
