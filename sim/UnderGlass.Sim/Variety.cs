namespace UnderGlass.Sim;

/// <summary>
/// One turn of a run's story, the kind a player would tell a friend about (actions-and-twists
/// section 3, mechanism 1; the story sifter of design phase 0d): a feud, a friendship, a
/// reconciliation, a scandal and who did it, a secret that came out, a confession, a verdict on the
/// wrong person, the constable's election, a hermit's or a brawler's spell, a household in debt.
/// Read from a run's results only; no rule reads it. <see cref="Id"/> is what makes it this story
/// rather than another (its kind and its people), without the day or the detail (a scandal's act,
/// an office), so the same story in two seeds has the same Id. A twist (batch 2's m-1) also carries
/// its minute, its cause chain and whether it is fair (<see cref="Variety.Fair"/>).
/// </summary>
public sealed record StoryEvent(string Kind, int Day, IReadOnlyList<string> People, string Detail = "",
    int Tick = -1, IReadOnlyList<CauseStep>? Chain = null, bool Fair = false)
{
    public string Id => $"{Kind} {string.Join("-", People)}";
}

/// <summary>
/// How different the runs are from each other (actions-and-twists section 3, the variety gate):
/// a town where Sam and Shane feud in most seeds, or the same person is always the culprit, is a
/// town whose new games feel the same. Over seed-years (a run of a year or less counts as one):
/// <list type="bullet">
/// <item>V1: the share of seed-years holding the commonest named story (a feud pair, a culprit, a
/// hermit, the withdrawn or a brawler). Target 30% or less. Families are pooled first (batch 2's
/// m-1): a pair's feud, falling out and feud in a family are one story, and so are a person's first
/// scandal and later ones; V1Feud and V1Culprit give each family's commonest.</item>
/// <item>V2: the effective number of headlines: the exponential of the Shannon entropy of each
/// seed-year's biggest story. 20 or more.</item>
/// <item>V3: the share of seed-year pairs sharing 80% of their stories (Jaccard). Under 5%.</item>
/// <item>V4: the share of seed-years holding a story found in under 2% of seed-years. 60% or more.
/// It needs 50 seed-years or more (NaN under that).</item>
/// <item>V5: for the person whose arc repeats most, the share of seed-years holding their commonest
/// arc (the kinds of story they were in that year). 50% or less.</item>
/// <item>V6: the median number of fair twists (reversals and revelations a player could have seen
/// coming: <see cref="Variety.Fair"/>) a seed-season. 2 or more. V6All counts every twist.</item>
/// <item>V8: how many kinds of town the runs make: seed-years on a 3 x 3 grid of conflict against
/// warmth, each axis cut at 0.8 and 1.2 times its median; the cells holding 5% or more. 3 or more.</item>
/// </list>
/// V7 (copies of a run split at day 28) needs runs that fork (m-2), and is NaN until then. The
/// constable's commonest winner is reported beside them (mechanism 10, fixed dates with open outcomes).
/// </summary>
public sealed record VarietyStats(int SeedYears,
    double V1, IReadOnlyList<(string Story, double Share)> Commonest,
    double V2, string TopHeadline, double TopHeadlineShare,
    double V3, double V4,
    double V5, string V5Person, string V5Arc,
    double V6, double TwistsPerSeason,
    int V8, IReadOnlyList<(string Cell, double Share)> Kinds,
    string Constable, double ConstableShare,
    double V1Feud = 0, string V1FeudPair = "", double V1Culprit = 0, string V1CulpritName = "",
    double V6All = 0, double FairShare = double.NaN, IReadOnlyDictionary<string, int>? Unfair = null,
    double V7 = double.NaN, double V7Headline = double.NaN);

public static partial class Variety
{
    /// <summary>The kinds of story event, biggest first: a seed-year's headline is its biggest story.
    /// Batch 2's order (spec 6.2); the kinds its slices bring (a let-off found out, a kept purse, a
    /// debt revealed or called in, a date stood up, a contest won) find nothing until they land.</summary>
    public static readonly IReadOnlyList<string> Kinds = new[]
    {
        "WrongVerdict", "BlameMoved", "SecretOut", "LetOffExposed", "KeptExposed", "DebtRevealed", "LetOff",
        "FirstScandal", "Scandal", "DebtCalledIn", "KinFeud", "Upheaval", "Hermit", "Humiliated", "Withdrawn",
        "Confessed", "FellOut", "StoodUp", "Feud", "Reconciled", "Brawler", "Friendship", "Won", "Debt", "Elected",
    };

    /// <summary>Kinds whose Id names a person or a pair that can repeat across seeds (V1).</summary>
    public static readonly IReadOnlySet<string> Named = new HashSet<string>(StringComparer.Ordinal)
    {
        "Feud", "FellOut", "KinFeud", "FirstScandal", "Scandal", "Hermit", "Withdrawn", "Brawler",
    };

    /// <summary>Twists (V6): a reversal (a feud mended, friends who fell out, the wrong person
    /// punished, the blame moved to someone else) or a revelation (a secret out, a confession).</summary>
    public static readonly IReadOnlySet<string> Twists = new HashSet<string>(StringComparer.Ordinal)
    {
        "WrongVerdict", "BlameMoved", "SecretOut", "Confessed", "FellOut", "Reconciled",
    };

    /// <summary>A named story's family for V1 (batch 2's m-1): a pair's feud, falling out and feud in
    /// a family are "Feud a-b"; a person's first scandal and later ones are "Scandal a"; any other
    /// story is itself.</summary>
    public static string FamilyOf(StoryEvent e) => e.Kind switch
    {
        "Feud" or "FellOut" or "KinFeud" => "Feud " + string.Join("-", e.People),
        "FirstScandal" or "Scandal" => "Scandal " + string.Join("-", e.People),
        _ => e.Id,
    };

    public const int Year = 4 * Clock.DaysPerSeason;

    private static int Rank(string kind)
    {
        for (int i = 0; i < Kinds.Count; i++)
            if (Kinds[i] == kind)
                return i;
        return Kinds.Count;
    }

    /// <summary>A run's story events, in day order, then kind order, then Id. Twists carry their
    /// cause chains and whether they are fair.</summary>
    public static IReadOnlyList<StoryEvent> Of(SimResult r, IReadOnlyList<ActKind> kinds)
    {
        var byName = kinds.ToDictionary(k => k.Name, StringComparer.Ordinal);
        RunLog log = RunLog.Of(r);
        var events = new List<StoryEvent>();
        static string[] Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? new[] { a, b } : new[] { b, a };

        // Ties: feuds, feuds in a family, friendships, and reconciliations (one a pair a season: the
        // engine notes each making-up). A feud between a pair who were friends earlier is a falling out.
        var friends = new HashSet<(string, string)>();
        var reconciled = new HashSet<(string, string, int)>();
        foreach (var (day, a, b, what) in r.Ties.OrderBy(t => t.Day).ThenBy(t => t.A, StringComparer.Ordinal).ThenBy(t => t.B, StringComparer.Ordinal))
        {
            string[] pair = Pair(a, b);
            var key = (pair[0], pair[1]);
            switch (what)
            {
                case "feud":
                    events.Add(new StoryEvent(friends.Contains(key) ? "FellOut" : "Feud", day, pair));
                    break;
                case "kin-feud":
                    events.Add(new StoryEvent("KinFeud", day, pair));
                    break;
                case "friendship":
                    friends.Add(key);
                    events.Add(new StoryEvent("Friendship", day, pair));
                    break;
                case "reconciled":
                    if (reconciled.Add((pair[0], pair[1], day / Clock.DaysPerSeason)))
                        events.Add(new StoryEvent("Reconciled", day, pair));
                    break;
            }
        }

        // Scandals the town made itself (not the harness's): a person's first is a first scandal, later
        // ones scandals. One nobody saw that someone later pinned on its actor by name is a secret out,
        // on the day the first name was heard.
        var culprits = new HashSet<string>(StringComparer.Ordinal);
        foreach (Act act in r.Acts)
        {
            if (act.Injected || !byName.TryGetValue(act.Kind, out ActKind? kind) || !kind.IsScandal)
                continue;
            events.Add(new StoryEvent(culprits.Add(act.Actor) ? "FirstScandal" : "Scandal", Clock.Day(act.Tick), new[] { act.Actor }, act.Kind, act.Tick));
            if (r.Witnesses.GetValueOrDefault(act.Id) > 0)
                continue;
            // The first naming, from the log: a belief's GotTick is its first hearing, often of a trace
            // with no name in it, so it can come days before anyone names the actor.
            int named = FirstNaming(log, act);
            if (named != int.MaxValue)
                events.Add(new StoryEvent("SecretOut", Clock.Day(named), new[] { act.Actor }, act.Kind, named));
        }
        // A confession, when it was made (the interview's line in the log).
        foreach (var (actId, who, confessed) in r.Interviews)
            if (confessed && actId >= 0 && actId < r.Acts.Count && !r.Acts[actId].Injected)
            {
                int at = log.Questioned.Where(q => q.Confessed && q.Suspect == who && q.ActId == actId).Select(q => q.Tick)
                    .DefaultIfEmpty(r.Acts[actId].Tick).Min();
                events.Add(new StoryEvent("Confessed", Clock.Day(at), new[] { who }, r.Acts[actId].Kind, at));
            }
        foreach (Verdict v in r.Verdicts)
            if (!v.Correct && !v.LetOff && v.ActId >= 0 && v.ActId < r.Acts.Count && !r.Acts[v.ActId].Injected)
                events.Add(new StoryEvent("WrongVerdict", Clock.Day(v.Tick), new[] { v.Accused }, r.Acts[v.ActId].Kind, v.Tick));
        foreach (Election e in r.Elections)
            events.Add(new StoryEvent("Elected", Clock.Day(e.Tick), new[] { e.Winner }, e.Office));

        // Spells (0d.6): a hermit or the withdrawn (stance at -0.5 or below for 28 days), a brawler (+0.5).
        foreach (Spell s in WithdrawalMetrics.Spells(r, brawlers: false))
            events.Add(new StoryEvent(s.Hermit ? "Hermit" : "Withdrawn", s.From, new[] { s.Name }));
        foreach (Spell s in WithdrawalMetrics.Spells(r, brawlers: true))
            events.Add(new StoryEvent("Brawler", s.From, new[] { s.Name }));

        // A household that ends the run in debt.
        foreach (var (house, purse) in r.Purses.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (purse < 0)
                events.Add(new StoryEvent("Debt", Math.Max(0, r.Days - 1), new[] { house }));

        // Batch 2's m-1: the blame moved, let-offs, upheavals and humiliations; and every twist's chain.
        events.AddRange(MoreEvents(r, byName, log));
        return events.Select(e => Twists.Contains(e.Kind) && e.Chain is null ? WithChain(e, r, log) : e)
            .OrderBy(e => e.Day).ThenBy(e => Rank(e.Kind)).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>The biggest story of a seed-year: the first kind in <see cref="Kinds"/>, the earliest
    /// on a tie, then by Id; null for a year with none.</summary>
    public static StoryEvent? Headline(IEnumerable<StoryEvent> year)
        => year.OrderBy(e => Rank(e.Kind)).ThenBy(e => e.Day).ThenBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>A run cut into seed-years: its events, conflict and warmth a person, by year (a run of
    /// a year or less is one); and its fair twists by season.</summary>
    public static (IReadOnlyList<SeedYear> Years, IReadOnlyList<int> SeasonTwists) Split(SimResult r, IReadOnlyList<ActKind> kinds)
    {
        var (years, fair, _, _) = SplitAll(r, kinds);
        return (years, fair);
    }

    /// <summary><see cref="Split"/> with every twist by season too, and the unfair ones by kind.</summary>
    public static (IReadOnlyList<SeedYear> Years, IReadOnlyList<int> FairTwists, IReadOnlyList<int> AllTwists, IReadOnlyDictionary<string, int> Unfair)
        SplitAll(SimResult r, IReadOnlyList<ActKind> kinds)
    {
        var byName = kinds.ToDictionary(k => k.Name, StringComparer.Ordinal);
        var events = Of(r, kinds);
        int years = Math.Max(1, r.Days / Year), n = Math.Max(1, r.CastSize);
        var split = Enumerable.Range(0, years).Select(_ => (Events: new List<StoryEvent>(), Conflict: 0.0, Warmth: 0.0)).ToArray();
        foreach (StoryEvent e in events)
            split[Math.Min(years - 1, e.Day / Year)].Events.Add(e);
        // Conflict and warmth: hostile and warm acts aimed at someone, and the year's feuds and
        // friendships at ten acts each.
        foreach (Act a in r.Acts)
            if (a.Target is not null && byName.TryGetValue(a.Kind, out ActKind? k) && k.Valence != 0)
            {
                int y = Math.Min(years - 1, Clock.Day(a.Tick) / Year);
                if (k.Valence < 0) split[y].Conflict++;
                else split[y].Warmth++;
            }
        var list = split.Select(s => new SeedYear(s.Events,
            (s.Conflict + 10.0 * s.Events.Count(e => e.Kind is "Feud" or "KinFeud" or "FellOut")) / n,
            (s.Warmth + 10.0 * s.Events.Count(e => e.Kind is "Friendship" or "Reconciled")) / n)).ToList();
        int seasons = Math.Max(1, r.Days / Clock.DaysPerSeason);
        var fair = new int[seasons];
        var all = new int[seasons];
        var unfair = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (StoryEvent e in events)
            if (Twists.Contains(e.Kind))
            {
                int s = Math.Min(seasons - 1, e.Day / Clock.DaysPerSeason);
                all[s]++;
                if (e.Fair)
                    fair[s]++;
                else
                    unfair[e.Kind] = unfair.GetValueOrDefault(e.Kind) + 1;
            }
        return (list, fair, all, unfair);
    }

    public static VarietyStats Measure(IReadOnlyList<SimResult> runs, IReadOnlyList<ActKind> kinds)
    {
        var years = new List<SeedYear>();
        var fair = new List<int>();
        var all = new List<int>();
        var unfair = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (SimResult r in runs)
        {
            var (y, f, a, u) = SplitAll(r, kinds);
            years.AddRange(y);
            fair.AddRange(f);
            all.AddRange(a);
            foreach (var (kind, n) in u)
                unfair[kind] = unfair.GetValueOrDefault(kind) + n;
        }
        return From(years, fair, all, unfair);
    }

    /// <summary>The measures from seed-years and fair twists a seed-season (see <see cref="VarietyStats"/>);
    /// with every twist a seed-season and the unfair ones by kind when given.</summary>
    public static VarietyStats From(IReadOnlyList<SeedYear> years, IReadOnlyList<int> seasonTwists,
        IReadOnlyList<int>? allSeasonTwists = null, IReadOnlyDictionary<string, int>? unfair = null)
    {
        int N = years.Count;
        var ids = years.Select(y => y.Events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)).ToList();
        var count = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var set in ids)
            foreach (string id in set)
                count[id] = count.GetValueOrDefault(id) + 1;
        double Share(int c) => N == 0 ? 0 : c / (double)N;

        // V1: the commonest named story, families pooled; and each family's commonest.
        var families = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SeedYear year in years)
            foreach (string family in year.Events.Where(e => Named.Contains(e.Kind)).Select(FamilyOf).ToHashSet(StringComparer.Ordinal))
                families[family] = families.GetValueOrDefault(family) + 1;
        var named = families.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
        double v1 = named.Count == 0 ? 0 : Share(named[0].Value);
        var feud = named.FirstOrDefault(c => c.Key.StartsWith("Feud ", StringComparison.Ordinal));
        var culprit = named.FirstOrDefault(c => c.Key.StartsWith("Scandal ", StringComparison.Ordinal));

        // V2: the effective number of headlines.
        var heads = years.Select(y => Headline(y.Events)?.Id).Where(h => h is not null).Select(h => h!)
            .GroupBy(h => h, StringComparer.Ordinal).Select(g => (Id: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count).ThenBy(g => g.Id, StringComparer.Ordinal).ToList();
        int headed = heads.Sum(h => h.Count);
        double v2 = headed == 0 ? 0 : Math.Exp(-heads.Sum(h => h.Count / (double)headed * Math.Log(h.Count / (double)headed)));

        // V3: seed-year pairs sharing 80% of their stories, among pairs with any.
        long pairs = 0, alike = 0;
        for (int i = 0; i < N; i++)
            for (int j = i + 1; j < N; j++)
            {
                if (ids[i].Count + ids[j].Count == 0)
                    continue;
                pairs++;
                int both = ids[i].Count(ids[j].Contains);
                if (both >= 0.8 * (ids[i].Count + ids[j].Count - both))
                    alike++;
            }
        double v3 = pairs == 0 ? 0 : alike / (double)pairs;

        // V4: seed-years holding a story found in under 2% of seed-years (NaN under 50 seed-years,
        // where no story can be that rare).
        double v4 = N < 50 ? double.NaN : Share(ids.Count(set => set.Any(id => count[id] < 0.02 * N)));

        // V5: the person whose arc repeats most. An arc is the kinds of story they were in that year;
        // a household's debt or upheaval and an office are not a person's arc.
        var arcs = new SortedDictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (SeedYear year in years)
        {
            var kindsOf = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (StoryEvent e in year.Events)
                if (e.Kind is not ("Debt" or "Elected" or "Upheaval"))
                    foreach (string p in e.People)
                    {
                        if (!kindsOf.TryGetValue(p, out var set))
                            kindsOf[p] = set = new SortedSet<string>(StringComparer.Ordinal);
                        set.Add(e.Kind);
                    }
            foreach (var (p, set) in kindsOf)
            {
                if (!arcs.TryGetValue(p, out var c))
                    arcs[p] = c = new Dictionary<string, int>(StringComparer.Ordinal);
                string arc = string.Join("+", set);
                c[arc] = c.GetValueOrDefault(arc) + 1;
            }
        }
        string v5Person = "", v5Arc = "";
        int v5Count = 0;
        foreach (var (p, c) in arcs) // name order, so the first of equals wins
        {
            var best = c.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).First();
            if (best.Value > v5Count)
                (v5Person, v5Arc, v5Count) = (p, best.Key, best.Value);
        }

        // V6: fair twists a seed-season; every twist beside it.
        var sorted = seasonTwists.OrderBy(x => x).ToList();
        double v6 = Median(sorted.Select(x => (double)x).ToList());
        var allSorted = (allSeasonTwists ?? seasonTwists).OrderBy(x => x).ToList();
        int allTwists = allSorted.Sum(), fairTwists = sorted.Sum();

        // V8: kinds of town, on a grid of conflict against warmth cut at 0.8 and 1.2 times the medians.
        double mc = Median(years.Select(y => y.Conflict).OrderBy(x => x).ToList());
        double mw = Median(years.Select(y => y.Warmth).OrderBy(x => x).ToList());
        static string Band(double v, double m) => v < 0.8 * m ? "low" : v > 1.2 * m ? "high" : "mid";
        var cells = years.GroupBy(y => $"conflict {Band(y.Conflict, mc)}, warmth {Band(y.Warmth, mw)}", StringComparer.Ordinal)
            .Select(g => (Cell: g.Key, Share: Share(g.Count())))
            .OrderByDescending(c => c.Share).ThenBy(c => c.Cell, StringComparer.Ordinal).ToList();

        var constables = years.Select(y => y.Events.FirstOrDefault(e => e.Kind == "Elected")?.People[0]).Where(w => w is not null)
            .GroupBy(w => w!, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        int elections = constables.Sum(g => g.Count());
        return new VarietyStats(N, v1, named.Take(5).Select(c => (c.Key, Share(c.Value))).ToList(),
            v2, heads.Count == 0 ? "" : heads[0].Id, heads.Count == 0 ? 0 : Share(heads[0].Count),
            v3, v4, Share(v5Count), v5Person, v5Arc,
            v6, sorted.Count == 0 ? 0 : sorted.Average(),
            cells.Count(c => c.Share >= 0.05), cells,
            constables.Count == 0 ? "" : constables[0].Key, elections == 0 ? 0 : constables[0].Count() / (double)elections,
            feud.Key is null ? 0 : Share(feud.Value), feud.Key is null ? "" : feud.Key["Feud ".Length..],
            culprit.Key is null ? 0 : Share(culprit.Value), culprit.Key is null ? "" : culprit.Key["Scandal ".Length..],
            Median(allSorted.Select(x => (double)x).ToList()), allTwists == 0 ? double.NaN : fairTwists / (double)allTwists,
            unfair ?? new SortedDictionary<string, int>(StringComparer.Ordinal));
    }

    private static double Median(IReadOnlyList<double> sorted)
        => sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
}

/// <summary>One seed-year: its story events, and its conflict and warmth a person (Variety, V8).</summary>
public sealed record SeedYear(IReadOnlyList<StoryEvent> Events, double Conflict = 0, double Warmth = 0);
