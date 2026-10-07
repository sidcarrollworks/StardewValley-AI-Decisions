namespace UnderGlass.Sim;

/// <summary>
/// The desire gate (phase 0d; design rule 10; Sid, 2026-10-07). Feelings give wants: being argued
/// with stirs a motive to answer, a kindness a motive to return it, a hurt from someone still loved
/// a motive to make up, a theft from one's shop or being named while innocent a motive to have it
/// out. A villager acts on a motive only when its subject is in reach and rule 10's gate says yes:
/// boldness + 0.5 x familiarity + 0.5 x intensity against the act's cost, decided in code outside a
/// band and by a seeded close call inside it. The hurt who cannot answer keep away, and the shy go
/// home. Motives come only from what the holder saw or took part in (and rule 9's keeper), never
/// from hearsay; families cover, so the gate never acts between kin or housemates. Everything is
/// read from the holder's own beliefs, regard and character, and what they perceive now.
/// It also keeps a record of each life's events for phase 0e's plasticity (rule 18).
/// </summary>
public sealed partial class Simulation
{
    public const string Snubbed = "Snubbed", TurnedAway = "TurnedAway";

    private sealed class Motive
    {
        public int Holder, Subject;
        public DesireKind Kind;
        public bool Hostile;
        public int Source;
        public int Since;
        public double Felt;
        public string Act = "";
        public double AskedAt = double.NaN;
        public int Asks;
    }

    private readonly SortedDictionary<(int Holder, int Subject, int Kind), Motive> _desires = new();
    private readonly Dictionary<(int Holder, int Subject, int Day), int> _slotsUsed = new();
    private readonly Dictionary<(int Actor, int Target), int> _lastHostile = new();
    private readonly Dictionary<(int Holder, int By), List<int>> _hits = new();
    private readonly SortedDictionary<(int Holder, int Subject), int> _avoid = new();
    private readonly List<(int Tick, string Holder, string Subject, int Source, int Until)> _avoids = new();
    private readonly Dictionary<(int Holder, int Day), int> _lightToday = new();
    private readonly HashSet<(int Holder, int Subject, int Day)> _turnedToday = new();
    private readonly Dictionary<(int Holder, int Subject), int> _lastContactDay = new();
    private readonly Dictionary<(int Holder, int Subject), int> _lastKindDay = new();
    private readonly HashSet<(int Holder, int Subject, int Day, string Kind)> _fondAsked = new();
    private readonly HashSet<(int I, int J, int Day)> _toned = new();
    private double[] _stance = null!;
    private readonly Dictionary<string, double[]> _stances = new();
    private readonly Dictionary<string, int[]> _outMinutes = new();
    private readonly List<Pursuit> _pursuits = new();
    private readonly List<Stirring> _stirred = new();
    private readonly List<LifeEvent> _life = new();
    private readonly List<string> _motiveLog = new();
    private readonly HashSet<int> _pursuedActs = new();
    private int _withdrawals, _marks;

    /// <summary>The gate runs: feelings steer and the desire switch is on.</summary>
    private bool Desiring => Steering && _fo.Desire;

    /// <summary>The gate acts (and avoids, and withdraws); off, motives are only watched.</summary>
    private bool Acting => Desiring && _fo.DesireActs;

    /// <summary>Kin or housemates: families cover, so the gate never acts between them.</summary>
    private bool Close(int a, int b) => _cast[a].Household == _cast[b].Household || AreKin(_names[a], _names[b]);

    private static bool IsLight(ActKind k) => k.Name is Snubbed or TurnedAway;

    /// <summary>A hostile act aimed at someone chosen, not a light one (an argument).</summary>
    private static bool IsHeavyHostile(ActKind k) => k.Affect is { Target: TargetIs.Chosen, Joy: < 0 } && !IsLight(k);

    private static bool IsKindAimed(ActKind k) => k.Affect is { Target: TargetIs.Chosen, Joy: > 0 };

    private double Stance(int i) => _fo.StanceOn ? _stance[i] : 0;

    private void DesireLog(string line)
    {
        if (_fo.DesireActs)
            _log.Add(line);
        else
            _motiveLog.Add(line);
    }

    private void StartDesire(int days)
    {
        _stance = new double[_names.Length];
        if (!Desiring)
            return;
        foreach (string n in _names)
        {
            _stances[n] = new double[days];
            _outMinutes[n] = new int[(days + Clock.DaysPerSeason - 1) / Clock.DaysPerSeason];
        }
    }

    /// <summary>Each tick: who is awake and away from home (the fringe line's hermit measure). Only read.</summary>
    private void CountOut(int m)
    {
        if (!Desiring)
            return;
        int season = Clock.Day(m) / Clock.DaysPerSeason;
        foreach (Person p in _people)
            if (!p.Asleep && p.Place != p.V.Home)
                _outMinutes[p.V.Name][season] += Clock.TickMinutes;
    }

    /// <summary>Within reach to act toward: free, in the same place, within NearTiles, in sight.</summary>
    private bool InReach(Person p, Person o, int m)
        => Free(o, m) && o.Place == p.Place && o.At.Chebyshev(p.At) <= _po.NearTiles
           && Perception.LineOfSight(_places[p.Place], p.At, o.At, _po) > 0;

    private bool Avoids(int h, int s, int m) => Acting && _fo.AvoidOn && _avoid.TryGetValue((h, s), out int until) && until > m;

    private int AvoidCount(int h, int m) => _avoid.Count(p => p.Key.Holder == h && p.Value > m);

    /// <summary>The home option's weight in a person's choice of where to spend free time: more for
    /// each person they keep away from, and for a withdrawn stance.</summary>
    private double HomeWeight(Person p, int m)
    {
        if (!Acting || !_fo.WithdrawOn)
            return 1.0;
        int i = _index[p.V.Name];
        return 1.0 + (_fo.AvoidOn ? AvoidCount(i, m) : 0) + _fo.StanceHome * Math.Max(0, -Stance(i));
    }

    /// <summary>The ordered pair had a heavy hostile act (an argument or a confrontation) less than
    /// HostileCooldownDays ago.</summary>
    private bool InHostileCooldown(int a, int o, int m)
        => Acting && _lastHostile.TryGetValue((a, o), out int last) && m - last < _fo.HostileCooldownDays * Clock.MinutesPerDay;

    /// <summary>Whether a rate-drawn act may go to this person: not to someone avoided, and no
    /// argument inside the hostile cooldown.</summary>
    private bool RateTargetAllowed(ActKind kind, int a, int o, int m)
    {
        if (!Acting)
            return true;
        if (Avoids(a, o, m))
            return false;
        return !(IsHeavyHostile(kind) && _lastHostile.TryGetValue((a, o), out int last) && m - last < _fo.HostileCooldownDays * Clock.MinutesPerDay);
    }

    // ---- where motives come from (R1) -------------------------------------------------------

    /// <summary>A belief stirs a motive toward whoever the holder believes did it to them. Called
    /// at the end of Add, for every stored or changed belief.</summary>
    private void StirFrom(string who, Belief b, Belief? prior, int m)
    {
        if (!Desiring)
            return;
        Act act = _acts[b.ActId];
        ActKind kind = KindOf(act);
        if (kind.Affect is not { } row || row.Patient != Patient.Target || b.Target != who || b.Actor is not { } s || s == who
            || prior?.Actor == s || !_index.TryGetValue(s, out int si))
            return;
        int h = _index[who];
        bool keeperScandal = row.Target == TargetIs.Keeper && kind.IsScandal && row.Joy < 0;
        if (b.Source != Source.Witnessed && !keeperScandal)
            return; // seen, not heard (the mod's D34); rule 9's keeper excepted
        if (Close(h, si))
            return;
        if (prior?.Actor is { } old && old != s && _index.TryGetValue(old, out int oi))
            foreach (var key in _desires.Where(p => p.Key.Holder == h && p.Key.Subject == oi && p.Value.Source == act.Id).Select(p => p.Key).ToList())
            {
                Motive gone = _desires[key];
                _desires.Remove(key);
                Life(m, h, oi, act.Id, kind.Name, LifeRole.Dropped, gone.Felt, gone.Hostile, false, Outcome.None);
            }
        double felt = _felt.TryGetValue((h, act.Id), out FeltRecord? rec) ? Math.Abs(rec.Mood) : Math.Abs(row.Joy) * Sens(h);
        if (felt <= 0)
            return;
        if (row.Target == TargetIs.Chosen && row.Joy < 0)
        {
            if (!IsLight(kind))
                Hit(h, si, act.Tick);
            Hurt(h, felt);
            if (act.About >= 0 && Did(who, act.About) && KindOf(_acts[act.About]).IsScandal)
            {
                // Argued with over their own scandal: they gave cause, and feel shame, not a grudge (III P40 schol., VERIFY).
                Life(m, h, si, act.Id, kind.Name, LifeRole.GaveCause, felt, true, IsLight(kind), Outcome.None);
                DesireLog($"{m} gave-cause {who} {s} act {act.Id}");
                return;
            }
            if (IsLight(kind) && act.About >= 0 && Did(who, act.About))
                return; // a light answer to one's own hostility stirs nothing more
            if (_fo.MakeUpOn && St(who, s) >= _fo.LoveAt)
                Stir(h, si, DesireKind.MakeUp, false, "GaveGift", act.Id, felt, m);
            else if (_fo.AnswerOn)
                Stir(h, si, DesireKind.Answer, true, "Argued", act.Id, felt, m);
        }
        else if (keeperScandal)
        {
            if (_fo.RetaliateOn)
            {
                Hurt(h, felt);
                Stir(h, si, DesireKind.Retaliate, true, "Argued", act.Id, felt, m);
            }
        }
        else if (row.Target == TargetIs.Chosen && row.Joy > 0 && _fo.ReturnOn)
        {
            if (act.About >= 0 && Did(who, act.About) && IsKindAimed(KindOf(_acts[act.About])))
                return; // the return of one's own kindness is not returned again
            Stir(h, si, DesireKind.Return, false, act.Kind, act.Id, felt, m);
            if (_fo.StanceOn)
                _stance[h] = DesireMath.StanceAfterKindness(_stance[h], felt);
        }
    }

    /// <summary>An innocent who was named resents the namer: a motive to have it out (F13).</summary>
    private void StirAccused(int s, int namer, int actId, double felt, int m)
    {
        if (!Desiring || !_fo.RetaliateOn || Close(s, namer) || felt <= 0)
            return;
        Hurt(s, felt);
        Stir(s, namer, DesireKind.Retaliate, true, "Argued", actId, felt, m);
    }

    private void Hurt(int h, double felt)
    {
        if (_fo.StanceOn)
            _stance[h] = DesireMath.StanceAfterHurt(_stance[h], felt, CharacterOf(h).Boldness);
    }

    private void Hit(int h, int by, int tick)
    {
        if (!_hits.TryGetValue((h, by), out var list))
            _hits[(h, by)] = list = new List<int>();
        list.Add(tick);
    }

    private int Hits(int h, int by, int m)
        => _hits.TryGetValue((h, by), out var list) ? list.Count(t => m - t < _fo.FearDays * Clock.MinutesPerDay) : 0;

    private void Stir(int h, int s, DesireKind kind, bool hostile, string act, int source, double felt, int m)
    {
        var key = (h, s, (int)kind);
        if (_desires.TryGetValue(key, out Motive? d))
        {
            d.Felt = Math.Min(1, EventPart(d, m) + felt);
            d.Since = m;
            d.Source = source;
            d.Act = act;
        }
        else
            _desires[key] = new Motive { Holder = h, Subject = s, Kind = kind, Hostile = hostile, Act = act, Source = source, Since = m, Felt = Math.Min(1, felt) };
        _stirred.Add(new Stirring(m, _names[h], _names[s], kind, source, felt));
        DesireLog($"{m} stirred {_names[h]} {kind} {_names[s]} act {source} felt {felt:0.00}");
    }

    private int Window(DesireKind k) => k == DesireKind.Pity ? _fo.PityMinutes : _fo.MotiveDays * Clock.MinutesPerDay;

    private double EventPart(Motive d, int m) => DesireMath.EventPart(d.Felt, d.Since, m, Window(d.Kind));

    /// <summary>A motive's intensity: what the event gave it, and for a hostile one, the grudge.
    /// Positive regard never adds (it only covers).</summary>
    private double Intensity(Motive d, int m)
        => DesireMath.Intensity(EventPart(d, m), d.Hostile, St(_names[d.Holder], _names[d.Subject]));

    /// <summary>Pity at a mishap seen (R4): when an act whose actor suffers it, with nobody to
    /// blame, ends, each witness who named them may want to help. Never at home.</summary>
    private void StirPity(Act act, ActKind kind, int m)
    {
        if (!Desiring || !_fo.PityOn || kind.Affect is not { Patient: Patient.Actor, Joy: < 0, Freedom: <= 0 } row)
            return;
        int x = _index[act.Actor];
        foreach (string w in _names)
        {
            if (w == act.Actor || !_beliefs[w].TryGetValue(act.Id, out Belief? b) || b.Actor != act.Actor
                || b.Source != Source.Witnessed || b.Clarity <= 0)
                continue;
            int h = _index[w];
            if (Close(h, x))
                continue;
            double felt = DesireMath.Pity(row.Joy, Sens(h), St(w, act.Actor), b.Clarity, _fo);
            if (felt > 0)
                Stir(h, x, DesireKind.Pity, false, "HelpedSomeone", act.Id, felt, m);
        }
    }

    // ---- the gate (R5, R6) ------------------------------------------------------------------

    private double Form(string kind) => kind switch
    {
        "GaveGift" => _fo.GiftCost, "HelpedSomeone" => _fo.HelpCost, Snubbed => _fo.SnubForm, _ => _fo.ArgueForm,
    };

    private double Min(string kind) => kind switch
    {
        "GaveGift" => _fo.GiftMin, "HelpedSomeone" => _fo.HelpMin, Snubbed => _fo.SnubMin, _ => _fo.ArgueMin,
    };

    /// <summary>The acts a motive may use, most expensive first.</summary>
    private List<ActKind> ActsFor(DesireKind k, string act, Person p, int s, int m)
    {
        string[] names = k switch
        {
            DesireKind.Answer or DesireKind.Retaliate => _fo.LightActsOn ? new[] { "Argued", Snubbed } : new[] { "Argued" },
            DesireKind.Return when act == "HelpedSomeone" => new[] { "HelpedSomeone", "GaveGift" },
            DesireKind.Pity => new[] { "HelpedSomeone" },
            _ => new[] { "GaveGift" },
        };
        int h = _index[p.V.Name];
        var list = new List<ActKind>();
        foreach (string n in names)
        {
            if (_kinds.FirstOrDefault(x => x.Name == n) is not { } kind || !kind.FitsAge(p.V.Age)
                || kind.Allowed.Count > 0 && !kind.Allowed.Contains(p.Place))
                continue;
            if (IsHeavyHostile(kind) && _lastHostile.TryGetValue((h, s), out int last) && m - last < _fo.HostileCooldownDays * Clock.MinutesPerDay)
                continue;
            if (IsLight(kind) && _lightToday.GetValueOrDefault((h, Clock.Day(m))) >= _fo.LightPerDay)
                continue;
            list.Add(kind);
        }
        return list;
    }

    /// <summary>Fear of greater harm: hostile acts the subject did to the holder lately, as the
    /// holder saw them, and the subject's household standing by.</summary>
    private double Fear(int h, int s, Person p, int m)
    {
        double fear = _fo.FearPerHit * Hits(h, s, m);
        if (_fo.FearPerKin > 0)
            for (int q = 0; q < _people.Length; q++)
                if (q != s && q != h && _cast[q].Household == _cast[s].Household && _cast[q].Household != _cast[h].Household
                    && InReach(p, _people[q], m))
                    fear += _fo.FearPerKin;
        return fear;
    }

    /// <summary>Each tick: every free holder weighs their motives toward people in reach, the
    /// strongest first, and acts on at most one.</summary>
    private void Pursue(int m)
    {
        if (!Desiring)
            return;
        int day = Clock.Day(m);
        foreach (var key in _desires.Where(p => EventPart(p.Value, m) <= 0).Select(p => p.Key).ToList())
        {
            Motive gone = _desires[key];
            _desires.Remove(key);
            DesireLog($"{m} lapsed {_names[gone.Holder]} {gone.Kind} {_names[gone.Subject]} act {gone.Source}");
            Life(m, gone.Holder, gone.Subject, gone.Source, gone.Act, LifeRole.Lapsed, gone.Felt, gone.Hostile, false, Outcome.None);
        }
        var byHolder = _desires.Values.GroupBy(d => d.Holder).ToDictionary(g => g.Key, g => g.ToList());
        for (int h = 0; h < _people.Length; h++)
        {
            Person p = _people[h];
            if (!Free(p, m))
                continue;
            var motives = byHolder.TryGetValue(h, out var mine) ? mine.Select(d => (D: d, I: Intensity(d, m))).ToList() : new List<(Motive D, double I)>();
            if (_fo.FondOn)
                motives.AddRange(FondMotives(h, p, m));
            if (motives.Count == 0)
                continue;
            foreach (var (d, I) in motives.OrderByDescending(x => x.I).ThenBy(x => x.D.Subject).ThenBy(x => (int)x.D.Kind).ToList())
                if (Weigh(p, h, d, I, m, day) || !Free(p, m))
                    break; // one act a tick: a turning away in the weighing counts too
        }
    }

    /// <summary>Love wants to be near (R3): a gift to someone loved, more the longer since a day together.</summary>
    private IEnumerable<(Motive D, double I)> FondMotives(int h, Person p, int m)
    {
        int day = Clock.Day(m);
        for (int s = 0; s < _people.Length; s++)
        {
            if (s == h || Close(h, s) || !InReach(p, _people[s], m))
                continue;
            int last = Math.Max(_lastContactDay.GetValueOrDefault((h, s)), _lastKindDay.GetValueOrDefault((h, s), int.MinValue));
            double i = DesireMath.Fond(St(_names[h], _names[s]), day - last, _fo);
            if (i > 0)
                yield return (new Motive { Holder = h, Subject = s, Kind = DesireKind.Fond, Act = "GaveGift", Source = -1, Since = m, Felt = i }, i);
        }
    }

    /// <summary>One motive through the gate. True if the holder acted (one act a tick).</summary>
    private bool Weigh(Person p, int h, Motive d, double I, int m, int day)
    {
        int s = d.Subject;
        Person o = _people[s];
        if (I <= 0 || !InReach(p, o, m))
            return false;
        string hn = p.V.Name, sn = o.V.Name;
        double st = St(hn, sn);
        if (d.Hostile ? st >= _fo.CoverAt : st <= -_fo.LoveAt)
            return false; // love covers; and nobody is kind to someone they dislike
        if (!d.Hostile && Avoids(h, s, m))
            return false;
        if (_slotsUsed.GetValueOrDefault((h, s, day)) >= _fo.SlotsPerDay)
            return false;
        double fam = _fam[h, s];
        double fear = d.Hostile ? Fear(h, s, p, m) : 0;
        double eff = DesireMath.Effective(CharacterOf(h).Boldness, Stance(h), d.Hostile, fam, I, PowerOf(h), _fo);
        var acts = ActsFor(d.Kind, d.Act, p, s, m);
        ActKind? chosen = null;
        bool declined = false, drew = false;
        double best = double.NaN;
        string call = "";
        double chance = 1;
        foreach (ActKind k in acts)
        {
            if (I < Min(k.Name))
                continue;
            double cost = DesireMath.Cost(Form(k.Name), d.Hostile, fear, _fo);
            double margin = eff - cost;
            best = double.IsNaN(best) ? margin : Math.Max(best, margin);
            string c = DesireMath.Call(margin, _fo);
            if (c == "clear")
            {
                chosen = k;
                call = "clear";
                Record(m, hn, sn, d, k.Name, I, eff, cost, margin, "clear", 1, true);
                break;
            }
            if (c == "no" || declined)
            {
                Record(m, hn, sn, d, k.Name, I, eff, cost, margin, "no", 0, false);
                continue;
            }
            // A close call. The earlier answer stands until the motive moves by AskAgainStep.
            if (d.Kind != DesireKind.Fond && !double.IsNaN(d.AskedAt) && Math.Abs(I - d.AskedAt) < _fo.AskAgainStep)
            {
                Record(m, hn, sn, d, k.Name, I, eff, cost, margin, "stands", 0, false);
                declined = true;
                continue;
            }
            if (d.Kind == DesireKind.Fond && !_fondAsked.Add((h, s, day, k.Name)))
            {
                declined = true;
                continue;
            }
            double pr = DesireMath.Tilted(DesireMath.CloseCallChance(margin), MoodOf(h), d.Hostile, _fo);
            var question = new Pursuit(m, hn, sn, d.Kind, d.Source, k.Name, I, eff, cost, margin, "close", pr, false, -1);
            bool yes = _fo.CloseCall?.Invoke(question) ?? Rng.Unit(_seed, "desire", hn, sn, d.Kind.ToString(),
                d.Kind == DesireKind.Fond ? day.ToString() : d.Source.ToString(), d.Asks.ToString(), k.Name) < pr;
            if (d.Kind != DesireKind.Fond)
            {
                d.AskedAt = I;
                d.Asks++;
            }
            drew = true;
            Record(m, hn, sn, d, k.Name, I, eff, cost, margin, yes ? "close-yes" : "close-no", pr, yes);
            if (yes)
            {
                chosen = k;
                call = "close yes";
                chance = pr;
                break;
            }
            declined = true;
            Life(m, h, s, d.Source, k.Name, LifeRole.Declined, I, d.Hostile, IsLight(k), Outcome.None);
        }
        if (chosen is null)
        {
            if (drew)
                _slotsUsed[(h, s, day)] = _slotsUsed.GetValueOrDefault((h, s, day)) + 1;
            if (d.Hostile && _fo.AvoidOn && Acting && !double.IsNaN(best)
                && (_fo.LightActsOn ? best < 0 : !drew && !declined && best < -_fo.ClearBand))
                StartAvoid(p, d, I, best, m);
            return false;
        }
        if (!Acting)
            return false; // watched only: the motive stays, nothing starts
        _slotsUsed[(h, s, day)] = _slotsUsed.GetValueOrDefault((h, s, day)) + 1;
        if (d.Kind != DesireKind.Fond)
            _desires.Remove((h, s, (int)d.Kind));
        _pursuedActs.Add(_acts.Count);
        DesireLog($"{m} desire {hn} {d.Kind} {sn} act {(d.Source >= 0 ? d.Source.ToString() : "regard")}: {chosen.Name} intensity {I:0.00} eff {eff:0.00} cost {DesireMath.Cost(Form(chosen.Name), d.Hostile, fear, _fo):0.00} {call}{(call == "close yes" ? $" p {chance:0.00}" : "")}");
        Begin(m, chosen, p, injected: false, target: sn, about: d.Source >= 0 ? d.Source : -1);
        return true;
    }

    private void Record(int m, string hn, string sn, Motive d, string kind, double I, double eff, double cost, double margin, string call, double p, bool acted)
        => _pursuits.Add(new Pursuit(m, hn, sn, d.Kind, d.Source, kind, I, eff, cost, margin, call, p, acted && _fo.DesireActs,
            acted && _fo.DesireActs ? _acts.Count : -1));

    // ---- avoidance, turning away, withdrawal (R9) -------------------------------------------

    private void StartAvoid(Person p, Motive d, double I, double best, int m)
    {
        int h = d.Holder, s = d.Subject;
        _desires.Remove((h, s, (int)d.Kind));
        int until = m + _fo.AvoidDays * Clock.MinutesPerDay;
        _avoid[(h, s)] = until;
        _avoids.Add((m, _names[h], _names[s], d.Source, until));
        DesireLog($"{m} avoid {_names[h]} {_names[s]} act {d.Source} margin {best:+0.00;-0.00}");
        Life(m, h, s, d.Source, d.Act, LifeRole.Avoided, I, true, false, Outcome.None);
        Resolve(h, s, LifeRole.Undergone, Outcome.Avoided, m);
        Resolve(s, h, LifeRole.Did, Outcome.Avoided, m);
        if (_fo.LightActsOn)
            TurnAway(p, h, s, d.Source, m);
    }

    /// <summary>The shy's cold shoulder: no daring needed, seen and felt, never retold.</summary>
    private void TurnAway(Person p, int h, int s, int source, int m)
    {
        int day = Clock.Day(m);
        if (!Free(p, m) || !InReach(p, _people[s], m) || _lightToday.GetValueOrDefault((h, day)) >= _fo.LightPerDay
            || !_turnedToday.Add((h, s, day)) || _kinds.FirstOrDefault(k => k.Name == TurnedAway) is not { } kind)
            return;
        _pursuedActs.Add(_acts.Count);
        Begin(m, kind, p, injected: false, target: _names[s], about: source >= 0 ? source : -1);
    }

    /// <summary>Each tick: someone avoiding a person in reach during free time goes home, if they
    /// could not answer them now (with light acts on: the bold who avoid stay).</summary>
    private void Withdraw(int m)
    {
        if (!Acting || !_fo.AvoidOn || !_fo.WithdrawOn || _avoid.Count == 0)
            return;
        foreach (var g in _avoid.Where(x => x.Value > m).GroupBy(x => x.Key.Holder).OrderBy(g => g.Key).ToList())
        {
            int h = g.Key;
            Person p = _people[h];
            if (!Free(p, m) || p.Why != "haunt" || !_places.ContainsKey(p.V.Home))
                continue;
            int? from = null;
            foreach (var x in g.OrderBy(x => x.Key.Subject))
            {
                int s = x.Key.Subject;
                if (!InReach(p, _people[s], m))
                    continue;
                if (_fo.LightActsOn)
                {
                    double margin = DesireMath.Effective(CharacterOf(h).Boldness, Stance(h), true, _fam[h, s], Math.Max(0, -St(_names[h], _names[s])), PowerOf(h), _fo)
                                    - DesireMath.Cost(_fo.ArgueForm, true, Fear(h, s, p, m), _fo);
                    if (margin >= -_fo.ClearBand)
                        continue; // they could answer: they stay
                }
                from = s;
                break;
            }
            if (from is not { } who)
                continue;
            if (_fo.LightActsOn)
                TurnAway(p, h, who, _avoids.Last(v => v.Holder == _names[h] && v.Subject == _names[who]).Source, m); // it answers the quarrel
            _withdrawals++;
            DesireLog($"{m} withdraws {p.V.Name} home from {_names[who]}");
            Life(m, h, who, -1, "", LifeRole.Withdrew, 0, true, false, Outcome.None);
            Goal(p, p.V.Home, DefaultTown.Sofa, p.GoalUntil, "home", null);
        }
    }

    // ---- acting, the mark, the tone (R7, R8, R13) -------------------------------------------

    /// <summary>Called by Begin for every act while the gate runs: the cooldown, the light cap, the
    /// last kindness, and the record (a Did, and how earlier acts between the two turned out).</summary>
    private void BeganAct(Act act, ActKind kind, int m)
    {
        if (!Desiring || act.Target is not { } t || !_index.TryGetValue(t, out int ti) || t == act.Actor)
            return;
        int a = _index[act.Actor];
        if (IsHeavyHostile(kind))
            _lastHostile[(a, ti)] = m;
        if (IsLight(kind))
            _lightToday[(a, Clock.Day(m))] = _lightToday.GetValueOrDefault((a, Clock.Day(m))) + 1;
        if (IsKindAimed(kind))
            _lastKindDay[(a, ti)] = Clock.Day(m);
        if (kind.Affect is not { } row || row.Patient == Patient.Actor)
            return; // an act the actor undergoes (warned, taken in) is no deed of theirs
        bool hostile = row.Joy < 0, light = IsLight(kind);
        if (kind.Affect.Target == TargetIs.Chosen)
        {
            // How the target's earlier act toward the actor turned out, from both sides.
            Outcome? outcome = null;
            if (FindOpen(ti, a, LifeRole.Did, m) is { } prev)
            {
                bool prevHostile = _life[prev].Hostile;
                outcome = prevHostile && kind.Name == TurnedAway ? Outcome.Avoided
                    : prevHostile && hostile ? Outcome.Answered
                    : !prevHostile && !hostile ? Outcome.Returned
                    : !prevHostile && hostile ? Outcome.Rebuffed : null;
                if (outcome is { } o)
                {
                    SetOutcome(prev, o, m);
                    if (FindOpen(a, ti, LifeRole.Undergone, m) is { } got)
                        SetOutcome(got, o, m);
                }
            }
        }
        Life(m, a, ti, act.Id, kind.Name, LifeRole.Did, Math.Abs(row.Joy), hostile, light, Outcome.Open);
    }

    /// <summary>After a light act ends: a third slight of the same kind from one person within a
    /// few days leaves a mark (rule 4).</summary>
    private void Mark(Act act, ActKind kind, int m)
    {
        if (!Acting || !_fo.LightActsOn || !IsLight(kind) || act.Target is not { } t || !_index.TryGetValue(t, out int ti))
            return;
        int since = m - _fo.MarkDays * Clock.MinutesPerDay;
        int count = _beliefs[t].Values.Count(b => b.Kind == kind.Name && b.Actor == act.Actor && b.Target == t
                                                  && b.Source == Source.Witnessed && _acts[b.ActId].Tick >= since);
        if (count < _fo.MarkCount)
            return;
        _marks++;
        DesireLog($"{m} mark {t} {act.Actor} {kind.Name} x{count}");
        Move(ti, _index[act.Actor], -_fo.MarkSize, act.Id, "Mark", "Event", true, m);
    }

    /// <summary>A day's first meeting, taken warm or curt by how the holder is (R13).</summary>
    private void Tone(int i, int j, int m)
    {
        if (!Acting || !_fo.ToneOn || !_toned.Add((i, j, Clock.Day(m))) || Close(i, j))
            return;
        var (warm, curt) = DesireMath.Tone(PowerOf(i), U(i), St(_names[i], _names[j]), _fo);
        double u = Rng.Unit(_seed, "tone", _names[i], _names[j], Clock.Day(m).ToString());
        int sign = u < warm ? 1 : u >= 1 - curt ? -1 : 0;
        if (sign == 0)
            return;
        double f = sign * _fo.ToneJoy * Sens(i);
        AddMood(i, f);
        Move(i, j, f * 0.3 * _fo.PlasticScale * Feelings.Keep(f, Ret(i), _fo), -2 - m, "Tone", "Event", true, m);
        DesireLog($"{m} tone {(sign > 0 ? "warm" : "curt")} {_names[i]} {_names[j]} because power {PowerOf(i):0.00} understanding {U(i):0.00} regard {St(_names[i], _names[j]):+0.00;-0.00}");
        if (sign < 0 && _fo.AnswerOn)
        {
            Hurt(i, Math.Abs(f));
            Stir(i, j, DesireKind.Answer, true, "Argued", -2 - m, Math.Abs(f), m); // no act: keyed by the minute, so each greeting draws anew
        }
    }

    // ---- the record (R12) --------------------------------------------------------------------

    private readonly Dictionary<(int Person, int Other, LifeRole Role), List<int>> _openLife = new();

    private void Life(int m, int person, int other, int actId, string kind, LifeRole role, double severity, bool hostile, bool light, Outcome outcome)
    {
        if (!Desiring)
            return;
        actId = actId >= 0 ? actId : -1; // a curt greeting's motive is keyed by its minute, not an act
        _life.Add(new LifeEvent(m, _names[person], other >= 0 ? _names[other] : "someone", actId, kind, role, severity, hostile, light,
            outcome, outcome == Outcome.Open ? -1 : m));
        if (outcome == Outcome.Open && other >= 0)
        {
            if (!_openLife.TryGetValue((person, other, role), out var list))
                _openLife[(person, other, role)] = list = new List<int>();
            list.Add(_life.Count - 1);
        }
    }

    /// <summary>A person underwent an act by someone (written where it is felt).</summary>
    private void Underwent(int person, string? by, int actId, double severity, int m)
    {
        if (!Desiring || actId < 0)
            return;
        ActKind kind = KindOf(_acts[actId]);
        int other = by is not null && _index.TryGetValue(by, out int o) ? o : -1;
        Life(m, person, other, actId, kind.Name, LifeRole.Undergone, severity, (kind.Affect?.Joy ?? 0) < 0, IsLight(kind),
            other >= 0 ? Outcome.Open : Outcome.None);
    }

    private int? FindOpen(int person, int other, LifeRole role, int m)
    {
        if (!_openLife.TryGetValue((person, other, role), out var list))
            return null;
        for (int k = list.Count - 1; k >= 0; k--)
        {
            int idx = list[k];
            if (_life[idx].Outcome == Outcome.Open && m - _life[idx].Tick <= _fo.OutcomeDays * Clock.MinutesPerDay)
                return idx;
        }
        return null;
    }

    private void Resolve(int person, int other, LifeRole role, Outcome outcome, int m)
    {
        if (_openLife.TryGetValue((person, other, role), out var list))
            foreach (int idx in list)
                if (_life[idx].Outcome == Outcome.Open)
                    SetOutcome(idx, outcome, m);
    }

    private void SetOutcome(int idx, Outcome o, int m) => _life[idx] = _life[idx] with { Outcome = o, ResolvedTick = m };

    // ---- night -------------------------------------------------------------------------------

    /// <summary>At 23:59, before the feelings' night: days together, stance, outcomes past their
    /// window, and pruning.</summary>
    private void CloseDesires(int day)
    {
        if (!Desiring)
            return;
        int n = _names.Length;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (_together[i, j] >= _fo.ContactMinutes)
                    _lastContactDay[(i, j)] = _lastContactDay[(j, i)] = day;
        for (int i = 0; i < n; i++)
        {
            if (_fo.StanceOn)
                _stance[i] *= _fo.StanceKeepPerDay;
            _stances[_names[i]][day] = _stance[i];
        }
        int end = (day + 1) * Clock.MinutesPerDay;
        foreach (var list in _openLife.Values)
        {
            for (int k = list.Count - 1; k >= 0; k--)
            {
                int idx = list[k];
                LifeEvent e = _life[idx];
                if (e.Outcome != Outcome.Open)
                    list.RemoveAt(k);
                else if (end - e.Tick > _fo.OutcomeDays * Clock.MinutesPerDay)
                {
                    SetOutcome(idx, e.Light ? Outcome.None : Outcome.Ignored, end - 1);
                    list.RemoveAt(k);
                }
            }
        }
        foreach (var key in _slotsUsed.Keys.Where(k => k.Day < day).ToList())
            _slotsUsed.Remove(key);
        foreach (var key in _lightToday.Keys.Where(k => k.Day < day).ToList())
            _lightToday.Remove(key);
        _turnedToday.RemoveWhere(k => k.Day < day);
        _toned.RemoveWhere(k => k.Day < day);
        _fondAsked.RemoveWhere(k => k.Day < day);
        foreach (var list in _hits.Values)
            list.RemoveAll(t => end - t >= _fo.FearDays * Clock.MinutesPerDay);
    }

    /// <summary>At the end of a run: what was still open is left Open (the window had not passed).</summary>
    private IReadOnlyList<LifeEvent> LifeEvents() => _life;
}
