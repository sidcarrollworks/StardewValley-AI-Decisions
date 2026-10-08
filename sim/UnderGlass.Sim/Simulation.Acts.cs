namespace UnderGlass.Sim;

/// <summary>
/// The act catalog's rules (acts spec 3; Sid, 2026-10-08). Slice acts-0 builds the seams the gate
/// reads: Fits, which every act the gate offers must pass, and the pride term in its cost. Every
/// shipped row passes Fits (its hours are the whole day and its gate asks for nothing more) and has
/// no pride, so the town is unchanged. Slice acts-1 adds the rules for light kind acts (acts spec
/// 1): they stir no motive, a cold reading, the warm cap and the warm budget; and watch mode. Slice
/// acts-2 adds the per-head draws (3.2), the limits of a game and a treat, what follows them
/// (AfterAct), and Fond's gifts kept to occasions. The arrivals and the new motives come with
/// later slices.
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
        if (g.MinAudience > 0 && Audience(p, o) < g.MinAudience)
            return false;
        return KindFits(kind, p, _people[o], m);
    }

    /// <summary>The rules a catalog kind adds of its own (acts spec 3.1 and section 4).</summary>
    private bool KindFits(ActKind kind, Person p, Person o, int m) => kind.Name switch
    {
        // A game: at the saloon in the evening, outdoors by day; the two near in age, or both children.
        "PlayedGame" => (p.Place == "Saloon" ? Clock.OfDay(m) >= Clock.At(17) : Clock.OfDay(m) < Clock.At(19))
                        && (Math.Abs(p.V.Age - o.V.Age) <= 6 || p.V.Age < 13 && o.V.Age < 13),
        // A treat: both of age, and the purse holds a drink (the bar's own household treats free).
        "TreatedToDrink" => o.V.Age >= 18 && (!HasMoney || HouseholdOf(p.V.Name) == BarHousehold()
                                              || _purse.GetValueOrDefault(HouseholdOf(p.V.Name)) >= _mo.SaloonDrink),
        // A welcome: in a public place, once for each ordered pair.
        "Welcomed" => !p.Place.StartsWith("Home:", StringComparison.Ordinal) && !_welcomed.Contains((_index[p.V.Name], _index[o.V.Name])),
        _ => true,
    };

    private string? BarHousehold() => _ao.Keepers.TryGetValue("Saloon", out string? keeper) && _index.ContainsKey(keeper) ? HouseholdOf(keeper) : null;

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

    /// <summary>A light kind act (a thank-you, a compliment, a joke, a game), under its rules: those
    /// are Returns' rules (acts spec 2.3), and a catalog row's own slice brings them too (a game with
    /// Company alone). With them off, a light kind row of a town's own is treated as before the catalog.</summary>
    private bool IsWarm(ActKind k) => IsLight(k) && k.Affect is { Joy: > 0 }
        && (_fo.Acts.Returns || ActCatalog.SliceOf(k.Name) is { } slice && _fo.Acts.IsOn(slice));

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
    private bool WarmOnly(int h, Act act, ActKind kind, double felt, FeltRecord? rec, int m)
    {
        if (!IsWarm(kind))
            return false;
        if (rec is { Route: "Cold" })
        {
            Hurt(h, felt);
            if (_index.TryGetValue(act.Actor, out int a))
                StirRemorse(a, h, act, felt, m); // with Repair on: the joker saw it land
        }
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

    // ---- per-head draws and company (acts spec 3.2, 4.4, 4.5; slice acts-2) -------------------

    private readonly HashSet<(int Holder, int Day)> _fondGiftDay = new();

    /// <summary>
    /// The per-head draws (acts spec 3.2), each tick after the town's rate draws: for each per-head
    /// kind whose slice is on, in catalog order, each person in name order who is free, carries it,
    /// fits its age, place and hours and has someone to aim it at draws at weight x PerDay / 288. The
    /// draws are keyed apart (kind, name, minute), so they move no other draw, and stay the same per
    /// person as the town grows. They run with Company on (the slice that builds them), only while
    /// the gate acts, and not in watch mode.
    /// </summary>
    private void StartPerHead(int m)
    {
        if (!Acting || !_fo.Acts.Company || _fo.Acts.Watch)
            return;
        double ticksPerDay = Clock.MinutesPerDay / (double)Clock.TickMinutes;
        foreach (ActKind kind in _kinds)
        {
            if (!kind.PerHead || kind.PerDay <= 0 || ActCatalog.SliceOf(kind.Name) is not { } slice || !_fo.Acts.IsOn(slice)
                || !kind.OpenAt(Clock.OfDay(m)))
                continue;
            foreach (Person p in _people)
            {
                if (!p.V.Acts.TryGetValue(kind.Name, out double w) || w <= 0 || !Free(p, m) || !kind.FitsAge(p.V.Age)
                    || kind.Allowed.Count > 0 && !kind.Allowed.Contains(p.Place)
                    || IsLight(kind) && LightCapReached(kind, _index[p.V.Name], m)
                    || Rng.Unit(_seed, "perhead", kind.Name, p.V.Name, m.ToString()) >= w * kind.PerDay / ticksPerDay
                    || !HasTarget(kind, p, m))
                    continue;
                Begin(m, kind, p, injected: false);
            }
        }
    }

    /// <summary>With Company on, Fond gives a gift only on an occasion (acts spec question 1, answer
    /// b): the other's birthday or a festival, and once a day; between, it reaches for the small
    /// acts. Any other motive and kind may go ahead. In watch mode the rule holds only for the watch
    /// record's weighing (<paramref name="watching"/>), so the run is the town's own.</summary>
    private bool FondMayUse(DesireKind motive, ActKind kind, int h, int s, int m, bool watching)
    {
        if (!_fo.Acts.Company || _fo.Acts.Watch && !watching || motive != DesireKind.Fond || kind.Name != "GaveGift")
            return true;
        int day = Clock.Day(m);
        return (Calendar.IsBirthday(_cast[s].Birthday, day) || Calendar.FestivalOn(day) is not null) && !_fondGiftDay.Contains((h, day));
    }

    /// <summary>A Fond gift on an occasion was given: the day's one.</summary>
    private void FondGave(DesireKind motive, ActKind kind, int h, int m)
    {
        if (_fo.Acts.Company && !_fo.Acts.Watch && motive == DesireKind.Fond && kind.Name == "GaveGift")
            _fondGiftDay.Add((h, Clock.Day(m)));
    }

    /// <summary>What follows a catalog act as it ends (acts spec 3, AfterAct): a game warms the one who
    /// asked as company does; a drink bought for someone is paid for.</summary>
    private void AfterAct(Act act, ActKind kind, int m)
    {
        if (!Steering || ActCatalog.SliceOf(kind.Name) is null || act.Target is not { } t || !_index.TryGetValue(t, out int ti))
            return;
        int a = _index[act.Actor];
        if (kind.Name == "PlayedGame")
            AddMood(a, _fo.CompanyJoy * (0.5 + CharacterOf(a).Chattiness) * (E(a, ti) > -_fo.LoveAt ? 1 : -1));
        else if (kind.Name == "TreatedToDrink")
            Treat(act.Actor, t, m);
        else if (kind.Name == "Welcomed")
            Welcome(a, ti);
        else if (kind.Name == "Apologised")
            AnswerApology(act, a, ti, m);
    }

    // ---- welcome (acts spec 4.6; slice acts-3) ------------------------------------------------

    private readonly HashSet<(int Holder, int Subject)> _welcomed = new();

    /// <summary>Computed motives (acts spec 4.6): worked out each tick from the world, not stored, and
    /// asked at most once a day for a close call. Fond and Curious.</summary>
    private static bool Computed(DesireKind k) => k is DesireKind.Fond or DesireKind.Curious;

    /// <summary>Curiosity at what is new (law 13; acts spec 4.6), computed each tick as Fond is: a free
    /// holder feels it toward everyone in reach they know below NewAt and haven't welcomed, never kin
    /// or housemates, at CuriousBase x (0.5 + chattiness). Only with Welcome on.</summary>
    private IEnumerable<(Motive D, double I)> CuriousMotives(int h, Person p, int m)
    {
        if (!_fo.Acts.Welcome)
            yield break;
        double i = _fo.Acts.CuriousBase * (0.5 + CharacterOf(h).Chattiness);
        for (int s = 0; s < _people.Length; s++)
        {
            if (s == h || _fam[h, s] >= _fo.Acts.NewAt || _welcomed.Contains((h, s)) || Close(h, s) || !InReach(p, _people[s], m))
                continue;
            yield return (new Motive { Holder = h, Subject = s, Kind = DesireKind.Curious, Act = "Welcomed", Source = -1, Since = m, Felt = i }, i);
        }
    }

    /// <summary>A welcome ends: each knows the other WelcomeFamiliarity better, and it isn't asked again.</summary>
    private void Welcome(int a, int t)
    {
        _welcomed.Add((a, t));
        _fam[a, t] = Math.Min(1, _fam[a, t] + _fo.Acts.WelcomeFamiliarity);
        _fam[t, a] = Math.Min(1, _fam[t, a] + _fo.Acts.WelcomeFamiliarity);
    }

    /// <summary>A drink bought for someone (acts spec 4.5): the actor's household pays one drink to the
    /// bar's household, which restocks from outside, as Drinks does; it is the target's drink of the
    /// day if they had none (Drinks won't charge their household again). The bar's own household
    /// treats on the house. Money stays in town, so it is conserved to the gram.</summary>
    private void Treat(string actor, string target, int m)
    {
        _drank.Add((target, Clock.Day(m)));
        if (!HasMoney || BarHousehold() is not { } bar)
            return;
        string home = HouseholdOf(actor);
        if (home == bar)
            return;
        Move(home, bar, _mo.SaloonDrink);
        ToOutside(bar, _mo.SaloonDrink * _mo.SaloonRestock);
    }

    // ---- repair (acts spec 4.7; slice acts-4) -------------------------------------------------

    /// <summary>Accepted apologies, by apologiser, target and the hurt's kind, with their days: the
    /// excuse wears out (question 3, answer b).</summary>
    private readonly Dictionary<(int A, int T, string Kind), List<int>> _accepted = new();

    /// <summary>
    /// Remorse (acts spec 4.7), stirred in the actor of a heavy hostile act, or of a joke read cold,
    /// as the target takes it: the actor took part, so they saw it land. Only with Repair on, and
    /// only if the actor holds the target at RemorseAt or above, the act wasn't over the target's own
    /// scandal (they gave cause: the actor was in the right), and the two aren't kin or housemates
    /// (families cover). Felt: what the target felt x RemorseShare x (0.5 + understanding). It lasts
    /// RemorseDays. A kind motive, so the gate's guard holds: nobody apologises to someone they now
    /// dislike.
    /// </summary>
    private void StirRemorse(int a, int t, Act act, double targetFelt, int m)
    {
        if (!_fo.Acts.Repair || !Acting || a == t || Close(a, t) || St(_names[a], _names[t]) < _fo.Acts.RemorseAt)
            return;
        if (act.About >= 0 && Did(_names[t], act.About) && KindOf(_acts[act.About]).IsScandal)
            return; // over their own scandal: they gave cause
        double felt = targetFelt * _fo.Acts.RemorseShare * (0.5 + U(a));
        if (felt <= 0)
            return;
        if (_fo.Acts.Watch)
            _catalogWatch.Add(new CatalogWatched(m, _names[a], _names[t], DesireKind.Remorse, "Remorse", felt)); // watched: recorded, not stirred
        else
            Stir(a, t, DesireKind.Remorse, false, "Apologised", act.Id, felt, m);
    }

    /// <summary>After a heavy hostile act ends, its actor's remorse (acts spec 4.7), from what the
    /// target felt of it.</summary>
    private void RemorseAfter(Act act, ActKind kind, int m)
    {
        if (!_fo.Acts.Repair || !IsHeavyHostile(kind) || act.Target is not { } t || !_index.TryGetValue(t, out int ti)
            || !_index.TryGetValue(act.Actor, out int a) || !_felt.TryGetValue((ti, act.Id), out FeltRecord? rec))
            return;
        StirRemorse(a, ti, act, Math.Abs(rec.Mood), m);
    }

    /// <summary>
    /// The apology's answer, on the spot (acts spec 4.7; design B's Ask in its smallest form).
    /// Obliged = 0.5 x regard for the apologiser (if any) + 0.5 x familiarity + 0.3 x understanding;
    /// the cost = ApologyCost + dislike x (0.5 + retention). A margin beyond the close-call band decides;
    /// inside it, a draw keyed by the act at logistic(8 x margin), tilted by mood.
    /// </summary>
    private void AnswerApology(Act act, int a, int t, int m)
    {
        double regard = St(_names[t], _names[a]);
        double obliged = 0.5 * Math.Max(0, regard) + 0.5 * _fam[t, a] + 0.3 * U(t);
        double cost = _fo.Acts.ApologyCost + Math.Max(0, -regard) * (0.5 + Ret(t));
        double margin = obliged - cost;
        bool accepted = DesireMath.Call(margin, _fo) switch
        {
            "clear" => true,
            "no" => false,
            _ => Rng.Unit(_seed, "apology", act.Id.ToString())
                 < DesireMath.Tilted(DesireMath.CloseCallChance(margin), MoodOf(t), false, _fo),
        };
        foreach (int i in LifeOf(act))
            SetOutcome(i, accepted ? Outcome.Accepted : Outcome.Refused, m);
        DesireLog($"{m} apology {_names[a]} {_names[t]} act {act.About} {(accepted ? "accepted" : "refused")} margin {margin:+0.00;-0.00}");
        if (!accepted)
        {
            AddMood(a, -_fo.Acts.RefusedSting * Sens(a), act.Id); // refused: the apologiser smarts, the hurt stays
            return;
        }
        if (act.About < 0 || act.About >= _acts.Count)
            return;
        Act hurt = _acts[act.About];
        // Law 10's excuse, which wears out: half the first time for this kind of harm from this
        // person within ApologyDays, a quarter the second, then nothing.
        var key = (a, t, hurt.Kind);
        if (!_accepted.TryGetValue(key, out var days))
            _accepted[key] = days = new List<int>();
        days.RemoveAll(d => Clock.Day(m) - d >= _fo.Acts.ApologyDays);
        IReadOnlyList<double> excuse = _fo.Acts.ApologyExcuse;
        double share = excuse.Count == 0 ? 0 : excuse[Math.Min(days.Count, excuse.Count - 1)];
        days.Add(Clock.Day(m));
        if (share > 0 && _felt.TryGetValue((t, hurt.Id), out FeltRecord? rec)
            && rec.Entries.Find(e => e.Subject == a) is { Applied: < 0 } cost0)
            Move(t, a, -cost0.Applied * share, act.Id, "Apology", "Event", true, m);
        if (_hits.TryGetValue((t, a), out var hits))
            hits.Remove(hurt.Tick); // the fear it left is lifted
        if (_desires.TryGetValue((t, a, (int)DesireKind.Answer), out Motive? grudge) && grudge.Source == hurt.Id)
            _desires.Remove((t, a, (int)DesireKind.Answer)); // and the grudge it stirred
        if (_fo.StanceOn && _felt.TryGetValue((t, act.Id), out FeltRecord? warm))
            _stance[t] = DesireMath.StanceAfterKindness(_stance[t], Math.Abs(warm.Mood));
    }

    /// <summary>The life record's open entries for an act (its doer's and its target's). The record is
    /// written in time order, so the search stops at the act's own minute.</summary>
    private List<int> LifeOf(Act act)
    {
        var found = new List<int>();
        for (int i = _life.Count - 1; i >= 0 && _life[i].Tick >= act.Tick; i--)
            if (_life[i].ActId == act.Id && _life[i].Role is LifeRole.Did or LifeRole.Undergone && _life[i].Outcome == Outcome.Open)
                found.Add(i);
        return found;
    }
}
