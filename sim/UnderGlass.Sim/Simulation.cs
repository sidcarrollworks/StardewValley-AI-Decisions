namespace UnderGlass.Sim;

/// <summary>Knobs for gossip and scandal (design rules 8 and 9). First guesses for phase 0.</summary>
public sealed class GossipOptions
{
    /// <summary>A pair must be together this long before they chat.</summary>
    public int ChatMinMinutes { get; set; } = 15;
    /// <summary>Chance of a chat per 10 minutes together, x (0.5 + mean chattiness).</summary>
    public double ChatChance { get; set; } = 0.3;
    /// <summary>A pair chats at most once per span, and again every this many minutes they stay
    /// together (people side by side all day talk more than once).</summary>
    public int ChatEveryMinutes { get; set; } = 120;
    public double VolunteerLevel { get; set; } = 2;
    public double KnowsBonus { get; set; } = 0.5;      // the listener knows the believed actor well
    public double KnowsAt { get; set; } = 0.4;         // familiarity that counts as knowing well
    /// <summary>A listener gets the story at this share of the teller's juiciness. Tuned on placed
    /// scandals (2026-10-06): 0.4 for 13 villagers, 0.35 for the 26 with every family. A scandal
    /// heard second-hand is then passed on only to people who know the culprit well, and news
    /// heard second-hand goes no further.</summary>
    public double RetellFactor { get; set; } = 0.35;
    public double FadePerDay { get; set; } = 0.5;
    /// <summary>A witness keeps a scandal worth telling for about three days (4, 3.2, 2.4), as in the
    /// mod (D33). It was 0.65 for 13 villagers; with 26, tellers have two listeners a day.</summary>
    public double ScandalFadePerDay { get; set; } = 0.8;
    /// <summary>A teller tells one story to at most this many listeners a day; 0 sizes it by the
    /// town (1 under 20 villagers, 2 under 30, else 3).</summary>
    public int TellsPerDay { get; set; }
    public double FamiliarityGrowthPerHour { get; set; } = 0.012; // per hour spent near someone
    public double ConfrontShare { get; set; } = 0.25;
    public int ConfrontMin { get; set; } = 3;
    public double KnowsActorAt { get; set; } = 0.2;     // familiarity that counts as knowing someone at all
    /// <summary>A witness who saw "someone" suspects whoever they saw around the place this many
    /// minutes either side of it; a finder, anyone seen there in the hours before the find.</summary>
    public int SuspectSeenMinutes { get; set; } = 30;
    public int SuspectFoundHours { get; set; } = 8;
    public int MaxSuspects { get; set; } = 3;
}

/// <summary>Knobs for bodies, sleep and getting about (design rule 1). First guesses.</summary>
public sealed class BodyOptions
{
    /// <summary>Waking hours at rest that empty a whole bar. Rest drains a share of each person's
    /// own bar, so everyone's day is about 24 hours long whatever their size.</summary>
    public double AwakeHoursAtRest { get; set; } = 22;
    /// <summary>Exertion on top of rest (1 is rest) costs a fixed amount, the same for everyone:
    /// (effort - 1) x 100 / AwakeHoursAtRest an hour. A bigger bar takes hard work in its stride.</summary>
    public double WalkEffort { get; set; } = 1.2;
    /// <summary>The body clock (the circadian half of feeling sleepy): tiredness felt is the share of
    /// the bar used plus this weight x cos(hours from the peak), so people get sleepy toward the
    /// small hours and stay alert in the afternoon. Bedtime still comes from each body.</summary>
    public double CircadianWeight { get; set; } = 0.15;
    public int CircadianPeak { get; set; } = 4 * 60;
    /// <summary>Hours of sleep that fill an empty bar, whatever its size.</summary>
    public double SleepHoursToFill { get; set; } = 11;
    /// <summary>Start for home a little before feeling tired enough for bed.</summary>
    public double HeadHomeMargin { get; set; } = 0.05;
    /// <summary>The chance an alarm wakes a sleeper: base + per-energy x the share of the bar filled.</summary>
    public double AlarmBase { get; set; } = 0.35;
    public double AlarmPerEnergy { get; set; } = 0.6;
    /// <summary>Workers set the alarm this long before work starts.</summary>
    public int MorningMinutes { get; set; } = 60;
    /// <summary>Workers leave for work this long before it starts.</summary>
    public int CommuteMinutes { get; set; } = 30;
    /// <summary>Not at work this long after the start counts as late.</summary>
    public int LateAfterMinutes { get; set; } = 15;
    public int WalkTilesPerMinute { get; set; } = 2;

    public double WakeChance(double filled) => Math.Clamp(AlarmBase + AlarmPerEnergy * filled, 0, 1);

    /// <summary>How tired someone feels: the share of their bar used, plus the body clock.</summary>
    public double Tiredness(double filled, int minuteOfDay)
        => 1 - filled + CircadianWeight * Math.Cos(2 * Math.PI * (minuteOfDay - CircadianPeak) / Clock.MinutesPerDay);
}

/// <summary>What a run produced.</summary>
public sealed class SimResult
{
    public required IReadOnlyList<Act> Acts { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<int, Belief>> Beliefs { get; init; }
    public required IReadOnlyList<Confrontation> Confrontations { get; init; }
    /// <summary>Holders of each act at the end of each day: [actId][day] = count.</summary>
    public required IReadOnlyDictionary<int, int[]> HoldersByDay { get; init; }
    /// <summary>The same, leaving out people who only found a trace and never heard a name: the
    /// story's spread by sight and gossip.</summary>
    public required IReadOnlyDictionary<int, int[]> HeardByDay { get; init; }
    public required IReadOnlyDictionary<int, int> Witnesses { get; init; }
    public required IReadOnlyList<string> Log { get; init; }
    public required int CastSize { get; init; }
    public required int Days { get; init; }
    public required IReadOnlyList<Sleep> Sleeps { get; init; }
    /// <summary>Workers not at work <see cref="BodyOptions.LateAfterMinutes"/> after it started.</summary>
    public required IReadOnlyList<(string Name, int Day)> Late { get; init; }
    /// <summary>Who was around when each act began.</summary>
    public required IReadOnlyDictionary<int, Scene> Scenes { get; init; }
    /// <summary>Every account the mayor received, in order (design rule 16).</summary>
    public required IReadOnlyList<Account> Accounts { get; init; }
    public required IReadOnlyList<Verdict> Verdicts { get; init; }
    public required IReadOnlyList<Election> Elections { get; init; }
    /// <summary>Everyone questioned about a case, and whether they confessed.</summary>
    public required IReadOnlyList<(int ActId, string Who, bool Confessed)> Interviews { get; init; }
    /// <summary>The town's cash at the start and at the end of each day (phase 0b); empty without money.</summary>
    public required IReadOnlyList<double> TownCash { get; init; }
    /// <summary>Money that came into town from outside, and went out, over the run.</summary>
    public required double OutsideIn { get; init; }
    public required double OutsideOut { get; init; }
    /// <summary>Each household's purse and each person's pocket at the end.</summary>
    public required IReadOnlyDictionary<string, double> Purses { get; init; }
    public required IReadOnlyDictionary<string, double> Pockets { get; init; }
    /// <summary>Why each tempted scandal happened: "want 640g", "need" or "thrill".</summary>
    public required IReadOnlyList<(int ActId, string Who, string Motive)> Motives { get; init; }
    public required string? Constable { get; init; }
}

/// <summary>
/// Phase 0a: the gossip harness (design section 11), on the 24-hour clock (rule 1). Villagers
/// sleep and wake by their energy and alarms, go to work and to their haunts along the roads,
/// acts happen minute by minute, witnesses perceive them in layers (rule 2), traces let unseen
/// acts be found later, and stories spread by juiciness (rule 8) until a scandal leads to a
/// confrontation (rule 9) or a verdict from the mayor (rule 16; Simulation.Authority.cs).
/// Deterministic for a seed. Single-threaded.
/// </summary>
public sealed partial class Simulation
{
    public const string Collapsed = "Collapsed";

    /// <summary>For an act placed with <see cref="Harness.Anyone"/>: the mean number of minutes
    /// with someone able before it happens.</summary>
    public const int PlacedMeanWait = 120;

    private sealed class Person
    {
        public required Villager V;
        public string Place = "";
        public Tile At;
        public double Energy;
        public bool Asleep;
        public int? Alarm;
        public bool MissedAlarm;
        public int SleepIndex = -1;
        public bool PendingCollapse;
        public bool Walking;
        public int BusyUntil = -1;
        public string GoalPlace = "";
        public Tile GoalSpot;
        public int GoalUntil;
        public string Why = "";
        public Haunt? Haunt;
        public Tile Target;
        public int DetainedUntil = -1;
        public string HoldPlace = "";
        public Tile HoldSpot;
    }

    private readonly long _seed;
    private readonly IReadOnlyList<Villager> _cast;
    private readonly Dictionary<string, Location> _places;
    private readonly IReadOnlyList<ActKind> _kinds;
    private readonly PerceptionOptions _po;
    private readonly GossipOptions _go;
    private readonly BodyOptions _bo;
    private readonly HabitOptions _ho;
    private readonly string[] _names;
    private readonly Dictionary<string, int> _index;
    private readonly Person[] _people;
    private readonly double[,] _fam;
    private readonly Dictionary<string, Dictionary<int, Belief>> _beliefs = new();
    private readonly HashSet<(string, string, int)> _told = new();
    private readonly Dictionary<(string, int, int), int> _tellsToday = new();
    private readonly Dictionary<(string, string), int> _spans = new();
    private readonly HashSet<(string, string, int)> _chatted = new();
    private readonly List<Act> _acts = new();
    private readonly Dictionary<int, Dictionary<string, List<double>>> _watching = new();
    private readonly Dictionary<int, int> _witnesses = new();
    private readonly HashSet<int> _confronted = new();
    private readonly List<Confrontation> _confrontations = new();
    private readonly Dictionary<int, int[]> _holdersByDay = new();
    private readonly Dictionary<int, int[]> _heardByDay = new();
    private readonly List<Sleep> _sleeps = new();
    private readonly List<(string, int)> _late = new();
    private readonly Dictionary<int, Scene> _scenes = new();
    private readonly IReadOnlyList<Gathering> _gatherings;
    private readonly List<string> _log = new();
    private readonly List<(int Tick, string Actor, string Kind)> _pending;
    private readonly int _wander;
    private readonly Dictionary<(string, string), (Tile Exit, string Next, Tile Entry)?> _hops = new();
    private readonly Dictionary<string, List<(string Next, Tile Exit, Tile Entry)>> _doors = new();
    private readonly Dictionary<(string, Tile), int[,]> _fields = new();

    /// <param name="scheduled">Acts to place: at or after the minute, the first time the actor is
    /// awake, free and somewhere the act is allowed (within 3 days, else dropped). The actor
    /// <see cref="Harness.Anyone"/> means whoever is first able. Marked injected.</param>
    /// <param name="links">Doors between places; defaults to the town's when the places are the town's.</param>
    /// <param name="gatherings">Hubs; default to the town's when the places are the town's.</param>
    /// <param name="authority">The mayor, keepers, constable and ladder; default to the town's when
    /// the places are the town's, else no authority.</param>
    public Simulation(long seed, IReadOnlyList<Villager>? cast = null, IReadOnlyList<Location>? places = null,
        IReadOnlyList<ActKind>? kinds = null, PerceptionOptions? perception = null, GossipOptions? gossip = null,
        IReadOnlyList<(int Tick, string Actor, string Kind)>? scheduled = null, int wander = 2,
        IReadOnlyList<Link>? links = null, BodyOptions? body = null, IReadOnlyList<Gathering>? gatherings = null,
        AuthorityOptions? authority = null, HabitOptions? habits = null, Economy? economy = null, MoneyOptions? money = null)
    {
        _economy = economy ?? (places is null ? DefaultTown.TownEconomy() : null);
        _mo = money ?? new MoneyOptions();
        _ho = habits ?? new HabitOptions();
        _ao = authority ?? (places is null ? DefaultTown.TownAuthority() : new AuthorityOptions());
        _constable = _ao.Constable;
        foreach (var (name, count) in _ao.Record)
            _record[name] = count;
        _gatherings = gatherings ?? (places is null ? DefaultTown.Gatherings() : Array.Empty<Gathering>());
        _wander = wander;
        _pending = (scheduled ?? Array.Empty<(int, string, string)>()).ToList();
        _seed = seed;
        _cast = (cast ?? DefaultTown.Cast()).OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
        _places = (places ?? DefaultTown.Locations()).ToDictionary(p => p.Name);
        _kinds = kinds ?? DefaultTown.Acts();
        _po = perception ?? new PerceptionOptions();
        _go = gossip ?? new GossipOptions();
        _bo = body ?? new BodyOptions();
        _names = _cast.Select(v => v.Name).ToArray();
        _index = _names.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);
        _fam = new double[_names.Length, _names.Length];
        foreach (Villager a in _cast)
            foreach (Villager b in _cast)
                if (a != b)
                    _fam[_index[a.Name], _index[b.Name]] = SeedFamiliarity(a, b);
        foreach (string n in _names)
            _beliefs[n] = new Dictionary<int, Belief>();
        foreach (Link l in links ?? (places is null ? DefaultTown.Links() : Array.Empty<Link>()))
        {
            Door(l.A, l.B, l.DoorA, l.DoorB);
            Door(l.B, l.A, l.DoorB, l.DoorA);
        }
        foreach (var list in _doors.Values)
            list.Sort((x, y) => StringComparer.Ordinal.Compare(x.Next, y.Next));
        _people = _cast.Select(v => new Person { V = v }).ToArray();
    }

    private void Door(string from, string to, Tile exit, Tile entry)
    {
        if (!_doors.TryGetValue(from, out var list))
            _doors[from] = list = new List<(string, Tile, Tile)>();
        list.Add((to, exit, entry));
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

    /// <summary>Where someone is now, for tests and the runner. Never read by the rules.</summary>
    public (string Place, Tile At, bool Asleep, double Energy) Where(string name)
    {
        Person p = _people[_index[name]];
        return (p.Place, p.At, p.Asleep, p.Energy);
    }

    private int TellsPerDay => _go.TellsPerDay > 0 ? _go.TellsPerDay : _names.Length < 20 ? 1 : _names.Length < 30 ? 2 : 3;

    /// <summary>Run whole days from midnight of day 0. Everyone starts asleep at home, part rested,
    /// and wakes some time before 8:00. <paramref name="each"/> is called after every minute.</summary>
    public SimResult Run(int days, Action<int, Simulation>? each = null)
    {
        // The log is formatted in the invariant culture, so a run hashes the same on every machine
        // (juiciness 1.5 would print as "1,5" under de-DE).
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            return RunDays(days, each);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    private SimResult RunDays(int days, Action<int, Simulation>? each)
    {
        foreach (Person p in _people)
        {
            bool home = _places.ContainsKey(p.V.Home);
            (string place, Tile spot) = home ? (p.V.Home, DefaultTown.Bed) : FirstSpot(p.V);
            p.Place = place;
            p.At = spot;
            p.Energy = p.V.Body.MaxEnergy * Rng.Range(_seed, 30, 50, "rested", p.V.Name) / 100.0;
            StartSleep(p, 0, collapsed: false, log: false);
        }
        StartMoney();
        for (int m = 0; m < days * Clock.MinutesPerDay; m++)
        {
            if (HasMoney && Clock.OfDay(m) == 0)
            {
                if (Clock.Weekday(m) == 0)
                    Payday(m);
                Wants(m);
            }
            Step(m);
            if (Clock.OfDay(m) == Clock.MinutesPerDay - 1)
                CloseDay(Clock.Day(m), days);
            each?.Invoke(m, this);
        }
        return new SimResult
        {
            Acts = _acts,
            Beliefs = _beliefs.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<int, Belief>)p.Value),
            Confrontations = _confrontations,
            HoldersByDay = _holdersByDay,
            HeardByDay = _heardByDay,
            Witnesses = _witnesses,
            Log = _log,
            CastSize = _names.Length,
            Days = days,
            Sleeps = _sleeps,
            Late = _late,
            Scenes = _scenes,
            Accounts = _filed,
            Verdicts = _verdicts,
            Elections = _elections,
            Interviews = _interviews,
            TownCash = _townCash,
            OutsideIn = _outsideIn,
            OutsideOut = _outsideOut,
            Purses = _purse,
            Pockets = _pocket,
            Motives = _motives,
            Constable = _constable,
        };
    }

    private static (string, Tile) FirstSpot(Villager v)
        => v.Job is { } j ? (j.Place, j.Spot) : v.Haunts.Count > 0 ? (v.Haunts[0].Place, v.Haunts[0].Spot) : throw new InvalidOperationException($"{v.Name} has nowhere to be");

    private void Step(int m)
    {
        int t = Clock.OfDay(m);
        bool tick = m % Clock.TickMinutes == 0;
        foreach (Person p in _people)
            Live(p, m, tick);
        if (tick)
        {
            StartActs(m);
            if (HasMoney)
            {
                Temptation(m);
                Drinks(m);
            }
        }
        StartScheduled(m);
        Watch(m, t);
        FinishActs(m);
        if (tick)
        {
            Socialise(m);
            CheckScandals(m);
            CheckTraces(m);
            See(m);
            PieceTogether(m);
            Authorities(m);
            Rows(m);
        }
        CheckLate(m, t);
    }

    // ---- bodies: energy, sleep, alarms (design rule 1) -------------------------------------

    private void Live(Person p, int m, bool tick)
    {
        double max = p.V.Body.MaxEnergy;
        if (p.Asleep)
        {
            p.Energy = Math.Min(max, p.Energy + max / (_bo.SleepHoursToFill * 60));
            if (p.Energy >= max)
                Wake(p, m, "rested");
            else if (p.Alarm == m)
            {
                if (Rng.Unit(_seed, "alarm", p.V.Name, m.ToString()) < _bo.WakeChance(p.Energy / max))
                    Wake(p, m, "alarm");
                else
                {
                    p.MissedAlarm = true;
                    p.Alarm = null;
                    _log.Add($"{m} slept-through {p.V.Name}");
                }
            }
            return;
        }
        if (p.PendingCollapse)
        {
            p.PendingCollapse = false;
            if (_places.ContainsKey(p.V.Home))
            {
                p.Place = p.V.Home;
                p.At = DefaultTown.Bed;
            }
            StartSleep(p, m, collapsed: true, log: true);
            return;
        }

        bool atWork = p.Why == "work" && p.V.Job is { } job && p.Place == job.Place;
        double effort = p.Walking ? _bo.WalkEffort : atWork ? p.V.Job!.Effort : 1;
        p.Energy -= (max + Math.Max(0, effort - 1) * 100) / (_bo.AwakeHoursAtRest * 60);
        if (p.Energy <= 0)
        {
            p.Energy = 0;
            p.PendingCollapse = true;
            _log.Add($"{m} collapse {p.V.Name} at {p.Place}");
            if (_kinds.FirstOrDefault(k => k.Name == Collapsed) is { } kind)
                Begin(m, kind, p, injected: false);
            return;
        }
        if (tick || p.Why == "")
            Decide(p, m);
        if (p.BusyUntil >= m)
        {
            p.Walking = false;
            return; // in the middle of an act: stands still
        }
        Walk(p, m, tick);
        if (p.Why == "bed" && p.Place == p.GoalPlace && p.At == p.GoalSpot)
            StartSleep(p, m, collapsed: false, log: true);
    }

    private void StartSleep(Person p, int m, bool collapsed, bool log)
    {
        p.Asleep = true;
        p.Walking = false;
        p.Why = "";
        p.Haunt = null;
        p.MissedAlarm = false;
        p.Alarm = NextAlarm(p, m);
        p.SleepIndex = _sleeps.Count;
        _sleeps.Add(new Sleep(p.V.Name, m, null, p.Energy, false, collapsed));
        if (log)
            _log.Add($"{m} sleep {p.V.Name} {p.Energy:0.#}{(p.Alarm is { } a ? " alarm " + a : "")}");
    }

    private void Wake(Person p, int m, string why)
    {
        p.Asleep = false;
        p.Alarm = null;
        _sleeps[p.SleepIndex] = _sleeps[p.SleepIndex] with { WokeAt = m, MissedAlarm = p.MissedAlarm };
        p.MissedAlarm = false;
        p.Why = "";
        _log.Add($"{m} wake {p.V.Name} {why} {p.Energy:0.#}");
    }

    /// <summary>The next workday's alarm after <paramref name="m"/>, or none for someone without a job.</summary>
    private int? NextAlarm(Person p, int m)
    {
        if (p.V.Job is not { } job)
            return null;
        for (int d = Clock.Day(m); d <= Clock.Day(m) + Clock.DaysPerWeek; d++)
        {
            if (!job.WorksOn(d % Clock.DaysPerWeek))
                continue;
            int a = d * Clock.MinutesPerDay + job.Start - _bo.MorningMinutes;
            if (a > m)
                return a;
        }
        return null;
    }

    private void CheckLate(int m, int t)
    {
        foreach (Person p in _people)
        {
            if (p.V.Job is not { } job || t != job.Start + _bo.LateAfterMinutes || !job.WorksOn(Clock.Weekday(m)))
                continue;
            if (p.Place != job.Place)
            {
                _late.Add((p.V.Name, Clock.Day(m)));
                _log.Add($"{m} late {p.V.Name}");
            }
        }
    }

    // ---- where everyone goes ---------------------------------------------------------------

    private void Decide(Person p, int m)
    {
        int t = Clock.OfDay(m);
        bool hasHome = _places.ContainsKey(p.V.Home);
        Body body = p.V.Body;
        bool tired = p.Why == "bed" || body.BedAt >= 0 && _bo.Tiredness(p.Energy / body.MaxEnergy, t) >= 1 - body.BedAt - _bo.HeadHomeMargin;
        if (p.DetainedUntil > m)
        {
            // Held until the time is up: they sleep there too (design rule 16).
            var (lockup, spot) = Lockup(p);
            Goal(p, lockup, spot, p.DetainedUntil, tired ? "bed" : "held", null);
            return;
        }
        if (tired)
        {
            if (hasHome)
                Goal(p, p.V.Home, DefaultTown.Bed, int.MaxValue, "bed", null);
            else
                Goal(p, p.Place, p.At, int.MaxValue, "bed", null); // no home in this world: sleep where you stand
            return;
        }
        if (p.V.Job is { } job && job.WorksOn(Clock.Weekday(m)) && t >= job.Start - _bo.CommuteMinutes && t < job.End)
        {
            Goal(p, job.Place, job.Spot, m - t + job.End, "work", null);
            return;
        }
        if (Patrol(p, m))
            return;
        if (p.Why is "haunt" or "home" && p.GoalUntil > m && (p.Haunt is null || p.Haunt.Open(t)))
            return;

        var options = p.V.Haunts.Where(h => h.Open(t)).Select(h => (Haunt: (Haunt?)h, h.Weight)).ToList();
        foreach (Gathering g in _gatherings.Where(g => g.On(m)))
        {
            // A spot in the crowd, the same for this person all through the gathering.
            int day = Clock.Day(m);
            var spot = new Tile(g.Center.X + Rng.Range(_seed, -g.Radius, g.Radius, "crowd-x", g.Name, p.V.Name, day.ToString()),
                                g.Center.Y + Rng.Range(_seed, -g.Radius, g.Radius, "crowd-y", g.Name, p.V.Name, day.ToString()));
            if (!_places[g.Place].Walkable(spot))
                spot = g.Center;
            int to = g.To;
            options.Add((new Haunt(g.Place, spot, g.From, to, g.Weight), g.Weight));
        }
        if (hasHome)
            options.Add((null, 1.0));
        if (options.Count == 0)
        {
            Goal(p, p.Place, p.At, m + 60, "home", null);
            return;
        }
        double r = Rng.Unit(_seed, "haunt", p.V.Name, m.ToString()) * options.Sum(o => o.Weight);
        Haunt? pick = options[^1].Haunt;
        foreach (var o in options)
        {
            r -= o.Weight;
            if (r < 0) { pick = o.Haunt; break; }
        }
        int stay = Rng.Range(_seed, 60, 180, "stay", p.V.Name, m.ToString());
        if (pick is null)
            Goal(p, p.V.Home, DefaultTown.Sofa, m + stay, "home", null);
        else
            Goal(p, pick.Place, pick.Spot, m + stay, "haunt", pick);
    }

    private static void Goal(Person p, string place, Tile spot, int until, string why, Haunt? haunt)
    {
        bool same = p.GoalPlace == place && p.GoalSpot == spot && p.Why == why;
        p.GoalPlace = place;
        p.GoalSpot = spot;
        p.GoalUntil = until;
        p.Why = why;
        p.Haunt = haunt;
        if (!same)
            p.Target = spot;
    }

    private void Walk(Person p, int m, bool tick)
    {
        bool moved = false;
        if (tick && p.Why is not "bed" && _wander > 0 && p.Place == p.GoalPlace && p.At == p.Target)
        {
            var w = new Tile(p.GoalSpot.X + Rng.Range(_seed, -_wander, _wander, "wx", p.V.Name, m.ToString()),
                             p.GoalSpot.Y + Rng.Range(_seed, -_wander, _wander, "wy", p.V.Name, m.ToString()));
            if (_places[p.Place].Walkable(w))
                p.Target = w;
        }
        for (int s = 0; s < _bo.WalkTilesPerMinute; s++)
        {
            if (p.Place != p.GoalPlace)
            {
                if (Hop(p.Place, p.GoalPlace) is not { } hop)
                {
                    p.Place = p.GoalPlace; // no road there in this world: arrive
                    p.At = p.Target = p.GoalSpot;
                    moved = true;
                    break;
                }
                if (p.At == hop.Exit)
                {
                    p.Place = hop.Next;
                    p.At = hop.Entry;
                    if (p.Place == p.GoalPlace)
                        p.Target = p.GoalSpot;
                }
                else
                    StepToward(p, hop.Exit);
                moved = true;
                continue;
            }
            if (p.At == p.Target)
                break;
            StepToward(p, p.Target);
            moved = true;
        }
        p.Walking = moved;
    }

    private static readonly (int Dx, int Dy)[] Steps =
        { (0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1) };

    private void StepToward(Person p, Tile target)
    {
        int[,] field = Field(p.Place, target);
        int here = Inside(field, p.At) ? field[p.At.X, p.At.Y] : -1;
        if (here <= 0)
        {
            p.At = target; // unreachable from here (or there): step over it
            return;
        }
        foreach (var (dx, dy) in Steps)
        {
            var n = new Tile(p.At.X + dx, p.At.Y + dy);
            if (Inside(field, n) && field[n.X, n.Y] == here - 1)
            {
                p.At = n;
                return;
            }
        }
        p.At = target;
    }

    private static bool Inside(int[,] f, Tile t) => t.X >= 0 && t.Y >= 0 && t.X < f.GetLength(0) && t.Y < f.GetLength(1);

    /// <summary>Walking distance in steps to <paramref name="target"/> from every tile (-1:
    /// unreachable), 8 directions over open tiles. Cached per place and target.</summary>
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
        if (Inside(f, target))
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
                if (Inside(f, n) && f[n.X, n.Y] < 0 && loc.Walkable(n))
                {
                    f[n.X, n.Y] = f[c.X, c.Y] + 1;
                    queue.Enqueue(n);
                }
            }
        }
        _fields[(place, target)] = f;
        return f;
    }

    /// <summary>The next door on the way from one place to another (fewest doors), or null.</summary>
    private (Tile Exit, string Next, Tile Entry)? Hop(string from, string to)
    {
        if (_hops.TryGetValue((from, to), out var cached))
            return cached;
        var dist = new Dictionary<string, int> { [to] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(to);
        while (queue.Count > 0)
        {
            string c = queue.Dequeue();
            foreach (var (next, _, _) in _doors.GetValueOrDefault(c) ?? new())
                if (dist.TryAdd(next, dist[c] + 1))
                    queue.Enqueue(next);
        }
        (Tile, string, Tile)? hop = null;
        if (dist.TryGetValue(from, out int d) && d > 0)
            foreach (var (next, exit, entry) in _doors[from])
                if (dist.TryGetValue(next, out int dn) && dn == d - 1)
                {
                    hop = (exit, next, entry);
                    break;
                }
        _hops[(from, to)] = hop;
        return hop;
    }

    // ---- acts and witnessing ---------------------------------------------------------------

    private bool Free(Person p, int m) => !p.Asleep && !p.PendingCollapse && p.BusyUntil < m && p.DetainedUntil <= m;

    private void StartActs(int m)
    {
        double ticksPerDay = Clock.MinutesPerDay / (double)Clock.TickMinutes;
        foreach (ActKind kind in _kinds)
        {
            if (kind.PerDay <= 0 || Rng.Unit(_seed, "act", kind.Name, m.ToString()) >= kind.PerDay / ticksPerDay)
                continue;
            var candidates = _people
                .Where(p => Free(p, m) && p.V.Acts.TryGetValue(kind.Name, out double w) && w > 0 && kind.FitsAge(p.V.Age))
                .Where(p => kind.Allowed.Count == 0 || kind.Allowed.Contains(p.Place))
                .Where(p => kind.WithKin is not { } role || _people.Any(o => o != p && !o.Asleep && p.V.KinOf(o.V.Name) == role
                                                                             && o.Place == p.Place && o.At.Chebyshev(p.At) <= _po.FarTiles))
                .ToList();
            if (candidates.Count == 0)
                continue;
            double r = Rng.Unit(_seed, "actor", kind.Name, m.ToString()) * candidates.Sum(p => p.V.Acts[kind.Name]);
            Person actor = candidates[^1];
            foreach (Person p in candidates)
            {
                r -= p.V.Acts[kind.Name];
                if (r < 0) { actor = p; break; }
            }
            Begin(m, kind, actor, injected: false);
        }
    }

    private void StartScheduled(int m)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            var (at, who, kindName) = _pending[i];
            if (at > m)
                continue;
            ActKind kind = _kinds.First(k => k.Name == kindName);
            bool Able(Person p) => Free(p, m) && kind.FitsAge(p.V.Age) && (kind.Allowed.Count == 0 || kind.Allowed.Contains(p.Place));
            Person? actor = null;
            if (who == Harness.Anyone)
            {
                // Not the keeper in their own place of work: nobody robs their own shop.
                var able = _people.Where(p => Able(p) && p.V.Job?.Place != p.Place).ToList();
                // A seeded chance each minute someone is able, so the moment is spread over the
                // times people are there instead of always the first (often an empty shop at dawn).
                if (able.Count > 0 && Rng.Unit(_seed, "placed-when", i.ToString(), m.ToString()) < 1.0 / PlacedMeanWait)
                    actor = able[Rng.Range(_seed, 0, able.Count - 1, "placed", i.ToString(), m.ToString())];
            }
            else if (Able(_people[_index[who]]))
                actor = _people[_index[who]];
            if (actor is not null)
                Begin(m, kind, actor, injected: true);
            else if (m < at + 3 * Clock.MinutesPerDay)
                continue;
            else
                _log.Add($"{m} dropped {kindName} by {who}");
            _pending.RemoveAt(i--);
        }
    }

    private void Begin(int m, ActKind kind, Person actor, bool injected)
    {
        var act = new Act(_acts.Count, m, actor.V.Name, kind.Name, actor.Place, actor.At, injected);
        _acts.Add(act);
        _scenes[act.Id] = SceneOf(act, actor);
        Gains(act, actor);
        actor.BusyUntil = m + kind.DurationMinutes - 1;
        _watching[act.Id] = new Dictionary<string, List<double>>();
        _log.Add($"{m} act {act.Id} {act.Kind} by {act.Actor} at {act.Location}");
    }

    private ActKind KindOf(Act a) => _kinds.First(k => k.Name == a.Kind);

    private Scene SceneOf(Act act, Person actor)
    {
        int inRange = 0, samePlace = 0, elsewhere = 0, asleep = 0;
        foreach (Person o in _people)
        {
            if (o == actor) continue;
            if (o.Asleep) asleep++;
            else if (o.Place != act.Location) elsewhere++;
            else if (o.At.Chebyshev(act.At) <= _po.FarTiles) inRange++;
            else samePlace++;
        }
        return new Scene(inRange, samePlace, elsewhere, asleep);
    }

    private void Watch(int m, int t)
    {
        foreach (Act act in _acts.Where(a => _watching.ContainsKey(a.Id)))
        {
            Location place = _places[act.Location];
            foreach (Person o in _people)
            {
                if (o.V.Name == act.Actor || o.Asleep || o.Place != act.Location)
                    continue;
                double c = Perception.Instant(place, o.At, act.At, t, _po);
                if (c <= 0)
                    continue;
                var seen = _watching[act.Id];
                if (!seen.TryGetValue(o.V.Name, out var list))
                    seen[o.V.Name] = list = new List<double>();
                list.Add(c);
            }
        }
    }

    private void FinishActs(int m)
    {
        foreach (Act act in _acts.Where(a => _watching.ContainsKey(a.Id)).ToList())
        {
            ActKind kind = KindOf(act);
            if (act.Tick + kind.DurationMinutes - 1 > m)
                continue;
            int witnesses = 0;
            foreach (var (observer, instants) in _watching[act.Id].OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                double clarity = Perception.OfAct(instants, kind.ReadMinutes);
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
                    actor = Guess(observer, act);
                    confidence = 0.8;
                }
                Add(observer, new Belief(act.Id, act.Kind, actor, confidence, clarity, Source.Witnessed,
                    kind.Juiciness, m, Array.Empty<string>()), m);
            }
            _witnesses[act.Id] = witnesses;
            _watching.Remove(act.Id);
            LeaveTrace(act, kind, m);
        }
    }

    /// <summary>A confident guess: someone awake and present, weighted by how well the observer knows them.</summary>
    private string Guess(string observer, Act act)
    {
        var present = _people.Where(p => p.V.Name != observer && !p.Asleep && p.Place == act.Location)
            .Select(p => p.V.Name).ToList();
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

    private void Add(string who, Belief b, int m)
    {
        _beliefs[who][b.ActId] = b;
        _log.Add($"{m} belief {who} {b.ActId} {b.Actor ?? "someone"} {b.Source} {b.Juiciness:0.###}");
        OwnAccount(who, b, m);
        CurfewBroken(who, b, m);
    }

    // ---- chats and gossip ------------------------------------------------------------------

    private void Socialise(int m)
    {
        double hours = Clock.TickMinutes / 60.0;
        for (int i = 0; i < _people.Length; i++)
        {
            for (int j = i + 1; j < _people.Length; j++)
            {
                Person pa = _people[i], pb = _people[j];
                string a = pa.V.Name, b = pb.V.Name;
                var key = (a, b);
                bool together = !pa.Asleep && !pb.Asleep && pa.Place == pb.Place && pa.At.Chebyshev(pb.At) <= _po.FarTiles;
                if (!together)
                {
                    _spans.Remove(key);
                    continue;
                }
                _fam[i, j] += _go.FamiliarityGrowthPerHour * hours * (1 - _fam[i, j]);
                _fam[j, i] += _go.FamiliarityGrowthPerHour * hours * (1 - _fam[j, i]);
                if (!_spans.TryGetValue(key, out int start))
                    _spans[key] = start = m;
                int window = start + (m - start) / Math.Max(1, _go.ChatEveryMinutes);
                if (m - start < _go.ChatMinMinutes || _chatted.Contains((a, b, window)))
                    continue;
                double chattiness = (pa.V.Temperament.Chattiness + pb.V.Temperament.Chattiness) / 2;
                double per10 = Math.Min(1, _go.ChatChance * (0.5 + chattiness));
                double chance = 1 - Math.Pow(1 - per10, Clock.TickMinutes / 10.0);
                if (Rng.Unit(_seed, "chat", a, b, m.ToString()) >= chance)
                    continue;
                _chatted.Add((a, b, window));
                TryTell(a, b, m);
                TryTell(b, a, m);
            }
        }
    }

    /// <summary>How juicy a belief still is: its value when got, less the fade per whole 24 hours since.</summary>
    public double Current(Belief b, int m)
    {
        int days = Math.Max(0, (m - b.GotTick) / Clock.MinutesPerDay);
        double baseJ = _kinds.First(k => k.Name == b.Kind).Juiciness;
        double fade = baseJ >= 4 ? _go.ScandalFadePerDay : _go.FadePerDay;
        return Math.Max(0, b.Juiciness - fade * days);
    }

    private void TryTell(string teller, string listener, int m)
    {
        int day = Clock.Day(m);
        Belief? best = null;
        double bestScore = 0;
        foreach (Belief b in _beliefs[teller].Values.OrderBy(b => b.ActId))
        {
            if (b.Actor == listener || b.Suspects?.Contains(listener) == true || b.Chain.Count > 0 && b.Chain[0] == listener)
                continue; // never tell people about themselves (or that they're suspected), or back to who told you
            if (b.Actor is { } culprit && AreKin(teller, culprit))
                continue; // families cover: no stories that hurt kin (rule 17)
            if (_told.Contains((teller, listener, b.ActId)))
                continue;
            if (_tellsToday.TryGetValue((teller, b.ActId, day), out int n) && n >= TellsPerDay)
                continue;
            string? named = b.Actor ?? b.Suspects?.FirstOrDefault();
            double score = Current(b, m)
                + (named is { } who && Familiarity(listener, who) >= _go.KnowsAt ? _go.KnowsBonus : 0);
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
            _go.RetellFactor * Current(best, m), m, new[] { teller }.Concat(best.Chain).ToList(),
            best.Actor is null ? WithoutKin(teller, best.Suspects) : null);
        _log.Add($"{m} told {teller} {listener} {best.ActId}");
        if (_acts[best.ActId].Actor == listener)
            return; // the culprit hears about their own act: they learn nothing they didn't know

        if (_beliefs[listener].TryGetValue(best.ActId, out Belief? held))
        {
            // Already known: a name can fill in "someone", or replace a weaker name. Never more juice.
            if (told.Actor is not null && (held.Actor is null || told.Confidence > held.Confidence))
                Add(listener, held with { Actor = told.Actor, Confidence = told.Confidence }, m);
            else if (told.Actor is null && held.Actor is null && held.Suspects is not { Count: > 0 } && told.Suspects is { Count: > 0 })
                Add(listener, held with { Suspects = told.Suspects }, m); // someone's suspicion fills in their "someone"
            return;
        }
        Add(listener, told, m);
    }

    // ---- scandal ---------------------------------------------------------------------------

    private void CheckScandals(int m)
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
                var members = group.Where(n => n != target && !AreKin(n, target)).ToList(); // nobody confronts their own kin
                if (members.Count < needed)
                    continue;
                string by = members
                    .OrderByDescending(n => _cast[_index[n]].Temperament.Boldness * Current(_beliefs[n][act.Id], m))
                    .ThenBy(n => n, StringComparer.Ordinal).First();
                var c = new Confrontation(act.Id, m, by, target, target == act.Actor);
                _confrontations.Add(c);
                _confronted.Add(act.Id);
                _log.Add($"{m} confront {by} {target} {act.Id} {(c.Correct ? "right" : "wrong")}");
                break;
            }
        }
    }

    private void CloseDay(int day, int days)
    {
        ForgetSightings((day + 1) * Clock.MinutesPerDay);
        CloseMoneyDay();
        foreach (Act act in _acts)
        {
            if (!_holdersByDay.TryGetValue(act.Id, out int[]? counts))
                _holdersByDay[act.Id] = counts = new int[days];
            counts[day] = _names.Count(n => _beliefs[n].ContainsKey(act.Id));
            if (!_heardByDay.TryGetValue(act.Id, out int[]? heard))
                _heardByDay[act.Id] = heard = new int[days];
            heard[day] = _names.Count(n => _beliefs[n].TryGetValue(act.Id, out Belief? b) && !(b.Source == Source.Found && b.Actor is null));
        }
    }
}
