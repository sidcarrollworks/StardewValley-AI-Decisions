namespace UnderGlass.Sim;

/// <summary>
/// Checks a town before it runs (town spec 4.4): the mistakes the engine forgives quietly. A place
/// no door reaches makes people arrive there at once; a spot on a wall makes them step over it; a
/// keeper or a mayor who isn't in the cast makes the authority's rules do nothing. Returns every
/// problem found, in a fixed order; none for a sound town.
/// </summary>
public static class TownCheck
{
    public static IReadOnlyList<string> Problems(TownData town)
    {
        var problems = new List<string>();
        var places = new Dictionary<string, Location>();
        foreach (Location p in town.Places)
        {
            if (!places.TryAdd(p.Name, p))
                problems.Add($"place {p.Name} is listed twice");
            if (p.Rows.Count == 0 || p.Rows.Any(r => r.Length != p.Rows[0].Length))
                problems.Add($"place {p.Name} is not a rectangle");
        }

        var uses = new List<(string What, string Place, Tile At)>();
        bool Spot(string what, string place, Tile t)
        {
            if (!places.TryGetValue(place, out Location? loc))
            {
                problems.Add($"{what}: no place {place}");
                return false;
            }
            if (!loc.Walkable(t))
            {
                problems.Add($"{what}: {place} ({t.X},{t.Y}) can't be stood on");
                return false;
            }
            uses.Add((what, place, t));
            return true;
        }

        // Doors: on open tiles, one door to a tile, and every place reachable from every other.
        var doorTiles = new HashSet<(string, Tile)>();
        var next = new Dictionary<string, List<string>>();
        foreach (Link l in town.Links)
        {
            bool a = Spot($"door {l.A}-{l.B}", l.A, l.DoorA), b = Spot($"door {l.A}-{l.B}", l.B, l.DoorB);
            if (a && !doorTiles.Add((l.A, l.DoorA)))
                problems.Add($"door {l.A}-{l.B}: {l.A} ({l.DoorA.X},{l.DoorA.Y}) is already a door");
            if (b && !doorTiles.Add((l.B, l.DoorB)))
                problems.Add($"door {l.A}-{l.B}: {l.B} ({l.DoorB.X},{l.DoorB.Y}) is already a door");
            if (a && b)
            {
                (next.TryGetValue(l.A, out var na) ? na : next[l.A] = new List<string>()).Add(l.B);
                (next.TryGetValue(l.B, out var nb) ? nb : next[l.B] = new List<string>()).Add(l.A);
            }
        }
        if (places.Count > 0)
        {
            string from = town.Places.OrderByDescending(p => p.Outdoor).ThenByDescending(p => p.Width * p.Height)
                .ThenBy(p => p.Name, StringComparer.Ordinal).First().Name;
            var seen = new HashSet<string> { from };
            var queue = new Queue<string>(seen);
            while (queue.Count > 0)
                foreach (string n in next.GetValueOrDefault(queue.Dequeue()) ?? new List<string>())
                    if (seen.Add(n))
                        queue.Enqueue(n);
            foreach (string p in places.Keys.Where(p => !seen.Contains(p)).OrderBy(p => p, StringComparer.Ordinal))
                problems.Add($"place {p}: no door leads there from {from}");
        }

        // People: named once, a home with a bed and a sofa, and spots to stand on at work and in haunts.
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Villager v in town.Cast)
        {
            if (!names.Add(v.Name))
                problems.Add($"{v.Name} is in the cast twice");
            if (places.TryGetValue(v.Home, out Location? home))
            {
                if (!home.Walkable(DefaultTown.Bed) || !home.Walkable(DefaultTown.Sofa))
                    problems.Add($"{v.Name}: no bed or sofa to stand on at {v.Home}");
                else
                {
                    uses.Add(($"{v.Name}'s bed", v.Home, DefaultTown.Bed));
                    uses.Add(($"{v.Name}'s sofa", v.Home, DefaultTown.Sofa));
                }
            }
            else if (v.Job is null && v.Haunts.Count == 0)
                problems.Add($"{v.Name}: no home, no job and no haunt");
            if (v.Job is { } job)
                Spot($"{v.Name}'s work", job.Place, job.Spot);
            foreach (Haunt h in v.Haunts)
                Spot($"{v.Name}'s haunt", h.Place, h.Spot);
            foreach (string f in v.Friends)
                if (!town.Cast.Any(o => o.Name == f))
                    problems.Add($"{v.Name}: friend {f} is not in the town");
            foreach (string k in v.Family?.Keys ?? Enumerable.Empty<string>())
                if (!town.Cast.Any(o => o.Name == k))
                    problems.Add($"{v.Name}: kin {k} is not in the town");
        }

        foreach (Gathering g in town.Gatherings)
            Spot($"gathering {g.Name}", g.Place, g.Center);
        foreach (ActKind k in town.Acts)
            foreach (string p in k.Allowed)
                if (!places.ContainsKey(p))
                    problems.Add($"act {k.Name}: no place {p}");

        AuthorityOptions a2 = town.Authority;
        if (a2.Mayor is { } mayor && !names.Contains(mayor))
            problems.Add($"the mayor {mayor} is not in the town");
        if (a2.Constable is { } constable && !names.Contains(constable))
            problems.Add($"the constable {constable} is not in the town");
        foreach (var (place, keeper) in a2.Keepers.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!places.ContainsKey(place))
                problems.Add($"keeper of {place}: no such place");
            if (!names.Contains(keeper))
                problems.Add($"keeper of {place}: {keeper} is not in the town");
        }
        foreach (var (place, spot) in a2.Patrol.OrderBy(k => k.Key, StringComparer.Ordinal))
            Spot("patrol", place, spot);
        if (a2.Mayor is not null)
        {
            Spot("the lockup", a2.LockupPlace, a2.LockupSpot);
            Spot("service", a2.ServicePlace, a2.ServiceSpot);
        }

        if (town.Economy is { } e)
        {
            var households = town.Cast.Select(v => v.Household).ToHashSet(StringComparer.Ordinal);
            foreach (string h in households.Where(h => !e.StartPurse.ContainsKey(h)).OrderBy(h => h, StringComparer.Ordinal))
                problems.Add($"household {h} has no purse to start with");
            foreach (var (who, _, payer) in e.Incomes)
            {
                if (!names.Contains(who))
                    problems.Add($"income: {who} is not in the town");
                if (payer is not null && payer != Simulation.Town && !households.Contains(payer))
                    problems.Add($"income of {who}: no household {payer} to pay it");
            }
            foreach (var (h, shop) in e.GroceriesAt.OrderBy(g => g.Key, StringComparer.Ordinal))
                if (!places.ContainsKey(shop))
                    problems.Add($"groceries of {h}: no shop {shop}");
        }
        // Inside each place, every door and every spot in use can be walked to from the place's first
        // door (8 ways over open tiles, as the engine walks); otherwise a walker steps over walls to it.
        var region = new Dictionary<string, HashSet<Tile>>();
        HashSet<Tile> Reach(string place)
        {
            if (region.TryGetValue(place, out var seen))
                return seen;
            Location loc = places[place];
            Tile start = town.Links.Where(l => l.A == place).Select(l => l.DoorA).Concat(town.Links.Where(l => l.B == place).Select(l => l.DoorB)).First();
            seen = new HashSet<Tile> { start };
            var queue = new Queue<Tile>(seen);
            while (queue.Count > 0)
            {
                Tile c = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = new Tile(c.X + dx, c.Y + dy);
                        if ((dx != 0 || dy != 0) && loc.Walkable(n) && seen.Add(n))
                            queue.Enqueue(n);
                    }
            }
            return region[place] = seen;
        }
        foreach (var (place, tile) in doorTiles.OrderBy(d => d.Item1, StringComparer.Ordinal).ThenBy(d => d.Item2.X).ThenBy(d => d.Item2.Y))
            if (!Reach(place).Contains(tile))
                problems.Add($"door {place} ({tile.X},{tile.Y}) can't be walked to from {place}'s other doors");
        foreach (var (what, place, at) in uses)
            if (doorTiles.Any(d => d.Item1 == place) && !Reach(place).Contains(at))
                problems.Add($"{what}: {place} ({at.X},{at.Y}) can't be walked to from its doors");

        foreach (var (x, y, _) in town.Familiarity)
            if (!names.Contains(x) || !names.Contains(y) || x == y)
                problems.Add($"familiarity seed {x}-{y}: both must be different people in the town");
        return problems;
    }
}
