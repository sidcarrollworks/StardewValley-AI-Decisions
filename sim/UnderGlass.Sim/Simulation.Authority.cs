namespace UnderGlass.Sim;

/// <summary>
/// The authority in a run (design rule 16): the opening vote for constable, the constable's
/// patrols, reports to the mayor, the mayor's verdicts, and delivering a warning or taking someone
/// in. The mayor knows only what he is told and what he has seen himself (rule 2).
/// </summary>
public sealed partial class Simulation
{
    private readonly AuthorityOptions _ao;
    private string? _constable;
    private bool _voted;
    private readonly SortedDictionary<int, Dictionary<string, Account>> _cases = new();
    private readonly HashSet<int> _decided = new();
    private readonly HashSet<(string, string, int, string)> _reported = new();
    private readonly List<Account> _carried = new();
    private readonly List<Account> _filed = new();
    private readonly Dictionary<string, int> _record = new();
    private readonly List<(int ActId, string Accused, Consequence Step)> _toDeliver = new();
    private readonly List<Verdict> _verdicts = new();
    private readonly List<Election> _elections = new();
    private readonly HashSet<(int, string)> _interviewed = new();
    private readonly List<(int ActId, string Who, bool Confessed)> _interviews = new();

    /// <summary>The constable now: fixed in the options, or elected at the opening meeting.</summary>
    public string? Constable => _constable;

    private bool HasMayor => _ao.Mayor is { } mayor && _index.ContainsKey(mayor);

    /// <summary>Each tick: the opening vote, reports from everyone with the mayor or the
    /// constable, the mayor's review of his cases, and delivering what he decided.</summary>
    private void Authorities(int m)
    {
        if (!HasMayor)
            return;
        if (_ao.ElectConstable && _constable is null && !_voted && m >= _ao.VoteMinute)
            ElectConstable(m);
        foreach (string auth in new[] { _ao.Mayor!, _constable }.OfType<string>().Distinct())
        {
            Person a = _people[_index[auth]];
            if (a.Asleep)
                continue;
            foreach (Person p in _people)
            {
                if (p == a || p.V.Name == _ao.Mayor || p.Asleep || p.Place != a.Place || p.At.Chebyshev(a.At) > _po.FarTiles)
                    continue;
                Report(p, auth, m);
            }
        }
        Interviews(m);
        Review(m);
        Deliver(m);
    }

    /// <summary>
    /// The opening town meeting (Sid, 2026-10-06: "the game can start with the town voting the
    /// constable in... at the start of a run it might be a different constable"). The bold stand;
    /// everyone but the newcomer votes, candidates for themselves, the rest for whoever appeals to
    /// them most: someone they know, fair-minded and bold, with a seeded share of whim.
    /// </summary>
    private void ElectConstable(int m)
    {
        _voted = true;
        var candidates = _cast
            .Where(v => v.Name != _ao.Mayor && v.Name != DefaultTown.Newcomer && v.Temperament.Boldness >= _ao.StandAt
                        && v.Stage is Stage.Adult or Stage.Elder)
            .Select(v => v.Name).ToList();
        if (candidates.Count == 0)
            return;
        var votes = candidates.ToDictionary(c => c, _ => 0);
        foreach (Villager voter in _cast)
        {
            if (voter.Name == DefaultTown.Newcomer || voter.Age < VotingAge)
                continue;
            string choice = candidates.Contains(voter.Name)
                ? voter.Name
                : candidates.OrderByDescending(c => Appeal(voter.Name, c)).ThenBy(c => c, StringComparer.Ordinal).First();
            votes[choice]++;
        }
        string winner = votes.OrderByDescending(p => p.Value).ThenBy(p => Rng.Unit(_seed, "tie", p.Key)).First().Key;
        _constable = winner;
        _elections.Add(new Election("Constable", m, winner, votes));
        _log.Add($"{m} elected constable {winner} {votes[winner]} of {votes.Values.Sum()}");
    }

    /// <summary>Who votes at the town meeting.</summary>
    public const int VotingAge = 16;

    private double Appeal(string voter, string candidate)
    {
        Temperament t = _cast[_index[candidate]].Temperament;
        return Familiarity(voter, candidate) + 0.4 * t.Understanding + 0.3 * t.Boldness
            + 0.6 * Rng.Unit(_seed, "vote", voter, candidate);
    }

    /// <summary>The constable's walk in free time: a public place every
    /// <see cref="AuthorityOptions.PatrolStopMinutes"/>, during the patrol hours.</summary>
    private bool Patrol(Person p, int m)
    {
        int t = Clock.OfDay(m);
        if (p.V.Name != _constable || _ao.Patrol.Count == 0 || !_ao.PatrolHours.Any(h => t >= h.From && t < h.To))
            return false;
        if (p.Why == "patrol" && p.GoalUntil > m)
            return true;
        var stops = _ao.Patrol.Keys.Where(k => k != p.Place && _places.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (stops.Count == 0)
            return false;
        string next = stops[Rng.Range(_seed, 0, stops.Count - 1, "patrol", m.ToString())];
        Goal(p, next, _ao.Patrol[next], m + _ao.PatrolStopMinutes, "patrol", null);
        return true;
    }

    /// <summary>
    /// What <paramref name="p"/> tells the authority <paramref name="to"/> about scandals: the
    /// victim (the keeper of the place) always, even from hearsay; the constable always, from what
    /// they saw or found; a witness or finder if they are willing. Each scandal once per
    /// authority. The constable carries what they gather to the mayor.
    /// </summary>
    private void Report(Person p, string to, int m)
    {
        string who = p.V.Name;
        if (who == _constable && to == _ao.Mayor && _carried.Count > 0)
        {
            foreach (Account c in _carried)
                File(c with { Tick = m }, m); // it reaches the mayor now
            _log.Add($"{m} handed-on {who} {to} {_carried.Count}");
            _carried.Clear();
        }
        foreach (Belief b in _beliefs[who].Values.OrderBy(b => b.ActId))
        {
            Act act = _acts[b.ActId];
            // Once per authority for each thing they know: again when "someone" becomes suspects or a name.
            string what = b.Actor ?? string.Join(",", b.Suspects ?? Array.Empty<string>());
            if (!KindOf(act).IsScandal || _decided.Contains(act.Id) || b.Actor == who || _reported.Contains((who, to, act.Id, what)))
                continue;
            bool victim = _ao.Keepers.TryGetValue(act.Location, out string? keeper) && keeper == who;
            if (b.Actor is { } culprit && AreKin(who, culprit))
            {
                // Families cover (rule 17): never reported; a keeper has it out at home instead.
                if (victim)
                    KeepItInTheFamily(act.Id, who, culprit, m);
                continue;
            }
            bool firstHand = b.Source != Source.Told;
            if (!victim && !(firstHand && (who == _constable || Willing(p.V, act.Id, b.Actor))))
                continue;
            _reported.Add((who, to, act.Id, what));
            var account = AccountOf(who, b, m);
            _log.Add($"{m} report {who} {to} {act.Id} {b.Actor ?? "someone"}{(b.Actor is null && b.Suspects is { Count: > 0 } ? " nearby " + string.Join(",", b.Suspects) : "")}");
            if (to == _ao.Mayor)
                File(account, m);
            else
                _carried.Add(account);
        }
    }

    /// <summary>Whether a witness or finder goes to the authority. S5 (III P25, VERIFY): less
    /// willing about a culprit they love, more about one they hate.</summary>
    private bool Willing(Villager v, int actId, string? culprit)
    {
        double chance = Steering && culprit is not null
            ? Feelings.ReportChance(_ao.ReportBase, _ao.ReportPerBoldness, v.Temperament.Boldness, St(v.Name, culprit), _fo)
            : _ao.ReportBase + _ao.ReportPerBoldness * v.Temperament.Boldness;
        return Rng.Unit(_seed, "willing", v.Name, actId.ToString()) < chance;
    }

    private void File(Account a, int m)
    {
        if (!_cases.TryGetValue(a.ActId, out var accounts))
            _cases[a.ActId] = accounts = new Dictionary<string, Account>();
        accounts[a.From] = a;
        _filed.Add(a);
    }

    /// <summary>The mayor's own knowledge counts as his own account (called whenever he gets or
    /// changes a belief).</summary>
    private void OwnAccount(string who, Belief b, int m)
    {
        if (who != _ao.Mayor || _decided.Contains(b.ActId) || !KindOf(_acts[b.ActId]).IsScandal)
            return;
        File(AccountOf(who, b, m), m);
    }

    /// <summary>An account from a belief: the name if there is one, else who was seen nearby, and
    /// when the teller places it if they saw or found it themselves.</summary>
    private Account AccountOf(string who, Belief b, int m)
    {
        var window = Window(b);
        return new Account(b.ActId, who, b.Actor, b.Confidence, b.Source != Source.Told, m,
            b.Actor is null ? WithoutKin(who, b.Suspects) : null, window?.Since ?? -1, window?.Until ?? -1);
    }

    /// <summary>
    /// The constable questions the people named as nearby in an open case (Sid, 2026-10-06: "the
    /// constable could interview the character and see if any more info comes up"); the mayor does
    /// it if there is no constable. They share what is known of the case. Each person once per
    /// case, when the two are together, the most-named first. Being questioned is an act others
    /// can see. The culprit may confess (more likely if timid); anyone questioned says who they
    /// saw around the place at the time, which can name the culprit or point elsewhere.
    /// </summary>
    private void Interviews(int m)
    {
        foreach (var (actId, accounts) in _cases)
        {
            if (_decided.Contains(actId))
                continue;
            var known = accounts.Values.Concat(_carried.Where(c => c.ActId == actId)).ToList();
            var suspects = known.SelectMany(a => a.Actor is null ? a.Nearby ?? Array.Empty<string>() : Array.Empty<string>())
                .Where(n => n != _ao.Mayor && _index.ContainsKey(n))
                .GroupBy(n => n).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key).ToList();
            // Then the kin who live with the most-suspected: they can say where they were.
            var families = suspects.Take(1).SelectMany(n => (_cast[_index[n]].Family?.Keys ?? Enumerable.Empty<string>())
                    .Where(k => _index.ContainsKey(k) && _cast[_index[k]].Household == _cast[_index[n]].Household))
                .Where(n => n != _ao.Mayor).Distinct().OrderBy(n => n, StringComparer.Ordinal);
            var named = suspects.Concat(families).Distinct().Where(n => !_interviewed.Contains((actId, n)));
            foreach (string suspect in named)
            {
                Person s = _people[_index[suspect]];
                Person? by = new[] { _constable, _ao.Mayor }.OfType<string>().Distinct()
                    .Where(n => n != suspect)
                    .Select(n => _people[_index[n]])
                    .FirstOrDefault(a => Free(a, m) && Free(s, m) && a.Place == s.Place && a.At.Chebyshev(s.At) <= _po.FarTiles);
                if (by is null)
                    continue;
                Interview(actId, s, by, known, m);
                break; // one interview per case per tick
            }
        }
    }

    private void Interview(int actId, Person s, Person by, IReadOnlyList<Account> known, int m)
    {
        string suspect = s.V.Name;
        Act act = _acts[actId];
        _interviewed.Add((actId, suspect));
        if (_kinds.FirstOrDefault(k => k.Name == Authority.Questioned) is { } kind)
        {
            Begin(m, kind, s, injected: false, target: by.V.Name, about: actId);
            by.BusyUntil = Math.Max(by.BusyUntil, s.BusyUntil);
        }
        Account account;
        bool confessed = suspect == act.Actor
            && Rng.Unit(_seed, "confess", actId.ToString(), suspect) < _ao.ConfessBase + _ao.ConfessPerTimidity * (1 - s.V.Temperament.Boldness);
        if (confessed)
            account = new Account(actId, suspect, suspect, 1, true, m);
        else
        {
            // When the case is placed: the narrowest window anyone gave; else the day before.
            var windows = known.Where(a => a.Since >= 0).Select(a => (a.Since, a.Until)).OrderBy(w => w.Until - w.Since).ToList();
            var (since, until) = windows.Count > 0 ? windows[0] : (m - Clock.MinutesPerDay, m);
            _ao.Keepers.TryGetValue(act.Location, out string? keeper);
            // Families cover (rule 17): kin are left out of who they saw, and vouched for if suspected.
            var seen = SeenAt(suspect, act.Location, since, until)
                .Where(n => n != keeper && n != by.V.Name && !AreKin(suspect, n)).Take(_go.MaxSuspects).ToList();
            var alibi = known.SelectMany(a => a.Nearby ?? Array.Empty<string>()).Where(n => AreKin(suspect, n)).Distinct().ToList();
            account = new Account(actId, suspect, null, 0, true, m, seen, since, until, alibi.Count > 0 ? alibi : null);
        }
        _interviews.Add((actId, suspect, confessed));
        Accused(suspect, actId, Namers(known, suspect), _fo.AccusedJoy, "interview", m); // F13: the constable says who named them
        _log.Add($"{m} questioned {suspect} by {by.V.Name} for {actId}: {(confessed ? "confessed" : "saw " + string.Join(",", account.Nearby ?? Array.Empty<string>()))}{(account.Alibi is { } vouched ? "; vouched for " + string.Join(",", vouched) : "")}");
        if (!confessed && known.Any(a => a.From == suspect && a.Actor is not null))
            return; // they already named someone; that account stands
        if (by.V.Name == _ao.Mayor)
            File(account, m);
        else
            _carried.Add(account);
    }

    /// <summary>How much the mayor believes a teller. S4 (rule 9): with feelings steering, he
    /// leans a little toward people he likes, less the more understanding he is.</summary>
    private double Trust(string from) => from == _ao.Mayor ? 1
        : Steering ? Feelings.Credence(Familiarity(_ao.Mayor!, from), St(_ao.Mayor!, from), U(_index[_ao.Mayor!]), _fo)
        : 0.5 + 0.5 * Familiarity(_ao.Mayor!, from);

    /// <summary>The mayor decides every case whose accounts point clearly at one person
    /// (<see cref="Authority.Weigh"/>). He can be wrong when the accounts are. He never accuses
    /// himself. Someone close to him may be let off, rarely.</summary>
    private void Review(int m)
    {
        string mayor = _ao.Mayor!;
        foreach (var (actId, accounts) in _cases)
        {
            if (_decided.Contains(actId))
                continue;
            var (accused, weight, _) = Authority.Weigh(accounts.Values.OrderBy(a => a.From, StringComparer.Ordinal), Trust, _ao);
            if (accused is null || accused == mayor || !_index.ContainsKey(accused))
                continue;
            _decided.Add(actId);
            bool household = _cast[_index[accused]].Household == _cast[_index[mayor]].Household;
            bool close = Steering
                ? Feelings.Close(household, Familiarity(mayor, accused), St(mayor, accused), _ao.SwayCloseAt, _fo)
                : Familiarity(mayor, accused) >= _ao.SwayCloseAt || household;
            bool letOff = Authority.Swayed(_seed, actId, close, _ao);
            Consequence step = Authority.StepFor(_record.GetValueOrDefault(accused));
            var v = new Verdict(actId, m, mayor, accused, accused == _acts[actId].Actor, step, letOff);
            _verdicts.Add(v);
            _log.Add($"{m} verdict {actId} {accused} {(v.Correct ? "right" : "wrong")} {(letOff ? "let-off" : step.ToString())} weight {weight:0.##}");
            if (letOff)
                continue;
            _record[accused] = _record.GetValueOrDefault(accused) + 1;
            if (step == Consequence.RestitutionAndFine)
            {
                // Paid at once from pocket and purse; whatever can't be paid is served instead.
                bool paid = PayUp(actId, accused, m, out double back);
                Act scandal = _acts[actId];
                if (back > 0 && scandal.Location != "Mart" && _ao.Keepers.TryGetValue(scandal.Location, out string? keeper) && _index.ContainsKey(keeper))
                    Confirm(keeper, actId, accused, m); // F15: the goods paid back confirm it to the keeper
                Accused(accused, actId, Namers(Known(actId), accused), _fo.AccusedJoy, "verdict", m); // F13: found guilty and fined
                if (!paid)
                    _toDeliver.Add((actId, accused, Consequence.Service));
                continue;
            }
            _toDeliver.Add((actId, accused, step));
        }
    }

    /// <summary>The mayor gives a warning in person; the mayor or the constable takes someone in.
    /// Either is an act others can see, so it becomes a story.</summary>
    private void Deliver(int m)
    {
        for (int i = 0; i < _toDeliver.Count; i++)
        {
            var (actId, accused, step) = _toDeliver[i];
            Person p = _people[_index[accused]];
            Person? by = new[] { _ao.Mayor, step != Consequence.Warning ? _constable : null }
                .OfType<string>()
                .Select(n => _people[_index[n]])
                .FirstOrDefault(a => a != p && Free(a, m) && Free(p, m) && a.Place == p.Place && a.At.Chebyshev(p.At) <= _po.FarTiles);
            if (by is null)
                continue;
            string kindName = step switch { Consequence.Detained => Authority.TakenIn, Consequence.Service => Service, _ => Authority.Warned };
            if (_kinds.FirstOrDefault(k => k.Name == kindName) is { } kind)
            {
                Begin(m, kind, p, injected: false, target: by.V.Name, about: actId);
                by.BusyUntil = Math.Max(by.BusyUntil, p.BusyUntil);
            }
            Accused(accused, actId, Namers(Known(actId), accused), _fo.AccusedJoy, "verdict", m);
            if (step is Consequence.Detained or Consequence.Service)
            {
                bool detained = step == Consequence.Detained;
                p.DetainedUntil = m + (detained ? _ao.DetainMinutes : _mo.ServiceMinutes);
                (p.HoldPlace, p.HoldSpot) = detained ? (_ao.LockupPlace, _ao.LockupSpot) : (_ao.ServicePlace, _ao.ServiceSpot);
                p.Why = "";
            }
            _log.Add($"{m} {step switch { Consequence.Detained => "taken-in", Consequence.Service => "service", _ => "warned" }} {accused} by {by.V.Name} for {actId}");
            _toDeliver.RemoveAt(i--);
        }
    }

    /// <summary>Where someone detained or doing service is held, or where they stand if this world
    /// has no such place.</summary>
    private (string Place, Tile Spot) Lockup(Person p)
        => _places.ContainsKey(p.HoldPlace) ? (p.HoldPlace, p.HoldSpot) : (p.Place, p.At);
}
