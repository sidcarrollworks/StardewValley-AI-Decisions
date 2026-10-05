namespace UnderGlass.Sim;

/// <summary>Knobs for gossip and scandal (design rules 8 and 9). First guesses for phase 0.</summary>
public sealed class GossipOptions
{
    public int ChatMinTicks { get; set; } = 3;
    public double ChatChance { get; set; } = 0.3;      // per tick, x (0.5 + mean chattiness)
    /// <summary>A pair chats at most once per span, and again every this many ticks they stay
    /// together (people side by side all day talk more than once).</summary>
    public int ChatEveryTicks { get; set; } = 12;
    public double VolunteerLevel { get; set; } = 2;
    public double KnowsBonus { get; set; } = 0.5;      // the listener knows the believed actor well
    public double KnowsAt { get; set; } = 0.4;         // familiarity that counts as knowing well
    public double RetellFactor { get; set; } = 0.7;
    public double FadePerDay { get; set; } = 0.5;
    public double ScandalFadePerDay { get; set; } = 0.8;
    /// <summary>A teller tells one story to at most this many listeners a day; 0 sizes it by the
    /// town (1 under 20 villagers, 2 under 30, else 3).</summary>
    public int TellsPerDay { get; set; }
    public double FamiliarityGrowth { get; set; } = 0.002; // per tick spent near someone
    public double ConfrontShare { get; set; } = 0.25;
    public int ConfrontMin { get; set; } = 3;
    public double KnowsActorAt { get; set; } = 0.2;     // familiarity that counts as knowing someone at all
}

/// <summary>What a run produced.</summary>
public sealed class SimResult
{
    public required IReadOnlyList<Act> Acts { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<int, Belief>> Beliefs { get; init; }
    public required IReadOnlyList<Confrontation> Confrontations { get; init; }
    /// <summary>Holders of each act at the end of each day: [actId][day] = count.</summary>
    public required IReadOnlyDictionary<int, int[]> HoldersByDay { get; init; }
    public required IReadOnlyDictionary<int, int> Witnesses { get; init; }
    public required IReadOnlyList<string> Log { get; init; }
    public required int CastSize { get; init; }
}

/// <summary>
/// Phase 0a: the gossip-only harness (design section 11). Villagers follow routines, acts happen,
/// witnesses perceive them in layers (rule 2), and stories spread by juiciness (rule 8) until a
/// scandal leads to a confrontation (rule 9). Deterministic for a seed. Single-threaded.
/// </summary>
public sealed class Simulation
{
    public const int TicksPerDay = 120;

    private readonly long _seed;
    private readonly IReadOnlyList<Villager> _cast;
    private readonly Dictionary<string, Location> _places;
    private readonly IReadOnlyList<ActKind> _kinds;
    private readonly PerceptionOptions _po;
    private readonly GossipOptions _go;
    private readonly string[] _names;
    private readonly Dictionary<string, int> _index;
    private readonly double[,] _fam;
    private readonly Dictionary<string, (string Place, Tile At)> _pos = new();
    private readonly Dictionary<string, Dictionary<int, Belief>> _beliefs = new();
    private readonly HashSet<(string, string, int)> _told = new();
    private readonly Dictionary<(string, int, int), int> _tellsToday = new();
    private readonly Dictionary<(string, string), (int Start, int Length)> _spans = new();
    private readonly HashSet<(string, string, int)> _chatted = new();
    private readonly List<Act> _acts = new();
    private readonly Dictionary<int, Dictionary<string, List<double>>> _watching = new();
    private readonly Dictionary<int, int> _witnesses = new();
    private readonly HashSet<int> _confronted = new();
    private readonly List<Confrontation> _confrontations = new();
    private readonly Dictionary<int, int[]> _holdersByDay = new();
    private readonly List<string> _log = new();
    private readonly IReadOnlyList<(int Tick, string Actor, string Kind)> _scheduled;
    private readonly int _wander;

    public Simulation(long seed, IReadOnlyList<Villager>? cast = null, IReadOnlyList<Location>? places = null,
        IReadOnlyList<ActKind>? kinds = null, PerceptionOptions? perception = null, GossipOptions? gossip = null,
        IReadOnlyList<(int Tick, string Actor, string Kind)>? scheduled = null, int wander = 2)
    {
        _wander = wander;
        _scheduled = scheduled ?? Array.Empty<(int, string, string)>();
        _seed = seed;
        _cast = (cast ?? DefaultTown.Cast()).OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
        _places = (places ?? DefaultTown.Locations()).ToDictionary(p => p.Name);
        _kinds = kinds ?? DefaultTown.Acts();
        _po = perception ?? new PerceptionOptions();
        _go = gossip ?? new GossipOptions();
        _names = _cast.Select(v => v.Name).ToArray();
        _index = _names.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);
        _fam = new double[_names.Length, _names.Length];
        foreach (Villager a in _cast)
            foreach (Villager b in _cast)
                if (a != b)
                    _fam[_index[a.Name], _index[b.Name]] = SeedFamiliarity(a, b);
        foreach (string n in _names)
            _beliefs[n] = new Dictionary<int, Belief>();
    }

    private static double SeedFamiliarity(Villager a, Villager b)
    {
        if (a.Name == DefaultTown.Newcomer || b.Name == DefaultTown.Newcomer)
            return 0;
        if (a.Household == b.Household)
            return 0.8;
        if (a.Friends.Contains(b.Name) || b.Friends.Contains(a.Name))
            return 0.5;
        return 0.25;
    }

    public double Familiarity(string a, string b) => a == b ? 1 : _fam[_index[a], _index[b]];

    private int TellsPerDay => _go.TellsPerDay > 0 ? _go.TellsPerDay : _names.Length < 20 ? 1 : _names.Length < 30 ? 2 : 3;

    /// <summary>Run whole days from day 0.</summary>
    public SimResult Run(int days)
    {
        for (int T = 0; T < days * TicksPerDay; T++)
        {
            Step(T);
            if (T % TicksPerDay == TicksPerDay - 1)
                CloseDay(T / TicksPerDay, days);
        }
        return new SimResult
        {
            Acts = _acts,
            Beliefs = _beliefs.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<int, Belief>)p.Value),
            Confrontations = _confrontations,
            HoldersByDay = _holdersByDay,
            Witnesses = _witnesses,
            Log = _log,
            CastSize = _names.Length,
        };
    }

    private void Step(int T)
    {
        int t = T % TicksPerDay;
        Move(T, t);
        StartActs(T);
        Watch(T, t);
        FinishActs(T);
        Socialise(T);
        CheckScandals(T);
    }

    // ---- where everyone is -----------------------------------------------------------------

    private void Move(int T, int t)
    {
        int day = T / TicksPerDay;
        foreach (Villager v in _cast)
        {
            RoutineStep step = v.Routine[0];
            for (int i = 1; i < v.Routine.Count; i++)
            {
                RoutineStep s = v.Routine[i];
                int start = s.FromTick + Rng.Range(_seed, -3, 3, "jitter", v.Name, day.ToString(), i.ToString());
                bool skip = Rng.Unit(_seed, "skip", v.Name, day.ToString(), i.ToString()) < 0.15
                            && i < v.Routine.Count - 1; // never skip going home
                if (t >= start && !skip)
                    step = s;
            }
            Location place = _places[step.Location];
            var wander = new Tile(step.Spot.X + Rng.Range(_seed, -_wander, _wander, "wx", v.Name, T.ToString()),
                                  step.Spot.Y + Rng.Range(_seed, -_wander, _wander, "wy", v.Name, T.ToString()));
            _pos[v.Name] = (place.Name, place.Walkable(wander) ? wander : step.Spot);
        }
    }

    // ---- acts and witnessing ---------------------------------------------------------------

    private void StartActs(int T)
    {
        foreach (var (tick, who, kindName) in _scheduled.Where(s => s.Tick == T))
            Begin(T, _kinds.First(k => k.Name == kindName), who);
        foreach (ActKind kind in _kinds)
        {
            if (Rng.Unit(_seed, "act", kind.Name, T.ToString()) >= kind.PerDay / TicksPerDay)
                continue;
            var candidates = _cast
                .Where(v => v.Acts.TryGetValue(kind.Name, out double w) && w > 0)
                .Where(v => kind.Allowed.Count == 0 || kind.Allowed.Contains(_pos[v.Name].Place))
                .ToList();
            if (candidates.Count == 0)
                continue;
            double total = candidates.Sum(v => v.Acts[kind.Name]);
            double r = Rng.Unit(_seed, "actor", kind.Name, T.ToString()) * total;
            Villager actor = candidates[^1];
            foreach (Villager v in candidates)
            {
                r -= v.Acts[kind.Name];
                if (r < 0) { actor = v; break; }
            }
            Begin(T, kind, actor.Name);
        }
    }

    private void Begin(int T, ActKind kind, string actor)
    {
        var act = new Act(_acts.Count, T, actor, kind.Name, _pos[actor].Place, _pos[actor].At);
        _acts.Add(act);
        _watching[act.Id] = new Dictionary<string, List<double>>();
        _log.Add($"{T} act {act.Id} {act.Kind} by {act.Actor} at {act.Location}");
    }

    private ActKind KindOf(Act a) => _kinds.First(k => k.Name == a.Kind);

    private void Watch(int T, int t)
    {
        foreach (Act act in _acts.Where(a => _watching.ContainsKey(a.Id)))
        {
            Location place = _places[act.Location];
            foreach (string n in _names)
            {
                if (n == act.Actor || _pos[n].Place != act.Location)
                    continue;
                double c = Perception.Instant(place, _pos[n].At, act.At, t, _po);
                if (c <= 0)
                    continue;
                var seen = _watching[act.Id];
                if (!seen.TryGetValue(n, out var list))
                    seen[n] = list = new List<double>();
                list.Add(c);
            }
        }
    }

    private void FinishActs(int T)
    {
        foreach (Act act in _acts.Where(a => _watching.ContainsKey(a.Id)).ToList())
        {
            ActKind kind = KindOf(act);
            if (act.Tick + kind.Duration - 1 > T)
                continue;
            int witnesses = 0;
            foreach (var (observer, instants) in _watching[act.Id].OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                double clarity = Perception.OfAct(instants, kind.ReadTicks);
                if (clarity < _po.KnowWhat)
                    continue;
                witnesses++;
                Villager o = _cast[_index[observer]];
                string? actor = null;
                double confidence = 0;
                if (Perception.Identifies(clarity, Familiarity(observer, act.Actor), _po))
                {
                    actor = act.Actor;
                    confidence = Math.Min(1, clarity / Perception.NeededToIdentify(Familiarity(observer, act.Actor), _po));
                }
                else if (Perception.GuessesConfidently(o.Temperament))
                {
                    actor = Guess(observer, act, T);
                    confidence = 0.8;
                }
                Add(observer, new Belief(act.Id, act.Kind, actor, confidence, clarity, Source.Witnessed,
                    kind.Juiciness, T, Array.Empty<string>()), T);
            }
            _witnesses[act.Id] = witnesses;
            _watching.Remove(act.Id);
        }
    }

    /// <summary>A confident guess: someone present, weighted by how well the observer knows them.</summary>
    private string Guess(string observer, Act act, int T)
    {
        var present = _names.Where(n => n != observer && _pos[n].Place == act.Location).ToList();
        double total = present.Sum(n => Familiarity(observer, n) + 0.05);
        double r = Rng.Unit(_seed, "guess", observer, act.Id.ToString()) * total;
        foreach (string n in present)
        {
            r -= Familiarity(observer, n) + 0.05;
            if (r < 0)
                return n;
        }
        return present.Count > 0 ? present[^1] : act.Actor;
    }

    private void Add(string who, Belief b, int T)
    {
        _beliefs[who][b.ActId] = b;
        _log.Add($"{T} belief {who} {b.ActId} {b.Actor ?? "someone"} {b.Source} {b.Juiciness:0.###}");
    }

    // ---- chats and gossip ------------------------------------------------------------------

    private void Socialise(int T)
    {
        for (int i = 0; i < _names.Length; i++)
        {
            for (int j = i + 1; j < _names.Length; j++)
            {
                string a = _names[i], b = _names[j];
                var key = (a, b);
                bool together = _pos[a].Place == _pos[b].Place && _pos[a].At.Chebyshev(_pos[b].At) <= _po.FarTiles;
                if (!together)
                {
                    _spans.Remove(key);
                    continue;
                }
                _fam[i, j] += _go.FamiliarityGrowth * (1 - _fam[i, j]);
                _fam[j, i] += _go.FamiliarityGrowth * (1 - _fam[j, i]);
                var span = _spans.TryGetValue(key, out var s) ? (s.Start, s.Length + 1) : (T, 1);
                _spans[key] = span;
                int window = span.Item1 + (T - span.Item1) / Math.Max(1, _go.ChatEveryTicks);
                if (span.Item2 < _go.ChatMinTicks || _chatted.Contains((a, b, window)))
                    continue;
                double chattiness = (_cast[i].Temperament.Chattiness + _cast[j].Temperament.Chattiness) / 2;
                if (Rng.Unit(_seed, "chat", a, b, T.ToString()) >= _go.ChatChance * (0.5 + chattiness))
                    continue;
                _chatted.Add((a, b, window));
                TryTell(a, b, T);
                TryTell(b, a, T);
            }
        }
    }

    /// <summary>How juicy a belief still is: its value when got, less the fade per whole day since.</summary>
    public double Current(Belief b, int T)
    {
        int days = T / TicksPerDay - b.GotTick / TicksPerDay;
        double baseJ = _kinds.First(k => k.Name == b.Kind).Juiciness;
        double fade = baseJ >= 4 ? _go.ScandalFadePerDay : _go.FadePerDay;
        return Math.Max(0, b.Juiciness - fade * Math.Max(0, days));
    }

    private void TryTell(string teller, string listener, int T)
    {
        int day = T / TicksPerDay;
        Belief? best = null;
        double bestScore = 0;
        foreach (Belief b in _beliefs[teller].Values.OrderBy(b => b.ActId))
        {
            if (b.Actor == listener || b.Chain.Count > 0 && b.Chain[0] == listener)
                continue; // never tell people about themselves, or back to who told you
            if (_told.Contains((teller, listener, b.ActId)))
                continue;
            if (_tellsToday.TryGetValue((teller, b.ActId, day), out int n) && n >= TellsPerDay)
                continue;
            double score = Current(b, T)
                + (b.Actor is { } who && Familiarity(listener, who) >= _go.KnowsAt ? _go.KnowsBonus : 0);
            if (score < _go.VolunteerLevel)
                continue;
            if (best is null || score > bestScore || score == bestScore && b.ActId > best.ActId)
            {
                best = b;
                bestScore = score;
            }
        }
        if (best is null)
            return;

        _told.Add((teller, listener, best.ActId));
        _tellsToday[(teller, best.ActId, day)] = _tellsToday.GetValueOrDefault((teller, best.ActId, day)) + 1;
        double confidence = best.Confidence * (0.5 + 0.5 * Familiarity(listener, teller));
        var told = new Belief(best.ActId, best.Kind, best.Actor, confidence, best.Clarity, Source.Told,
            _go.RetellFactor * Current(best, T), T, new[] { teller }.Concat(best.Chain).ToList());
        _log.Add($"{T} told {teller} {listener} {best.ActId}");

        if (_beliefs[listener].TryGetValue(best.ActId, out Belief? held))
        {
            // Already known: a name can fill in "someone", or replace a weaker name. Never more juice.
            if (told.Actor is not null && (held.Actor is null || told.Confidence > held.Confidence))
                Add(listener, held with { Actor = told.Actor, Confidence = told.Confidence }, T);
            return;
        }
        Add(listener, told, T);
    }

    // ---- scandal ---------------------------------------------------------------------------

    private void CheckScandals(int T)
    {
        foreach (Act act in _acts)
        {
            if (_confronted.Contains(act.Id) || !KindOf(act).IsScandal || _watching.ContainsKey(act.Id))
                continue;
            var holders = _names
                .Where(n => _beliefs[n].TryGetValue(act.Id, out Belief? b) && b.Actor is not null)
                .GroupBy(n => _beliefs[n][act.Id].Actor!)
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in holders)
            {
                string target = group.Key;
                int knowers = _names.Count(n => n != target && Familiarity(n, target) >= _go.KnowsActorAt);
                int needed = Math.Max(_go.ConfrontMin, (int)Math.Ceiling(_go.ConfrontShare * knowers));
                var members = group.Where(n => n != target).ToList();
                if (members.Count < needed)
                    continue;
                string by = members
                    .OrderByDescending(n => _cast[_index[n]].Temperament.Boldness * Current(_beliefs[n][act.Id], T))
                    .ThenBy(n => n, StringComparer.Ordinal).First();
                var c = new Confrontation(act.Id, T, by, target, target == act.Actor);
                _confrontations.Add(c);
                _confronted.Add(act.Id);
                _log.Add($"{T} confront {by} {target} {act.Id} {(c.Correct ? "right" : "wrong")}");
                break;
            }
        }
    }

    private void CloseDay(int day, int days)
    {
        foreach (Act act in _acts)
        {
            if (!_holdersByDay.TryGetValue(act.Id, out int[]? counts))
                _holdersByDay[act.Id] = counts = new int[days];
            counts[day] = _names.Count(n => _beliefs[n].ContainsKey(act.Id));
        }
    }
}
