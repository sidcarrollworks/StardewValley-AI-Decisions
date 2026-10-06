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
    private readonly HashSet<(string, string, int)> _reported = new();
    private readonly List<Account> _carried = new();
    private readonly List<Account> _filed = new();
    private readonly Dictionary<string, int> _record = new();
    private readonly List<(int ActId, string Accused, Consequence Step)> _toDeliver = new();
    private readonly List<Verdict> _verdicts = new();
    private readonly List<Election> _elections = new();

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
            .Where(v => v.Name != _ao.Mayor && v.Name != DefaultTown.Newcomer && v.Temperament.Boldness >= _ao.StandAt)
            .Select(v => v.Name).ToList();
        if (candidates.Count == 0)
            return;
        var votes = candidates.ToDictionary(c => c, _ => 0);
        foreach (Villager voter in _cast)
        {
            if (voter.Name == DefaultTown.Newcomer)
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
            if (!KindOf(act).IsScandal || _decided.Contains(act.Id) || b.Actor == who || _reported.Contains((who, to, act.Id)))
                continue;
            bool victim = _ao.Keepers.TryGetValue(act.Location, out string? keeper) && keeper == who;
            bool firstHand = b.Source != Source.Told;
            if (!victim && !(firstHand && (who == _constable || Willing(p.V, act.Id))))
                continue;
            _reported.Add((who, to, act.Id));
            var account = new Account(act.Id, who, b.Actor, b.Confidence, firstHand, m);
            _log.Add($"{m} report {who} {to} {act.Id} {b.Actor ?? "someone"}");
            if (to == _ao.Mayor)
                File(account, m);
            else
                _carried.Add(account);
        }
    }

    private bool Willing(Villager v, int actId)
        => Rng.Unit(_seed, "willing", v.Name, actId.ToString()) < _ao.ReportBase + _ao.ReportPerBoldness * v.Temperament.Boldness;

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
        File(new Account(b.ActId, who, b.Actor, b.Confidence, b.Source != Source.Told, m), m);
    }

    private double Trust(string from) => from == _ao.Mayor ? 1 : 0.5 + 0.5 * Familiarity(_ao.Mayor!, from);

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
            bool close = Familiarity(mayor, accused) >= _ao.SwayCloseAt
                         || _cast[_index[accused]].Household == _cast[_index[mayor]].Household;
            bool letOff = Authority.Swayed(_seed, actId, close, _ao);
            Consequence step = Authority.StepFor(_record.GetValueOrDefault(accused));
            var v = new Verdict(actId, m, mayor, accused, accused == _acts[actId].Actor, step, letOff);
            _verdicts.Add(v);
            _log.Add($"{m} verdict {actId} {accused} {(v.Correct ? "right" : "wrong")} {(letOff ? "let-off" : step.ToString())} weight {weight:0.##}");
            if (letOff)
                continue;
            _record[accused] = _record.GetValueOrDefault(accused) + 1;
            if (step is Consequence.Warning or Consequence.Detained)
                _toDeliver.Add((actId, accused, step));
            else
                _log.Add($"{m} pending {accused} {step}"); // fines and service need money (0b)
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
            Person? by = new[] { _ao.Mayor, step == Consequence.Detained ? _constable : null }
                .OfType<string>()
                .Select(n => _people[_index[n]])
                .FirstOrDefault(a => a != p && Free(a, m) && Free(p, m) && a.Place == p.Place && a.At.Chebyshev(p.At) <= _po.FarTiles);
            if (by is null)
                continue;
            string kindName = step == Consequence.Detained ? Authority.TakenIn : Authority.Warned;
            if (_kinds.FirstOrDefault(k => k.Name == kindName) is { } kind)
            {
                Begin(m, kind, p, injected: false);
                by.BusyUntil = Math.Max(by.BusyUntil, p.BusyUntil);
            }
            if (step == Consequence.Detained)
            {
                p.DetainedUntil = m + _ao.DetainMinutes;
                p.Why = "";
            }
            _log.Add($"{m} {(step == Consequence.Detained ? "taken-in" : "warned")} {accused} by {by.V.Name} for {actId}");
            _toDeliver.RemoveAt(i--);
        }
    }

    /// <summary>Where a detained person is held, or where they stand if this world has no lockup.</summary>
    private (string Place, Tile Spot) Lockup(Person p)
        => _places.ContainsKey(_ao.LockupPlace) ? (_ao.LockupPlace, _ao.LockupSpot) : (p.Place, p.At);
}
