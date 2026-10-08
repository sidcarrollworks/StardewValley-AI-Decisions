using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnderGlass.Sim;

/// <summary>What to record: a seed of a town (the default one unless given), for how many days,
/// with which feelings, whether a scandal is placed (as the runner's --inject), and any traits set
/// before the run.</summary>
public sealed record ReplayOptions
{
    public long Seed { get; init; } = 1;
    public int Days { get; init; } = 28;
    /// <summary>The town to run; the default town when null.</summary>
    public TownData? Town { get; init; }
    /// <summary>Feelings in place of the town's own; the town's when null.</summary>
    public FeelingOptions? Feelings { get; init; }
    public bool Inject { get; init; }
    public IReadOnlyList<(string Who, Trait Trait, double Value)> Traits { get; init; } = Array.Empty<(string, Trait, double)>();
    /// <summary>A note for the viewer's header, such as the flags the run was made with.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// A run of the default town recorded for the viewer (sim/viewer/index.html): who the people are,
/// the places, every act, where everyone was at each tick, mood and power by the hour, regard and
/// stance by the day, ties, who came to believe what and who told whom, regard changes and their
/// causes, the gate's weighings, the life record, the authority's cases, and the event log (with
/// the gate's lines when it only watches). Recording only reads the simulation: the run is the
/// same run, with the same log hash, as without it. People are indexes into "names" (the cast in
/// name order), places into "places", and the acts' kinds into "kinds"; the gate's weighings, the
/// life record and each person's own acts name their act kinds instead. Times are game minutes
/// from midnight of day 0. Real numbers are rounded to three places; regard is stored as integers
/// in thousandths, and mood and power in hundredths. Movements are packed per person (see
/// <see cref="Pack"/>). Settings that are not finite numbers (a threshold set to Infinity to turn
/// something off) are written as the strings "Infinity", "-Infinity" and "NaN".
/// </summary>
public static class Replay
{
    /// <summary>Raised when the file's shape changes, so the viewer can tell an old file.</summary>
    public const int Version = 1;

    private static readonly JsonSerializerOptions Options = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public static string Json(ReplayOptions o) => JsonSerializer.Serialize(Record(o), Options);

    public static Dictionary<string, object?> Record(ReplayOptions o)
    {
        TownData given = o.Town ?? TownData.Default();
        FeelingOptions feelings = o.Feelings ?? given.Feelings;
        // The act catalog's rows and cards for the slices that are on, added as the runner adds them
        // (acts spec 2.4); none yet in acts-0, so this is the town given.
        TownData town = given with { Acts = ActCatalog.Kinds(feelings.Acts, given.Acts), Cast = ActCatalog.Cards(given.Cast, feelings.Acts) };
        IReadOnlyList<Villager> cast = town.Cast;
        IReadOnlyList<Location> places = town.Places;
        IReadOnlyList<Link> links = town.Links;
        IReadOnlyList<Gathering> hubs = town.Gatherings;
        IReadOnlyList<ActKind> kinds = town.Acts;
        var scheduled = o.Inject ? new[] { Harness.ScandalFor(o.Seed, kinds) } : null;
        var sim = new Simulation(o.Seed, town with { Feelings = feelings }, scheduled);
        foreach (var (who, trait, value) in o.Traits)
            sim.SetTrait(who, trait, value);

        string[] names = cast.Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var person = names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
        var place = places.Select((p, i) => (p.Name, i)).ToDictionary(x => x.Name, x => x.i);
        var kind = kinds.Select((k, i) => (k.Name, i)).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().i);
        int P(string? n) => n is not null && person.TryGetValue(n, out int i) ? i : -1;

        // Live samples: where everyone is at each tick (a row only when it changes), mood and
        // power each hour, and regard at the end of each day.
        var moves = names.Select(_ => new List<byte>()).ToArray();
        var lastTick = new int[names.Length];
        var last = new (int Place, int X, int Y, bool Asleep)[names.Length];
        for (int i = 0; i < last.Length; i++)
            last[i] = (-1, 0, 0, false);
        var mood = new List<int[]>();
        var power = new List<int[]>();
        var regard = new List<int[]>();
        int n = names.Length;
        SimResult r = sim.Run(o.Days, (m, s) =>
        {
            if (m % Clock.TickMinutes == 0)
                for (int i = 0; i < n; i++)
                {
                    var (where, at, asleep, _) = s.Where(names[i]);
                    var now = (place[where], at.X, at.Y, asleep);
                    if (now == last[i])
                        continue;
                    last[i] = now;
                    Pack(moves[i], m / Clock.TickMinutes - lastTick[i], now.Item1, at.X, at.Y, asleep);
                    lastTick[i] = m / Clock.TickMinutes;
                }
            if (feelings.Enabled && m % 60 == 59)
            {
                mood.Add(names.Select(x => (int)Math.Round(s.Mood(x) * 100)).ToArray());
                power.Add(names.Select(x => (int)Math.Round(s.Power(x) * 100)).ToArray());
            }
            if (feelings.Enabled && Clock.OfDay(m) == Clock.MinutesPerDay - 1)
            {
                var flat = new int[n * n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        flat[i * n + j] = i == j ? 0 : (int)Math.Round(s.PersonalRegard(names[i], names[j]) * 1000);
                regard.Add(flat);
            }
        });

        // The log: beliefs and tellings become tables of their own; every other line is kept. A
        // tie is logged as it is made, in the order of r.Ties, which holds only its day: the line
        // gives its minute (23:59 for feuds and friendships, the moment for a reconciliation).
        var beliefs = new List<int[]>();
        var tellings = new List<int[]>();
        var events = new List<object[]>();
        var tieTicks = new List<int>();
        foreach (string line in r.Log)
        {
            int space = line.IndexOf(' ');
            if (space < 0 || !int.TryParse(line.AsSpan(0, space), out int tick))
                continue;
            string rest = line[(space + 1)..];
            string[] w = rest.Split(' ');
            if (w[0] == "belief" && w.Length >= 5 && int.TryParse(w[2], out int bAct))
                beliefs.Add(new[] { tick, P(w[1]), bAct, w[3] == "someone" ? -1 : P(w[3]), (int)Enum.Parse<Source>(w[4]) });
            else if (w[0] == "told" && w.Length >= 4 && int.TryParse(w[3], out int tAct))
                tellings.Add(new[] { tick, P(w[1]), P(w[2]), tAct });
            else
            {
                if (w[0] is "tie" or "reconciled")
                    tieTicks.Add(tick);
                events.Add(new object[] { tick, rest });
            }
        }
        // With the gate only watching (DesireActs off), its lines are kept out of the log and its
        // hash, in r.MotiveLog; the viewer shows them with the rest, in time order.
        foreach (string line in r.MotiveLog)
        {
            int space = line.IndexOf(' ');
            if (space > 0 && int.TryParse(line.AsSpan(0, space), out int tick))
                events.Add(new object[] { tick, line[(space + 1)..] });
        }
        if (r.MotiveLog.Count > 0)
            events = events.OrderBy(e => (int)e[0]).ToList(); // stable: the log's own order holds within a minute
        int TieTick(int k) => tieTicks.Count == r.Ties.Count ? tieTicks[k] : r.Ties[k].Day * Clock.MinutesPerDay + Clock.MinutesPerDay - 1;

        // Every trait in the order of "traitNames", so a trait added to Trait is recorded with no change here.
        int[] Traits(Temperament t) => Enum.GetValues<Trait>().Select(x => (int)Math.Round(Simulation.Get(t, x) * 100)).ToArray();
        var byName = cast.ToDictionary(v => v.Name);
        string[] routes = r.Feelings.Select(f => f.Route).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var route = routes.Select((x, i) => (x, i)).ToDictionary(p => p.x, p => p.i);
        // Regard can be aimed at a kind of person ("kind:<Kind>", the Kind and Spill routes and
        // the reattributions that undo them), kept apart from regard between two people.
        const string KindMark = "kind:";
        string[] personKinds = cast.Select(v => v.Kind).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var personKind = personKinds.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i);
        int K(string? toward) => toward is not null && toward.StartsWith(KindMark, StringComparison.Ordinal)
            && personKind.TryGetValue(toward[KindMark.Length..], out int i) ? i : -1;

        return new Dictionary<string, object?>
        {
            ["format"] = "under-glass-run",
            ["version"] = Version,
            ["seed"] = o.Seed,
            ["days"] = o.Days,
            ["label"] = o.Label,
            ["hash"] = Metrics.LogHash(r),
            ["clock"] = new { minutesPerDay = Clock.MinutesPerDay, tick = Clock.TickMinutes, daysPerWeek = Clock.DaysPerWeek, daysPerSeason = Clock.DaysPerSeason },
            ["settings"] = Settings(town, feelings, o),
            ["defaults"] = Scalars(TownData.Default(), DefaultTown.Feelings()),
            ["traitNames"] = Enum.GetNames<Trait>(),
            ["names"] = names,
            ["people"] = names.Select(x => byName[x]).Select(v => new
            {
                name = v.Name,
                household = v.Household,
                home = place.TryGetValue(v.Home, out int h) ? h : -1,
                kind = v.Kind,
                age = v.Age,
                stage = v.Stage.ToString(),
                job = v.Job is { } j ? new { place = P2(place, j.Place), start = j.Start, end = j.End, daysOff = j.DaysOff } : null,
                family = (v.Family ?? new Dictionary<string, Kin>()).OrderBy(f => f.Key, StringComparer.Ordinal)
                    .Select(f => new object[] { P(f.Key), f.Value.ToString() }).ToArray(),
                friends = v.Friends.Select(P).Where(i => i >= 0).ToArray(),
                acts = v.Acts.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => a.Key).ToArray(),
                character = Traits(r.CharactersAtStart.TryGetValue(v.Name, out Temperament? c0) ? c0 : v.Temperament),
                characterEnd = Traits(r.CharactersAtEnd.TryGetValue(v.Name, out Temperament? c1) ? c1 : v.Temperament),
            }).ToArray(),
            ["places"] = places.Select(p => new
            {
                name = p.Name,
                outdoor = p.Outdoor,
                group = p.Name.StartsWith("Home:", StringComparison.Ordinal) ? "home" : p.Outdoor && p.Height <= 3 ? "road" : p.Outdoor ? "outdoors" : "indoors",
                rows = p.Rows,
            }).ToArray(),
            ["links"] = links.Select(l => new[] { place[l.A], l.DoorA.X, l.DoorA.Y, place[l.B], l.DoorB.X, l.DoorB.Y }).ToArray(),
            ["hubs"] = hubs.Select(g => new
            {
                name = g.Name, place = place[g.Place], x = g.Center.X, y = g.Center.Y, radius = g.Radius,
                from = g.From, to = g.To, weekdays = g.Weekdays, onlyDay = g.OnlyDay,
            }).ToArray(),
            ["kinds"] = kinds.Select(k => new
            {
                name = k.Name, juiciness = k.Juiciness, valence = k.Valence, tier = k.Tier.ToString(),
                duration = k.DurationMinutes, joy = k.Affect?.Joy, aimed = k.Affect?.Target.ToString(),
            }).ToArray(),
            ["acts"] = r.Acts.Select(a => new[]
            {
                a.Id, a.Tick, P(a.Actor), kind[a.Kind], place[a.Location], a.At.X, a.At.Y, P(a.Target), a.About,
                (a.Injected ? 1 : 0) | (r.Pursued.Contains(a.Id) ? 2 : 0),
                r.Witnesses.GetValueOrDefault(a.Id),
            }).ToArray(),
            ["moves"] = moves.Select(b => Convert.ToBase64String(b.ToArray())).ToArray(),
            ["mood"] = mood,
            ["power"] = power,
            ["regard"] = regard,
            ["baseline"] = feelings.Enabled
                ? names.SelectMany(a => names.Select(b => a == b ? 0 : (int)Math.Round(r.Baseline[(a, b)] * 1000))).ToArray()
                : Array.Empty<int>(),
            ["stance"] = r.Stances.Count == 0 ? null : names.Select(x => r.Stances[x].Select(R).ToArray()).ToArray(),
            ["minutesOut"] = r.OutMinutes.Count == 0 ? null : names.Select(x => r.OutMinutes[x]).ToArray(),
            ["withdrawal"] = Withdrawal(r, names, P),
            ["ties"] = r.Ties.Select((t, k) => new object[] { TieTick(k), P(t.A), P(t.B), t.What }).ToArray(),
            ["beliefs"] = beliefs,
            ["tellings"] = tellings,
            ["routes"] = routes,
            // Regard changes with a cause: [tick, holder, toward, act, route, change in thousandths].
            ["feelings"] = r.Feelings.Where(f => f.Change != 0 && P(f.Toward) >= 0)
                .Select(f => new[] { f.Tick, P(f.Holder), P(f.Toward), f.ActId, route[f.Route], (int)Math.Round(f.Change * 1000) }).ToArray(),
            // The same for regard toward a kind of person: [tick, holder, kind, act, route, change].
            ["personKinds"] = personKinds,
            ["kindFeelings"] = r.Feelings.Where(f => f.Change != 0 && K(f.Toward) >= 0)
                .Select(f => new[] { f.Tick, P(f.Holder), K(f.Toward), f.ActId, route[f.Route], (int)Math.Round(f.Change * 1000) }).ToArray(),
            // [holder, toward (-1 for a kind), name, act, strength, since, count, kind (-1 for a person)].
            ["sentiments"] = r.Sentiments.Select(s => new object[] { P(s.Holder), P(s.Toward), s.Name, s.ActId, R(s.Strength), s.Since, s.Count, K(s.Toward) }).ToArray(),
            ["motives"] = Enum.GetNames<DesireKind>(),
            ["pursuits"] = r.Pursuits.Select(p => new object[]
            {
                p.Tick, P(p.Holder), P(p.Subject), (int)p.Motive, p.Source, p.ActKind, R(p.Intensity), R(p.Margin), p.Call, p.Acted ? 1 : 0, p.ActId,
            }).ToArray(),
            ["stirred"] = r.Stirred.Select(s => new object[] { s.Tick, P(s.Holder), P(s.Subject), (int)s.Motive, s.Source, R(s.Felt) }).ToArray(),
            ["roles"] = Enum.GetNames<LifeRole>(),
            ["outcomes"] = Enum.GetNames<Outcome>(),
            ["life"] = r.LifeEvents.Select(e => new object[]
            {
                e.Tick, P(e.Person), P(e.Other), e.ActId, e.Kind, (int)e.Role, R(e.Severity), e.Hostile ? 1 : 0, e.Light ? 1 : 0, (int)e.Outcome, e.ResolvedTick,
            }).ToArray(),
            ["constable"] = P(r.Constable),
            ["accounts"] = r.Accounts.Select(a => new object[] { a.Tick, a.ActId, P(a.From), P(a.Actor), R(a.Confidence), a.FirstHand ? 1 : 0 }).ToArray(),
            ["verdicts"] = r.Verdicts.Select(v => new object[] { v.Tick, v.ActId, P(v.By), P(v.Accused), v.Correct ? 1 : 0, v.Step.ToString(), v.LetOff ? 1 : 0 }).ToArray(),
            ["confrontations"] = r.Confrontations.Select(c => new object[] { c.Tick, c.ActId, P(c.By), P(c.Target), c.Correct ? 1 : 0 }).ToArray(),
            ["townCash"] = r.TownCash.Select(x => Math.Round(x, 2)).ToArray(),
            ["events"] = events,
        };
    }

    private static int P2(Dictionary<string, int> place, string name) => place.TryGetValue(name, out int i) ? i : -1;

    /// <summary>One movement, appended to a person's stream (stored base64): the ticks since their
    /// last movement as a varint (7 bits a byte, low first, the high bit set on all but the last;
    /// the first is counted from tick 0), then a byte of place index with 128 added when asleep,
    /// then x and y as a byte each. A row is written only when place, tile or sleep changes.</summary>
    public static void Pack(List<byte> into, int ticks, int place, int x, int y, bool asleep)
    {
        if (place is < 0 or > 127 || x is < 0 or > 255 || y is < 0 or > 255 || ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(place), "a place, tile or time the packed form can't hold");
        uint t = (uint)ticks;
        while (t >= 0x80)
        {
            into.Add((byte)(t | 0x80));
            t >>= 7;
        }
        into.Add((byte)t);
        into.Add((byte)(place | (asleep ? 0x80 : 0)));
        into.Add((byte)x);
        into.Add((byte)y);
    }

    /// <summary>The reverse of <see cref="Pack"/>, for tests: (tick, place, x, y, asleep) rows.</summary>
    public static List<(int Tick, int Place, int X, int Y, bool Asleep)> Unpack(byte[] stream)
    {
        var rows = new List<(int, int, int, int, bool)>();
        int i = 0, tick = 0;
        while (i < stream.Length)
        {
            int dt = 0, shift = 0;
            byte b;
            do
            {
                b = stream[i++];
                dt |= (b & 0x7f) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            tick += dt;
            byte pl = stream[i++];
            rows.Add((tick * Clock.TickMinutes, pl & 0x7f, stream[i++], stream[i++], (pl & 0x80) != 0));
        }
        return rows;
    }

    private static double R(double x) => Math.Round(x, 3);

    /// <summary>Hermits, brawlers and being left out (phase 0d.6), while the gate runs: each person's
    /// being left out (E) at each day's end in hundredths; every sustained spell (28 nights or more
    /// at -0.5 or below, a hermit if their free hours out fell too, or at +0.5 or above, a brawler),
    /// as WithdrawalMetrics finds them; the mood each person passed on and caught; and what each
    /// 0d.6 rule did (or, watching, would have done).</summary>
    private static object? Withdrawal(SimResult r, string[] names, Func<string?, int> person)
    {
        if (r.Daily.Count == 0)
            return null;
        var spells = WithdrawalMetrics.Spells(r, brawlers: false).Select(s => (s, Kind: s.Hermit ? "hermit" : "withdrawn"))
            .Concat(WithdrawalMetrics.Spells(r, brawlers: true).Select(s => (s, Kind: "brawler")))
            .OrderBy(x => x.s.From).ThenBy(x => x.s.Name, StringComparer.Ordinal).ThenBy(x => x.Kind, StringComparer.Ordinal);
        return new
        {
            leftOut = names.Select(x => r.Daily.TryGetValue(x, out PersonDays? d) ? d.LeftOut.Select(v => (int)Math.Round(v * 100)).ToArray() : Array.Empty<int>()).ToArray(),
            spells = spells.Select(x => new
            {
                person = person(x.s.Name),
                from = x.s.From,
                to = x.s.To,
                kind = x.Kind,
                ended = x.s.Ended,
                hoursFall = double.IsNaN(x.s.HoursFall) ? (double?)null : R(x.s.HoursFall),
            }).ToArray(),
            contagion = names.Select(x => r.Contagion.TryGetValue(x, out var c) ? new[] { R(c.Gave), R(c.Caught), R(c.Net) } : null).ToArray(),
            rules = r.Rules.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => new object[] { x.Value.Count, R(x.Value.Sum) }),
        };
    }

    /// <summary>Every switch and number the run used, and how it was set up.</summary>
    private static Dictionary<string, object?> Settings(TownData town, FeelingOptions f, ReplayOptions o)
    {
        var s = Scalars(town, f);
        s["Tensions"] = f.Start.OrderBy(t => t.Key.From, StringComparer.Ordinal).ThenBy(t => t.Key.To, StringComparer.Ordinal)
            .Select(t => new object[] { t.Key.From, t.Key.To, t.Value }).ToArray();
        s["Inject"] = o.Inject;
        s["Traits"] = o.Traits.Select(t => new object[] { t.Who, t.Trait.ToString(), t.Value }).ToArray();
        return s;
    }

    /// <summary>Every switch and number of a town's options, by name: the feelings' bare
    /// (<c>LoveAt</c>), the others with their class (<c>GossipOptions.ChatChance</c>), each class in
    /// name order. Written for the run and, as "defaults", for the shipped town, so the viewer can
    /// show which differ.</summary>
    private static Dictionary<string, object?> Scalars(TownData town, FeelingOptions f)
    {
        var s = new Dictionary<string, object?>();
        void Add(string prefix, object options)
        {
            foreach (var p in options.GetType().GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal))
                if (p.PropertyType == typeof(bool) || p.PropertyType == typeof(int) || p.PropertyType == typeof(double))
                    s[prefix + p.Name] = p.GetValue(options);
        }
        Add("", f);
        Add("ActOptions.", f.Acts);
        Add("AuthorityOptions.", town.Authority);
        Add("BodyOptions.", town.Body);
        Add("ForgettingOptions.", town.Gossip.Forgetting);
        Add("GossipOptions.", town.Gossip);
        Add("HabitOptions.", town.Habits);
        Add("MoneyOptions.", town.Money);
        Add("PerceptionOptions.", town.Perception);
        s["TownData.Wander"] = town.Wander;
        return s;
    }
}
