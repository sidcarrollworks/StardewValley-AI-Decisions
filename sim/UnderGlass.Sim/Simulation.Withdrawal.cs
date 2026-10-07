namespace UnderGlass.Sim;

/// <summary>
/// Hermits, brawlers and moods that spread (phase 0d.6; Sid, 2026-10-07;
/// docs/under-glass/withdrawal-research.md and docs/under-glass/specs/0d6-spec.md). Step a measures:
/// for each person and day, kindness received from outside kin and household, days with company,
/// kindness left unanswered, free time out, and from them being left out (E), relative to the town.
/// It records only: it writes nothing to the log and draws nothing, so every pin holds. The rules
/// that read it each have a switch, off by default. Watching (WithdrawalWatch) runs every rule that
/// is switched on, records what it would do, and changes nothing.
/// </summary>
public sealed partial class Simulation
{
    private PersonDays[] _daily = Array.Empty<PersonDays>();
    /// <summary>Each pair's days together (an hour or more), a bit a day, today lowest; both ways.</summary>
    private uint[,] _pairMet = new uint[0, 0];
    /// <summary>The first day being left out is read from (a season's first day, with recovery).</summary>
    private int _windowFrom;
    /// <summary>What each 0d.6 rule did (watching: would have done): how often, and in sum.</summary>
    private readonly SortedDictionary<string, (int Count, double Sum)> _rules = new(StringComparer.Ordinal);
    /// <summary>Contagion (X3): what each person passed on and took over the run, and took today (the cap).</summary>
    private double[] _gave = Array.Empty<double>(), _caught = Array.Empty<double>(), _caughtNet = Array.Empty<double>(),
        _caughtToday = Array.Empty<double>();
    /// <summary>W2 (X1): the ticks of each person's hurts with no motive, for repetition.</summary>
    private List<int>[] _lasting = Array.Empty<List<int>>();
    /// <summary>X4: who held whom as a friend, for a friend lost.</summary>
    private bool[,] _friendLast = new bool[0, 0];

    /// <summary>X13: the 0d.6 rules record what they would do and change nothing.</summary>
    private bool Watching => _fo.WithdrawalWatch;

    /// <summary>Records what a rule did, or would do; true if it may act (not watching).</summary>
    private bool Note(string rule, double amount)
    {
        var (count, sum) = _rules.GetValueOrDefault(rule);
        _rules[rule] = (count + 1, sum + amount);
        return !Watching;
    }

    private void StartWithdrawal(int days)
    {
        int n = _names.Length;
        _daily = Enumerable.Range(0, n).Select(_ => new PersonDays(new int[days], new bool[days], new int[days], new int[days],
            new int[days], new double[days])).ToArray();
        _pairMet = new uint[n, n];
        _gave = new double[n];
        _caught = new double[n];
        _caughtNet = new double[n];
        _caughtToday = new double[n];
        _lasting = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        _friendLast = new bool[n, n];
        for (int h = 0; h < n; h++)
            for (int f = 0; f < n; f++)
                _friendLast[h, f] = h != f && E(h, f) >= _fo.FriendAt;
    }

    // ---- stance from hurts: the split of X10, and W2 (X1) -------------------------------------

    /// <summary>A stance after a hurt of this size: by boldness, as every hurt moves it; with
    /// expression on (X10), the held part pulls toward withdrawn whatever the boldness.</summary>
    private double StanceAfterHurt(int h, double felt)
    {
        double plain = DesireMath.StanceAfterHurt(_stance[h], felt, CharacterOf(h).Boldness);
        if (!_fo.ShowOn || !Acting)
            return plain;
        double show = Show(h);
        double split = WithdrawalMath.StanceAfterHeldHurt(_stance[h], felt, CharacterOf(h).Boldness, show, _fo);
        return Note("held", felt * (1 - show)) ? split : plain;
    }

    /// <summary>X10: how much of a feeling shows on this person now (1 with expression off).</summary>
    private double Show(int h) => _fo.ShowOn ? WithdrawalMath.Show(CharacterOf(h).Expression, MoodOf(h), _fo) : 1;

    /// <summary>X10: the part of a hurt that shows, which is what drives an answer (M2).</summary>
    private double Shown(int h, double felt)
    {
        if (!_fo.ShowOn || !Acting)
            return felt;
        double shown = felt * Show(h);
        return Note("shown", shown - felt) ? shown : felt;
    }

    /// <summary>
    /// W2 (X1): a hurt that stirs no motive still moves stance: a row at home, an act undergone
    /// (warned, taken in, questioned, service, a family row), being named over one's own scandal,
    /// kin's shame, a friend lost. Repetition sensitizes, up to double; a friend buffers (X4).
    /// </summary>
    private void Lasting(int h, double felt, double weight, int m, string rule)
    {
        if (!Acting || !_fo.StanceOn || felt <= 0)
            return;
        List<int> ticks = _lasting[h];
        ticks.RemoveAll(t => m - t >= _fo.HurtRepeatDays * Clock.MinutesPerDay);
        double f = felt * weight * WithdrawalMath.Repeated(ticks.Count, _fo) * Buffer(h, m);
        ticks.Add(m);
        if (Note(rule, f))
            _stance[h] = StanceAfterHurt(h, f);
    }

    /// <summary>
    /// A hurt from kin or a housemate (X1, X2): an argument, a squabble, kin robbing one's shop.
    /// Families cover, so as before nothing stirs; but the hurt moves stance with no motive, at half
    /// weight (W2); and when households argue and regard there is bad, an argument stirs an answer
    /// (Sid, 2026-10-07). At home, stance moves by W2 only, never by the answer's own hurt.
    /// </summary>
    private void HomeRow(string who, string s, int h, int si, Act act, ActKind kind, Affect row, int m)
    {
        if (row.Joy >= 0)
            return;
        double felt = _felt.TryGetValue((h, act.Id), out FeltRecord? rec) ? Math.Abs(rec.Mood) : Math.Abs(row.Joy) * Sens(h);
        if (felt <= 0)
            return;
        if (_fo.HomeHurtOn)
            Lasting(h, felt, _fo.HomeHurtWeight, m, "hurt at home");
        if (!_fo.HouseholdGateOn || !IsHeavyHostile(kind) || St(who, s) >= _fo.HomeCoverAt)
            return; // love covers at home
        if (!Note("household answer", felt))
            return;
        Hit(h, si, act.Tick);
        if (act.About >= 0 && Did(who, act.About) && KindOf(_acts[act.About]).IsScandal)
        {
            Life(m, h, si, act.Id, kind.Name, LifeRole.GaveCause, felt, true, false, Outcome.None);
            DesireLog($"{m} gave-cause {who} {s} act {act.Id}");
            return;
        }
        if (_fo.AnswerOn)
            Stir(h, si, DesireKind.Answer, true, "Argued", act.Id, Shown(h, felt), m);
    }

    // ---- X4, X5: left out, the friend buffer, inclusion discounted --------------------------------

    /// <summary>
    /// The friend buffer (X4; Frenkel, Laursen, Bukowski), on W1 and W2: a friend, someone outside
    /// kin and household the person holds at FriendAt or more and spent a day with in the last
    /// FriendSeenDays, cuts both to FriendBuffer; to FriendBufferWithdrawn when every such friend is
    /// withdrawn themselves (Rubin 2006). 1 with none, or with being left out off. The research's
    /// "mutual" is the holder's own regard: no rule reads another mind's regard; a friend's
    /// withdrawal is read as what shows (staying in, talking less).
    /// </summary>
    private double Buffer(int h, int m)
    {
        if (!_fo.LeftOutOn)
            return 1;
        int day = Clock.Day(m);
        double best = 1;
        for (int f = 0; f < _names.Length; f++)
        {
            if (f == h || Close(h, f) || E(h, f) < _fo.FriendAt || !_lastContactDay.TryGetValue((h, f), out int last)
                || day - last >= _fo.FriendSeenDays)
                continue;
            best = Math.Min(best, Stance(f) > _fo.FriendWithdrawnAt ? _fo.FriendBuffer : _fo.FriendBufferWithdrawn);
        }
        return best;
    }

    /// <summary>
    /// W1 (X4; Gazelle and Ladd; Gazelle and Rudolph): at night, being left out pushes a stance
    /// toward withdrawn by shy squared, sensitivity and low self-regard, less with a friend; the
    /// included shy come closer. And a friend lost (regard falling below FriendAt less a margin) is
    /// a W2 hurt.
    /// </summary>
    private void LeftOutRules(int day)
    {
        if (!Acting || !_fo.StanceOn || !_fo.LeftOutOn)
            return;
        int n = _names.Length;
        for (int i = 0; i < n; i++)
        {
            Temperament c = CharacterOf(i);
            double e = _daily[i].LeftOut[day];
            double push = WithdrawalMath.LeftOutPush(e, c.Boldness, Sens(i), c.SelfRegard, Buffer(i, _now), _fo,
                WithdrawalMath.Solitary(c.Chattiness, c.Boldness, _fo));
            if (push > 0 && Note("left out", push))
                _stance[i] = Math.Max(-1, _stance[i] - push);
            double pull = WithdrawalMath.IncludedPull(_stance[i], e, c.Boldness, Sens(i), _fo);
            if (pull > 0 && Note("included", pull))
                _stance[i] += pull;
        }
        for (int h = 0; h < n; h++)
        {
            for (int f = 0; f < n; f++)
            {
                if (f == h || Close(h, f))
                    continue;
                double r = E(h, f);
                if (r >= _fo.FriendAt)
                    _friendLast[h, f] = true;
                else if (_friendLast[h, f] && r < _fo.FriendAt - _fo.FriendLossMargin)
                {
                    _friendLast[h, f] = false;
                    Lasting(h, _fo.FriendLossHurt, 1, _now, "friend lost");
                }
            }
        }
    }

    /// <summary>
    /// X5 (Vanhalst; Masi): the share of a kindness that eases a withdrawn stance. From a friend, or
    /// inside a quarrel (a heavy hostile act between the two, either way, in FearDays), in full;
    /// from anyone else, InclusionShare. Combative stances keep the full share (a choice: it holds
    /// the brawler count).
    /// </summary>
    private double Inclusion(int h, int s, int m)
    {
        if (!Acting || !_fo.InclusionDiscountOn || _stance[h] >= _fo.WithdrawnAt || E(h, s) >= _fo.FriendAt || Quarrel(h, s, m))
            return 1;
        return Note("inclusion discounted", _fo.InclusionShare) ? _fo.InclusionShare : 1;
    }

    private bool Quarrel(int a, int b, int m)
        => _lastHostile.TryGetValue((a, b), out int t1) && m - t1 < _fo.FearDays * Clock.MinutesPerDay
           || _lastHostile.TryGetValue((b, a), out int t2) && m - t2 < _fo.FearDays * Clock.MinutesPerDay;

    // ---- X6: the dials -------------------------------------------------------------------------

    /// <summary>X6: the home's pull per point of withdrawn stance: StanceHomeDial with the dials on.</summary>
    private double StanceHomeNow(int i)
    {
        if (!_fo.DialsOn || !Acting || Stance(i) >= 0)
            return _fo.StanceHome;
        return Note("dial: home", (_fo.StanceHomeDial - _fo.StanceHome) * -Stance(i)) ? _fo.StanceHomeDial : _fo.StanceHome;
    }

    /// <summary>X6: chattiness in use: the withdrawn talk less.</summary>
    private double ChatInUse(int i)
    {
        double c = CharacterOf(i).Chattiness;
        if (!_fo.DialsOn || !Acting || Stance(i) >= 0)
            return c;
        double use = c * (1 + _fo.ChatStance * Stance(i));
        return Note("dial: chat", use - c) ? use : c;
    }

    /// <summary>X6: the withdrawn seek less, even those they love (Williams; Riva).</summary>
    private double Seeking(int h, double wish)
    {
        if (!_fo.DialsOn || !Acting || Stance(h) >= 0 || wish <= 0)
            return wish;
        double damped = wish * (1 + Stance(h));
        return Note("dial: seek", damped - wish) ? damped : wish;
    }

    // ---- X7: recovery -------------------------------------------------------------------------

    /// <summary>X7: how much of a stance is kept tonight: by retention, with recovery on.</summary>
    private double StanceKeepTonight(int i)
    {
        if (!_fo.RecoveryOn || !Acting)
            return _fo.StanceKeepPerDay;
        double keep = WithdrawalMath.StanceKeep(Ret(i), _fo);
        return Note("recovery: keep", keep - _fo.StanceKeepPerDay) ? keep : _fo.StanceKeepPerDay;
    }

    /// <summary>X7 (Masi: being understood helps most): one's own kindness returned eases a stance, in full.</summary>
    private void AnsweredKindly(int h, double felt)
    {
        if (!_fo.RecoveryOn || !Acting || !_fo.StanceOn || _stance[h] == 0)
            return;
        double eased = DesireMath.StanceAfterKindness(_stance[h], felt);
        if (Note("recovery: answered", eased - _stance[h]))
            _stance[h] = eased;
    }

    // ---- X8, X9: patience, and the coercion ratchet -------------------------------------------

    private readonly Dictionary<(int H, int S), (double Used, int Tick)> _patience = new();

    /// <summary>X8: a hostile round from s uses up one of h's rounds of patience with them.</summary>
    private void PatienceRound(int h, int s, int tick)
    {
        if (!_fo.PatienceOn || !Acting)
            return;
        var (used, last) = _patience.GetValueOrDefault((h, s));
        _patience[(h, s)] = (WithdrawalMath.Refilled(used, (tick - last) / (double)Clock.MinutesPerDay, _fo) + 1, tick);
    }

    /// <summary>X8: how far h's kind daring toward s has fallen, with the patience h has left.</summary>
    private double Impatience(int h, int s, int m)
    {
        if (!_fo.PatienceOn || !Acting || !_patience.TryGetValue((h, s), out var p))
            return 0;
        double left = WithdrawalMath.Patience(U(h), CharacterOf(h).Sensitivity, _fo)
                      - WithdrawalMath.Refilled(p.Used, (m - p.Tick) / (double)Clock.MinutesPerDay, _fo);
        double drop = WithdrawalMath.Impatience(left, _fo);
        return drop > 0 && Note("impatience", drop) ? drop : 0;
    }

    /// <summary>X9 (B2; Patterson): an argument met by keeping away makes the arguer bolder.</summary>
    private void Coerced(int arguer, double intensity)
    {
        if (!_fo.CoercionOn || !_fo.StanceOn || !Acting)
            return;
        double up = _fo.CoerceStep * intensity;
        if (Note("coerced", up))
            _stance[arguer] = Math.Min(1, _stance[arguer] + up);
    }

    // ---- X12: missing people -------------------------------------------------------------------

    /// <summary>X12: who has given their one occasion gift today.</summary>
    private readonly HashSet<(int Holder, int Day)> _occasionGiven = new();

    /// <summary>
    /// X12 (Sid, 2026-10-07): the wish to give to someone loved, in place of Fond's: missing them,
    /// sooner for the prone and less in a steady tie; and in a steady tie an occasion (their
    /// birthday, a festival), one occasion gift a person a day. Also whether the wish is an
    /// occasion's.
    /// </summary>
    private (double Wish, bool Occasion) Missing(int h, int s, int day, int daysApart, double fond)
    {
        if (!_fo.MissingOn || !Acting)
            return (fond, false);
        double regard = St(_names[h], _names[s]);
        double steady = WithdrawalMath.Steady(DaysTogether(h, s), _fo);
        double wish = WithdrawalMath.Missed(regard, daysApart, WithdrawalMath.Prone(CharacterOf(h), _fo), steady, _fo);
        double occasion = (Calendar.IsBirthday(_cast[s].Birthday, day) || Calendar.FestivalOn(day) is not null)
                          && !_occasionGiven.Contains((h, day)) ? WithdrawalMath.OccasionPart(regard, steady, _fo) : 0;
        if (occasion > 0)
            Note("missing: occasion", occasion);
        return Note("missing", wish + occasion - fond) ? (wish + occasion, occasion > 0) : (fond, false);
    }

    /// <summary>X12: hedonic adaptation: a kindness of the same kind from the same giver counts
    /// AdaptFactor less for each earlier one in AdaptDays (on top of rule 4's halving in a week).</summary>
    private double Adaptation(List<int> earlier, int tick)
    {
        if (!_fo.MissingOn || !Acting)
            return 1;
        int n = earlier.Count(t => t < tick && tick - t < _fo.AdaptDays * Clock.MinutesPerDay);
        double f = WithdrawalMath.Adapted(n, _fo);
        return n == 0 || !Note("adapted", f - 1) ? 1 : f;
    }

    // ---- X3: moods spread (C1) ----------------------------------------------------------------

    /// <summary>
    /// After a chat (from Company), each takes a little of the other's state: toward what the other
    /// shows, never past it; bad moods weigh more at home, less the more one loves the other; at most
    /// ContagionCap a day. Both entries come from the states before either is added. Contagion moves
    /// mood only, never regard, and its entries pass on like any mood.
    /// </summary>
    private void Catch(int a, int b)
    {
        double ea = Contagion(a, b), eb = Contagion(b, a);
        Take(a, b, ea);
        Take(b, a, eb);
    }

    private double Contagion(int a, int b)
        => WithdrawalMath.Contagion(StateOf(b, without: a) * Show(b), StateOf(a, without: b), Sens(a), Close(a, b), E(a, b), _fo);

    /// <summary>Someone's state for contagion: their mood, leaving out what they felt about acts the
    /// other also felt (sympathy and imitation already shared those), plus a share of their
    /// conditions (ContagionConditions; 0 is mood only).</summary>
    private double StateOf(int x, int without)
    {
        double sum = 0;
        foreach (var (tick, amount, src) in _affects[x])
            if (src < 0 || !_felt.ContainsKey((without, src)))
                sum += amount * Feelings.MoodWeight(_now - tick, _fo);
        double state = Feelings.Squash(sum);
        return _fo.ContagionConditions == 0 ? state : state + _fo.ContagionConditions * Conditions(x) / _fo.PowerScale;
    }

    private void Take(int a, int from, double e)
    {
        e = WithdrawalMath.Capped(e, _caughtToday[a], _fo);
        if (e == 0 || !Note("contagion", e))
            return;
        _caughtToday[a] += e;
        _caught[a] += Math.Abs(e);
        _caughtNet[a] += e;
        _gave[from] += Math.Abs(e);
        AddMood(a, e);
    }

    // ---- X0: measuring ------------------------------------------------------------------------

    /// <summary>A kindness received (from Underwent): counted when the giver is outside kin and household.</summary>
    private void TallyKindIn(int person, int by, ActKind kind, int m)
    {
        if (by >= 0 && IsKindAimed(kind) && !Close(person, by))
            _daily[person].KindIn[Clock.Day(m)]++;
    }

    /// <summary>An outcome settled (from SetOutcome): one's own kindness to someone outside kin and
    /// household, and whether it went unanswered. A kindness that returns theirs is left out: it is
    /// never returned again (no ping-pong), so it would always look unanswered.</summary>
    private void TallyOutcome(LifeEvent e, int m)
    {
        if (e.Role != LifeRole.Did || e.Hostile || e.Light || e.Outcome is Outcome.Open or Outcome.None
            || !_index.TryGetValue(e.Person, out int p) || !_index.TryGetValue(e.Other, out int o) || Close(p, o))
            return;
        if (e.ActId >= 0 && _acts[e.ActId].About is int about and >= 0 && _acts[about].Actor == e.Other && IsKindAimed(KindOf(_acts[about])))
            return;
        int day = Clock.Day(m);
        _daily[p].KindOut[day]++;
        if (e.Outcome == Outcome.Ignored)
            _daily[p].Unanswered[day]++;
    }

    /// <summary>At night, after the gate's own night: days with company, each pair's days together,
    /// and being left out; then the rules of 0d.6 that work by the night.</summary>
    private void CloseWithdrawal(int day)
    {
        if (!Desiring)
            return;
        int n = _names.Length;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                bool met = _together[i, j] >= _fo.ContactMinutes;
                uint bits = (_pairMet[i, j] << 1) | (met ? 1u : 0u);
                _pairMet[i, j] = _pairMet[j, i] = bits;
                if (met && !Close(i, j))
                    _daily[i].Met[day] = _daily[j].Met[day] = true;
            }
        }
        LeftOutTonight(day);
        LeftOutRules(day);
        if (_fo.RecoveryOn && Acting && day % Clock.DaysPerSeason == Clock.DaysPerSeason - 1 && Note("recovery: fresh start", 1))
            _windowFrom = day + 1; // X7: a fresh start each season (Gazelle and Faldowski 2019, VERIFY)
        Array.Clear(_caughtToday);
        _occasionGiven.RemoveWhere(x => x.Day <= day);
        for (int i = 0; i < n; i++)
            _stances[_names[i]][day] = _stance[i]; // again, after the night's rules
    }

    /// <summary>Being left out, E (X0), for everyone tonight: over the window, against the town's medians.</summary>
    private void LeftOutTonight(int day)
    {
        int n = _names.Length;
        int from = Math.Max(_windowFrom, day - _fo.LeftOutDays + 1);
        var kindIn = new int[n];
        var met = new int[n];
        var kindOut = new int[n];
        var unanswered = new int[n];
        for (int i = 0; i < n; i++)
        {
            PersonDays d = _daily[i];
            for (int k = from; k <= day; k++)
            {
                kindIn[i] += d.KindIn[k];
                met[i] += d.Met[k] ? 1 : 0;
                kindOut[i] += d.KindOut[k];
                unanswered[i] += d.Unanswered[k];
            }
        }
        double kindMedian = WithdrawalMath.Median(kindIn.Select(x => (double)x).OrderBy(x => x).ToList());
        double metMedian = WithdrawalMath.Median(met.Select(x => (double)x).OrderBy(x => x).ToList());
        double[] share = kindOut.Select((k, i) => k > 0 ? unanswered[i] / (double)k : double.NaN).ToArray();
        var given = share.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
        double shareMedian = given.Count == 0 ? double.NaN : WithdrawalMath.Median(given);
        bool fresh = _fo.RecoveryOn && Acting && day - from + 1 < _fo.FreshDays; // X7: a season's first week
        for (int i = 0; i < n; i++)
        {
            Temperament c = CharacterOf(i);
            _daily[i].LeftOut[day] = fresh ? 0 : WithdrawalMath.LeftOut(kindIn[i], kindMedian, met[i], metMedian, share[i], shareMedian,
                WithdrawalMath.Solitary(c.Chattiness, c.Boldness, _fo), _fo);
        }
    }

    /// <summary>Days together in the last four weeks for a pair (X12's steadiness).</summary>
    private int DaysTogether(int i, int j) => System.Numerics.BitOperations.PopCount(_pairMet[i, j] & 0x0FFFFFFFu);

    private IReadOnlyDictionary<string, PersonDays> Daily()
        => Desiring ? _names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => _daily[x.i]) : new Dictionary<string, PersonDays>();

    private IReadOnlyDictionary<string, (double Gave, double Caught, double Net)> ContagionTotals()
        => Desiring ? _names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => (_gave[x.i], _caught[x.i], _caughtNet[x.i]))
            : new Dictionary<string, (double, double, double)>();
}
