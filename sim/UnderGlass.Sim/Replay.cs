using System.Text.Json;

namespace UnderGlass.Sim;

/// <summary>What to record: a seed of the default town, for how many days, with which feelings,
/// whether a scandal is placed (as the runner's --inject), and any traits set before the run.</summary>
public sealed record ReplayOptions
{
    public long Seed { get; init; } = 1;
    public int Days { get; init; } = 28;
    /// <summary>The town's feelings when null (DefaultTown.Feelings).</summary>
    public FeelingOptions? Feelings { get; init; }
    public bool Inject { get; init; }
    public IReadOnlyList<(string Who, Trait Trait, double Value)> Traits { get; init; } = Array.Empty<(string, Trait, double)>();
    /// <summary>A note for the viewer's header, such as the flags the run was made with.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// A run of the default town recorded for the viewer (sim/viewer/index.html): who the people are,
/// the places, every act, where everyone was at each tick, mood by the hour, regard, power and
/// stance by the day, ties, who came to believe what and who told whom, regard changes and their
/// causes, the gate's weighings, the life record, the authority's cases, and the event log.
/// Recording only reads the simulation: the run is the same run, with the same log hash, as
/// without it. People are indexes into "names" (the cast in name order), places into "places",
/// act kinds into "kinds"; times are game minutes from midnight of day 0. Real numbers are
/// rounded to three places; regard is stored as integers in thousandths, and mood and power in
/// hundredths. Movements are packed per person (see <see cref="Pack"/>).
/// </summary>
public static class Replay
{
    /// <summary>Raised when the file's shape changes, so the viewer can tell an old file.</summary>
    public const int Version = 1;

    public static string Json(ReplayOptions o) => JsonSerializer.Serialize(Record(o));

    public static Dictionary<string, object?> Record(ReplayOptions o)
    {
        IReadOnlyList<Villager> cast = DefaultTown.Cast();
        IReadOnlyList<Location> places = DefaultTown.Locations();
        IReadOnlyList<Link> links = DefaultTown.Links();
        IReadOnlyList<Gathering> hubs = DefaultTown.Gatherings();
        IReadOnlyList<ActKind> kinds = DefaultTown.Acts();
        FeelingOptions feelings = o.Feelings ?? DefaultTown.Feelings();
        var scheduled = o.Inject ? new[] { Harness.ScandalFor(o.Seed, kinds) } : null;
        var sim = new Simulation(o.Seed, scheduled: scheduled, feelings: feelings);
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

        // The log: beliefs and tellings become tables of their own; every other line is kept.
        var beliefs = new List<int[]>();
        var tellings = new List<int[]>();
        var events = new List<object[]>();
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
                events.Add(new object[] { tick, rest });
        }

        int[] Traits(Temperament t) => new[]
        {
            (int)Math.Round(t.Chattiness * 100), (int)Math.Round(t.Boldness * 100), (int)Math.Round(t.Understanding * 100),
            (int)Math.Round(t.SelfRegard * 100), (int)Math.Round(t.Sensitivity * 100), (int)Math.Round(t.Retention * 100),
        };
        var byName = cast.ToDictionary(v => v.Name);
        string[] routes = r.Feelings.Select(f => f.Route).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var route = routes.Select((x, i) => (x, i)).ToDictionary(p => p.x, p => p.i);

        return new Dictionary<string, object?>
        {
            ["format"] = "under-glass-run",
            ["version"] = Version,
            ["seed"] = o.Seed,
            ["days"] = o.Days,
            ["label"] = o.Label,
            ["hash"] = Metrics.LogHash(r),
            ["clock"] = new { minutesPerDay = Clock.MinutesPerDay, tick = Clock.TickMinutes, daysPerWeek = Clock.DaysPerWeek, daysPerSeason = Clock.DaysPerSeason },
            ["settings"] = Settings(feelings, o),
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
            ["hoursOut"] = r.OutMinutes.Count == 0 ? null : names.Select(x => r.OutMinutes[x]).ToArray(),
            ["ties"] = r.Ties.Select(t => new object[] { t.Day, P(t.A), P(t.B), t.What }).ToArray(),
            ["beliefs"] = beliefs,
            ["tellings"] = tellings,
            ["routes"] = routes,
            // Regard changes with a cause: [tick, holder, toward, act, route, change in thousandths].
            ["feelings"] = r.Feelings.Where(f => f.Toward is not null && f.Change != 0)
                .Select(f => new[] { f.Tick, P(f.Holder), P(f.Toward), f.ActId, route[f.Route], (int)Math.Round(f.Change * 1000) }).ToArray(),
            ["sentiments"] = r.Sentiments.Select(s => new object[] { P(s.Holder), P(s.Toward), s.Name, s.ActId, R(s.Strength), s.Since, s.Count }).ToArray(),
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

    /// <summary>Every switch and number of the feelings the run used, and how it was set up.</summary>
    private static Dictionary<string, object?> Settings(FeelingOptions f, ReplayOptions o)
    {
        var s = new Dictionary<string, object?>();
        foreach (var p in typeof(FeelingOptions).GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal))
            if (p.PropertyType == typeof(bool) || p.PropertyType == typeof(int) || p.PropertyType == typeof(double))
                s[p.Name] = p.GetValue(f);
        s["Tensions"] = f.Start.OrderBy(t => t.Key.From, StringComparer.Ordinal).ThenBy(t => t.Key.To, StringComparer.Ordinal)
            .Select(t => new object[] { t.Key.From, t.Key.To, t.Value }).ToArray();
        s["Inject"] = o.Inject;
        s["Traits"] = o.Traits.Select(t => new object[] { t.Who, t.Trait.ToString(), t.Value }).ToArray();
        return s;
    }
}
