namespace UnderGlass.Sim;

/// <summary>
/// A whole town as one value (town spec, E1): the people, the places and the doors between them,
/// the gatherings, the act kinds, and every option the rules read. It is passed whole to
/// <see cref="Simulation(long, TownData, IReadOnlyList{ValueTuple{int, string, string}})"/>, so no part
/// defaults quietly: with the long constructor, passing places alone switches off feelings, money,
/// the authority, the gatherings and the doors unless each is passed too. Treat it as immutable:
/// one town may be shared by many runs. (Named TownData because <c>Simulation.Town</c> is the
/// town's purse.)
/// </summary>
public sealed record TownData
{
    public required IReadOnlyList<Villager> Cast { get; init; }
    public required IReadOnlyList<Location> Places { get; init; }
    public required IReadOnlyList<Link> Links { get; init; }
    public required IReadOnlyList<Gathering> Gatherings { get; init; }
    public required IReadOnlyList<ActKind> Acts { get; init; }
    public required FeelingOptions Feelings { get; init; }
    public required AuthorityOptions Authority { get; init; }
    /// <summary>Who sells and buys what; null for a town with no money.</summary>
    public Economy? Economy { get; init; }
    public PerceptionOptions Perception { get; init; } = new();
    public GossipOptions Gossip { get; init; } = new();
    public BodyOptions Body { get; init; } = new();
    public HabitOptions Habits { get; init; } = new();
    public MoneyOptions Money { get; init; } = new();
    /// <summary>How far, in tiles, people drift about a haunt's spot while they are there.</summary>
    public int Wander { get; init; } = 2;

    /// <summary>The town as it ships, part for part as the long constructor builds it with no
    /// arguments, so <c>new Simulation(seed, TownData.Default())</c> is <c>new Simulation(seed)</c>.</summary>
    public static TownData Default() => new()
    {
        Cast = DefaultTown.Cast(),
        Places = DefaultTown.Locations(),
        Links = DefaultTown.Links(),
        Gatherings = DefaultTown.Gatherings(),
        Acts = DefaultTown.Acts(),
        Feelings = DefaultTown.Feelings(),
        Authority = DefaultTown.TownAuthority(),
        Economy = DefaultTown.TownEconomy(),
    };
}
