namespace UnderGlass.Sim;

/// <summary>
/// Forgetting (town spec E6), as Sid described it (2026-10-08): people forget each other, but how
/// fast depends on the tie. The more a tie has built, the harder it is to forget; a new face is
/// remembered more easily when there was some attraction; and weak ties are pushed out when too many
/// new faces pass by. Each night, familiarity falls by FadePerDay x (1 - strength)^StrengthPower,
/// where strength joins familiarity and regard both ways (1 - (1 - familiarity)(1 - |regard|)); kin
/// and housemates never fade. While someone is still a new face (familiarity below NewFaceBelow),
/// time together builds it 1 + WarmMeetingBoost x warmth times as fast, warmth being the regard felt
/// toward them (prejudice about their kind included). Someone who met more than NewFacesPerWeek new
/// faces in the last seven days loses weak ties faster: the rate is multiplied by 1 +
/// InterferenceWeight x (new faces / NewFacesPerWeek - 1) x (1 - strength). All of it is off while
/// FadePerDay is 0, and the shipped town is then unchanged. First guesses, to be measured at 26,
/// 31 and 60 people.
/// </summary>
public sealed class ForgettingOptions
{
    /// <summary>The nightly fade of a tie that has built nothing (rule 5 says 1% a day); 0 is off.</summary>
    public double FadePerDay { get; set; }
    /// <summary>How strongly a built tie resists fading: the rate is scaled by (1 - strength) to this power.</summary>
    public double StrengthPower { get; set; } = 2;
    /// <summary>A new face is remembered this much faster per unit of warmth felt toward them.</summary>
    public double WarmMeetingBoost { get; set; } = 2;
    /// <summary>Familiarity under which someone counts as a new face.</summary>
    public double NewFaceBelow { get; set; } = 0.2;
    /// <summary>New faces in seven days that someone takes in before their weak ties suffer.</summary>
    public double NewFacesPerWeek { get; set; } = 10;
    /// <summary>How much faster weak ties fade for each NewFacesPerWeek's worth of new faces over it.</summary>
    public double InterferenceWeight { get; set; } = 1;

    public bool On => FadePerDay > 0;
}

/// <summary>
/// Town growth (town spec, step T1): routing by walking distance (E3), what a grown town's hubs add
/// (E4: ages, a crowd limit, local households), and familiarity seeded by circle and fading (E6).
/// The commute (E5) is read where the work rule and the alarm are. Every part leaves the shipped
/// town as it was: its door graph is a tree, so routing takes the same doors, and the rest is off
/// unless a town sets it.
/// </summary>
public sealed partial class Simulation
{
    // ---- routing by walking distance (E3) ------------------------------------------------------

    // For each place a walker may be heading to: the fewest tiles to reach it from each door's tile,
    // crossing a door counting 1 (Dijkstra over the doors, once per target, cached).
    private readonly Dictionary<string, Dictionary<(string, Tile), int>> _routeCost = new();

    /// <summary>The next door on the way from one place to another, fewest tiles first counting the
    /// walk from where the walker stands; on a tie, the door nearer them, then the doors' order. Null
    /// when there is no way there (the walker then arrives at once, as tests' small worlds rely on).</summary>
    private (Tile Exit, string Next, Tile Entry)? Route(string from, string to, Tile at)
    {
        if (from == to || !_doors.TryGetValue(from, out var doors))
            return null;
        Dictionary<(string, Tile), int> cost = RouteCost(to);
        (Tile, string, Tile)? best = null;
        long bestTotal = long.MaxValue;
        int bestNear = int.MaxValue;
        foreach (var (next, exit, entry) in doors)
        {
            if (!cost.TryGetValue((next, entry), out int beyond))
                continue;
            int[,] field = Field(from, exit);
            // From a tile the field can't reach (a test's teleport, a wall) the walk there doesn't count.
            int near = Inside(field, at) && field[at.X, at.Y] >= 0 ? field[at.X, at.Y] : 0;
            long total = (long)near + 1 + beyond;
            if (total < bestTotal || total == bestTotal && near < bestNear)
            {
                best = (exit, next, entry);
                bestTotal = total;
                bestNear = near;
            }
        }
        return best;
    }

    // Every door by where it lands: (place, tile) -> the places and tiles it is entered from.
    private Dictionary<(string Place, Tile Entry), List<(string From, Tile Exit)>>? _into;

    private Dictionary<(string Place, Tile Entry), List<(string From, Tile Exit)>> Into()
    {
        if (_into is not null)
            return _into;
        _into = new Dictionary<(string, Tile), List<(string, Tile)>>();
        foreach (var (from, list) in _doors.OrderBy(d => d.Key, StringComparer.Ordinal))
            foreach (var (next, exit, entry) in list)
            {
                if (!_into.TryGetValue((next, entry), out var sources))
                    _into[(next, entry)] = sources = new List<(string, Tile)>();
                sources.Add((from, exit));
            }
        return _into;
    }

    private Dictionary<(string, Tile), int> RouteCost(string to)
    {
        if (_routeCost.TryGetValue(to, out var cached))
            return cached;
        var cost = new Dictionary<(string, Tile), int>();
        var queue = new SortedSet<(int Cost, string Place, int X, int Y)>(Comparer<(int, string, int, int)>.Create((a, b) =>
        {
            int c = a.Item1.CompareTo(b.Item1);
            if (c != 0) return c;
            c = StringComparer.Ordinal.Compare(a.Item2, b.Item2);
            return c != 0 ? c : a.Item3 != b.Item3 ? a.Item3.CompareTo(b.Item3) : a.Item4.CompareTo(b.Item4);
        }));
        void Offer(string place, Tile t, int c)
        {
            if (cost.TryGetValue((place, t), out int had))
            {
                if (had <= c)
                    return;
                queue.Remove((had, place, t.X, t.Y));
            }
            cost[(place, t)] = c;
            queue.Add((c, place, t.X, t.Y));
        }
        // Being anywhere in the target place is arriving: every door tile there costs nothing.
        foreach (var (_, exit, _) in _doors.GetValueOrDefault(to) ?? new())
            Offer(to, exit, 0);
        foreach (var (key, _) in Into())
            if (key.Place == to)
                Offer(to, key.Entry, 0);
        var settled = new HashSet<(string, Tile)>();
        while (queue.Count > 0)
        {
            var (c, place, x, y) = queue.Min;
            queue.Remove(queue.Min);
            var here = new Tile(x, y);
            if (!settled.Add((place, here)))
                continue;
            // Step back through a door: from the far side of any door that lands on this tile.
            if (Into().TryGetValue((place, here), out var sources))
                foreach (var (from, exit) in sources)
                    if (from != to)
                        Offer(from, exit, c + 1);
            // Walk back within the place: from every other door tile that can reach this one.
            if (place == to)
                continue;
            int[,] field = Field(place, here);
            foreach (var (_, exit, _) in _doors.GetValueOrDefault(place) ?? new())
                if (exit != here && Inside(field, exit) && field[exit.X, exit.Y] > 0)
                    Offer(place, exit, c + field[exit.X, exit.Y]);
        }
        _routeCost[to] = cost;
        return cost;
    }

    // ---- hubs in a grown town (E4) ---------------------------------------------------------------

    // Hubs struck off for someone for the rest of a day, after they turned back from the crowd.
    private readonly HashSet<(string Who, string Hub, int Day)> _struck = new();

    /// <summary>Whether someone may pick a hub now: their age is within its range, and they haven't
    /// turned back from its crowd today.</summary>
    private bool Admits(Gathering g, Person p, int m)
        => p.V.Age >= g.MinAge && p.V.Age <= g.MaxAge && (g.Capacity <= 0 || !_struck.Contains((p.V.Name, g.Name, Clock.Day(m))));

    /// <summary>A hub's weight for someone: its own for its households (or everyone, with none named),
    /// times Visitors for anyone else.</summary>
    private static double HubWeight(Gathering g, Person p)
        => g.Local is null || g.Local.Contains(p.V.Household) ? g.Weight : g.Weight * g.Visitors;

    /// <summary>On arriving at the place of a hub with a crowd limit: someone who sees Capacity or more
    /// awake people within its radius turns back, strikes it off for the day and decides again. Only
    /// what they see counts (a wall hides people; a hedge or a shelf only dims them, so they still
    /// count), so it reads no one's mind.</summary>
    private void Arrive(Person p, int m)
    {
        if (p.Haunt?.Hub is not { } name)
        {
            p.HubSeen = null;
            return;
        }
        if (p.HubSeen == name || p.Place != p.GoalPlace)
            return;
        p.HubSeen = name;
        Gathering? g = null;
        foreach (Gathering x in _gatherings)
            if (x.Name == name && x.Capacity > 0 && x.On(m))
            {
                g = x;
                break;
            }
        if (g is null)
            return;
        Location place = _places[g.Place];
        int seen = 0;
        foreach (Person o in _people)
            if (o != p && !o.Asleep && o.Place == g.Place && o.At.Chebyshev(g.Center) <= g.Radius
                && Perception.LineOfSight(place, p.At, o.At, _po) > 0)
                seen++;
        if (seen < g.Capacity)
            return;
        _struck.Add((p.V.Name, g.Name, Clock.Day(m)));
        _log.Add($"{m} turned-back {p.V.Name} {g.Name} {seen}");
        p.Why = "";
        p.Haunt = null;
        p.HubSeen = null;
    }

    // ---- familiarity seeded by circle, and forgetting (E6) ----------------------------------------

    private void SeedFamiliarity(IReadOnlyList<(string A, string B, double Value)> seeds)
    {
        foreach (var (a, b, value) in seeds)
        {
            if (!_index.TryGetValue(a, out int i) || !_index.TryGetValue(b, out int j) || i == j)
                throw new ArgumentException($"familiarity seed {a}-{b}: both must be different people in the town");
            _fam[i, j] = _fam[j, i] = Math.Clamp(value, 0, 1);
        }
    }

    private ForgettingOptions Fo => _go.Forgetting;
    // The day each new face was last met, per person, for the faces met in the last seven days (a
    // face counts once toward a week's new faces, however often it is met); and today's.
    private Dictionary<(int, int), int>? _faceLastMet;
    private HashSet<(int, int)>? _metToday;

    /// <summary>How much faster than usual time together builds i's familiarity with j: 1, unless
    /// forgetting is on and j is still a new face to i, when warmth toward them makes them stick
    /// (only while feelings steer: feelings that only watch change nothing). Also counts j as a new
    /// face i met today.</summary>
    private double Meeting(int i, int j, int m)
    {
        if (!Fo.On || _fam[i, j] >= Fo.NewFaceBelow)
            return 1;
        _metToday ??= new HashSet<(int, int)>();
        _metToday.Add((i, j));
        double warmth = Steering ? Math.Max(0, E(i, j)) : 0;
        return 1 + Fo.WarmMeetingBoost * warmth;
    }

    /// <summary>The night's forgetting: every tie but kin's and housemates' fades by its strength,
    /// faster for weak ties of someone flooded with new faces (different people met in the last seven
    /// days while still new). Every tie fades from the night's starting values, so the two halves of
    /// a pair fade alike. Regard adds to a tie's strength only while feelings steer.</summary>
    private void Forget(int day)
    {
        if (!Fo.On)
            return;
        int n = _people.Length;
        _faceLastMet ??= new Dictionary<(int, int), int>();
        if (_metToday is not null)
            foreach (var pair in _metToday)
                _faceLastMet[pair] = day;
        _metToday?.Clear();
        foreach (var gone in _faceLastMet.Where(f => f.Value <= day - Clock.DaysPerWeek).Select(f => f.Key).ToList())
            _faceLastMet.Remove(gone);
        var week = new int[n];
        foreach (var ((i, _), _) in _faceLastMet)
            week[i]++;
        var before = (double[,])_fam.Clone();
        for (int i = 0; i < n; i++)
        {
            double flood = Fo.NewFacesPerWeek > 0 ? Math.Max(0, week[i] / Fo.NewFacesPerWeek - 1) : 0;
            Villager vi = _people[i].V;
            for (int j = 0; j < n; j++)
            {
                if (i == j || before[i, j] <= 0)
                    continue;
                Villager vj = _people[j].V;
                if (vi.Household == vj.Household || vi.KinOf(vj.Name) is not null)
                    continue;
                double known = (before[i, j] + before[j, i]) / 2;
                double felt = Steering ? (Math.Abs(_regard[i, j]) + Math.Abs(_regard[j, i])) / 2 : 0;
                double strength = 1 - (1 - known) * (1 - Math.Min(1, felt));
                double rate = Fo.FadePerDay * Math.Pow(1 - strength, Fo.StrengthPower)
                              * (1 + Fo.InterferenceWeight * flood * (1 - strength));
                _fam[i, j] = before[i, j] - before[i, j] * Math.Min(1, rate);
            }
        }
    }
}
