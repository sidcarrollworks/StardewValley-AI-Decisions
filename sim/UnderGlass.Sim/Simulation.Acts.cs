namespace UnderGlass.Sim;

/// <summary>
/// The act catalog's rules (acts spec 3; Sid, 2026-10-08). Slice acts-0 builds the seams the gate
/// reads: Fits, which every act the gate offers must pass, and the pride term in its cost. Every
/// shipped row passes Fits (its hours are the whole day and its gate asks for nothing more) and has
/// no pride, so the town is unchanged. Slice acts-1 adds the rules for light kind acts (acts spec
/// 1): they stir no motive, a cold reading, the warm cap and the warm budget; and watch mode. The
/// per-head draws, the arrivals, what follows an act and the new motives come with later slices.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Whether an act fits the actor a, the target o and the minute (acts spec 3.1): its
    /// hours; its card, if it needs one; the actor's familiarity with and regard for the target; and
    /// its audience, if it needs one.</summary>
    /// <param name="watching">Weighing for the watch record: a catalog row of a slice that is on
    /// fits even in watch mode.</param>
    private bool Fits(ActKind kind, int a, int o, int m, bool watching = false)
    {
        if (ActCatalog.SliceOf(kind.Name) is { } slice && (!_fo.Acts.IsOn(slice) || _fo.Acts.Watch && !watching))
            return false; // a row whose slice is off is never offered; in watch mode, only recorded
        if (!kind.OpenAt(Clock.OfDay(m)))
            return false;
        if (GateOf(kind) is not { } g)
            return true;
        Person p = _people[a];
        if (g.NeedsCard && !(p.V.Acts.TryGetValue(kind.Name, out double w) && w > 0))
            return false;
        if (g.MinFamiliarity > 0 && _fam[a, o] < g.MinFamiliarity)
            return false;
        if (g.MinRegard > -1 && St(_names[a], _names[o]) < g.MinRegard)
            return false;
        return g.MinAudience <= 0 || Audience(p, o) >= g.MinAudience;
    }

    /// <summary>Others awake in the actor's place, within FarTiles and in sight, not counting the target.</summary>
    private int Audience(Person p, int o)
    {
        int n = 0;
        for (int q = 0; q < _people.Length; q++)
        {
            Person x = _people[q];
            if (x != p && q != o && !x.Asleep && x.Place == p.Place && x.At.Chebyshev(p.At) <= _po.FarTiles
                && Perception.LineOfSight(_places[p.Place], p.At, x.At, _po) > 0)
                n++;
        }
        return n;
    }

    /// <summary>Pride (acts spec 2.2): a kind with a pride weight costs more for the proud, by
    /// PrideWeight x (self-regard - 0.5). No shipped row has one.</summary>
    private double Pride(ActKind kind, int h)
        => GateOf(kind) is { PrideWeight: not 0 } g ? g.PrideWeight * (CharacterOf(h).SelfRegard - 0.5) : 0;

    // ---- light kind acts (acts spec 1; slice acts-1) -------------------------------------------

    private readonly Dictionary<int, (int Day, int Count)> _warmToday = new();
    private readonly Dictionary<(int Holder, int Cause, int Day), double> _warmUsed = new();
    private readonly Dictionary<(int Holder, int ActId), double> _warmTaken = new();
    private readonly List<CatalogWatched> _catalogWatch = new();
    private readonly HashSet<(int Holder, int Subject, DesireKind Motive, int Source, int Day)> _watched = new();

    private readonly Dictionary<(int Holder, int Subject), int> _fondWarmDay = new();

    /// <summary>A light kind act (a thank-you, a compliment, a joke), under its rules: those are
    /// Returns' rules (acts spec 2.3), so with Returns off a light kind row of a town's own is
    /// treated as it was before the catalog.</summary>
    private bool IsWarm(ActKind k) => _fo.Acts.Returns && IsLight(k) && k.Affect is { Joy: > 0 };

    /// <summary>The day's cap on light acts is reached: WarmPerDay for light kind acts, counted apart
    /// from LightPerDay, the cap on light hostile ones.</summary>
    private bool LightCapReached(ActKind kind, int h, int m)
        => IsWarm(kind)
            ? (_warmToday.TryGetValue(h, out var w) && w.Day == Clock.Day(m) ? w.Count : 0) >= _fo.Acts.WarmPerDay
            : _lightToday.GetValueOrDefault((h, Clock.Day(m))) >= _fo.LightPerDay;

    /// <summary>Counts a light act the actor began toward the day's cap.</summary>
    private void CountLight(ActKind kind, int a, int m)
    {
        int day = Clock.Day(m);
        if (IsWarm(kind))
            _warmToday[a] = (day, _warmToday.TryGetValue(a, out var w) && w.Day == day ? w.Count + 1 : 1);
        else
            _lightToday[(a, day)] = _lightToday.GetValueOrDefault((a, day)) + 1;
    }

    /// <summary>A light kind act seen by its target stirs no motive: a thank-you is never thanked
    /// back and a compliment obliges no gift. Read cold it hurts a little (its stance), and still
    /// stirs nothing, being light. True when handled.</summary>
    private bool WarmOnly(int h, ActKind kind, double felt, FeltRecord? rec)
    {
        if (!IsWarm(kind))
            return false;
        if (rec is { Route: "Cold" })
            Hurt(h, felt);
        return true;
    }

    /// <summary>The joy a patient feels (acts spec 2.1, ReadWarmAt): a warm act is read cold, and taken
    /// badly at -ColdShare x |joy|, by its target when they hold the actor below the row's ReadWarmAt
    /// (a compliment from someone disliked is flattery; a tease from someone not liked is a jab).</summary>
    private double ReadJoy(int h, int patient, Act act, Affect row)
        => patient == h && row.Joy > 0 && row.ReadWarmAt > double.NegativeInfinity && St(_names[h], act.Actor) < row.ReadWarmAt
            ? -_fo.Acts.ColdShare * Math.Abs(row.Joy)
            : row.Joy;

    /// <summary>The warm budget (acts spec 1): regard h gains toward the actor c from light kind acts
    /// is at most WarmBudget a day, all such acts together. Repeat halving is kept per kind, so
    /// without it a mix of small kindnesses would pass undiscounted. Each act keeps what it took, so
    /// feeling it again asks only for the difference.</summary>
    private double WarmBudgeted(int h, int c, Act act, ActKind kind, double dr)
    {
        if (dr <= 0 || !IsWarm(kind))
            return dr;
        var key = (h, c, Clock.Day(act.Tick));
        double taken = _warmTaken.GetValueOrDefault((h, act.Id));
        double allowed = Math.Min(dr, taken + Math.Max(0, _fo.Acts.WarmBudget - _warmUsed.GetValueOrDefault(key)));
        _warmUsed[key] = _warmUsed.GetValueOrDefault(key) + allowed - taken;
        _warmTaken[(h, act.Id)] = allowed;
        return allowed;
    }

    /// <summary>Watch mode (acts spec 2.3): after the gate weighed a motive on the rows it may use,
    /// the same weighing with the catalog's rows, as Weigh walks them: the row the gate chose ends
    /// it; a shipped row it weighed as a close call and didn't take is passed over, and after that
    /// only a row that clears counts. If that is a catalog row (the first that clears, or a close
    /// call before any was declined), it is recorded and nothing starts. Reads only. A motive stays
    /// when nothing starts, so each is recorded once (Fond, which recurs, once a day).</summary>
    private void WatchCatalog(Person p, int h, Motive d, double I, double eff, double fear, ActKind? chosen, int m)
    {
        if (!_fo.Acts.Watch || !Acting)
            return;
        bool declined = false;
        foreach (ActKind k in ActsFor(d.Kind, d.Act, p, d.Subject, m, watching: true))
        {
            if (k == chosen)
                return;
            if (I < Min(k))
                continue;
            double margin = eff - (DesireMath.Cost(Form(k), d.Hostile, fear, _fo) + Pride(k, h));
            string call = DesireMath.Call(margin, _fo);
            bool catalog = ActCatalog.SliceOf(k.Name) is not null;
            if (call == "no" || call == "close" && (declined || !catalog))
            {
                declined |= call == "close"; // the gate weighed it and didn't take it
                continue;
            }
            if (catalog && _watched.Add((h, d.Subject, d.Kind, d.Source, d.Kind == DesireKind.Fond ? Clock.Day(m) : -1)))
                _catalogWatch.Add(new CatalogWatched(m, p.V.Name, _names[d.Subject], d.Kind, k.Name, margin));
            return;
        }
    }

    /// <summary>Fond waits a few days (FondWarmDays) after it was answered with a light kind act
    /// toward someone before it reaches for another: a small act doesn't reset missing someone
    /// (question 2, answer c), so without the wait Fond would compliment the same person every day
    /// they meet. A gift, which does reset it, is not held back.</summary>
    private bool FondWarmWaits(DesireKind motive, ActKind kind, int h, int s, int m)
        => motive == DesireKind.Fond && IsWarm(kind) && _fondWarmDay.TryGetValue((h, s), out int last)
           && Clock.Day(m) - last < _fo.Acts.FondWarmDays;

    /// <summary>Fond was answered with a light kind act: its wait starts.</summary>
    private void FondWarmed(DesireKind motive, ActKind kind, int h, int s, int m)
    {
        if (motive == DesireKind.Fond && IsWarm(kind))
            _fondWarmDay[(h, s)] = Clock.Day(m);
    }

    private IReadOnlyList<CatalogWatched> CatalogWatch() => _catalogWatch;
}
