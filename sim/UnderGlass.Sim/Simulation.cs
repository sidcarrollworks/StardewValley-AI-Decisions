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
    /// <summary>Familiarity fading, by Sid's forgetting model (town spec E6); off unless its FadePerDay is set.</summary>
    public ForgettingOptions Forgetting { get; set; } = new();
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

    // Feelings (phase 0c); empty when feelings are off.
    /// <summary>The cast in name order.</summary>
    public required IReadOnlyList<string> Names { get; init; }
    /// <summary>Personal regard at the end, for every ordered pair.</summary>
    public required IReadOnlyDictionary<(string From, string To), double> Regard { get; init; }
    /// <summary>Where each pair's regard started and heals back to.</summary>
    public required IReadOnlyDictionary<(string From, string To), double> Baseline { get; init; }
    public required IReadOnlyDictionary<(string From, string Kind), double> KindRegard { get; init; }
    /// <summary>Personal regard at each season's end and on the last day, flat n x n in name order.</summary>
    public required IReadOnlyList<(int Day, double[] Regard)> RegardSnapshots { get; init; }
    /// <summary>Each person's power of acting at the end of each day.</summary>
    public required IReadOnlyDictionary<string, double[]> PowerByDay { get; init; }
    /// <summary>The sentiments held at the end.</summary>
    public required IReadOnlyList<Sentiment> Sentiments { get; init; }
    /// <summary>Every feeling event: each regard change, and each change of mood.</summary>
    public required IReadOnlyList<Felt> Feelings { get; init; }
    /// <summary>New feuds, feuds inside a family, friendships, and reconciliations.</summary>
    public required IReadOnlyList<(int Day, string A, string B, string What)> Ties { get; init; }
    public required IReadOnlyList<(int Day, string Household, string From, string To)> ShopSwitches { get; init; }
    /// <summary>For each act aimed at someone chosen: the actor's regard for them as it began.</summary>
    public required IReadOnlyDictionary<int, double> AimedAt { get; init; }

    // Character (phase 0d): each person's temperament when the run began and when it ended.
    public required IReadOnlyDictionary<string, Temperament> CharactersAtStart { get; init; }
    public required IReadOnlyDictionary<string, Temperament> CharactersAtEnd { get; init; }

    // The desire gate (phase 0d; rule 10); empty when it is off.
    /// <summary>Every weighing of an act for a motive.</summary>
    public required IReadOnlyList<Pursuit> Pursuits { get; init; }
    public required IReadOnlyList<Stirring> Stirred { get; init; }
    /// <summary>The acts the gate started (motives acted on, and turning away).</summary>
    public required IReadOnlySet<int> Pursued { get; init; }
    public required IReadOnlyList<(int Tick, string Holder, string Subject, int Source, int Until)> Avoids { get; init; }
    public required int Withdrawals { get; init; }
    public required int Marks { get; init; }
    /// <summary>Each person's stance at the end of each day (-1 withdrawn, +1 combative).</summary>
    public required IReadOnlyDictionary<string, double[]> Stances { get; init; }
    /// <summary>Each life's events and how they turned out, for phase 0e (rule 18).</summary>
    public required IReadOnlyList<LifeEvent> LifeEvents { get; init; }
    /// <summary>With DesireActs off: the gate's lines, kept out of the log (and its hash).</summary>
    public required IReadOnlyList<string> MotiveLog { get; init; }
    /// <summary>Minutes each person spent awake, not at work and away from home, by season (the gate on).</summary>
    public required IReadOnlyDictionary<string, int[]> OutMinutes { get; init; }

    // Hermits, brawlers and moods that spread (phase 0d.6); empty when the gate is off.
    /// <summary>Each person's days: kindness received, company, kindness unanswered, free time out,
    /// and being left out (spec X0).</summary>
    public required IReadOnlyDictionary<string, PersonDays> Daily { get; init; }
    /// <summary>The mood each person passed on to others and took from them by contagion over the run
    /// (X3): in all, either way; and what they took, signed (Net).</summary>
    public required IReadOnlyDictionary<string, (double Gave, double Caught, double Net)> Contagion { get; init; }
    /// <summary>What each 0d.6 rule did (watching: would have done), how often and in sum (X13).</summary>
    public required IReadOnlyDictionary<string, (int Count, double Sum)> Rules { get; init; }
    /// <summary>Familiarity at the end, for every ordered pair (town spec E6: how forgetting leaves the town).</summary>
    public IReadOnlyDictionary<(string From, string To), double> Familiarity { get; init; } = new Dictionary<(string, string), double>();
    /// <summary>Each spell a pair spent in a feud (acts spec 7.3: how long feuds last); empty with feelings off.</summary>
    public IReadOnlyList<FeudSpell> FeudSpells { get; init; } = Array.Empty<FeudSpell>();
    /// <summary>The act catalog's watch record (acts spec 2.3): the catalog rows the gate would have
    /// started, had watch been off. Empty unless <see cref="ActOptions.Watch"/> is on.</summary>
    public IReadOnlyList<CatalogWatched> CatalogWatch { get; init; } = Array.Empty<CatalogWatched>();
    /// <summary>Batch 2's reach checks (spec 5.4): each scandal's and each scenario act's circle, the
    /// people who knew its actor at familiarity 0.2 or more as its day began, by act id.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> Circles { get; init; } = new Dictionary<int, IReadOnlyList<string>>();
    /// <summary>The acts scenarios placed (spec 5.2), by act id, with the scenario's name.</summary>
    public IReadOnlyDictionary<int, string> Scenarios { get; init; } = new Dictionary<int, string>();
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
        public string? HubSeen; // the gathering whose crowd they looked over on arriving (town spec E4)
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
    // Indexes over the beliefs, kept by Add, the one writer (they never change what the rules do;
    // they spare scanning everything a person has ever known): the stories each person might still
    // volunteer, which fade out of reach and are dropped; and each person's scandal beliefs, in act order.
    private readonly Dictionary<string, HashSet<int>> _tellable = new();
    private readonly Dictionary<string, SortedSet<int>> _scandalBeliefs = new();
    private readonly Dictionary<string, ActKind> _kindByName = new();
    // The scandal acts, in act order, taken from the act list as it grows (CheckScandals).
    private readonly List<int> _scandalActs = new();
    private int _actsScanned;
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
    private readonly Dictionary<string, List<(string Next, Tile Exit, Tile Entry)>> _doors = new();
    private readonly Dictionary<(string, Tile), int[,]> _fields = new();

    /// <param name="scheduled">Acts to place: at or after the minute, the first time the actor is
    /// awake, free and somewhere the act is allowed (within 3 days, else dropped). The actor
    /// <see cref="Harness.Anyone"/> means whoever is first able. Marked injected.</param>
    /// <param name="links">Doors between places; defaults to the town's when the places are the town's.</param>
    /// <param name="gatherings">Hubs; default to the town's when the places are the town's.</param>
    /// <param name="authority">The mayor, keepers, constable and ladder; default to the town's when
    /// the places are the town's, else no authority.</param>
    /// <param name="feelings">Feelings (phase 0c); default to the town's when the places are the
    /// town's, else off, so worlds built by tests feel nothing unless they ask.</param>
    public Simulation(long seed, IReadOnlyList<Villager>? cast = null, IReadOnlyList<Location>? places = null,
        IReadOnlyList<ActKind>? kinds = null, PerceptionOptions? perception = null, GossipOptions? gossip = null,
        IReadOnlyList<(int Tick, string Actor, string Kind)>? scheduled = null, int wander = 2,
        IReadOnlyList<Link>? links = null, BodyOptions? body = null, IReadOnlyList<Gathering>? gatherings = null,
        AuthorityOptions? authority = null, HabitOptions? habits = null, Economy? economy = null, MoneyOptions? money = null,
        FeelingOptions? feelings = null)
    {
        _fo = feelings ?? (places is null ? DefaultTown.Feelings() : FeelingOptions.Off);
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
        foreach (ActKind k in _kinds)
            _kindByName.TryAdd(k.Name, k); // the first of a name, as a search of the list would find
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
        {
            _beliefs[n] = new Dictionary<int, Belief>();
            _tellable[n] = new HashSet<int>();
            _scandalBeliefs[n] = new SortedSet<int>();
        }
        foreach (Link l in links ?? (places is null ? DefaultTown.Links() : Array.Empty<Link>()))
        {
            Door(l.A, l.B, l.DoorA, l.DoorB);
            Door(l.B, l.A, l.DoorB, l.DoorA);
        }
        foreach (var list in _doors.Values)
            list.Sort((x, y) => StringComparer.Ordinal.Compare(x.Next, y.Next));
        _people = _cast.Select(v => new Person { V = v }).ToArray();
        StartCharacter();
        StartFeelings();
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
        _charactersAtStart = Characters();
        StartDesire(days);
        StartMoney();
        if (_fo.Enabled)
            foreach (string n in _names)
                _powerByDay[n] = new double[days];
        for (int m = 0; m < days * Clock.MinutesPerDay; m++)
        {
            _now = m;
            if (Clock.OfDay(m) == 0)
                KeepDayStart(); // batch 2's reach checks: who knew whom as the day began
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
            Names = _names,
            Regard = _fo.Enabled ? Pairs(_regard) : new Dictionary<(string, string), double>(),
            Baseline = _fo.Enabled ? Pairs(_baseline) : new Dictionary<(string, string), double>(),
            KindRegard = _fo.Enabled
                ? _names.SelectMany((n, i) => _kindNames.Select((k, j) => (Key: (n, k), V: _kind[i, j]))).ToDictionary(x => x.Key, x => x.V)
                : new Dictionary<(string, string), double>(),
            RegardSnapshots = _snapshots,
            PowerByDay = _powerByDay,
            Sentiments = _sentiments.Values.OrderBy(x => x.Holder, StringComparer.Ordinal).ThenBy(x => x.Toward, StringComparer.Ordinal)
                .ThenBy(x => x.Name, StringComparer.Ordinal).ToList(),
            Feelings = _feltLog,
            Ties = _ties,
            ShopSwitches = _shopSwitches,
            AimedAt = _aimedAt,
            CharactersAtStart = _charactersAtStart,
            CharactersAtEnd = Characters(),
            Pursuits = _pursuits,
            Stirred = _stirred,
            Pursued = _pursuedActs,
            Avoids = _avoids,
            Withdrawals = _withdrawals,
            Marks = _marks,
            Stances = _stances,
            LifeEvents = LifeEvents(),
            MotiveLog = _motiveLog,
            OutMinutes = _outMinutes,
            Daily = Daily(),
            Contagion = ContagionTotals(),
            Rules = _rules,
            Familiarity = Pairs(_fam),
            FeudSpells = FeudSpells(),
            CatalogWatch = CatalogWatch(),
            Circles = _circles,
            Scenarios = _scenarioActs,
        };
    }

    private Dictionary<(string, string), double> Pairs(double[,] values)
    {
        var d = new Dictionary<(string, string), double>();
        for (int i = 0; i < _names.Length; i++)
            for (int j = 0; j < _names.Length; j++)
                if (i != j)
                    d[(_names[i], _names[j])] = values[i, j];
        return d;
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
            StartPerHead(m); // the act catalog's per-head draws (acts spec 3.2); none without Company
            if (HasMoney)
            {
                Temptation(m);
                Drinks(m);
            }
            Pursue(m);   // the desire gate (rule 10)
            Withdraw(m);
            CountOut(m);
        }
        StartScheduled(m);
        StartScenarios(m); // batch 2's scenario harness; nothing without a scenario
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
        Arrive(p, m);
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
            int a = d * Clock.MinutesPerDay + job.Start - _bo.MorningMinutes - ((job.Commute ?? _bo.CommuteMinutes) - _bo.CommuteMinutes);
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
        if (p.V.Job is { } job && job.WorksOn(Clock.Weekday(m)) && t >= job.Start - (job.Commute ?? _bo.CommuteMinutes) && t < job.End)
        {
            Goal(p, job.Place, job.Spot, m - t + job.End, "work", null);
            return;
        }
        if (Patrol(p, m))
            return;
        if (p.Why is "haunt" or "home" && p.GoalUntil > m && (p.Haunt is null || p.Haunt.Open(t)))
            return;

        var options = p.V.Haunts.Where(h => h.Open(t)).Select(h => (Haunt: (Haunt?)h, h.Weight)).ToList();
        foreach (Gathering g in _gatherings.Where(g => g.On(m) && Admits(g, p, m)))
        {
            // A spot in the crowd, the same for this person all through the gathering.
            int day = Clock.Day(m);
            var spot = new Tile(g.Center.X + Rng.Range(_seed, -g.Radius, g.Radius, "crowd-x", g.Name, p.V.Name, day.ToString()),
                                g.Center.Y + Rng.Range(_seed, -g.Radius, g.Radius, "crowd-y", g.Name, p.V.Name, day.ToString()));
            if (!_places[g.Place].Walkable(spot))
                spot = g.Center;
            int to = g.To;
            options.Add((new Haunt(g.Place, spot, g.From, to, g.Weight, g.Name), HubWeight(g, p)));
        }
        if (hasHome)
            options.Add((null, HomeWeight(p, m)));
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
                if (Route(p.Place, p.GoalPlace, p.At) is not { } hop)
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

    // ---- acts and witnessing ---------------------------------------------------------------

    private bool Free(Person p, int m) => !p.Asleep && !p.PendingCollapse && p.BusyUntil < m && p.DetainedUntil <= m;

    private void StartActs(int m)
    {
        double ticksPerDay = Clock.MinutesPerDay / (double)Clock.TickMinutes;
        foreach (ActKind kind in _kinds)
        {
            if (kind.PerHead)
                continue; // drawn per head instead (StartPerHead)
            double perDay = Acting && kind.Affect is { Target: TargetIs.Chosen } && !IsLight(kind) ? kind.PerDay * _fo.AimedRateScale : kind.PerDay;
            if (perDay <= 0 || Rng.Unit(_seed, "act", kind.Name, m.ToString()) >= perDay / ticksPerDay)
                continue;
            var candidates = _people
                .Where(p => Free(p, m) && p.V.Acts.TryGetValue(kind.Name, out double w) && w > 0 && kind.FitsAge(p.V.Age))
                .Where(p => kind.Allowed.Count == 0 || kind.Allowed.Contains(p.Place))
                .Where(p => kind.WithKin is not { } role || _people.Any(o => o != p && !o.Asleep && p.V.KinOf(o.V.Name) == role
                                                                             && o.Place == p.Place && o.At.Chebyshev(p.At) <= _po.FarTiles))
                .Where(p => HasTarget(kind, p, m)) // S1: an act aimed at someone needs someone in reach
                // S2 (law 1): the joyful give and help more, the sad drink more.
                .Select(p => (P: p, W: Steering && kind.Affect is { Tilt: not 0 } a
                    ? p.V.Acts[kind.Name] * Feelings.PowerFactor(a.Tilt, PowerOf(_index[p.V.Name]), _fo)
                    : p.V.Acts[kind.Name]))
                .Where(x => x.W > 0)
                .ToList();
            if (candidates.Count == 0)
                continue;
            double r = Rng.Unit(_seed, "actor", kind.Name, m.ToString()) * candidates.Sum(x => x.W);
            Person actor = candidates[^1].P;
            foreach (var (p, w) in candidates)
            {
                r -= w;
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
            bool Able(Person p) => Free(p, m) && kind.FitsAge(p.V.Age) && (kind.Allowed.Count == 0 || kind.Allowed.Contains(p.Place))
                                   && HasTarget(kind, p, m);
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

    /// <param name="target">The other party, when the caller gives it (the questioner, the mayor,
    /// the parent); else found from the act's feeling row. Only while feelings are on.</param>
    /// <param name="about">The act a consequence answers.</param>
    /// <param name="with">A third person the act is for (the act catalog); only while feelings are on.</param>
    private void Begin(int m, ActKind kind, Person actor, bool injected, string? target = null, int about = -1, string? with = null)
    {
        if (!_fo.Enabled)
            (target, about, with) = (null, -1, null);
        else if (target is null && kind.Affect is { } row)
            target = TargetFor(kind, row, actor, m);
        var act = new Act(_acts.Count, m, actor.V.Name, kind.Name, actor.Place, actor.At, injected, target, about, with);
        _acts.Add(act);
        RecordCircle(act, kind); // batch 2's reach checks: a scandal's circle
        _scenes[act.Id] = SceneOf(act, actor);
        Gains(act, actor);
        actor.BusyUntil = m + kind.DurationMinutes - 1;
        _watching[act.Id] = new Dictionary<string, List<double>>();
        _log.Add($"{m} act {act.Id} {act.Kind} by {act.Actor} at {act.Location}{(target is not null ? " to " + target : "")}");
        if (!_fo.Enabled)
            return;
        _did.Add((actor.V.Name, act.Id));
        BeganAct(act, kind, m);
        if (target is null || kind.Affect is not { Target: TargetIs.Chosen or TargetIs.Kin } aimed)
            return;
        if (aimed.Target == TargetIs.Chosen)
            _aimedAt[act.Id] = E(_index[actor.V.Name], _index[target]);
        if (!Steering)
            return;
        // S1: the other party takes part, for as long as the act lasts.
        Person other = _people[_index[target]];
        other.BusyUntil = Math.Max(other.BusyUntil, actor.BusyUntil);
        if (aimed.Target == TargetIs.Chosen)
            Why(m, actor.V.Name, kind.Name, target);
    }

    private ActKind KindOf(Act a) => _kindByName[a.Kind];

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
        foreach (int id in _watching.Keys.Order().ToList()) // the acts in progress, in act order
        {
            Act act = _acts[id];
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
        foreach (int id in _watching.Keys.Order().ToList()) // the acts in progress, in act order
        {
            Act act = _acts[id];
            ActKind kind = KindOf(act);
            if (act.Tick + kind.DurationMinutes - 1 > m)
                continue;
            int witnesses = 0;
            var watchers = _watching[act.Id];
            string? participant = null;
            if (Steering && act.Target is { } other && kind.Affect is { Target: TargetIs.Chosen or TargetIs.Kin }
                && _people[_index[other]] is { Asleep: false } op && op.Place == act.Location)
            {
                participant = other; // S1: they took part, so they know what happened and who did it
                watchers.TryAdd(other, new List<double>());
            }
            foreach (var (observer, instants) in watchers.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                double clarity = observer == participant ? 1 : Perception.OfAct(instants, kind.ReadMinutes);
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
                else if (Perception.GuessesConfidently(CharacterOf(_index[observer])))
                {
                    actor = Guess(observer, act);
                    confidence = 0.8;
                }
                string? target = null, seenKind = null;
                if (_fo.Enabled)
                {
                    // F3: who it was done to, as this witness knows it; the kind of person they saw.
                    if (act.Target is { } tg && (kind.Affect?.Target == TargetIs.Keeper || observer == tg
                                                 || Perception.Identifies(clarity, Familiarity(observer, tg), _po)))
                        target = tg;
                    if (actor is null)
                        seenKind = _cast[_index[act.Actor]].Kind;
                }
                Add(observer, new Belief(act.Id, act.Kind, actor, confidence, clarity, Source.Witnessed,
                    kind.Juiciness, m, Array.Empty<string>(), Target: target, SeenKind: seenKind), m);
            }
            _witnesses[act.Id] = witnesses;
            _watching.Remove(act.Id);
            LeaveTrace(act, kind, m);
            Undergo(act, kind, m);
            StirPity(act, kind, m);
            Mark(act, kind, m);
            AfterAct(act, kind, m); // the act catalog: a game's company, a drink's bill, an apology's answer
            RemorseAfter(act, kind, m); // the act catalog: remorse in the one who hurt (Repair)
            SidesAfter(act, kind, m); // the act catalog: witnesses take sides (Sides)
        }
    }

    /// <summary>A confident guess: someone awake and present, weighted by how well the observer knows them.</summary>
    private string Guess(string observer, Act act)
    {
        var present = _people.Where(p => p.V.Name != observer && !p.Asleep && p.Place == act.Location)
            .Select(p => p.V.Name).ToList();
        // S6 (law 9; III P26, VERIFY): a guess falls more readily on someone disliked.
        double W(string n) => Steering
            ? Familiarity(observer, n) + 0.05 + _fo.GuessPerHate * Math.Max(0, -St(observer, n))
            : Familiarity(observer, n) + 0.05;
        double total = present.Sum(W);
        double r = Rng.Unit(_seed, "guess", observer, act.Id.ToString()) * total;
        foreach (string n in present)
        {
            r -= W(n);
            if (r < 0)
                return n;
        }
        return present.Count > 0 ? present[^1] : act.Actor;
    }

    /// <param name="teller">Who told it, when it came in a telling.</param>
    private void Add(string who, Belief b, int m, string? teller = null)
    {
        _beliefs[who].TryGetValue(b.ActId, out Belief? prior);
        _beliefs[who][b.ActId] = b;
        _tellable[who].Add(b.ActId);
        if (KindOf(_acts[b.ActId]).IsScandal)
            _scandalBeliefs[who].Add(b.ActId);
        _log.Add($"{m} belief {who} {b.ActId} {b.Actor ?? "someone"} {b.Source} {b.Juiciness:0.###}");
        OwnAccount(who, b, m);
        CurfewBroken(who, b, m);
        if (!_fo.Enabled)
            return;
        Feel(who, b, prior, teller, m);
        Answered(who, b, m);
        StirFrom(who, b, prior, m);
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
                _fam[i, j] += _go.FamiliarityGrowthPerHour * hours * (1 - _fam[i, j]) * Meeting(i, j, m);
                _fam[j, i] += _go.FamiliarityGrowthPerHour * hours * (1 - _fam[j, i]) * Meeting(j, i, m);
                if (_fo.Enabled)
                    _together[i, j] += Clock.TickMinutes;
                if (!_spans.TryGetValue(key, out int start))
                    _spans[key] = start = m;
                int window = start + (m - start) / Math.Max(1, _go.ChatEveryMinutes);
                if (m - start < _go.ChatMinMinutes || _chatted.Contains((a, b, window)))
                    continue;
                double chattiness = (ChatInUse(i) + ChatInUse(j)) / 2; // 0d.6 (X6): the withdrawn talk less
                double per10 = Math.Min(1, _go.ChatChance * (0.5 + chattiness));
                double chance = 1 - Math.Pow(1 - per10, Clock.TickMinutes / 10.0);
                if (Rng.Unit(_seed, "chat", a, b, m.ToString()) >= chance)
                    continue;
                _chatted.Add((a, b, window));
                Company(pa, pb);
                Tone(i, j, m);
                Tone(j, i, m);
                TryTell(a, b, m);
                TryTell(b, a, m);
            }
        }
    }

    /// <summary>How juicy a belief still is: its value when got, less the fade per whole 24 hours since.</summary>
    public double Current(Belief b, int m)
    {
        int days = Math.Max(0, (m - b.GotTick) / Clock.MinutesPerDay);
        double baseJ = _kindByName[b.Kind].Juiciness;
        double fade = baseJ >= 4 ? _go.ScandalFadePerDay : _go.FadePerDay;
        return Math.Max(0, b.Juiciness - fade * days);
    }

    private void TryTell(string teller, string listener, int m)
    {
        int day = Clock.Day(m);
        Belief? best = null;
        double bestScore = 0;
        // Only stories that could still reach the bar: the best is chosen by score, then the latest
        // act, so the order they are looked at in does not matter. A story's juiciness only fades,
        // so one that can no longer reach the bar with the bonus never will (until it is told again).
        List<int>? spent = null;
        foreach (int id in _tellable[teller])
        {
            Belief b = _beliefs[teller][id];
            if (Current(b, m) + Math.Max(0, _go.KnowsBonus) < _go.VolunteerLevel)
            {
                (spent ??= new List<int>()).Add(id);
                continue;
            }
            if (b.Actor == listener || b.Suspects?.Contains(listener) == true || b.Chain.Count > 0 && b.Chain[0] == listener)
                continue; // never tell people about themselves (or that they're suspected), or back to who told you
            if (b.Actor is { } culprit && AreKin(teller, culprit))
                continue; // families cover: no stories that hurt kin (rule 17)
            if (_told.Contains((teller, listener, b.ActId)))
                continue;
            if (_tellsToday.TryGetValue((teller, b.ActId, day), out int n) && n >= TellsPerDay)
                continue;
            string? named = b.Actor ?? b.Suspects?.FirstOrDefault();
            // S3 (rule 8): a story is worth more to someone who knows the person well, or loves them.
            double score = Current(b, m)
                + (named is { } who && (Familiarity(listener, who) >= _go.KnowsAt || Steering && St(listener, who) >= _fo.CloseTieAt)
                    ? _go.KnowsBonus : 0);
            if (score < _go.VolunteerLevel)
                continue;
            if (best is null || score > bestScore || score == bestScore && b.ActId > best.ActId)
            {
                best = b;
                bestScore = score;
            }
        }
        if (spent is not null)
            _tellable[teller].ExceptWith(spent);
        if (best is null)
            return;

        _told.Add((teller, listener, best.ActId));
        _tellsToday[(teller, best.ActId, day)] = _tellsToday.GetValueOrDefault((teller, best.ActId, day)) + 1;
        double confidence = best.Confidence * (0.5 + 0.5 * Familiarity(listener, teller));
        var told = new Belief(best.ActId, best.Kind, best.Actor, confidence, best.Clarity, Source.Told,
            _go.RetellFactor * Current(best, m), m, new[] { teller }.Concat(best.Chain).ToList(),
            best.Actor is null ? WithoutKin(teller, best.Suspects) : null, best.Target, best.SeenKind);
        _log.Add($"{m} told {teller} {listener} {best.ActId}");
        if (_acts[best.ActId].Actor == listener)
            return; // the culprit hears about their own act: they learn nothing they didn't know

        if (_beliefs[listener].TryGetValue(best.ActId, out Belief? held))
        {
            // F15: a second teller, independent of the first, names the same person (rule 9).
            bool agrees = held.Source == Source.Told && held.Actor is not null && told.Actor == held.Actor
                          && !told.Chain.Intersect(held.Chain).Any();
            // Already known: a name can fill in "someone", or replace a weaker name. Never more juice.
            if (told.Actor is not null && (held.Actor is null || told.Confidence > held.Confidence))
                Add(listener, held with { Actor = told.Actor, Confidence = told.Confidence, Target = held.Target ?? told.Target }, m, teller);
            else if (told.Actor is null && held.Actor is null && held.Suspects is not { Count: > 0 } && told.Suspects is { Count: > 0 })
                Add(listener, held with { Suspects = told.Suspects }, m, teller); // someone's suspicion fills in their "someone"
            if (agrees)
                Corroborate(listener, best.ActId, m);
            KinHears(listener, best.ActId, told.Chain, told.Actor, m);
            return;
        }
        Add(listener, told, m, teller);
        KinHears(listener, best.ActId, told.Chain, told.Actor, m);
    }

    // ---- scandal ---------------------------------------------------------------------------

    private void CheckScandals(int m)
    {
        for (; _actsScanned < _acts.Count; _actsScanned++)
            if (KindOf(_acts[_actsScanned]).IsScandal)
                _scandalActs.Add(_actsScanned);
        foreach (int id in _scandalActs)
        {
            Act act = _acts[id];
            if (_confronted.Contains(act.Id) || _watching.ContainsKey(act.Id))
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
                string? by = Steering
                    // S7 (rule 9; III P25, VERIFY): nobody confronts someone they love; the harmed,
                    // who feel it most, are the likeliest to.
                    ? members.Where(n => St(n, target) < _fo.CoverAt && !InHostileCooldown(_index[n], _index[target], m))
                        .OrderByDescending(n => CharacterOf(_index[n]).Boldness * Current(_beliefs[n][act.Id], m)
                                                * (1 + _fo.ConfrontPerHate * Math.Max(0, -St(n, target))))
                        .ThenBy(n => n, StringComparer.Ordinal).FirstOrDefault()
                    : members
                        .OrderByDescending(n => CharacterOf(_index[n]).Boldness * Current(_beliefs[n][act.Id], m))
                        .ThenBy(n => n, StringComparer.Ordinal).First();
                if (by is null)
                    continue;
                var c = new Confrontation(act.Id, m, by, target, target == act.Actor);
                _confrontations.Add(c);
                _confronted.Add(act.Id);
                if (Desiring)
                    _lastHostile[(_index[by], _index[target])] = m;
                _log.Add($"{m} confront {by} {target} {act.Id} {(c.Correct ? "right" : "wrong")}");
                Accused(target, act.Id, new[] { by }, _fo.ConfrontJoy, "confronted", m);
                break;
            }
        }
    }

    private void CloseDay(int day, int days)
    {
        ForgetSightings((day + 1) * Clock.MinutesPerDay);
        CloseMoneyDay();
        CloseDesires(day);
        CloseWithdrawal(day); // 0d.6: before CloseFeelings clears the day's time together
        CloseFeelings(day, days);
        Forget(day);
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
