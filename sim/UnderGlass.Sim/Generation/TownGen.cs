namespace UnderGlass.Sim.Generation;

/// <summary>What town to grow (town spec 4.1): the town seed (not the run seed), how many people,
/// the profile, the per-person share of the core's act rates, and the familiarity of strangers.
/// Written as <c>pelican:60@7</c>.</summary>
public sealed record TownSpec(long Seed, int People, string Profile = "pelican", double PerCapita = 0.8, double StrangerFamiliarity = 0.08)
{
    public override string ToString() => $"{Profile}:{People}@{Seed}";

    /// <summary>"pelican:60@7": profile, people, town seed.</summary>
    public static TownSpec Parse(string text)
    {
        int colon = text.IndexOf(':'), at = text.IndexOf('@');
        if (colon < 1 || at < colon + 2 || !int.TryParse(text[(colon + 1)..at], out int people)
            || !long.TryParse(text[(at + 1)..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long seed))
            throw new ArgumentException($"{text}: a town is written <profile>:<people>@<town seed>, for example pelican:60@7");
        return new TownSpec(seed, people, text[..colon]);
    }
}

/// <summary>
/// The town generator (town spec 4; steps T3 and T4 together, for 60 people). The pelican profile
/// keeps the 31 town (<see cref="Towns.Pelican31"/>) at the centre, untouched, and fills ring-1
/// slots round it with generated neighbourhoods. Every draw is keyed by structure (slot, plot,
/// member), never by how many draws came before, and each neighbourhood's content depends only on
/// the seed, its slot and the slots before it, so a town grown further keeps the smaller town's
/// people (the one exception: a kin tie from a later slot adds an entry to the earlier person).
/// </summary>
public static partial class TownGen
{
    /// <summary>Raised whenever a change makes the same spec build a different town.</summary>
    public const int Version = 2; // 2: the review's fixes (2026-10-08): ages, kin, spots, the lane's green, wildcards, the hash

    /// <summary>A ring-1 slot (town spec 2.3): where it joins the core, its template and size,
    /// and the people it holds.</summary>
    private sealed record Slot(string Code, string Name, string Label, string Template, int Homes, int People,
        string JoinPlace, Tile JoinTile, int ConnectorLength);

    // E and N make the 60 town (the 31 core + 15 + 14). The others are ring 1's rest, for 120 (T6).
    private static readonly Slot[] Ring1 =
    {
        new("E", "EastGreen", "East Green", "Green", 8, 15, "Square", new Tile(29, 10), 12),
        new("N", "NorthLane", "North Lane", "Lane", 8, 14, "MountainPath", new Tile(12, 0), 10),
    };

    public static TownData Build(TownSpec spec)
    {
        if (spec.Profile != "pelican")
            throw new ArgumentException($"profile {spec.Profile}: only pelican is built (the own profile is town spec step T8)");
        TownData core = Towns.Pelican31();
        int coreCount = core.Cast.Count;
        if (spec.People == coreCount)
            return core; // the 31 town itself: nothing is generated, so the town seed changes nothing
        var slots = new List<Slot>();
        int total = coreCount;
        foreach (Slot s in Ring1)
        {
            if (total >= spec.People)
                break;
            slots.Add(s);
            total += s.People;
        }
        if (total != spec.People)
            throw new ArgumentException($"{spec}: the pelican profile grows in whole neighbourhoods: {string.Join(", ", Sizes(coreCount))} people");
        var b = new Builder(spec, core);
        for (int k = 0; k < slots.Count; k++)
            b.Grow(slots[k], k);
        TownData town = b.Finish();
        var problems = TownCheck.Problems(town);
        if (problems.Count > 0)
            throw new InvalidOperationException($"{spec} failed its check: " + string.Join("; ", problems.Take(8)));
        return town;
    }

    /// <summary>The sizes the pelican profile can be grown to.</summary>
    public static IReadOnlyList<int> Sizes(int core = 31)
    {
        var sizes = new List<int> { core };
        foreach (Slot s in Ring1)
            sizes.Add(sizes[^1] + s.People);
        return sizes;
    }
}
