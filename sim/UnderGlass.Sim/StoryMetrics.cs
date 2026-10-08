namespace UnderGlass.Sim;

/// <summary>One spell a pair spent in a feud (acts spec 7.3): both held the other at
/// <see cref="FeelingOptions.FeudAt"/> or below from the night of From until the night of To, when
/// one of them rose above it (-1: still feuding at the run's end). Close: kin or housemates.
/// Seeded: the pair started the run in a feud (the starting tensions).</summary>
public sealed record FeudSpell(string A, string B, int From, int To, bool Close, bool Seeded)
{
    public bool Ended => To >= 0;

    /// <summary>The nights it was seen: From up to To, or through the run's last night (runDays - 1)
    /// if it never ended.</summary>
    public int Days(int runDays) => (Ended ? To : runDays) - From;
}

/// <summary>A thread (acts spec 7.3; design B's story-first gate): acts linked by
/// <see cref="Act.About"/>, which names the act a gate act answers (its motive's source) and the
/// scandal a consequence is for, in a chain three or more acts deep, touching two or more
/// households. Root: its first act. Acts: how many acts it holds. Depth: its longest chain. Day: the
/// day its third link landed. Shape: the kinds along its first chain three deep.</summary>
public sealed record StoryThread(int Root, int Acts, int Depth, int Households, int Day, string Shape);

/// <summary>One act kind's outcomes in the life record, across households, from the doer's side:
/// how many settled, and the share of each outcome.</summary>
public sealed record KindOutcomes(string Kind, int Settled, IReadOnlyDictionary<Outcome, double> Shares);

/// <summary>One person's year (acts spec 7.3, "per person"): acts done and undergone, in all and
/// by kind; the hurt taken and the kindness received (the life record's severities, summed; light
/// acts' kindness is warmth, not kindness); feuds and friendships (the variety measures' story
/// events); and warmth received (light kind acts, from slice acts-1 on).</summary>
public sealed record PersonStory(string Name, string Household, double Did, double Undergone,
    IReadOnlyDictionary<string, double> DidByKind, IReadOnlyDictionary<string, double> UndergoneByKind,
    double Hurt, double Kindness, double Feuds, double Friendships, double Warmth);

/// <summary>
/// The story measures of the act catalog (acts spec 7.2 and 7.3) over many runs. Every later slice
/// is measured against them on the same code. Per year means per 112 days; per person per
/// person-year. A share of nothing is NaN.
/// <list type="bullet">
/// <item>The life record, across households, from the doer's side: each kind's outcomes; the
/// Ignored share of kindnesses (light acts aside: they settle as None); the Answered and Avoided
/// shares of hostile acts; and kindnesses returned within a week, by any kind act back.</item>
/// <item>Per person: acts done and undergone, hurt, kindness and warmth, feuds and friendships; and
/// the town's feuds and friendships per 100 people a year, with the share of feuds between
/// households. Feuds are new feuds between people who aren't kin, friends falling out among them
/// (<see cref="Variety.Of"/>); feuds inside a family are counted apart.</item>
/// <item>Threads (<see cref="StoryThread"/>) a seed-season: the mean, the median, their depth and
/// their commonest shapes. The spec's target for batch 1 is 3 or more a season at 26 people.</item>
/// <item>How long feuds last: the median days a new feud between people who aren't close lasts,
/// counting those still on at the run's end as lasting at least that long (Kaplan-Meier). A feud
/// is a pair's spells (<see cref="FeudSpell"/>) joined across pauses of
/// <see cref="FeudPauseDays"/> or less: regard that sits at the line crosses it back and forth, and
/// most spells that end start again within three days. Once Repair is on it must stay at 28 days
/// or more.</item>
/// <item>The budgets of 7.2: trivia and news a year (the town's own acts, not the harness's), the
/// heavy hostile acts and the gifts the gate started, the gate's kind acts by year of the run (the
/// three-year drift: year 3 at most 1.5 x year 1), and warmth a person a year.</item>
/// </list>
/// </summary>
public sealed record StoryStats(int Runs, double SeedYears, double CastSize,
    IReadOnlyList<KindOutcomes> Outcomes, double IgnoredKindness, double AnsweredHostile, double AvoidedHostile,
    double ReturnedWithin7,
    IReadOnlyList<PersonStory> People, double ActsPerPerson, double FeudsPer100, double KinFeudsPer100,
    double FriendshipsPer100, double FeudsAcrossHouseholds,
    double ThreadsPerSeason, double MedianThreadsPerSeason, double MeanThreadDepth, int LongestThread,
    IReadOnlyList<(string Shape, double Share)> ThreadShapes,
    double TimedFeudsPerYear, double MedianFeudDays, double EndedFeudShare,
    double TriviaPerYear, double NewsPerYear, double HeavyHostileByGate, double GiftsByGate,
    IReadOnlyList<double> GateKindnessByYear, double WarmthPerPerson);

public static class StoryMetrics
{
    /// <summary>The links of a thread: three acts deep, two households.</summary>
    public const int ThreadDepth = 3;
    public const int ThreadHouseholds = 2;
    /// <summary>A pair's feud spells this many days apart or less are one feud.</summary>
    public const int FeudPauseDays = 7;

    private static double Ratio(double part, double whole) => whole == 0 ? double.NaN : part / whole;

    /// <param name="kinds">The act kinds the runs used (the town's, with any catalog rows).</param>
    /// <param name="cast">The cast the runs used (for households and kin).</param>
    /// <param name="o">The options the runs used (for which kinds are light).</param>
    public static StoryStats Summarise(IReadOnlyList<SimResult> runs, IReadOnlyList<ActKind> kinds, IReadOnlyList<Villager> cast, FeelingOptions o)
    {
        var byKind = kinds.ToDictionary(k => k.Name, StringComparer.Ordinal);
        var people = cast.ToDictionary(v => v.Name, StringComparer.Ordinal);
        bool Close(string a, string b) => people[a].Household == people[b].Household
                                          || people[a].KinOf(b) is not null || people[b].KinOf(a) is not null;
        bool Light(ActKind k) => Simulation.GateOf(k, o)?.Light == true;
        static bool KindAimed(ActKind k) => k.Affect is { Target: TargetIs.Chosen, Joy: > 0 };
        static bool HostileAimed(ActKind k) => k.Affect is { Target: TargetIs.Chosen, Joy: < 0 };
        double years = runs.Sum(r => r.Days) / (double)Variety.Year;
        double personYears = runs.Sum(r => r.CastSize * (double)r.Days) / Variety.Year;
        double PerYear(double x) => Ratio(x, years);
        double PerPerson(double x) => Ratio(x, personYears);

        // The life record, across households, from the doer's side.
        var settled = runs.SelectMany(r => r.LifeEvents)
            .Where(e => e.Role == LifeRole.Did && e.Outcome != Outcome.Open && people.ContainsKey(e.Person)
                        && people.ContainsKey(e.Other) && !Close(e.Person, e.Other))
            .ToList();
        var outcomes = settled.GroupBy(e => e.Kind, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KindOutcomes(g.Key, g.Count(),
                g.GroupBy(e => e.Outcome).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Count() / (double)g.Count())))
            .ToList();
        double ShareOf(Func<LifeEvent, bool> which, Outcome outcome)
        {
            var these = settled.Where(which).ToList();
            return Ratio(these.Count(e => e.Outcome == outcome), these.Count);
        }

        // Kindnesses returned within a week, across households: any kind act back counts, light or not.
        int kindAcross = 0, returned = 0;
        foreach (SimResult r in runs)
        {
            int end = r.Days * Clock.MinutesPerDay;
            var aimed = r.Acts.Where(a => a.Target is { } t && t != a.Actor && people.ContainsKey(a.Actor) && people.ContainsKey(t)
                                          && byKind.TryGetValue(a.Kind, out ActKind? k) && KindAimed(k)).ToList();
            var back = aimed.ToLookup(a => (a.Actor, a.Target!), a => a.Tick);
            foreach (Act a in aimed.Where(a => !Light(byKind[a.Kind]) && !Close(a.Actor, a.Target!)))
            {
                if (a.Tick + 7 * Clock.MinutesPerDay >= end)
                    continue;
                kindAcross++;
                if (back[(a.Target!, a.Actor)].Any(t => t > a.Tick && t - a.Tick <= 7 * Clock.MinutesPerDay))
                    returned++;
            }
        }

        // Per person: acts, the life record's severities, story events and warmth.
        var did = cast.ToDictionary(v => v.Name, _ => new SortedDictionary<string, double>(StringComparer.Ordinal), StringComparer.Ordinal);
        var undergone = cast.ToDictionary(v => v.Name, _ => new SortedDictionary<string, double>(StringComparer.Ordinal), StringComparer.Ordinal);
        var hurt = cast.ToDictionary(v => v.Name, _ => 0.0, StringComparer.Ordinal);
        var kindness = cast.ToDictionary(v => v.Name, _ => 0.0, StringComparer.Ordinal);
        var warmth = cast.ToDictionary(v => v.Name, _ => 0.0, StringComparer.Ordinal);
        var feuds = cast.ToDictionary(v => v.Name, _ => 0.0, StringComparer.Ordinal);
        var friendships = cast.ToDictionary(v => v.Name, _ => 0.0, StringComparer.Ordinal);
        int feudEvents = 0, kinFeudEvents = 0, feudsAcross = 0, friendshipEvents = 0;
        double trivia = 0, news = 0, heavyByGate = 0, giftsByGate = 0;
        int runYears = runs.Count == 0 ? 0 : runs.Max(r => r.Days / Variety.Year); // whole years only
        var gateKind = new double[runYears];
        var gateKindRuns = new int[runYears];
        foreach (SimResult r in runs)
        {
            foreach (Act a in r.Acts)
            {
                if (a.Injected || !byKind.TryGetValue(a.Kind, out ActKind? k))
                    continue;
                if (k.Tier == Tier.Trivia) trivia++;
                else if (k.Tier == Tier.News) news++;
                // Who did it and who underwent it. In a kind whose patient is the actor (a warning, a
                // stumble), the act's actor underwent it, and its target, if any (the official), did it.
                bool actorUndergoes = k.Affect?.Patient == Patient.Actor;
                string? doer = actorUndergoes ? a.Target : a.Actor;
                string? subject = actorUndergoes ? a.Actor : a.Target;
                if (doer is not null && did.TryGetValue(doer, out var d))
                    d[a.Kind] = d.GetValueOrDefault(a.Kind) + 1;
                if (subject is not null && subject != doer && undergone.TryGetValue(subject, out var u))
                {
                    u[a.Kind] = u.GetValueOrDefault(a.Kind) + 1;
                    if (Light(k) && KindAimed(k))
                        warmth[subject]++;
                }
            }
            int years1 = r.Days / Variety.Year;
            var thisRun = new double[years1];
            foreach (int id in r.Pursued)
            {
                Act a = r.Acts[id];
                if (!byKind.TryGetValue(a.Kind, out ActKind? k))
                    continue;
                if (HostileAimed(k) && !Light(k)) heavyByGate++;
                if (a.Kind == "GaveGift") giftsByGate++;
                if (KindAimed(k) && Clock.Day(a.Tick) / Variety.Year < years1)
                    thisRun[Clock.Day(a.Tick) / Variety.Year]++;
            }
            for (int y = 0; y < years1; y++)
            {
                gateKind[y] += thisRun[y];
                gateKindRuns[y]++;
            }
            foreach (LifeEvent e in r.LifeEvents)
                if (e.Role == LifeRole.Undergone && people.ContainsKey(e.Person))
                {
                    if (e.Hostile) hurt[e.Person] += e.Severity;
                    else if (!e.Light) kindness[e.Person] += e.Severity;
                }
            foreach (StoryEvent e in Variety.Of(r, kinds))
            {
                bool feud = e.Kind is "Feud" or "FellOut";
                if (!feud && e.Kind is not ("KinFeud" or "Friendship"))
                    continue;
                if (e.Kind == "KinFeud") kinFeudEvents++;
                else if (feud)
                {
                    feudEvents++;
                    if (people.TryGetValue(e.People[0], out Villager? x) && people.TryGetValue(e.People[1], out Villager? y)
                        && x.Household != y.Household)
                        feudsAcross++;
                }
                else friendshipEvents++;
                foreach (string p in e.People)
                {
                    if (feud && feuds.ContainsKey(p)) feuds[p]++;
                    else if (e.Kind == "Friendship" && friendships.ContainsKey(p)) friendships[p]++;
                }
            }
        }
        var rows = cast.OrderBy(v => v.Name, StringComparer.Ordinal).Select(v => new PersonStory(v.Name, v.Household,
            PerYear(did[v.Name].Values.Sum()), PerYear(undergone[v.Name].Values.Sum()),
            did[v.Name].ToDictionary(p => p.Key, p => PerYear(p.Value), StringComparer.Ordinal),
            undergone[v.Name].ToDictionary(p => p.Key, p => PerYear(p.Value), StringComparer.Ordinal),
            PerYear(hurt[v.Name]), PerYear(kindness[v.Name]), PerYear(feuds[v.Name]), PerYear(friendships[v.Name]),
            PerYear(warmth[v.Name]))).ToList();

        // Threads, counted in the season their third link landed.
        var threads = new List<StoryThread>();
        var perSeason = new List<int>();
        foreach (SimResult r in runs)
        {
            var mine = Threads(r.Acts, name => people.TryGetValue(name, out Villager? v) ? v.Household : null);
            threads.AddRange(mine);
            int seasons = r.Days / Clock.DaysPerSeason; // whole seasons only
            var counts = new int[seasons];
            foreach (StoryThread t in mine)
                if (t.Day / Clock.DaysPerSeason < seasons)
                    counts[t.Day / Clock.DaysPerSeason]++;
            perSeason.AddRange(counts);
        }
        int seasonsRun = perSeason.Count;
        var shapes = threads.GroupBy(t => t.Shape, StringComparer.Ordinal)
            .Select(g => (Shape: g.Key, Share: g.Count() / (double)threads.Count))
            .OrderByDescending(x => x.Share).ThenBy(x => x.Shape, StringComparer.Ordinal).Take(5).ToList();

        // How long new feuds between people who aren't close last.
        var spells = runs.SelectMany(r => Feuds(r.FeudSpells.Where(s => !s.Close && !s.Seeded), r.Days)).ToList();

        return new StoryStats(runs.Count, years, runs.Count == 0 ? 0 : runs.Average(r => r.CastSize),
            outcomes, ShareOf(e => !e.Hostile && !e.Light, Outcome.Ignored),
            ShareOf(e => e.Hostile && !e.Light, Outcome.Answered), ShareOf(e => e.Hostile && !e.Light, Outcome.Avoided),
            Ratio(returned, kindAcross),
            rows, PerPerson(did.Values.Sum(d => d.Values.Sum())),
            100 * PerPerson(feudEvents), 100 * PerPerson(kinFeudEvents), 100 * PerPerson(friendshipEvents), Ratio(feudsAcross, feudEvents),
            Ratio(perSeason.Sum(), seasonsRun), Median(perSeason.Select(x => (double)x)),
            threads.Count == 0 ? double.NaN : threads.Average(t => t.Depth), threads.Count == 0 ? 0 : threads.Max(t => t.Depth), shapes,
            PerYear(spells.Count), MedianDays(spells), Ratio(spells.Count(s => s.Ended), spells.Count),
            PerYear(trivia), PerYear(news), PerYear(heavyByGate), PerYear(giftsByGate),
            gateKind.Select((x, y) => Ratio(x, gateKindRuns[y])).ToList(), PerPerson(warmth.Values.Sum()));
    }

    /// <summary>A run's threads (<see cref="StoryThread"/>), by the day each became one. An act's
    /// <see cref="Act.About"/> names an earlier act; the acts it links form trees, and a tree is a
    /// thread when a chain in it is three acts deep and its acts' people (actor, target, the one
    /// stood up for) come from two or more households. A tree rooted in the harness's placed act is
    /// not the town's story and is left out.</summary>
    /// <param name="household">A person's household; null for someone outside the cast.</param>
    public static IReadOnlyList<StoryThread> Threads(IReadOnlyList<Act> acts, Func<string, string?> household)
    {
        int n = acts.Count;
        var parent = new int[n];
        var depth = new int[n];
        var root = new int[n];
        var trees = new SortedDictionary<int, (int Acts, int Depth, SortedSet<string> Houses, int Day, string Shape)>();
        for (int id = 0; id < n; id++)
        {
            Act a = acts[id];
            int p = a.About >= 0 && a.About < id ? a.About : -1;
            parent[id] = p;
            depth[id] = p >= 0 ? depth[p] + 1 : 1;
            root[id] = p >= 0 ? root[p] : id;
            if (!trees.TryGetValue(root[id], out var tree))
                tree = (0, 0, new SortedSet<string>(StringComparer.Ordinal), -1, "");
            tree.Acts++;
            tree.Depth = Math.Max(tree.Depth, depth[id]);
            foreach (string? who in new[] { a.Actor, a.Target, a.With })
                if (who is not null && household(who) is { } h)
                    tree.Houses.Add(h);
            if (depth[id] == ThreadDepth && tree.Day < 0)
            {
                tree.Day = Clock.Day(a.Tick);
                tree.Shape = $"{acts[parent[p]].Kind} > {acts[p].Kind} > {a.Kind}";
            }
            trees[root[id]] = tree;
        }
        return trees.Where(t => !acts[t.Key].Injected && t.Value.Depth >= ThreadDepth && t.Value.Houses.Count >= ThreadHouseholds)
            .Select(t => new StoryThread(t.Key, t.Value.Acts, t.Value.Depth, t.Value.Houses.Count, t.Value.Day, t.Value.Shape))
            .OrderBy(t => t.Day).ThenBy(t => t.Root).ToList();
    }

    /// <summary>Feuds from spells: each pair's spells in order, joined across pauses of
    /// <see cref="FeudPauseDays"/> or less. Each feud as <see cref="MedianDays"/> takes it: ended, the
    /// nights it lasted; not ended, a length it outlasted. A feud is still on at the run's end if its
    /// last spell is, and unsettled if that spell ended so late that a pause of FeudPauseDays does
    /// not fit before the last night (it may start again unseen). Either way it lasted longer than
    /// the nights before the last night it was seen feuding.</summary>
    public static IReadOnlyList<(int Days, bool Ended)> Feuds(IEnumerable<FeudSpell> spells, int runDays)
    {
        int last = runDays - 1;
        (int, bool) Feud(int from, int to) => to < 0 ? (last - from, false)
            : to + FeudPauseDays > last ? (to - 1 - from, false)
            : (to - from, true);
        var feuds = new List<(int Days, bool Ended)>();
        foreach (var pair in spells.GroupBy(s => (s.A, s.B)).OrderBy(g => g.Key.A, StringComparer.Ordinal).ThenBy(g => g.Key.B, StringComparer.Ordinal))
        {
            int from = -1, to = -1;
            foreach (FeudSpell s in pair.OrderBy(s => s.From))
            {
                if (from >= 0 && to >= 0 && s.From - to <= FeudPauseDays)
                {
                    to = s.To;
                    continue;
                }
                if (from >= 0)
                    feuds.Add(Feud(from, to));
                (from, to) = (s.From, s.To);
            }
            if (from >= 0)
                feuds.Add(Feud(from, to));
        }
        return feuds;
    }

    /// <summary>The median length of spells, some still running when they were last seen
    /// (Kaplan-Meier). Ended: the spell lasted exactly Days. Not ended: it lasted longer than Days.
    /// The median is the first length at which half or fewer are still running, to within rounding
    /// (a product that is exactly a half can come out a hair above it). Infinity if more than half
    /// outlast every ended one; NaN for none.</summary>
    public static double MedianDays(IReadOnlyCollection<(int Days, bool Ended)> spells)
    {
        if (spells.Count == 0)
            return double.NaN;
        var sorted = spells.OrderBy(s => s.Days).ToList();
        double running = 1;
        int atRisk = sorted.Count, i = 0;
        while (i < sorted.Count)
        {
            int t = sorted[i].Days, ended = 0, all = 0;
            for (; i < sorted.Count && sorted[i].Days == t; i++, all++)
                if (sorted[i].Ended)
                    ended++;
            if (ended > 0)
            {
                running *= 1 - ended / (double)atRisk;
                if (running <= 0.5 + 1e-9)
                    return t;
            }
            atRisk -= all;
        }
        return double.PositiveInfinity;
    }

    private static double Median(IEnumerable<double> values)
    {
        var s = values.OrderBy(x => x).ToList();
        return s.Count == 0 ? double.NaN : s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
    }
}
