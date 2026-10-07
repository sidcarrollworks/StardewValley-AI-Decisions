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
    private readonly SortedDictionary<string, (int Count, double Sum)> _watched = new(StringComparer.Ordinal);
    /// <summary>Contagion (X3): what each person passed on and took over the run, and took today (the cap).</summary>
    private double[] _gave = Array.Empty<double>(), _caught = Array.Empty<double>(), _caughtToday = Array.Empty<double>();

    /// <summary>X13: the 0d.6 rules record what they would do and change nothing.</summary>
    private bool Watching => _fo.WithdrawalWatch;

    private void Note(string rule, double amount)
    {
        var (count, sum) = _watched.GetValueOrDefault(rule);
        _watched[rule] = (count + 1, sum + amount);
    }

    private void StartWithdrawal(int days)
    {
        int n = _names.Length;
        _daily = Enumerable.Range(0, n).Select(_ => new PersonDays(new int[days], new bool[days], new int[days], new int[days],
            new int[days], new double[days])).ToArray();
        _pairMet = new uint[n, n];
        _gave = new double[n];
        _caught = new double[n];
        _caughtToday = new double[n];
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
        for (int i = 0; i < n; i++)
        {
            Temperament c = CharacterOf(i);
            _daily[i].LeftOut[day] = WithdrawalMath.LeftOut(kindIn[i], kindMedian, met[i], metMedian, share[i], shareMedian,
                WithdrawalMath.Solitary(c.Chattiness, c.Boldness, _fo), _fo);
        }
    }

    /// <summary>Days together in the last four weeks for a pair (X12's steadiness).</summary>
    private int DaysTogether(int i, int j) => System.Numerics.BitOperations.PopCount(_pairMet[i, j] & 0x0FFFFFFFu);

    private IReadOnlyDictionary<string, PersonDays> Daily()
        => Desiring ? _names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => _daily[x.i]) : new Dictionary<string, PersonDays>();

    private IReadOnlyDictionary<string, (double Gave, double Caught)> ContagionTotals()
        => Desiring ? _names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => (_gave[x.i], _caught[x.i]))
            : new Dictionary<string, (double, double)>();
}
