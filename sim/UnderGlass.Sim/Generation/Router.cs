namespace UnderGlass.Sim.Generation;

/// <summary>
/// Walking distance in tiles across a town while it is being built (for haunts by distance and for
/// commutes): Dijkstra over the door tiles, with the walk inside each place from a breadth-first
/// field over its open tiles (8 directions, as the engine walks). Crossing a door costs 1.
/// </summary>
internal sealed class Router
{
    private readonly Dictionary<string, Location> _places = new();
    private readonly Dictionary<string, List<(string Next, Tile Exit, Tile Entry)>> _doors = new();
    private readonly Dictionary<(string, Tile), int[,]> _fields = new();

    public void Add(Location place) => _places[place.Name] = place;

    public void Add(Link l)
    {
        Door(l.A, l.B, l.DoorA, l.DoorB);
        Door(l.B, l.A, l.DoorB, l.DoorA);
    }

    private void Door(string from, string to, Tile exit, Tile entry)
    {
        if (!_doors.TryGetValue(from, out var list))
            _doors[from] = list = new List<(string, Tile, Tile)>();
        list.Add((to, exit, entry));
    }

    private static readonly (int Dx, int Dy)[] Steps = { (0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1) };

    private int[,] Field(string place, Tile target)
    {
        if (_fields.TryGetValue((place, target), out int[,]? f))
            return f;
        Location loc = _places[place];
        f = new int[loc.Width, loc.Height];
        for (int x = 0; x < loc.Width; x++)
            for (int y = 0; y < loc.Height; y++)
                f[x, y] = -1;
        var queue = new Queue<Tile>();
        if (target.X >= 0 && target.Y >= 0 && target.X < loc.Width && target.Y < loc.Height)
        {
            f[target.X, target.Y] = 0;
            queue.Enqueue(target);
        }
        while (queue.Count > 0)
        {
            Tile c = queue.Dequeue();
            foreach (var (dx, dy) in Steps)
            {
                var n = new Tile(c.X + dx, c.Y + dy);
                if (n.X >= 0 && n.Y >= 0 && n.X < loc.Width && n.Y < loc.Height && f[n.X, n.Y] < 0 && loc.Walkable(n))
                {
                    f[n.X, n.Y] = f[c.X, c.Y] + 1;
                    queue.Enqueue(n);
                }
            }
        }
        _fields[(place, target)] = f;
        return f;
    }

    private int Within(string place, Tile from, Tile to)
    {
        int[,] f = Field(place, to);
        return from.X >= 0 && from.Y >= 0 && from.X < f.GetLength(0) && from.Y < f.GetLength(1) ? f[from.X, from.Y] : -1;
    }

    /// <summary>Tiles to walk from one spot to another, or -1 when there is no way.</summary>
    public int Tiles(string fromPlace, Tile from, string toPlace, Tile to)
    {
        if (fromPlace == toPlace && Within(fromPlace, from, to) is var direct && direct >= 0)
            return direct;
        var best = new Dictionary<(string, Tile), int>();
        var queue = new PriorityQueue<(string Place, Tile At), (int Cost, string Place, int X, int Y)>(
            Comparer<(int, string, int, int)>.Create((a, b) =>
            {
                int c = a.Item1.CompareTo(b.Item1);
                if (c != 0) return c;
                c = StringComparer.Ordinal.Compare(a.Item2, b.Item2);
                return c != 0 ? c : a.Item3 != b.Item3 ? a.Item3.CompareTo(b.Item3) : a.Item4.CompareTo(b.Item4);
            }));
        void Offer(string place, Tile t, int cost)
        {
            if (best.TryGetValue((place, t), out int had) && had <= cost)
                return;
            best[(place, t)] = cost;
            queue.Enqueue((place, t), (cost, place, t.X, t.Y));
        }
        foreach (var (_, exit, _) in _doors.GetValueOrDefault(fromPlace) ?? new())
            if (Within(fromPlace, from, exit) is var d && d >= 0)
                Offer(fromPlace, exit, d);
        int answer = int.MaxValue;
        while (queue.TryDequeue(out var node, out var key))
        {
            if (key.Cost > best[(node.Place, node.At)] || key.Cost >= answer)
                continue;
            // Crossing the door here.
            foreach (var (next, exit, entry) in _doors.GetValueOrDefault(node.Place) ?? new())
            {
                if (exit != node.At)
                    continue;
                int arrive = key.Cost + 1;
                if (next == toPlace && Within(toPlace, entry, to) is var last && last >= 0)
                    answer = Math.Min(answer, arrive + last);
                foreach (var (_, exit2, _) in _doors.GetValueOrDefault(next) ?? new())
                    if (Within(next, entry, exit2) is var w && w >= 0)
                        Offer(next, exit2, arrive + w);
            }
        }
        return answer == int.MaxValue ? -1 : answer;
    }
}
