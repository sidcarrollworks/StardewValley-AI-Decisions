namespace UnderGlass.Sim;

/// <summary>
/// Feelings (phase 0c; design section 3a, laws 1-12). Each villager has regard for every other
/// person (love and hate, law 3), regard for each kind of person (law 9), and a mood from the joy
/// and sadness of the last three days, which with their conditions gives a power of acting (laws
/// 1 and 2). Every belief a villager gets, and every act done to them, gives one felt amount: it
/// moves their mood, and is attributed to whoever they believe caused it, which moves their
/// regard. Everything is read from beliefs, never from the truth (rule 2); the only exception,
/// <see cref="Did"/>, is someone's knowledge of their own acts. Single-threaded, deterministic, no
/// randomness except the target draw.
/// </summary>
public sealed partial class Simulation
{
    private readonly FeelingOptions _fo;
    private double[,] _regard = null!, _baseline = null!, _trough = null!, _conquered = null!; // n x n
    private double[,] _kind = null!;          // n x kinds
    private string[] _kindNames = null!;      // distinct Villager.Kind, ordinal
    private int[] _kindOf = null!;            // villager index -> kind index
    private List<(int Tick, double Amount, int Src)>[] _affects = null!; // mood entries, last MoodDays; Src: the act felt, or -1
    private int[,] _together = null!;         // minutes together today, [min(i,j), max(i,j)]
    private bool[,] _slighted = null!;        // [h, j]: an act of j lowered h's regard today
    private int _now;                         // the current minute
    private readonly Dictionary<(int Holder, int ActId), FeltRecord> _felt = new();
    private readonly Dictionary<(int Holder, int ActId, int Subject), double> _actTotals = new(); // Subject: person i, or -(k+1)
    private readonly Dictionary<(int Cause, int Patient, string Kind), List<int>> _repeats = new();
    private readonly HashSet<(string Who, int ActId)> _did = new();       // self-knowledge only
    private readonly Dictionary<(int Kin, int ActId), HashSet<string>> _knownHolders = new();
    private readonly HashSet<(int Subject, int ActId, string Event, int Namer)> _eventsFelt = new();
    private readonly Dictionary<(string Holder, string Toward, string Name), Sentiment> _sentiments = new();
    private readonly List<Felt> _feltLog = new();
    private readonly List<(int Day, string A, string B, string What)> _ties = new();
    private readonly HashSet<(int, int, string)> _tied = new();
    private readonly List<(int Day, string Household, string From, string To)> _shopSwitches = new();
    private readonly Dictionary<string, double[]> _powerByDay = new();
    private readonly List<(int Day, double[] Regard)> _snapshots = new();
    private readonly Dictionary<int, double> _aimedAt = new();

    private sealed class FeltRecord
    {
        public bool Started;
        public int Patient = -1;
        public string Route = "";
        public double F0;
        public double Repeat = 1;
        public double S, Mood, W;
        public int Cause = -1;
        public bool NameHeard, Corroborated, Confirmed;
        public readonly List<Entry> Entries = new();
    }

    /// <summary>One subject's share of a feeling about an act: the change asked for, what was
    /// applied, and what counted against the per-act cap.</summary>
    private sealed class Entry
    {
        public int Subject;
        public string Route = "";
        public double Raw, Applied, Capped;
    }

    private bool Steering => _fo.Enabled && _fo.Steer;

    // ---- public reads, for tests and the runner (no rule reads another mind through these) ----

    /// <summary>Effective regard of a for b: personal, plus regard for b's kind as far as a doesn't
    /// know b. This is what decisions read.</summary>
    public double Regard(string a, string b) => _fo.Enabled && a != b ? E(_index[a], _index[b]) : 0;
    public double PersonalRegard(string a, string b) => _fo.Enabled && a != b ? _regard[_index[a], _index[b]] : 0;
    public double Baseline(string a, string b) => _fo.Enabled && a != b ? _baseline[_index[a], _index[b]] : 0;
    public double KindRegard(string a, string kind)
        => _fo.Enabled && Array.IndexOf(_kindNames, kind) is var k and >= 0 ? _kind[_index[a], k] : 0;
    public double Mood(string a) => _fo.Enabled ? MoodOf(_index[a]) : 0;
    public double Power(string a) => _fo.Enabled ? PowerOf(_index[a]) : 0.5;
    public IReadOnlyList<Sentiment> SentimentsOf(string a)
        => _sentiments.Values.Where(s => s.Holder == a).OrderByDescending(s => s.Strength).ThenBy(s => s.Toward, StringComparer.Ordinal)
            .ThenBy(s => s.Name, StringComparer.Ordinal).ToList();

    // ---- seeds (F1) ------------------------------------------------------------------------

    private void StartFeelings()
    {
        int n = _names.Length;
        _regard = new double[n, n];
        _baseline = new double[n, n];
        _trough = new double[n, n];
        _conquered = new double[n, n];
        _kindNames = _cast.Select(v => v.Kind).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToArray();
        _kindOf = _cast.Select(v => Array.IndexOf(_kindNames, v.Kind)).ToArray();
        _kind = new double[n, _kindNames.Length];
        _affects = Enumerable.Range(0, n).Select(_ => new List<(int, double, int)>()).ToArray();
        _together = new int[n, n];
        _slighted = new bool[n, n];
        if (!_fo.Enabled)
            return;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (i != j)
                    _regard[i, j] = _baseline[i, j] = SeedRegard(_cast[i], _cast[j]);
        foreach (var ((from, to), v) in _fo.Start.OrderBy(p => p.Key.From, StringComparer.Ordinal).ThenBy(p => p.Key.To, StringComparer.Ordinal))
            if (from != to && _index.TryGetValue(from, out int i) && _index.TryGetValue(to, out int j))
                _regard[i, j] = _baseline[i, j] = Math.Clamp(v, -1, 1);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                _trough[i, j] = Math.Min(0, _regard[i, j]);
    }

    /// <summary>Where regard starts and heals back to (rule 5): housemates, friends, everyone
    /// else neutral; nobody knows the newcomer yet.</summary>
    private double SeedRegard(Villager a, Villager b)
    {
        if (a.Name == DefaultTown.Newcomer || b.Name == DefaultTown.Newcomer)
            return 0;
        if (a.Household == b.Household)
            return _fo.Household;
        if (a.Friends.Contains(b.Name) || b.Friends.Contains(a.Name))
            return _fo.Friend;
        return 0;
    }

    private double Sens(int i) => 0.5 + CharacterOf(i).Sensitivity;
    private double U(int i) => CharacterOf(i).Understanding;
    private double SR(int i) => CharacterOf(i).SelfRegard;
    private double Ret(int i) => CharacterOf(i).Retention;

    /// <summary>Effective regard: personal regard plus prejudice toward the other's kind, which
    /// weighs as far as they are a stranger (law 9).</summary>
    private double E(int i, int j)
        => i == j ? 0 : _fo.Kinds ? Math.Clamp(_regard[i, j] + (1 - _fam[i, j]) * _kind[i, _kindOf[j]], -1, 1) : _regard[i, j];

    /// <summary>The only regard a decision reads: 0 unless feelings steer.</summary>
    private double St(string a, string b) => Steering && a != b ? E(_index[a], _index[b]) : 0;

    /// <summary>Whether someone did an act. Self-knowledge: only ever asked about the subject's own acts.</summary>
    private bool Did(string who, int actId) => _did.Contains((who, actId));

    // ---- mood and the power of acting (F16) ------------------------------------------------

    /// <param name="src">The act the feeling is about, or -1 (company, the tone, contagion): so
    /// contagion can leave out what both people felt (0d.6, X3).</param>
    private void AddMood(int i, double a, int src = -1)
    {
        if (a != 0)
            _affects[i].Add((_now, a, src));
    }

    private double MoodOf(int i)
    {
        double sum = 0;
        foreach (var (tick, amount, _) in _affects[i])
            sum += amount * Feelings.MoodWeight(_now - tick, _fo);
        return Feelings.Squash(sum);
    }

    /// <summary>What presses on someone (law 1): need, an unmet want, being held.</summary>
    private double Conditions(int i)
    {
        Person p = _people[i];
        double c = p.DetainedUntil > _now ? _fo.HeldPower : 0;
        if (HasMoney)
            c += _fo.NeedPower * NeedPressure(_names[i]) + _fo.WantPower * WantPressure(_names[i], _now);
        return -c;
    }

    private double PowerOf(int i) => Feelings.PowerOf(Conditions(i), MoodOf(i), _fo);

    /// <summary>After a chat: company is a small joy, or a small sadness with someone disliked.
    /// Mood only, so people who talk all day don't climb toward love just from talking.</summary>
    private void Company(Person pa, Person pb)
    {
        if (!_fo.Enabled)
            return;
        int a = _index[pa.V.Name], b = _index[pb.V.Name];
        AddMood(a, _fo.CompanyJoy * (0.5 + CharacterOf(a).Chattiness) * (E(a, b) > -_fo.LoveAt ? 1 : -1));
        AddMood(b, _fo.CompanyJoy * (0.5 + CharacterOf(b).Chattiness) * (E(b, a) > -_fo.LoveAt ? 1 : -1));
        if (Acting && _fo.ContagionOn)
            Catch(a, b); // 0d.6 (X3): moods spread
    }

    // ---- feeling a belief (F3-F11, F15) ----------------------------------------------------

    /// <summary>The patient and the cause of an act as the holder believes it (F3): never the truth.</summary>
    private (int X, int C) PatientAndCause(int h, Belief b, Affect row)
    {
        int Idx(string? n) => n is not null && _index.TryGetValue(n, out int i) ? i : -1;
        return row.Patient switch
        {
            Patient.Target => (Idx(b.Target), Idx(b.Actor)),
            Patient.Actor => (Idx(b.Actor), row.Freedom > 0 ? Idx(b.Target) : -1),
            // Onlookers: whoever saw it; told, the witness the story came from.
            _ => (b.Source == Source.Told && b.Chain.Count > 0 ? Idx(b.Chain[^1]) : h, Idx(b.Actor)),
        };
    }

    /// <summary>
    /// The one entry point for beliefs (F11), called whenever a belief is stored or changed. It
    /// resolves who was harmed or pleased and who caused it from the belief, works out what is
    /// felt and how much is believed, moves mood only up, and brings the regard changes this act
    /// stands for into line by recomputing them and applying the difference: a cause that changes
    /// takes the feeling with it, exactly.
    /// </summary>
    private void Feel(string who, Belief b, Belief? prior, string? teller, int m)
    {
        Act act = _acts[b.ActId];
        ActKind kind = KindOf(act);
        if (kind.Affect is not { } row || row.Joy == 0)
            return;
        int h = _index[who];
        var (x, c) = PatientAndCause(h, b, row);
        if (c == h)
            return; // they caused it
        if (!_felt.TryGetValue((h, b.ActId), out FeltRecord? rec))
            _felt[(h, b.ActId)] = rec = new FeltRecord();
        bool fresh = !rec.Started;
        if (fresh || x != rec.Patient)
        {
            bool known = x >= 0 && x != h;
            double joy = ReadJoy(h, x, act, row); // the act catalog: a warm act read cold is taken badly
            (rec.F0, rec.Route) = Feelings.Base(joy, Sens(h), x == h, known, known ? E(h, x) : 0,
                known ? Feelings.Likeness(_cast[h], _cast[x]) : 0, U(h), row.Patient == Patient.Onlookers, _fo);
            if (joy != row.Joy)
                rec.Route = "Cold"; // read cold: its own route, so the measures can count it
            rec.Patient = x;
        }
        if (fresh)
        {
            rec.Started = true;
            if (rec.Route == "Direct" && row.Joy > 0 && c >= 0)
            {
                // A kindness repeated within a week counts half each time (rule 4).
                if (!_repeats.TryGetValue((c, h, act.Kind), out var ticks))
                    _repeats[(c, h, act.Kind)] = ticks = new List<int>();
                foreach (int t in ticks)
                    if (t < act.Tick && act.Tick - t < _fo.RepeatDays * Clock.MinutesPerDay)
                        rec.Repeat *= 0.5;
                rec.F0 *= Adaptation(ticks, act.Tick); // 0d.6 (X12): repeated gifts count for less
                ticks.Add(act.Tick);
            }
        }
        if (!fresh && c != rec.Cause)
            rec.Corroborated = rec.Confirmed = false; // they were about someone else
        if (teller is not null && b.Source != Source.Told && b.Actor != prior?.Actor)
            rec.NameHeard = true; // saw or found it, and heard the name: confirmed first-hand (rule 9)

        var (s, w, basis) = Presence(h, b, rec, c, row);
        rec.S = Math.Max(rec.S, s);
        double mood = rec.F0 * rec.S;
        if (Math.Abs(mood) > Math.Abs(rec.Mood))
        {
            double d = mood - rec.Mood;
            rec.Mood = mood;
            AddMood(h, d, act.Id);
            _feltLog.Add(new Felt(m, who, act.Id, rec.Route, basis, d, null, 0, 0));
        }
        if (fresh && rec.Route is "Direct" or "Cold")
            Underwent(h, c >= 0 ? _names[c] : null, act.Id, Math.Abs(rec.Mood), m);
        rec.W = c == rec.Cause ? Math.Max(rec.W, w) : w;
        rec.Cause = c;

        var desired = new SortedDictionary<int, (double Raw, string Route)>();
        void Want(int subject, double raw, string route)
        {
            if (raw == 0)
                return;
            desired[subject] = desired.TryGetValue(subject, out var d) ? (d.Raw + raw, d.Route) : (raw, route);
        }
        double keep = Feelings.Keep(rec.F0 * rec.S, Ret(h), _fo);
        if (c >= 0)
        {
            // F7: regard toward the believed cause, by how freely they acted.
            double phi = Feelings.Phi(row.Freedom, Excuse(h, c), _fo);
            double dr = WarmBudgeted(h, c, act, kind, rec.F0 * rec.W * row.Plastic * _fo.PlasticScale * phi * keep * rec.Repeat);
            Want(c, dr, rec.Route);
            // F9b: a little of it spills onto their kind, as far as they are a stranger (III P46).
            if (_fo.Kinds)
                Want(-(_kindOf[c] + 1), _fo.KindShare * (1 - _fam[h, c]) * (1 - 0.5 * U(h)) * dr, "Spill");
            // F10: a scandal costs the culprit's kin some standing (rule 17).
            if (_fo.Association && dr < 0 && kind.IsScandal && _cast[c].Family is { } family)
                foreach (string q in family.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    if (_index.TryGetValue(q, out int qi) && qi != h && !AreKin(who, q))
                        Want(qi, _fo.AssocShare * (1 - _fam[h, qi]) * (1 - 0.5 * U(h)) * dr, "Association");
        }
        else if (_fo.Kinds && b.Source == Source.Witnessed && b.Actor is null && b.SeenKind is { } seen && row.Patient != Patient.Actor
                 && Array.IndexOf(_kindNames, seen) is var k and >= 0)
        {
            // F9a: "someone, a young man": the feeling goes to the kind of person seen (law 9).
            double freedom = _fo.Freedom ? row.Freedom : 1;
            double seenWeight = _fo.Presence ? b.Clarity : 1;
            Want(-(k + 1), rec.F0 * seenWeight * row.Plastic * _fo.PlasticScale * freedom * keep * (1 - 0.5 * U(h)), "Kind");
        }
        Reconcile(h, rec, desired, act.Id, basis, m);
    }

    /// <summary>How present a belief is (law 11; rule 9): s weighs mood, w weighs the regard
    /// change. Seen close and named counts most; hearsay moves mood only, until a second
    /// independent teller agrees or a public consequence confirms it.</summary>
    private (double S, double W, string Basis) Presence(int h, Belief b, FeltRecord rec, int c, Affect row)
    {
        double s, w;
        string basis = b.Source.ToString();
        if (!_fo.Presence)
        {
            s = 1;
            w = c >= 0 || b.SeenKind is not null ? 1 : 0;
        }
        else if (b.Source == Source.Told)
        {
            int t0 = b.Chain.Count > 0 && _index.TryGetValue(b.Chain[0], out int t) ? t : -1;
            s = _fo.HearsayMood * (t0 >= 0 ? Feelings.Credence(_fam[h, t0], E(h, t0), U(h), _fo) : 0.5);
            w = 0;
            if (rec.Corroborated)
            {
                w = _fo.CorroboratedWeight * b.Confidence;
                basis = "Corroborated";
            }
        }
        else
        {
            s = b.Clarity;
            w = 0;
            if (rec.NameHeard)
            {
                w = _fo.HeardNameWeight * b.Clarity * b.Confidence;
                basis = "HeardName";
            }
            else if (b.Source == Source.Witnessed && c >= 0)
                w = b.Clarity * (row.Patient == Patient.Actor ? 1 : b.Confidence);
        }
        if (rec.Confirmed && _fo.Presence)
        {
            w = Math.Max(w, _fo.HeardNameWeight * b.Confidence);
            basis = "Confirmed";
        }
        return (s, w, basis);
    }

    /// <summary>A known hardship (F6): housemates know the purse was short.</summary>
    private double Excuse(int h, int c)
        => HasMoney ? Feelings.Excuse(_cast[h].Household == _cast[c].Household, NeedPressure(_names[c]), U(h)) : 0;

    /// <summary>Brings the regard changes an act stands for into line with what is now believed
    /// (F11): changes for subjects no longer blamed, or now felt the other way, are taken back
    /// exactly; new ones are applied; a stronger feeling applies only the difference.</summary>
    private void Reconcile(int h, FeltRecord rec, SortedDictionary<int, (double Raw, string Route)> desired, int actId, string basis, int m)
    {
        foreach (Entry e in rec.Entries.ToList())
        {
            // A kind blamed for "someone" (route Kind) and the spill from a named person onto their
            // kind (route Spill) are different feelings: one never carries over into the other.
            if (desired.TryGetValue(e.Subject, out var d) && Math.Sign(d.Raw) == Math.Sign(e.Raw)
                && (e.Subject >= 0 || d.Route == e.Route))
                continue;
            Revert(h, e, actId, basis, m);
            rec.Entries.Remove(e);
        }
        foreach (var (subject, (raw, route)) in desired)
        {
            Entry? e = rec.Entries.Find(x => x.Subject == subject);
            if (e is null)
            {
                var (applied, capped) = Move(h, subject, raw, actId, route, basis, subject == rec.Cause, m);
                rec.Entries.Add(new Entry { Subject = subject, Route = route, Raw = raw, Applied = applied, Capped = capped });
            }
            else if (Math.Abs(raw) > Math.Abs(e.Raw))
            {
                var (applied, capped) = Move(h, subject, raw - e.Raw, actId, route, basis, subject == rec.Cause, m);
                e.Raw = raw;
                e.Applied += applied;
                e.Capped += capped;
            }
        }
    }

    private string SubjectName(int subject) => subject >= 0 ? _names[subject] : "kind:" + _kindNames[-subject - 1];

    /// <summary>
    /// Moves h's regard for a person or a kind (F8), within the cap per act. Regard saturates the
    /// way it leans. Love from someone h hated, carrying regard past zero, gains back up to the
    /// depth of that hate, bounded by the love given since (law 8; III P44, VERIFY). byThem: the
    /// subject is the one whose act this is.
    /// </summary>
    private (double Applied, double Capped) Move(int h, int subject, double raw, int actId, string route, string basis, bool byThem, int m)
    {
        var tk = (h, actId, subject);
        double used = _actTotals.GetValueOrDefault(tk);
        double capped = Math.Sign(raw) * Math.Min(Math.Abs(raw), Math.Max(0, _fo.MaxPerAct - used));
        if (capped == 0)
            return (0, 0);
        _actTotals[tk] = used + Math.Abs(capped);
        string toward = SubjectName(subject);
        double before, after, bonus = 0;
        if (subject >= 0)
        {
            int j = subject;
            before = _regard[h, j];
            after = Feelings.Saturate(before, capped);
            if (byThem && capped > 0)
                _conquered[h, j] += after - before;
            if (_fo.Reconcile && byThem && capped > 0 && before < 0 && after >= 0)
                bonus = Feelings.ReconcileBonus(_trough[h, j], _conquered[h, j], _fo);
            after = Math.Clamp(after + bonus, -1, 1);
            _regard[h, j] = after;
            if (after < _trough[h, j])
            {
                _trough[h, j] = after;
                _conquered[h, j] = 0;
            }
            if (after >= 0)
                _trough[h, j] = _conquered[h, j] = 0;
            if (capped < 0 && byThem)
                _slighted[h, j] = true;
        }
        else
        {
            int k = -subject - 1;
            before = _kind[h, k];
            after = Math.Clamp(before + capped, -1, 1);
            _kind[h, k] = after;
        }
        double change = after - before;
        _feltLog.Add(new Felt(m, _names[h], actId, route, basis, 0, toward, raw, change));
        if (Math.Abs(change) >= _fo.LogAt)
            _log.Add($"{m} regard {_names[h]} {toward} {change:+0.000;-0.000} -> {after:0.000} act {actId} {route}");
        if (Math.Abs(change - bonus) >= _fo.SentimentMin && Feelings.SentimentName(route, change - bonus) is { } name)
            AddSentiment(_names[h], toward, name, actId, Math.Abs(change - bonus), m);
        if (bonus > 0)
        {
            _log.Add($"{m} reconciled {_names[h]} {toward} bonus {bonus:0.000} act {actId}");
            AddSentiment(_names[h], toward, "Reconciled", actId, bonus, m);
            _ties.Add((Clock.Day(m), _names[h], toward, "reconciled"));
        }
        return (change, capped);
    }

    /// <summary>Takes back a regard change exactly (F11), and the sentiments that cite it.</summary>
    private void Revert(int h, Entry e, int actId, string basis, int m)
    {
        string toward = SubjectName(e.Subject);
        double after;
        if (e.Subject >= 0)
        {
            int j = e.Subject;
            after = _regard[h, j] = Math.Clamp(_regard[h, j] - e.Applied, -1, 1);
            if (after < _trough[h, j])
            {
                _trough[h, j] = after;
                _conquered[h, j] = 0;
            }
        }
        else
        {
            int k = -e.Subject - 1;
            after = _kind[h, k] = Math.Clamp(_kind[h, k] - e.Applied, -1, 1);
        }
        var tk = (h, actId, e.Subject);
        _actTotals[tk] = Math.Max(0, _actTotals.GetValueOrDefault(tk) - Math.Abs(e.Capped));
        _feltLog.Add(new Felt(m, _names[h], actId, "Reattributed", basis, 0, toward, -e.Raw, -e.Applied));
        if (Math.Abs(e.Applied) >= _fo.LogAt)
            _log.Add($"{m} regard {_names[h]} {toward} {-e.Applied:+0.000;-0.000} -> {after:0.000} act {actId} Reattributed");
        foreach (var key in _sentiments.Where(p => p.Key.Holder == _names[h] && p.Key.Toward == toward && p.Value.ActId == actId)
                     .Select(p => p.Key).OrderBy(k => k.Name, StringComparer.Ordinal).ToList())
            _sentiments.Remove(key);
    }

    // ---- sentiments (F18) ------------------------------------------------------------------

    /// <summary>A named lasting feeling with its cause: created, or renewed and strengthened. Each
    /// pair keeps its strongest few.</summary>
    private void AddSentiment(string holder, string toward, string name, int actId, double amount, int m)
    {
        var key = (holder, toward, name);
        Sentiment s = _sentiments.TryGetValue(key, out Sentiment? old)
            ? old with { Strength = Math.Min(1, old.Strength + amount), ActId = actId, Since = m, Count = old.Count + 1 }
            : new Sentiment(holder, toward, name, actId, Math.Min(1, amount), m);
        _sentiments[key] = s;
        var pair = _sentiments.Values.Where(x => x.Holder == holder && x.Toward == toward)
            .OrderBy(x => x.Strength).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        for (int i = 0; i < pair.Count - _fo.SentimentsPerPair; i++)
            _sentiments.Remove((holder, toward, pair[i].Name));
        _log.Add($"{m} sentiment {holder} {toward} {name} {s.Strength:0.00} act {actId}");
    }

    // ---- acts done to someone, and being named (F12-F14) -----------------------------------

    /// <summary>
    /// Undergoing an act (F12): the person warned, taken in, questioned, set to service or rowed
    /// with feels it, and blames whoever did it to them, less if they know they gave cause (law
    /// 10). A collapse or a stumble saddens and blames nobody.
    /// </summary>
    private void Undergo(Act act, ActKind kind, int m)
    {
        if (!_fo.Enabled || kind.Affect is not { Patient: Patient.Actor } row || row.Joy == 0)
            return;
        int s = _index[act.Actor];
        double f = row.Joy * Sens(s);
        AddMood(s, f, act.Id);
        _feltLog.Add(new Felt(m, act.Actor, act.Id, "Undergone", "Event", f, null, 0, 0));
        Underwent(s, act.Target, act.Id, Math.Abs(f), m);
        if (_fo.MishapHurtOn && row.Freedom <= 0 && f < 0 && CharacterOf(s).Boldness < 0.5)
            Lasting(s, -f, 1, m, "mishap"); // Sid's answer A4: a stumble or a collapse weighs on the shy
        if (row.Freedom <= 0 || act.Target is not { } t || t == act.Actor || !_index.TryGetValue(t, out int ti))
            return;
        if (_fo.HomeHurtOn && f < 0)
            Lasting(s, -f, Close(s, ti) ? _fo.HomeHurtWeight : 1, m, "undergone"); // 0d.6 (W2): a hurt with no motive
        double phi = _fo.Freedom ? row.Freedom * (act.About >= 0 && Did(act.Actor, act.About) ? 1 - U(s) : 1) : 1;
        double dr = f * row.Plastic * _fo.PlasticScale * phi * Feelings.Keep(f, Ret(s), _fo);
        Move(s, ti, dr, act.Id, "Undergone", "Event", true, m);
    }

    /// <summary>
    /// Being named or confronted over a scandal (F13). The mood is felt once per event. An
    /// innocent resents whoever named them (rule 16 note); the guilty, who know they gave just
    /// cause, feel shame instead and resent nobody (III P40 schol., VERIFY).
    /// </summary>
    private void Accused(string subject, int actId, IEnumerable<string> namers, double joy, string eventName, int m)
    {
        if (!_fo.Enabled)
            return;
        int s = _index[subject];
        bool guilty = Did(subject, actId);
        var list = namers.Where(n => n != subject && _index.ContainsKey(n)).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (!guilty && list.Count == 0 && eventName == "interview")
            return; // asked only for an alibi: nobody named them, and being questioned is felt as an act (F12)
        var (f, per) = Feelings.AccusedSplit(joy, Sens(s), SR(s), guilty, list.Count, Ret(s), _fo);
        per *= _fo.PlasticScale;
        string route = eventName == "confronted" ? "Confronted" : "Accused";
        if (_eventsFelt.Add((s, actId, eventName, -1)))
        {
            AddMood(s, f, actId);
            _feltLog.Add(new Felt(m, subject, actId, route, "Event", f, null, 0, 0));
            Underwent(s, list.FirstOrDefault(), actId, Math.Abs(f), m);
            if (guilty)
                AddSentiment(subject, subject, "Ashamed", actId, Math.Abs(f), m);
            if (guilty && _fo.HomeHurtOn)
                Lasting(s, Math.Abs(f), 1, m, "named"); // 0d.6 (W2): the guilty stir no motive, but it weighs
        }
        if (guilty || per == 0)
            return;
        foreach (string n in list)
            if (_eventsFelt.Add((s, actId, eventName, _index[n])))
            {
                Move(s, _index[n], per, actId, route, "Event", true, m);
                StirAccused(s, _index[n], actId, Math.Abs(f) / list.Count, m);
            }
    }

    /// <summary>Who named someone in what the authority holds: a name, or seen nearby.</summary>
    private static IEnumerable<string> Namers(IEnumerable<Account> known, string subject)
        => known.Where(a => a.From != subject && (a.Actor == subject || a.Nearby?.Contains(subject) == true)).Select(a => a.From);

    /// <summary>Everything the authority holds on a case: the mayor's accounts and what the
    /// constable carries.</summary>
    private IEnumerable<Account> Known(int actId)
        => (_cases.TryGetValue(actId, out var accounts) ? accounts.Values : Enumerable.Empty<Account>())
            .Concat(_carried.Where(c => c.ActId == actId));

    /// <summary>Kin hear the story told about their own (F14; rule 17): each new person they learn
    /// knows it is a step of shame, and of sadness at the one who brought it on them. Only a
    /// telling that names their kin counts.</summary>
    private void KinHears(string k, int actId, IReadOnlyList<string> chain, string? named, int m)
    {
        if (!_fo.Enabled || !_fo.Shame || !_beliefs[k].TryGetValue(actId, out Belief? b) || b.Actor is not { } c
            || named != c || !AreKin(k, c) || !KindOf(_acts[actId]).IsScandal)
            return;
        foreach (string name in chain)
            if (name != k && name != c && !AreKin(k, name))
                Shamed(k, c, actId, name, m);
    }

    /// <summary>Kin see their own warned, taken in, questioned or set to service in public: the
    /// whole town will know (F14).</summary>
    private void KinPublic(string k, int about, string c, int consequenceId, int m)
    {
        if (!_fo.Shame || !_beliefs[k].TryGetValue(about, out Belief? b) || b.Actor != c || !AreKin(k, c) || !KindOf(_acts[about]).IsScandal)
            return;
        for (int i = 0; i < _fo.PublicHolders; i++)
            Shamed(k, c, about, $"public:{consequenceId}:{i}", m);
    }

    private void Shamed(string k, string c, int actId, string holder, int m)
    {
        int ki = _index[k], ci = _index[c];
        if (!_knownHolders.TryGetValue((ki, actId), out var known))
            _knownHolders[(ki, actId)] = known = new HashSet<string>();
        if (known.Count >= _fo.ShameCap || !known.Add(holder))
            return;
        double f = _fo.ShameJoy * (1.5 - SR(ki)) * Sens(ki);
        AddMood(ki, f, actId);
        _feltLog.Add(new Felt(m, k, actId, "Shame", "Event", f, null, 0, 0));
        Underwent(ki, c, actId, Math.Abs(f), m);
        if (_fo.HomeHurtOn)
            Lasting(ki, Math.Abs(f), _fo.HomeHurtWeight, m, "shame"); // 0d.6 (W2): kin's shame weighs, at home's weight
        double freedom = KindOf(_acts[actId]).Affect?.Freedom ?? 1;
        double raw = f * _fo.EventPlastic * _fo.PlasticScale * Feelings.Phi(freedom, Excuse(ki, ci), _fo) * Feelings.Keep(f, Ret(ki), _fo);
        Move(ki, ci, raw, actId, "Shame", "Event", true, m);
    }

    /// <summary>A second teller, independent of the first, names the same person (F15; rule 9).</summary>
    private void Corroborate(string h, int actId, int m)
    {
        if (!_fo.Enabled || !_felt.TryGetValue((_index[h], actId), out FeltRecord? rec) || !rec.Started || rec.Corroborated)
            return;
        rec.Corroborated = true;
        Belief b = _beliefs[h][actId];
        Feel(h, b, b, null, m);
    }

    /// <summary>Seen first-hand: a public consequence, or goods paid back (F15; rule 9).</summary>
    private void Confirm(string h, int actId, string subject, int m)
    {
        if (!_fo.Enabled || !_beliefs[h].TryGetValue(actId, out Belief? b) || b.Actor != subject
            || !_felt.TryGetValue((_index[h], actId), out FeltRecord? rec) || !rec.Started || rec.Confirmed)
            return;
        rec.Confirmed = true;
        Feel(h, b, b, null, m);
    }

    /// <summary>Seeing a consequence act (F11 step 8): it confirms who did the act it answers, and
    /// shames their kin in public.</summary>
    private void Answered(string who, Belief b, int m)
    {
        Act act = _acts[b.ActId];
        if (act.About < 0 || b.Source != Source.Witnessed || b.Actor is not { } subject)
            return;
        if (act.Kind is Authority.Warned or Authority.TakenIn or Service or FamilyRow)
            Confirm(who, act.About, subject, m);
        if (act.Kind is Authority.Warned or Authority.TakenIn or Service or Authority.Questioned)
            KinPublic(who, act.About, subject, act.Id, m);
    }

    // ---- night (F17) -----------------------------------------------------------------------

    /// <summary>
    /// The night (F17; rule 5): grudges fade toward where the pair started, faster on days spent
    /// together with no new slight; a friendship holds on days together and fades apart, never
    /// past where it started. Prejudice fades. Sentiments fade. The power of acting is recorded,
    /// and new feuds and friendships.
    /// </summary>
    private void CloseFeelings(int day, int days)
    {
        if (!_fo.Enabled)
            return;
        int n = _names.Length;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                    continue;
                bool together = _together[Math.Min(i, j), Math.Max(i, j)] >= _fo.ContactMinutes;
                double r = _regard[i, j], b = _baseline[i, j];
                if (r < b)
                    r = Math.Min(b, r + _fo.DriftPerDay + (together && !_slighted[i, j] ? _fo.ContactHeal * (0.5 + U(i)) : 0));
                else if (r > b && !together)
                    r = Math.Max(b, r - _fo.DriftPerDay);
                _regard[i, j] = r;
                if (r >= 0)
                    _trough[i, j] = _conquered[i, j] = 0;
            }
            for (int k = 0; k < _kindNames.Length; k++)
                _kind[i, k] = _kind[i, k] > 0 ? Math.Max(0, _kind[i, k] - _fo.DriftPerDay) : Math.Min(0, _kind[i, k] + _fo.DriftPerDay);
        }
        foreach (var key in _sentiments.Keys.OrderBy(k => k.Holder, StringComparer.Ordinal).ThenBy(k => k.Toward, StringComparer.Ordinal)
                     .ThenBy(k => k.Name, StringComparer.Ordinal).ToList())
        {
            Sentiment s = _sentiments[key];
            double strength = s.Strength * _fo.SentimentKeepPerDay;
            if (strength < 0.01)
                _sentiments.Remove(key);
            else
                _sentiments[key] = s with { Strength = strength };
        }
        int window = _fo.MoodDays * Clock.MinutesPerDay;
        for (int i = 0; i < n; i++)
        {
            _affects[i].RemoveAll(e => _now - e.Tick >= window);
            _powerByDay[_names[i]][day] = PowerOf(i);
        }
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                bool kin = AreKin(_names[i], _names[j]);
                FeudSpellAt(day, i, j); // the story measures' record (acts spec 7.3); no rule reads it
                if (_regard[i, j] <= _fo.FeudAt && _regard[j, i] <= _fo.FeudAt
                    && !(_baseline[i, j] <= _fo.FeudAt && _baseline[j, i] <= _fo.FeudAt))
                    Tie(day, i, j, kin ? "kin-feud" : "feud");
                if (_regard[i, j] >= _fo.FriendAt && _regard[j, i] >= _fo.FriendAt && !kin && _cast[i].Household != _cast[j].Household
                    && !(_baseline[i, j] >= _fo.FriendAt && _baseline[j, i] >= _fo.FriendAt))
                    Tie(day, i, j, "friendship");
            }
        }
        if (day % Clock.DaysPerSeason == Clock.DaysPerSeason - 1 || day == days - 1)
        {
            var flat = new double[n * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    flat[i * n + j] = _regard[i, j];
            _snapshots.Add((day, flat));
        }
        Array.Clear(_together);
        Array.Clear(_slighted);
    }

    private void Tie(int day, int i, int j, string what)
    {
        if (!_tied.Add((i, j, what)))
            return;
        _ties.Add((day, _names[i], _names[j], what));
        _log.Add($"{_now} tie {_names[i]} {_names[j]} {what}");
    }

    // ---- steering (S1, S9, S10) ------------------------------------------------------------

    /// <summary>Who an act could be aimed at (S1): awake, in the same place, within reach and in
    /// sight; when feelings steer, also free, and weighted by the actor's regard.</summary>
    private List<(Person P, double W)> Candidates(ActKind kind, Person actor, int m)
    {
        var list = new List<(Person, double)>();
        Location place = _places[actor.Place];
        int a = _index[actor.V.Name];
        foreach (Person o in _people)
        {
            if (o == actor || o.Asleep || o.Place != actor.Place || o.At.Chebyshev(actor.At) > _po.NearTiles
                || Steering && !Free(o, m) || Perception.LineOfSight(place, actor.At, o.At, _po) <= 0
                || !RateTargetAllowed(kind, a, _index[o.V.Name], m))
                continue;
            double w = Steering ? Feelings.TargetWeight(kind.Affect!.Joy, E(a, _index[o.V.Name]), _fo) : 1;
            if (w > 0)
                list.Add((o, w));
        }
        return list;
    }

    /// <summary>Whether a kind aimed at someone chosen has anyone to aim at (S1; only when steering).</summary>
    private bool HasTarget(ActKind kind, Person actor, int m)
        => !Steering || kind.Affect is not { Target: TargetIs.Chosen } || Candidates(kind, actor, m).Count > 0;

    private string? PickTarget(ActKind kind, Person actor, int m)
    {
        var candidates = Candidates(kind, actor, m);
        if (candidates.Count == 0)
            return null;
        double r = Rng.Unit(_seed, "target", kind.Name, actor.V.Name, m.ToString()) * candidates.Sum(c => c.W);
        foreach (var (p, w) in candidates)
        {
            r -= w;
            if (r < 0)
                return p.V.Name;
        }
        return candidates[^1].P.V.Name;
    }

    /// <summary>The other party of an act as it begins (F2).</summary>
    private string? TargetFor(ActKind kind, Affect row, Person actor, int m) => row.Target switch
    {
        TargetIs.Keeper => _ao.Keepers.TryGetValue(actor.Place, out string? k) && k != actor.V.Name && _index.ContainsKey(k) ? k : null,
        TargetIs.Chosen => PickTarget(kind, actor, m),
        TargetIs.Kin => kind.WithKin is not { } role ? null : _people
            .Where(o => o != actor && !o.Asleep && actor.V.KinOf(o.V.Name) == role && o.Place == actor.Place
                        && o.At.Chebyshev(actor.At) <= _po.FarTiles)
            .OrderBy(o => o.At.Chebyshev(actor.At)).ThenBy(o => o.V.Name, StringComparer.Ordinal)
            .Select(o => o.V.Name).FirstOrDefault(),
        _ => null,
    };

    /// <summary>Why someone did something to someone (S10; principle 5): the strongest sentiment
    /// they hold toward them, with its cause.</summary>
    private void Why(int m, string actor, string kind, string toward)
    {
        Sentiment? s = _sentiments.Values.Where(x => x.Holder == actor && x.Toward == toward)
            .OrderByDescending(x => x.Strength).ThenBy(x => x.Name, StringComparer.Ordinal).FirstOrDefault();
        if (s is not null)
            _log.Add($"{m} why {actor} {kind} {toward} {s.Name} since d{s.Since / Clock.MinutesPerDay} act {s.ActId}");
    }

    /// <summary>
    /// Where each household buys its groceries (S9; rule 10). The adults' regard for the store's
    /// keeper pulls toward his store; the chain's prices pull away, harder when money is short.
    /// A household changes only by a clear margin, and holds a choice for a season.
    /// </summary>
    private void ShopChoice(int m)
    {
        if (_ao.Keepers.GetValueOrDefault("Store") is not { } keeper || !_index.ContainsKey(keeper))
            return;
        string keeperHome = HouseholdOf(keeper);
        foreach (string h in _shopOf.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            if (h == keeperHome)
                continue;
            var adults = _cast.Where(v => v.Household == h && v.Stage is Stage.Adult or Stage.Elder).ToList();
            if (adults.Count == 0)
                continue;
            double store = adults.Average(a => St(a.Name, keeper));
            double chain = _fo.ChainPull * _mo.MartDiscount * (1 + NeedPressure(adults[0].Name));
            var (shop, since) = _shopOf[h];
            if (m - since < _fo.ShopHoldDays * Clock.MinutesPerDay)
                continue;
            string? to = shop == "Store" && chain - store >= _fo.ShopMargin ? "Mart"
                : shop == "Mart" && store - chain >= _fo.ShopMargin ? "Store" : null;
            if (to is null)
                continue;
            _shopOf[h] = (to, m);
            _shopSwitches.Add((Clock.Day(m), h, shop, to));
            _log.Add($"{m} shop {h} {shop} {to} (store {store:0.00}, chain {chain:0.00})");
        }
    }
}
