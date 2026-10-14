namespace UnderGlass.Sim;

/// <summary>One person's day of bars (needs bars spec, step 1, and the law 1 experiment): minutes lonely
/// and bored, the day's longest waking stretch without a good chat and the person's usual line, their
/// share of days with an outing, good chats had, minutes out (awake, not at work, not at home), whether
/// they bought a drink, their mean power and mood (hourly), and minutes awake and free at each place
/// away from home.</summary>
public sealed record BarDay(int Day, string Who, int LonelyMinutes, int BoredMinutes, int LongestAlone, int UsualAlone,
    double OutingShare, int Chats, int OutMinutes, bool Drank, double Power, double Mood,
    IReadOnlyDictionary<string, int> Places);

/// <summary>
/// Hidden bars (needs bars spec, step 1): company and fun, each known only to its owner and measured
/// against the person's own usual life. Company: waking minutes since a good chat (with someone not
/// disliked, or a game or a drink together), lonely past twice the median of the last 14 days' longest
/// stretches (six hours at least). Fun: minutes since an outing (an hour at a pastime in one stay, or a
/// game or a drink), bored past 2.5 times one's usual gap between outings (two days at least), only for
/// someone with a pastime. Children are never pressed. With Bars on they are kept and recorded and read
/// by nothing (BarMode 0, watch); the law 1 experiment's other modes let them press:
/// <list type="bullet">
/// <item>1, general (the amendment as written): any hub or the Saloon by day leans with loneliness, a
/// pastime place with boredom; Fond's wish and the invitation answer ease.</item>
/// <item>2, particular (the recommendation): a place leans with loneliness by how many of the person's
/// own good chats of the last 14 days were there, and with boredom by how many of their own outings;
/// Fond and the invitation answer as in 1.</item>
/// <item>3, Spinoza: a bar is sadness in the body: it lowers the power of acting (Conditions), and
/// nothing else reads it.</item>
/// </list>
/// No mode draws a die, moves money or adds an act or a log line.
/// </summary>
public sealed partial class Simulation
{
    private const int BarMemoryDays = 14;

    private int[] _aloneFor = null!, _longestToday = null!, _sinceFun = null!, _stayMinutes = null!;
    private string?[] _stayPlace = null!;
    private Queue<int>[] _gaps = null!;
    private Queue<(int Day, string? Place)>[] _outings = null!;
    private Queue<(int Day, string Place)>[] _chatPlaces = null!;
    private int[] _lonelyToday = null!, _boredToday = null!, _chatsToday = null!, _outToday = null!, _samples = null!;
    private double[] _powerSum = null!, _moodSum = null!;
    private Dictionary<string, int>[] _placesToday = null!;
    private readonly List<BarDay> _barDays = new();

    private bool BarsKept => _fo.Acts.Bars && _fo.Enabled;
    private bool BarsPressing => BarsKept && Steering && _fo.Acts.BarMode is 1 or 2;
    private bool BarsSpinoza => BarsKept && _fo.Acts.BarMode == 3;

    private void StartBars()
    {
        if (!BarsKept)
            return;
        int n = _people.Length;
        (_aloneFor, _longestToday, _sinceFun, _stayMinutes) = (new int[n], new int[n], new int[n], new int[n]);
        _stayPlace = new string?[n];
        _gaps = Enumerable.Range(0, n).Select(_ => new Queue<int>()).ToArray();
        _outings = Enumerable.Range(0, n).Select(_ => new Queue<(int, string?)>()).ToArray();
        _chatPlaces = Enumerable.Range(0, n).Select(_ => new Queue<(int, string)>()).ToArray();
        (_lonelyToday, _boredToday, _chatsToday, _outToday, _samples) = (new int[n], new int[n], new int[n], new int[n], new int[n]);
        (_powerSum, _moodSum) = (new double[n], new double[n]);
        _placesToday = Enumerable.Range(0, n).Select(_ => new Dictionary<string, int>()).ToArray();
    }

    /// <summary>Each tick, after errands: the bars drain, a stay at a pastime becomes an outing, and the
    /// day's accounts are kept; at midnight the day closes.</summary>
    private void KeepBars(int m)
    {
        if (!BarsKept)
            return;
        int t = Clock.OfDay(m), day = Clock.Day(m);
        if (t == 0 && day > 0)
            CloseBarDay(day - 1);
        for (int i = 0; i < _people.Length; i++)
        {
            Person p = _people[i];
            _sinceFun[i] += Clock.TickMinutes;
            if (!p.Asleep)
            {
                _aloneFor[i] += Clock.TickMinutes;
                _longestToday[i] = Math.Max(_longestToday[i], _aloneFor[i]);
            }
            bool free = !p.Asleep && p.Why != "work";
            string? pastime = free ? PastimeAt(p, t) : null;
            if (pastime is not null && pastime == _stayPlace[i])
                _stayMinutes[i] += Clock.TickMinutes;
            else
                (_stayPlace[i], _stayMinutes[i]) = (pastime, pastime is null ? 0 : Clock.TickMinutes);
            if (pastime is not null && _stayMinutes[i] >= _fo.Acts.OutingMinutes && _sinceFun[i] > 0)
                FunFill(i, pastime, day);
            if (CompanyPressure(i) > 0)
                _lonelyToday[i] += Clock.TickMinutes;
            if (FunPressure(i) > 0)
                _boredToday[i] += Clock.TickMinutes;
            if (free && p.Place != p.V.Home)
            {
                _outToday[i] += Clock.TickMinutes;
                _placesToday[i][p.Place] = _placesToday[i].GetValueOrDefault(p.Place) + Clock.TickMinutes;
            }
            if (t % 60 == 0)
            {
                _powerSum[i] += PowerOf(i);
                _moodSum[i] += MoodOf(i);
                _samples[i]++;
            }
        }
    }

    /// <summary>A pastime: the Beach, the Saloon from 17:00, the Market hub (data of the shipped town;
    /// the spec's BarBook outlets). The pastime's name, or null.</summary>
    private static string? PastimeAt(Person p, int t)
        => p.Place == "Beach" ? "Beach"
            : p.Place == "Saloon" && t >= Clock.At(17) ? "Saloon"
            : p.Haunt?.Hub == "Market" ? "Market"
            : null;

    private void CloseBarDay(int day)
    {
        for (int i = 0; i < _people.Length; i++)
        {
            Enqueue(_gaps[i], _longestToday[i]);
            if (!_outings[i].Any(o => o.Day == day))
                _outings[i].Enqueue((day, null));
            while (_outings[i].Count > 0 && _outings[i].Peek().Day <= day - BarMemoryDays)
                _outings[i].Dequeue();
            while (_chatPlaces[i].Count > 0 && _chatPlaces[i].Peek().Day <= day - BarMemoryDays)
                _chatPlaces[i].Dequeue();
            _barDays.Add(new BarDay(day, _names[i], _lonelyToday[i], _boredToday[i], _longestToday[i], LonelyLine(i),
                Math.Round(OutingShare(i), 4), _chatsToday[i], _outToday[i], _drank.Contains((_names[i], day)),
                _samples[i] == 0 ? 0 : Math.Round(_powerSum[i] / _samples[i], 4), _samples[i] == 0 ? 0 : Math.Round(_moodSum[i] / _samples[i], 4),
                new SortedDictionary<string, int>(_placesToday[i], StringComparer.Ordinal)));
            _longestToday[i] = _people[i].Asleep ? 0 : _aloneFor[i];
            (_lonelyToday[i], _boredToday[i], _chatsToday[i], _outToday[i], _samples[i]) = (0, 0, 0, 0, 0);
            (_powerSum[i], _moodSum[i]) = (0, 0);
            _placesToday[i] = new Dictionary<string, int>();
        }
    }

    private static void Enqueue(Queue<int> q, int v)
    {
        q.Enqueue(v);
        while (q.Count > BarMemoryDays)
            q.Dequeue();
    }

    /// <summary>A good chat (with someone not disliked), at a place: company fills.</summary>
    private void CompanyFill(int i, string place)
    {
        _aloneFor[i] = 0;
        _chatsToday[i]++;
        _chatPlaces[i].Enqueue((Clock.Day(_now), place));
    }

    private void FunFill(int i, string place, int day)
    {
        _sinceFun[i] = 0;
        if (!_outings[i].Any(o => o.Day == day && o.Place == place))
            _outings[i].Enqueue((day, place));
    }

    /// <summary>After a chat in Socialise: each side whose regard for the other is above dislike.</summary>
    private void BarsAfterChat(int a, int b, string place)
    {
        if (!BarsKept)
            return;
        if (E(a, b) > -_fo.LoveAt)
            CompanyFill(a, place);
        if (E(b, a) > -_fo.LoveAt)
            CompanyFill(b, place);
    }

    /// <summary>A game or a drink together fills company and fun for both.</summary>
    private void BarsAfterAct(Act act)
    {
        if (!BarsKept || act.Kind is not ("PlayedGame" or "TreatedToDrink") || act.Target is not { } t || !_index.TryGetValue(t, out int ti))
            return;
        int day = Clock.Day(act.Tick);
        foreach (int i in new[] { _index[act.Actor], ti })
        {
            CompanyFill(i, act.Location);
            FunFill(i, act.Location, day);
        }
    }

    /// <summary>The waking stretch without a good chat past which this person is lonely: twice their
    /// usual longest (the median of up to 14 days), six hours at least; none until a week is known.</summary>
    private int LonelyLine(int i)
    {
        if (_gaps[i].Count < _fo.Acts.BarsSettleDays)
            return int.MaxValue;
        int[] g = _gaps[i].Order().ToArray();
        double median = g.Length % 2 == 1 ? g[g.Length / 2] : (g[g.Length / 2 - 1] + g[g.Length / 2]) / 2.0;
        return (int)Math.Max(_fo.Acts.LonelyFloorMinutes, _fo.Acts.LonelyTimes * median);
    }

    private double OutingShare(int i)
    {
        int days = Math.Min(BarMemoryDays, _outings[i].Select(o => o.Day).Distinct().Count());
        return days == 0 ? 0 : _outings[i].Where(o => o.Place is not null).Select(o => o.Day).Distinct().Count() / (double)days;
    }

    /// <summary>How lonely, 0 to 1: 0 up to the line, 1 at one and a half times it; children never.</summary>
    private double CompanyPressure(int i)
    {
        if (_cast[i].Age < 13)
            return 0;
        int line = LonelyLine(i);
        return line == int.MaxValue ? 0 : Math.Clamp((_aloneFor[i] - line) / (0.5 * line), 0, 1);
    }

    /// <summary>How bored, 0 to 1, for someone with a pastime (outings on a seventh of their days or more).</summary>
    private double FunPressure(int i)
    {
        if (_cast[i].Age < 13 || _outings[i].Select(o => o.Day).Distinct().Count() < _fo.Acts.BarsSettleDays)
            return 0;
        double share = OutingShare(i);
        if (share < _fo.Acts.RegularShare)
            return 0;
        double line = Math.Max(_fo.Acts.BoredFloorMinutes, _fo.Acts.BoredTimes * Clock.MinutesPerDay / share);
        return Math.Clamp((_sinceFun[i] - line) / (0.5 * line), 0, 1);
    }

    /// <summary>The free-time pick's lean (modes 1 and 2): an option's weight times min(cap, 1 + gain x
    /// the larger of the pressures it serves).</summary>
    private void BarLean(Person p, List<(Haunt? Haunt, double Weight)> options, int m)
    {
        if (!BarsPressing)
            return;
        int i = _index[p.V.Name], t = Clock.OfDay(m);
        double pc = CompanyPressure(i), pf = FunPressure(i);
        if (pc <= 0 && pf <= 0)
            return;
        Dictionary<string, int>? chats = null, outings = null;
        if (_fo.Acts.BarMode == 2)
        {
            chats = _chatPlaces[i].GroupBy(c => c.Place).ToDictionary(g => g.Key, g => g.Count());
            outings = _outings[i].Where(o => o.Place is not null).GroupBy(o => o.Place!).ToDictionary(g => g.Key, g => g.Count());
        }
        for (int k = 0; k < options.Count; k++)
        {
            if (options[k].Haunt is not { } h)
                continue;
            double c, f;
            if (chats is not null)
            {
                // Particular: where this person's own good chats and outings were.
                c = pc * Share(chats, h.Place);
                string? pastime = h.Hub == "Market" ? "Market" : h.Place == "Beach" ? "Beach" : h.Place == "Saloon" && t >= Clock.At(17) ? "Saloon" : null;
                f = pastime is null ? 0 : pf * Share(outings!, pastime);
            }
            else
            {
                // General: any gathering or the Saloon by day for company; any pastime for fun.
                c = h.Hub is not null || h.Place == "Saloon" && t >= Clock.At(12) ? pc : 0;
                f = h.Hub == "Market" || h.Place == "Beach" || h.Place == "Saloon" && t >= Clock.At(17) ? pf : 0;
            }
            double press = Math.Max(c, f);
            if (press > 0)
                options[k] = (h, options[k].Weight * Math.Min(_fo.Acts.BarCap, 1 + _fo.Acts.BarPullGain * press));
        }
    }

    private static double Share(Dictionary<string, int> counts, string place)
        => counts.Count == 0 ? 0 : counts.GetValueOrDefault(place) / (double)counts.Values.Max();

    /// <summary>Fond and Missing (modes 1 and 2): the wish to see someone in reach grows with loneliness.</summary>
    private double BarWish(int h)
        => BarsPressing ? Math.Min(_fo.Acts.BarWishCap, 1 + _fo.Acts.BarWishGain * CompanyPressure(h)) : 1;

    /// <summary>An invitation's answer (modes 1 and 2): the lonely or bored find yes easier.</summary>
    private double BarAskEase(int asked)
        => BarsPressing ? _fo.Acts.BarAskEase * Math.Max(CompanyPressure(asked), FunPressure(asked)) : 0;

    /// <summary>Mode 3, Spinoza: a bar is a sadness of the body, lowering the power of acting.</summary>
    private double BarSadness(int i)
        => BarsSpinoza ? _fo.Acts.BarPowerWeight * Math.Max(CompanyPressure(i), FunPressure(i)) : 0;
}
