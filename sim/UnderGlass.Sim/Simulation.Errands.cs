namespace UnderGlass.Sim;

/// <summary>One favour asked (needs spec): at a minute, who asked whom to fetch what, the answer and
/// its margin, and the act that followed (the asking on a yes, the refusal on a no). Tired, Busy,
/// Low, Bond, Eager as for an invitation; WontFetch: the asked won't fetch that thing for anyone, so
/// the answer was no whatever the margin; Short: their household couldn't pay for it; Running: they
/// were already on an errand. Recorded so an observer can see why.</summary>
public sealed record Favour(int Tick, string Asker, string Asked, string Item, bool Accepted, double Margin, int ActId,
    double Tired = 0, double Busy = 0, double Low = 0, double Bond = 0, double Eager = 0,
    bool WontFetch = false, bool Short = false, bool Running = false);

/// <summary>
/// Errands (agreed order step 4; needs spec): one need for one named thing, asked of someone, agreed
/// as a promise, attempted, and kept, kept late or broken. Each midnight, someone with things they
/// use may come to need one (a keyed draw). While the need is open they ask someone in reach, chosen
/// by the bond, through the general Ask (AskKind.Errand); a no is a refusal, as an invitation's is. A
/// yes is a promise: the one asked walks to the counter during its sign's hours, buys it if someone
/// is serving (Fetched; the money moves), and hands it over when the two meet (HandedOver; the asker
/// pays it back). Handed over after it was due, it is late; not handed over by a day after that, the
/// promise is broken and the one let down knows (LetDown, unseen). Nothing here runs with the switch
/// off, in watch mode, without steering, or without an errands book, so every pinned run is unchanged.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>A counter is worth setting out for only if it stays open this much longer.</summary>
    private const int ErrandLeadMinutes = 30;
    /// <summary>How long someone looks for the person they're bringing something to, at one place.</summary>
    private const int SeekMinutes = 60;
    /// <summary>The waking hours in which a need is noticed.</summary>
    private static readonly int NeedFrom = Clock.At(8), NeedTo = Clock.At(20);

    private readonly List<Need> _needs = new();
    private readonly List<Promise> _promises = new();
    private readonly List<Favour> _favours = new();
    /// <summary>The errands promised and not yet settled, by promise id, in id order.</summary>
    private readonly List<int> _openErrands = new();
    /// <summary>After a no: the day from which the asker may ask that person a favour again.</summary>
    private readonly Dictionary<(int Asker, int Asked), int> _favourAgain = new();
    private readonly Dictionary<int, int> _lastFavourAsk = new();
    /// <summary>Runners who found the counter shut, no one serving or no money, today.</summary>
    private readonly HashSet<(int Runner, int Day)> _shutToday = new();
    /// <summary>Places a runner has looked for the person they're bringing something to, today.</summary>
    private readonly HashSet<(int Runner, int Day, string Place)> _sought = new();
    /// <summary>Speech for errand acts: who speaks first and with which kind of line, who answers
    /// and with which, and the thing (as said).</summary>
    private readonly Dictionary<int, (string First, string FirstKind, string Second, string SecondKind, string Item)> _errandSpeech = new();
    /// <summary>What is being asked for during an errand's Ask (its costs read it).</summary>
    private string? _askingFor;

    private bool Errands => _fo.Acts.Errands && Steering && !_fo.Acts.Watch && _economy?.Errands is not null;

    private ErrandBook Book => _economy!.Errands!;

    /// <summary>Each tick, with Errands on: needs arise at midnight; needs lapse and promises break;
    /// favours are asked; counters sell; things are handed over.</summary>
    private void RunErrands(int m)
    {
        if (!Errands)
            return;
        if (Clock.OfDay(m) == 0)
            NeedsArise(m);
        SettleErrands(m);
        AskFavours(m);
        BuyAtCounters(m);
        HandOver(m);
    }

    /// <summary>Midnight: someone with things they use, and no need already, may come to need one,
    /// noticed at a keyed minute of the waking day (running out happens mid-morning as often as at
    /// breakfast), so who is around to ask varies.</summary>
    private void NeedsArise(int m)
    {
        int day = Clock.Day(m);
        _shutToday.RemoveWhere(s => s.Day < day);
        _sought.RemoveWhere(s => s.Day < day);
        foreach (string n in _names)
        {
            if (!Book.Uses.TryGetValue(n, out IReadOnlyList<string>? uses) || uses.Count == 0
                || _needs.Any(x => x.Holder == n && x.State is NeedState.Open or NeedState.Promised)
                || Rng.Unit(_seed, "need", n, day.ToString()) >= _fo.Acts.NeedChancePerDay)
                continue;
            string item = uses[Math.Min(uses.Count - 1, (int)(Rng.Unit(_seed, "need-item", n, day.ToString()) * uses.Count))];
            int at = m + Rng.Range(_seed, NeedFrom / Clock.TickMinutes, NeedTo / Clock.TickMinutes, "need-at", n, day.ToString()) * Clock.TickMinutes;
            _needs.Add(new Need(_needs.Count, at, n, item, at + _fo.Acts.NeedDays * Clock.MinutesPerDay));
            _log.Add($"{m} needs {n} {item} from {at}");
        }
    }

    /// <summary>An open need lapses when nobody has promised it in time; a promise not kept by a day
    /// after it was due is broken, and the one let down knows who let them down.</summary>
    private void SettleErrands(int m)
    {
        for (int i = 0; i < _needs.Count; i++)
            if (_needs[i] is { State: NeedState.Open } need && m >= need.Until)
            {
                _needs[i] = need with { State = NeedState.Lapsed, Settled = m };
                _log.Add($"{m} need-lapsed {need.Holder} {need.Item}");
            }
        foreach (int id in _openErrands.ToList())
        {
            Promise pr = _promises[id];
            if (m < pr.DueTo + _fo.Acts.ErrandGraceDays * Clock.MinutesPerDay)
                continue;
            Person runner = _people[_index[pr.By]];
            Act act = BeginUnseen(m, _kindByName["LetDown"], pr.By, runner.Place, runner.At, pr.To, pr.MadeAct,
                new[] { (pr.To, Source.Witnessed, 1.0) });
            _promises[id] = pr with { State = PromiseState.Broken, Settled = m, SettledAct = act.Id };
            SettleNeed(id, NeedState.Lapsed, m);
            _openErrands.Remove(id);
            if (runner.Why is "errand" or "deliver")
                runner.Why = "";
            _log.Add($"{m} errand-broken {pr.By} {pr.To} {pr.Item}");
        }
    }

    private void SettleNeed(int promise, NeedState state, int m)
    {
        int i = _needs.FindIndex(x => x.Promise == promise);
        if (i >= 0)
            _needs[i] = _needs[i] with { State = state, Settled = m };
    }

    /// <summary>Each open need's holder, free and not just turned down, asks someone in reach: never
    /// someone they dislike, too young to buy it, or who sells it; a person is drawn by the bond.</summary>
    private void AskFavours(int m)
    {
        int day = Clock.Day(m);
        foreach (Need need in _needs.Where(x => x.State == NeedState.Open && x.Tick <= m).ToList())
        {
            int h = _index[need.Holder];
            Person p = _people[h];
            if (!Free(p, m) || _lastFavourAsk.TryGetValue(h, out int last) && m - last < _fo.Acts.ErrandAskMinutes)
                continue;
            Good good = Book.Good(need.Item);
            var options = new List<(int I, double W)>();
            foreach (Person o in _people)
            {
                int i = _index[o.V.Name];
                double regard = St(need.Holder, o.V.Name);
                if (i == h || !Free(o, m) || o.Place != p.Place || o.At.Chebyshev(p.At) > _po.NearTiles || regard < 0
                    || o.V.Age < good.MinAge || o.V.Job?.Place == good.Place
                    || _favourAgain.TryGetValue((h, i), out int again) && day < again)
                    continue;
                options.Add((i, 0.1 + AskMath.Bond(Close(h, i), regard, _fam[h, i])));
            }
            if (options.Count == 0)
                continue;
            double r = Rng.Unit(_seed, "errand-ask", need.Holder, m.ToString()) * options.Sum(o => o.W);
            int t = options[^1].I;
            foreach (var (i, w) in options)
            {
                r -= w;
                if (r < 0) { t = i; break; }
            }
            AskFavour(need, h, t, good, m);
        }
    }

    private void AskFavour(Need need, int h, int t, Good good, int m)
    {
        Person asker = _people[h], asked = _people[t];
        int actId = _acts.Count;
        _askingFor = good.Name;
        var (yes, margin) = Ask(t, h, AskKind.Errand, actId, m);
        _askingFor = null;
        AskState state = AskStateOf(t, m);
        _favours.Add(new Favour(m, need.Holder, asked.V.Name, good.Name, yes, Math.Round(margin, 4), actId,
            Math.Round(state.Tired, 4), state.Busy, Math.Round(state.Low, 4),
            Math.Round(AskMath.Bond(Close(t, h), Math.Max(0, St(_names[t], _names[h])), _fam[t, h]), 4),
            Math.Round(AskMath.Eagerness(CharacterOf(t)), 4),
            WontFetch(t, good), Short(t, good), Running(t)));
        _lastFavourAsk[h] = m;
        if (!yes)
        {
            _errandSpeech[actId] = (need.Holder, "AskedFavour", asked.V.Name, "AskedFavour.Refused", good.Phrase);
            _favourAgain[(h, t)] = Clock.Day(m) + _fo.Acts.ErrandAskAgainDays;
            Begin(m, _kindByName["Refused"], asked, injected: false, target: need.Holder);
            return;
        }
        _errandSpeech[actId] = (need.Holder, "AskedFavour", asked.V.Name, "AskedFavour.Accepted", good.Phrase);
        Begin(m, _kindByName["AskedFavour"], asker, injected: false, target: asked.V.Name);
        Counter counter = Book.CounterFor(good.Place);
        int due = (Clock.Day(m) + _fo.Acts.ErrandDays + 1) * Clock.MinutesPerDay;
        var promise = new Promise(_promises.Count, PromiseKind.Errand, asked.V.Name, need.Holder, m, actId,
            counter.Place, counter.Spot, m, due, good.Price, Item: good.Name);
        _promises.Add(promise);
        _openErrands.Add(promise.Id);
        _needs[need.Id] = need with { State = NeedState.Promised, Promise = promise.Id };
        _log.Add($"{m} errand-promised {asked.V.Name} {need.Holder} {good.Name}");
    }

    /// <summary>What weighs against fetching it, besides the asked person's state (the Ask's Errand
    /// cost): one errand already running, or a household that can't pay for it (each 1, nearly always
    /// a no). Their own knowledge: what they promised, what their household has.</summary>
    private double ErrandCost(int asked)
    {
        if (_askingFor is not { } item)
            return 0;
        Good good = Book.Good(item);
        return (Running(asked) ? 1 : 0) + (Short(asked, good) ? 1 : 0);
    }

    /// <summary>The thing being asked for is one the asked person won't fetch for anyone (the book's
    /// WontFetch: Penny and beer). The Ask answers no to it whatever the margin.</summary>
    private bool WontFetchAsked(int asked) => _askingFor is { } item && WontFetch(asked, Book.Good(item));

    private bool WontFetch(int asked, Good good)
        => Book.WontFetch.TryGetValue(_names[asked], out IReadOnlyList<string>? wont) && wont.Contains(good.Name);

    private bool Short(int asked, Good good) => _purse.GetValueOrDefault(HouseholdOf(_names[asked])) < good.Price;

    private bool Running(int asked) => _openErrands.Any(id => _promises[id].By == _names[asked]);

    /// <summary>A runner at the counter buys the thing from whoever is serving there, if the sign
    /// says open and the household can pay; otherwise they try again tomorrow.</summary>
    private void BuyAtCounters(int m)
    {
        foreach (int id in _openErrands.ToList())
        {
            Promise pr = _promises[id];
            Person p = _people[_index[pr.By]];
            if (pr.FetchedAct >= 0 || p.Why != "errand" || p.Place != pr.Place || p.At.Chebyshev(pr.Spot) > _wander + 1 || !Free(p, m))
                continue;
            Good good = Book.Good(pr.Item!);
            string home = HouseholdOf(pr.By);
            Person? server = _people.FirstOrDefault(o => !o.Asleep && o.Place == pr.Place && o.Why == "work" && o.V.Job?.Place == pr.Place);
            bool ownShelf = pr.Place != "Mart" && _ao.Keepers.TryGetValue(pr.Place, out string? keeper) && _index.ContainsKey(keeper)
                && HouseholdOf(keeper) == home;
            p.Why = ""; // decide again next tick: home with it, or on to find the one it's for
            if (server is null || !Book.CounterFor(pr.Place).OpenAt(m) || !ownShelf && _purse.GetValueOrDefault(home) < good.Price)
            {
                _shutToday.Add((_index[pr.By], Clock.Day(m)));
                _log.Add($"{m} errand-shut {pr.By} {pr.Place} {(server is null ? "nobody" : "short")}");
                continue;
            }
            if (!ownShelf)
                Sell(pr.Place, home, good.Price);
            int actId = _acts.Count;
            Begin(m, _kindByName["Fetched"], p, injected: false, target: server.V.Name);
            _promises[id] = pr with { FetchedAct = actId };
            _log.Add($"{m} errand-bought {pr.By} {pr.Item} {good.Price:0}");
        }
    }

    /// <summary>Where the money for something bought goes: the chain's takings leave town (as its
    /// groceries' do); the store's and the saloon's go to the keeper's household, less what
    /// restocking sends out of town.</summary>
    private void Sell(string place, string buyer, double price)
    {
        if (place == "Mart" || !_ao.Keepers.TryGetValue(place, out string? keeper) || !_index.ContainsKey(keeper))
        {
            ToOutside(buyer, price);
            return;
        }
        string shop = HouseholdOf(keeper);
        Move(buyer, shop, price);
        ToOutside(shop, price * (place == "Saloon" ? _mo.SaloonRestock : _mo.StoreRestock));
    }

    /// <summary>A runner with the thing and the one it's for, both free and in reach: handed over,
    /// late if past due, and paid back unless they share a home.</summary>
    private void HandOver(int m)
    {
        foreach (int id in _openErrands.ToList())
        {
            Promise pr = _promises[id];
            if (pr.FetchedAct < 0)
                continue;
            Person r = _people[_index[pr.By]], n = _people[_index[pr.To]];
            if (!Free(r, m) || !Free(n, m) || r.Place != n.Place || r.At.Chebyshev(n.At) > _po.FarTiles)
                continue;
            bool late = m >= pr.DueTo;
            Good good = Book.Good(pr.Item!);
            int actId = _acts.Count;
            _errandSpeech[actId] = (pr.By, late ? "HandedOver.Late" : "HandedOver", pr.To, "HandedOver.Thanks", good.Phrase);
            Begin(m, _kindByName["HandedOver"], r, injected: false, target: pr.To, about: pr.MadeAct);
            if (HouseholdOf(pr.To) != HouseholdOf(pr.By))
                Move(HouseholdOf(pr.To), HouseholdOf(pr.By), good.Price);
            _promises[id] = pr with { State = PromiseState.Kept, Settled = m, SettledAct = actId, Late = late };
            SettleNeed(id, NeedState.Met, m);
            _openErrands.Remove(id);
            if (r.Why == "deliver")
                r.Why = "";
            _log.Add($"{m} errand-kept {pr.By} {pr.To} {pr.Item}{(late ? " late" : "")}");
        }
    }

    /// <summary>
    /// Where someone on an errand goes, in Decide after work and patrol: to the counter while its sign
    /// says open (unless it turned them away today); then, carrying it, to the one it's for, unless
    /// they share a home (they meet there). Looking reads memory and public knowledge only: where the
    /// person works, in working hours (anyone knows where the doctor works), then where this runner
    /// last saw them out of doors; each place once a day, for an hour.
    /// </summary>
    private bool ErrandGoal(Person p, int m)
    {
        if (!Errands)
            return false;
        int r = _index[p.V.Name], day = Clock.Day(m);
        Promise? buy = null, bring = null;
        foreach (int id in _openErrands)
        {
            Promise pr = _promises[id];
            if (pr.By != p.V.Name)
                continue;
            if (pr.FetchedAct < 0)
                buy ??= pr;
            else
                bring ??= pr;
        }
        if (buy is not null && !_shutToday.Contains((r, day)))
        {
            Counter c = Book.CounterFor(buy.Place);
            if (c.OpenAt(m) && c.OpenAt(m + ErrandLeadMinutes))
            {
                Goal(p, c.Place, c.Spot, m - Clock.OfDay(m) + c.To, "errand", null);
                return true;
            }
        }
        if (bring is null || HouseholdOf(bring.To) == HouseholdOf(p.V.Name))
            return false;
        if (p.Why == "deliver" && p.GoalUntil > m)
            return true;
        if (LookFor(p, bring.To, m) is not { } look)
            return false;
        _sought.Add((r, day, look.Place));
        Goal(p, look.Place, look.Spot, m + SeekMinutes, "deliver", null);
        return true;
    }

    private (string Place, Tile Spot)? LookFor(Person p, string who, int m)
    {
        int r = _index[p.V.Name], day = Clock.Day(m), t = Clock.OfDay(m);
        if (_cast[_index[who]].Job is { } job && job.WorksOn(Clock.Weekday(m)) && t >= job.Start && t < job.End - SeekMinutes
            && _places.ContainsKey(job.Place) && !_sought.Contains((r, day, job.Place)))
            return (job.Place, job.Spot);
        if (!_sightings.TryGetValue((p.V.Name, who), out var spans))
            return null;
        for (int i = spans.Count - 1; i >= 0; i--)
        {
            string place = spans[i].Place;
            if (!place.StartsWith("Home:", StringComparison.Ordinal) && _places.ContainsKey(place) && !_sought.Contains((r, day, place)))
                return (place, Middle(place));
        }
        return null;
    }

    /// <summary>A place's middle, or the nearest open tile to it.</summary>
    private Tile Middle(string place)
    {
        Location loc = _places[place];
        var mid = new Tile(loc.Width / 2, loc.Height / 2);
        if (loc.Walkable(mid))
            return mid;
        for (int d = 1; d < Math.Max(loc.Width, loc.Height); d++)
            for (int x = mid.X - d; x <= mid.X + d; x++)
                for (int y = mid.Y - d; y <= mid.Y + d; y++)
                    if (new Tile(x, y) is var tile && tile.Chebyshev(mid) == d && x >= 0 && y >= 0 && x < loc.Width && y < loc.Height && loc.Walkable(tile))
                        return tile;
        return mid;
    }
}
