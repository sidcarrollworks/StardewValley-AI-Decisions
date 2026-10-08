namespace UnderGlass.Sim.Generation;

/// <summary>
/// Reading a town (town spec 4.5): a census card, household by household, built from templates
/// with no model text; and the town's hash, FNV-1a over the same lines plus its places' rows, doors,
/// hubs, act rates and familiarity seeds.
/// </summary>
public static class Census
{
    private static string F(double x) => x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private static string Hm(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    /// <summary>One line per household (address, members with ages, kinds and jobs), then the places
    /// with their keepers, and the hubs.</summary>
    public static IReadOnlyList<string> Describe(TownData town)
    {
        var lines = new List<string> { $"{town.Cast.Count} people in {town.Cast.Select(v => v.Household).Distinct().Count()} households, {town.Places.Count} places, {town.Links.Count} doors" };
        var doorOf = new Dictionary<string, string>();
        foreach (Link l in town.Links)
        {
            if (l.B.StartsWith("Home:", StringComparison.Ordinal)) doorOf[l.B] = $"{l.A} ({l.DoorA.X},{l.DoorA.Y})";
            if (l.A.StartsWith("Home:", StringComparison.Ordinal)) doorOf[l.A] = $"{l.B} ({l.DoorB.X},{l.DoorB.Y})";
        }
        foreach (var house in town.Cast.GroupBy(v => v.Household).OrderBy(g => doorOf.GetValueOrDefault("Home:" + g.Key, "~"), StringComparer.Ordinal).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            string members = string.Join("; ", house.Select(v =>
                $"{v.Name}, {v.Age}, {v.Kind}" + (v.Job is { } j ? $", {(v.Age < 18 ? "lessons" : "works")} at {j.Place} {Hm(j.Start)}-{Hm(j.End)}" : v.Age >= 65 ? ", retired" : "")));
            lines.Add($"{house.Key} at {doorOf.GetValueOrDefault("Home:" + house.Key, "?")}: {members}");
        }
        foreach (var (place, keeper) in town.Authority.Keepers.OrderBy(k => k.Key, StringComparer.Ordinal))
            lines.Add($"keeper of {place}: {keeper}");
        foreach (Gathering g in town.Gatherings)
            lines.Add($"hub {g.Name} at {g.Place} ({g.Center.X},{g.Center.Y}) r{g.Radius} {Hm(g.From)}-{Hm(g.To)} weight {F(g.Weight)}"
                + (g.Capacity > 0 ? $", up to {g.Capacity}" : "") + (g.Local is { } local ? $", {local.Count} households of its own, others x{F(g.Visitors)}" : ""));
        return lines;
    }

    /// <summary>The town's hash: FNV-1a over the town written as JSON (<see cref="TownJson"/>), which
    /// holds everything a town is made of (cards, rows, doors, hubs, act kinds, every option, the
    /// money and the familiarity seeds), and the generator's version. Two towns that run differently
    /// can't share a hash, and a file read back keeps it.</summary>
    public static string Hash(TownData town) => Rng.Hash(TownJson.Write(town), "generator " + TownGen.Version).ToString("x16");
}
