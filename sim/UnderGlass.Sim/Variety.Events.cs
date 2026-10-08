namespace UnderGlass.Sim;

/// <summary>A step of a twist's cause chain (batch 2 spec 6.2), earliest first: an act, a belief, a
/// telling, a report, a tie, or a dealt start (Tick -1, ActId -1). Visible: something a player could
/// have noticed before the twist (an act seen, found or told by someone outside it, or a dealt start).
/// Ties, reports and verdicts are steps, not traces.</summary>
public sealed record CauseStep(int Tick, string What, int ActId, bool Visible);

/// <summary>
/// A run's log read once into the rows the story sifter needs (batch 2 spec 6.2): beliefs, tellings,
/// traces found, suspicions, reports, interviews and making-ups, each with its minute. Read only;
/// the log's own lines are the source (Simulation.cs's belief and told lines, the authority's report
/// and questioned lines, the traces' found line, the suspects line, the feelings' reconciled line).
/// </summary>
public sealed class RunLog
{
    public IReadOnlyList<(int Tick, string Holder, int ActId, string? Actor, Source Source)> Beliefs { get; }
    public IReadOnlyList<(int Tick, string Teller, string Listener, int ActId)> Told { get; }
    public IReadOnlyList<(int Tick, string Who, int ActId)> Found { get; }
    public IReadOnlyList<(int Tick, string Who, int ActId)> Suspected { get; }
    public IReadOnlyList<(int Tick, string Who, int ActId, string? Named)> Reports { get; }
    public IReadOnlyList<(int Tick, string Suspect, int ActId, bool Confessed)> Questioned { get; }
    public IReadOnlyList<(int Tick, string Holder, string Toward, int ActId)> Reconciled { get; }

    private RunLog(IEnumerable<string> log)
    {
        var beliefs = new List<(int, string, int, string?, Source)>();
        var told = new List<(int, string, string, int)>();
        var found = new List<(int, string, int)>();
        var suspected = new List<(int, string, int)>();
        var reports = new List<(int, string, int, string?)>();
        var questioned = new List<(int, string, int, bool)>();
        var reconciled = new List<(int, string, string, int)>();
        static string? Name(string s) => s == "someone" ? null : s;
        foreach (string line in log)
        {
            string[] w = line.Split(' ');
            if (w.Length < 4 || !int.TryParse(w[0], out int m))
                continue;
            switch (w[1])
            {
                case "belief" when w.Length >= 6 && int.TryParse(w[3], out int b) && Enum.TryParse(w[5], out Source source):
                    beliefs.Add((m, w[2], b, Name(w[4]), source));
                    break;
                case "told" when w.Length >= 5 && int.TryParse(w[4], out int t):
                    told.Add((m, w[2], w[3], t));
                    break;
                case "found" when int.TryParse(w[3], out int f):
                    found.Add((m, w[2], f));
                    break;
                case "suspects" when int.TryParse(w[3], out int s):
                    suspected.Add((m, w[2], s));
                    break;
                case "report" when w.Length >= 6 && int.TryParse(w[4], out int r):
                    reports.Add((m, w[2], r, Name(w[5])));
                    break;
                case "questioned" when w.Length >= 7 && w[3] == "by" && w[5] == "for" && int.TryParse(w[6].TrimEnd(':'), out int q):
                    questioned.Add((m, w[2], q, w.Length > 7 && w[7] == "confessed"));
                    break;
                case "reconciled" when w.Length >= 8 && w[6] == "act" && int.TryParse(w[7], out int k):
                    reconciled.Add((m, w[2], w[3], k));
                    break;
            }
        }
        (Beliefs, Told, Found, Suspected, Reports, Questioned, Reconciled) = (beliefs, told, found, suspected, reports, questioned, reconciled);
        _beliefsOf = beliefs.GroupBy(b => b.Item3).ToDictionary(g => g.Key, g => (IReadOnlyList<(int, string, int, string?, Source)>)g.ToList());
        _toldOf = told.GroupBy(t => t.Item4).ToDictionary(g => g.Key, g => g.Min(t => t.Item1));
    }

    private readonly Dictionary<int, IReadOnlyList<(int Tick, string Holder, int ActId, string? Actor, Source Source)>> _beliefsOf;
    private readonly Dictionary<int, int> _toldOf;

    /// <summary>The belief rows about one act, in log order.</summary>
    public IReadOnlyList<(int Tick, string Holder, int ActId, string? Actor, Source Source)> BeliefsOf(int actId)
        => _beliefsOf.TryGetValue(actId, out var rows) ? rows : Array.Empty<(int, string, int, string?, Source)>();

    /// <summary>The minute an act was first told, or int.MaxValue.</summary>
    public int FirstTold(int actId) => _toldOf.TryGetValue(actId, out int t) ? t : int.MaxValue;

    /// <summary>A run's log, read.</summary>
    public static RunLog Of(SimResult r) => new(r.Log);

    /// <summary>Log lines, read (for tests and made-up logs).</summary>
    public static RunLog Parse(IEnumerable<string> lines) => new(lines);
}

public static partial class Variety
{
    /// <summary>
    /// Whether an act was visible before a minute (batch 2 spec 6.2): someone other than its actor
    /// and target saw it or found its trace, or someone told it, before then.
    /// </summary>
    public static bool VisibleBefore(RunLog log, Act act, int tick)
        => log.FirstTold(act.Id) < tick
           || log.BeliefsOf(act.Id).Any(b => b.Tick < tick && b.Source is Source.Witnessed or Source.Found
                                             && b.Holder != act.Actor && b.Holder != act.Target);

    /// <summary>The minute someone other than its actor first named an act's actor (int.MaxValue if
    /// nobody did), from the log's belief rows.</summary>
    public static int FirstNaming(RunLog log, Act act)
        => log.BeliefsOf(act.Id).Where(b => b.Actor == act.Actor && b.Holder != act.Actor).Select(b => b.Tick).DefaultIfEmpty(int.MaxValue).Min();

    /// <summary>The fair-twist rule (research section 3; batch 2 spec 6.2): a twist is fair when its
    /// chain has 2 or more steps before it and one of them was visible.</summary>
    public static bool Fair(IReadOnlyList<CauseStep> chain, int tick)
    {
        var before = chain.Where(s => s.Tick < tick).ToList();
        return before.Count >= 2 && before.Any(s => s.Visible);
    }

    /// <summary>
    /// The lead name for a bad act, day by day (BlameMoved): from the act's belief rows, each holder's
    /// latest belief is theirs; the lead at a day's end is the name held by the most holders other than
    /// the actor whose belief names someone, if 2 or more do and one name has strictly the most. Yields
    /// (day, the minute of the last row that day, lead) for each day it is defined, from the act's day
    /// to <paramref name="lastDay"/>.
    /// </summary>
    public static IEnumerable<(int Day, int Tick, string Lead)> Leads(IEnumerable<(int Tick, string Holder, string? Actor)> rows, string actor, int fromDay, int lastDay)
    {
        var current = new Dictionary<string, string?>(StringComparer.Ordinal);
        var ordered = rows.Where(r => r.Holder != actor).OrderBy(r => r.Tick).ThenBy(r => r.Holder, StringComparer.Ordinal).ToList();
        int i = 0, lastTick = -1;
        for (int day = fromDay; day <= lastDay; day++)
        {
            int end = (day + 1) * Clock.MinutesPerDay;
            for (; i < ordered.Count && ordered[i].Tick < end; i++)
            {
                current[ordered[i].Holder] = ordered[i].Actor;
                lastTick = ordered[i].Tick;
            }
            var counts = current.Values.Where(v => v is not null).GroupBy(v => v!, StringComparer.Ordinal)
                .Select(g => (Name: g.Key, Count: g.Count())).OrderByDescending(g => g.Count).ThenBy(g => g.Name, StringComparer.Ordinal).ToList();
            if (counts.Sum(c => c.Count) >= 2 && (counts.Count == 1 || counts[0].Count > counts[1].Count))
                yield return (day, lastTick, counts[0].Name);
        }
    }

    /// <summary>
    /// The story events batch 2's m-1 adds (spec 6.2), with the twists' cause chains:
    /// <list type="bullet">
    /// <item>BlameMoved: a retold bad act (valence below 0, news or worse, not placed) whose lead name
    /// moves from p to q, once for each (act, p, q). A reversal.</item>
    /// <item>LetOff: the mayor let the accused off. Nobody knows, so no twist.</item>
    /// <item>Upheaval: a household switched its grocer.</item>
    /// <item>Humiliated: warned, taken in or set to service before 3 or more (those within 8 tiles,
    /// less the one who did it); one a person a day.</item>
    /// </list>
    /// FirstScandal and Scandal are split in <see cref="Of"/>.
    /// </summary>
    private static IEnumerable<StoryEvent> MoreEvents(SimResult r, IReadOnlyDictionary<string, ActKind> byName, RunLog log)
    {
        // BlameMoved.
        foreach (Act act in r.Acts)
        {
            if (act.Injected || !byName.TryGetValue(act.Kind, out ActKind? kind) || kind.Valence >= 0 || kind.Tier < Tier.News
                || log.FirstTold(act.Id) == int.MaxValue)
                continue; // a retold bad act, news or worse, not placed
            var rows = log.BeliefsOf(act.Id).Select(b => (b.Tick, b.Holder, b.Actor)).ToList();
            if (rows.Count == 0)
                continue;
            string? last = null;
            var seen = new HashSet<(string, string)>();
            foreach (var (day, tick, lead) in Leads(rows, act.Actor, Clock.Day(act.Tick), r.Days - 1))
            {
                if (last is not null && lead != last && seen.Add((last, lead)))
                {
                    int firstP = rows.Where(x => x.Actor == last).Select(x => x.Tick).DefaultIfEmpty(act.Tick).Min();
                    int firstQ = rows.Where(x => x.Actor == lead).Select(x => x.Tick).DefaultIfEmpty(tick).Min();
                    var chain = new List<CauseStep>
                    {
                        new(act.Tick, "act", act.Id, VisibleBefore(log, act, tick)),
                        new(firstP, "belief", act.Id, false),
                        new(firstQ, "belief", act.Id, false),
                    };
                    yield return new StoryEvent("BlameMoved", day, new[] { last, lead }, act.Kind, tick, chain, Fair(chain, tick + 1) && chain[0].Visible);
                }
                last = lead;
            }
        }

        // LetOff.
        foreach (Verdict v in r.Verdicts)
            if (v.LetOff && v.ActId >= 0 && v.ActId < r.Acts.Count && !r.Acts[v.ActId].Injected)
                yield return new StoryEvent("LetOff", Clock.Day(v.Tick), new[] { v.By, v.Accused }, r.Acts[v.ActId].Kind, v.Tick);

        // Upheaval: a grocer switch.
        foreach (var (day, house, from, to) in r.ShopSwitches)
            yield return new StoryEvent("Upheaval", day, new[] { house }, $"{from}->{to}");

        // Humiliated: shamed before 3 or more.
        var shamed = new HashSet<(string, int)>();
        foreach (Act act in r.Acts)
        {
            if (act.Kind is not (Authority.Warned or Authority.TakenIn or "Service") || !r.Scenes.TryGetValue(act.Id, out Scene? scene))
                continue;
            if (act.About >= 0 && act.About < r.Acts.Count && r.Acts[act.About].Injected)
                continue; // the harness's scandal
            int audience = scene.InRange - (act.Target is not null ? 1 : 0);
            if (audience >= 3 && shamed.Add((act.Actor, Clock.Day(act.Tick))))
                yield return new StoryEvent("Humiliated", Clock.Day(act.Tick), new[] { act.Actor }, act.Kind, act.Tick);
        }
    }

    /// <summary>A twist's cause chain and fairness (batch 2 spec 6.2), for the twists built before m-1:
    /// WrongVerdict, SecretOut, Confessed, FellOut and Reconciled. Other events are returned as they are.</summary>
    private static StoryEvent WithChain(StoryEvent e, SimResult r, RunLog log)
    {
        switch (e.Kind)
        {
            case "WrongVerdict":
            {
                Verdict? v = r.Verdicts.FirstOrDefault(v => !v.Correct && !v.LetOff && v.Accused == e.People[0] && v.Tick == e.Tick
                                                           && v.ActId >= 0 && v.ActId < r.Acts.Count && r.Acts[v.ActId].Kind == e.Detail);
                if (v is null)
                    return e;
                Act act = r.Acts[v.ActId];
                var chain = new List<CauseStep> { new(act.Tick, "act", act.Id, VisibleBefore(log, act, v.Tick)) };
                chain.AddRange(log.Reports.Where(x => x.ActId == act.Id && x.Named == v.Accused && x.Tick < v.Tick)
                    .Select(x => new CauseStep(x.Tick, "report", act.Id, false)));
                return e with { Tick = v.Tick, Chain = chain, Fair = Fair(chain, v.Tick) && chain[0].Visible };
            }
            case "SecretOut":
            {
                // The unseen act of this kind by this person first named at the event's minute.
                Act? act = r.Acts.FirstOrDefault(a => a.Actor == e.People[0] && a.Kind == e.Detail && !a.Injected
                                                     && r.Witnesses.GetValueOrDefault(a.Id) == 0 && FirstNaming(log, a) == e.Tick);
                if (act is null)
                    return e;
                int named = e.Tick;
                var chain = new List<CauseStep> { new(act.Tick, "act", act.Id, false) };
                chain.AddRange(log.Found.Where(x => x.ActId == act.Id && x.Tick < named).Select(x => new CauseStep(x.Tick, "found", act.Id, true)));
                chain.AddRange(log.Suspected.Where(x => x.ActId == act.Id && x.Tick < named).Select(x => new CauseStep(x.Tick, "suspects", act.Id, true)));
                chain.Add(new CauseStep(named, "belief", act.Id, false));
                chain = chain.OrderBy(s => s.Tick).ToList();
                return e with { Chain = chain, Fair = chain.Any(s => s.What is "found" or "suspects") };
            }
            case "Confessed":
            {
                var q = log.Questioned.FirstOrDefault(x => x.Confessed && x.Suspect == e.People[0] && x.Tick == e.Tick
                                                           && x.ActId >= 0 && x.ActId < r.Acts.Count && r.Acts[x.ActId].Kind == e.Detail);
                if (q.Suspect is null)
                    return e;
                Act act = r.Acts[q.ActId];
                var chain = new List<CauseStep> { new(act.Tick, "act", act.Id, VisibleBefore(log, act, q.Tick)) };
                chain.AddRange(log.Reports.Where(x => x.ActId == act.Id && x.Tick < q.Tick).Select(x => new CauseStep(x.Tick, "report", act.Id, false)));
                chain.Add(new CauseStep(q.Tick, "questioned", act.Id, false));
                return e with { Tick = q.Tick, Chain = chain, Fair = chain[0].Visible };
            }
            case "FellOut":
            {
                // The friendship, then each act between the two that lowered either's regard for the other.
                string a = e.People[0], b = e.People[1];
                int twist = (e.Day + 1) * Clock.MinutesPerDay - 1; // ties are noted at the day's end
                var friendship = r.Ties.Where(t => t.What == "friendship" && (t.A, t.B) is var p && (p == (a, b) || p == (b, a)) && t.Day <= e.Day)
                    .OrderBy(t => t.Day).FirstOrDefault();
                var chain = new List<CauseStep>();
                if (friendship.What is not null)
                    chain.Add(new CauseStep(friendship.Day * Clock.MinutesPerDay, "tie", -1, false));
                int since = chain.Count > 0 ? chain[0].Tick : 0;
                foreach (int id in r.Feelings.Where(f => f.Change < 0 && f.ActId >= 0 && f.Tick >= since && f.Tick <= twist
                                                          && (f.Holder == a && f.Toward == b || f.Holder == b && f.Toward == a))
                             .Select(f => f.ActId).Distinct().Order())
                    if (id < r.Acts.Count)
                        chain.Add(new CauseStep(r.Acts[id].Tick, "act", id, VisibleBefore(log, r.Acts[id], twist)));
                chain = chain.OrderBy(s => s.Tick).ToList();
                return e with { Tick = twist, Chain = chain, Fair = Fair(chain, twist + 1) };
            }
            case "Reconciled":
            {
                // Each act that lowered either's regard for the other before it, then the kindness the
                // making-up cites.
                string a = e.People[0], b = e.People[1];
                var made = log.Reconciled.Where(x => Clock.Day(x.Tick) == e.Day && (x.Holder == a && x.Toward == b || x.Holder == b && x.Toward == a))
                    .OrderBy(x => x.Tick).FirstOrDefault();
                if (made.Holder is null)
                    return e;
                var chain = r.Feelings.Where(f => f.Change < 0 && f.ActId >= 0 && f.Tick < made.Tick
                                                  && (f.Holder == a && f.Toward == b || f.Holder == b && f.Toward == a))
                    .Select(f => f.ActId).Distinct().Order().Where(id => id < r.Acts.Count)
                    .Select(id => new CauseStep(r.Acts[id].Tick, "act", id, VisibleBefore(log, r.Acts[id], made.Tick))).ToList();
                if (made.ActId >= 0 && made.ActId < r.Acts.Count)
                    chain.Add(new CauseStep(r.Acts[made.ActId].Tick, "kindness", made.ActId, VisibleBefore(log, r.Acts[made.ActId], made.Tick + 1)));
                chain = chain.OrderBy(s => s.Tick).ToList();
                return e with { Tick = made.Tick, Chain = chain, Fair = Fair(chain, made.Tick + 1) };
            }
            default:
                return e;
        }
    }
}
