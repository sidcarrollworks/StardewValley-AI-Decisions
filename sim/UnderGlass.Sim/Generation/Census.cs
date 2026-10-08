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

    /// <summary>The town's hash: FNV-1a over its census, every card's numbers, the places' rows, the
    /// doors, the hubs, the act rates, the familiarity seeds, the money and the starting tensions, in
    /// a fixed order.</summary>
    public static string Hash(TownData town)
    {
        var parts = new List<string>(Describe(town));
        foreach (Villager v in town.Cast.OrderBy(v => v.Name, StringComparer.Ordinal))
        {
            Temperament t = v.Temperament;
            parts.Add($"{v.Name} {v.Household} {v.Kind} {v.Age} {F(t.Chattiness)} {F(t.Boldness)} {F(t.Understanding)} {F(t.SelfRegard)} {F(t.Sensitivity)} {F(t.Retention)} {F(t.Expression)} {F(v.Body.MaxEnergy)} {F(v.Body.BedAt)}"
                + $" {v.Birthday} job {v.Job?.Place} {v.Job?.Spot} {v.Job?.Start} {v.Job?.End} {string.Join(",", v.Job?.DaysOff ?? Array.Empty<int>())} {v.Job?.Commute}"
                + " haunts " + string.Join(";", v.Haunts.Select(h => $"{h.Place} {h.Spot.X},{h.Spot.Y} {h.From}-{h.To} {F(h.Weight)}"))
                + " acts " + string.Join(";", v.Acts.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"{a.Key} {F(a.Value)}"))
                + " friends " + string.Join(",", v.Friends) + " kin " + string.Join(",", (v.Family ?? new Dictionary<string, Kin>()).OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}:{f.Value}")));
        }
        foreach (Location p in town.Places)
            parts.Add(p.Name + " " + string.Join("/", p.Rows));
        foreach (Link l in town.Links)
            parts.Add($"{l.A} {l.DoorA} {l.B} {l.DoorB}");
        foreach (ActKind a in town.Acts)
            parts.Add($"{a.Name} {F(a.PerDay)} {string.Join(",", a.Allowed)}");
        foreach (var (a, b, value) in town.Familiarity)
            parts.Add($"{a} {b} {F(value)}");
        if (town.Economy is { } e)
        {
            foreach (var (who, perWeek, from) in e.Incomes)
                parts.Add($"income {who} {F(perWeek)} {from}");
            foreach (var (h, purse) in e.StartPurse.OrderBy(x => x.Key, StringComparer.Ordinal))
                parts.Add($"purse {h} {F(purse)} {e.GroceriesAt.GetValueOrDefault(h)}");
            foreach (var (who, a) in e.Allowances.OrderBy(x => x.Key, StringComparer.Ordinal))
                parts.Add($"allowance {who} {F(a)}");
            parts.Add($"stipend {F(e.TownStipend)} start {F(e.TownStart)}");
        }
        foreach (var ((from, to), regard) in town.Feelings.Start.OrderBy(x => x.Key.From, StringComparer.Ordinal).ThenBy(x => x.Key.To, StringComparer.Ordinal))
            parts.Add($"tension {from} {to} {F(regard)}");
        parts.Add("generator " + TownGen.Version);
        return Rng.Hash(parts.ToArray()).ToString("x16");
    }
}
