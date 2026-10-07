namespace UnderGlass.Sim;

/// <summary>
/// The town's money (phase 0b; design rules 11, 12 and 15). Each household has a purse; each
/// person a pocket for their own wants. Money enters and leaves the town only through named
/// outside accounts (wages from away, pensions, the county, the chain store's head office,
/// wholesalers), so the town's total changes by exactly what flows across its edge.
/// </summary>
public sealed record Economy(
    /// <summary>Each household's purse at the start, in g.</summary>
    IReadOnlyDictionary<string, double> StartPurse,
    /// <summary>Weekly incomes: who earns, how much, and who pays (a household, <see cref="Simulation.Town"/>,
    /// or null for outside the town).</summary>
    IReadOnlyList<(string Who, double PerWeek, string? From)> Incomes,
    /// <summary>Weekly allowance from the person's own household purse to their pocket.</summary>
    IReadOnlyDictionary<string, double> Allowances,
    /// <summary>Where each household buys its groceries: "Store" (Pierre's) or "Mart" (the chain).</summary>
    IReadOnlyDictionary<string, string> GroceriesAt,
    /// <summary>What each person might come to want, as a price range.</summary>
    IReadOnlyDictionary<string, (double Min, double Max)> Wants,
    /// <summary>The county's weekly stipend to the town purse.</summary>
    double TownStipend,
    /// <summary>The town purse at the start.</summary>
    double TownStart);

/// <summary>Knobs for money and temptation (phase 0b). First guesses, from Stardew's prices
/// where it has them.</summary>
public sealed class MoneyOptions
{
    public double GroceriesPerPerson { get; set; } = 100;  // a week
    public double MartDiscount { get; set; } = 0.1;       // the chain is cheaper
    public double StoreRestock { get; set; } = 0.6;       // Pierre buys at 60% of shelf, from outside
    public double SaloonDrink { get; set; } = 12;          // once a day, for an adult who stops in
    public double SaloonRestock { get; set; } = 0.4;
    public double EarnerPocketShare { get; set; } = 0.15;  // of a wage, kept for oneself
    /// <summary>Someone without a want gains one with this chance a day.</summary>
    public double WantChancePerDay { get; set; } = 1.0 / 14;
    /// <summary>A want's pressure grows over this many days of waiting.</summary>
    public int WantPatienceDays { get; set; } = 14;
    public double WantWeight { get; set; } = 1.0;
    public double NeedWeight { get; set; } = 1.2;
    /// <summary>The pull of a dare for the bold: ThrillWeight x boldness.</summary>
    public double ThrillWeight { get; set; } = 0.15;
    /// <summary>Believed risk: (people in sight + 0.5) x severity of the next step for them x
    /// (1.2 - boldness) x RiskScale.</summary>
    public double RiskScale { get; set; } = 0.5;
    /// <summary>The chance per tick of acting is StealBase x (motive - risk), when positive.</summary>
    public double StealBase { get; set; } = 0.002;
    public (double Min, double Max) TheftValue { get; set; } = (20, 80);
    public double BinValue { get; set; } = 10;
    public double Fine { get; set; } = 100;
    public int ServiceMinutes { get; set; } = 180;
    /// <summary>Everyday spending: each week a purse spends this share of what it holds beyond
    /// BufferWeeks of its costs, half at Pierre's store and half out of town.</summary>
    public double SpendShare { get; set; } = 0.5;
    public double BufferWeeks { get; set; } = 4;
    /// <summary>The town purse keeps this much and spends half the rest on outside work.</summary>
    public double TownReserve { get; set; } = 3000;
    /// <summary>Pocket money beyond this is half spent each week the same way (a want is saved for
    /// first: the pocket keeps the price of an open want).</summary>
    public double PocketBuffer { get; set; } = 300;
}

public sealed partial class Simulation
{
    /// <summary>The town's own purse (the county stipend, fines, the teacher's pay).</summary>
    public const string Town = "Town";
    public const string Stole = "Stole", RummagedInBin = "RummagedInBin";
    /// <summary>The act kind for community service; its actor is the one serving.</summary>
    public const string Service = "Service";

    private readonly Economy? _economy;
    private readonly MoneyOptions _mo;
    private readonly Dictionary<string, double> _purse = new();
    private readonly Dictionary<string, double> _pocket = new();
    private readonly Dictionary<string, (double Price, int Since)> _want = new();
    private readonly Dictionary<int, double> _theftValue = new();
    private readonly List<(int ActId, string Who, string Motive)> _motives = new();
    private readonly HashSet<(string, int)> _drank = new();
    private double _outsideIn, _outsideOut;
    private readonly List<double> _townCash = new();
    /// <summary>Where each household buys its groceries now, and since when (S9).</summary>
    private readonly Dictionary<string, (string Shop, int Since)> _shopOf = new();

    private bool HasMoney => _economy is not null;

    private void StartMoney()
    {
        if (_economy is not { } e)
            return;
        foreach (string h in _cast.Select(v => v.Household).Distinct())
        {
            _purse[h] = e.StartPurse.GetValueOrDefault(h);
            _shopOf[h] = (e.GroceriesAt.GetValueOrDefault(h, "Store"), 0);
        }
        _purse[Town] = e.TownStart;
        foreach (string n in _names)
            _pocket[n] = 0;
        _townCash.Add(TownCash());
    }

    /// <summary>All the money in town: every purse and pocket.</summary>
    public double TownCash() => _purse.Values.Sum() + _pocket.Values.Sum();

    private void FromOutside(string purse, double g) { _purse[purse] = _purse.GetValueOrDefault(purse) + g; _outsideIn += g; }
    private void ToOutside(string purse, double g) { _purse[purse] -= g; _outsideOut += g; }
    private void Move(string from, string to, double g) { _purse[from] -= g; _purse[to] = _purse.GetValueOrDefault(to) + g; }

    private string HouseholdOf(string name) => _cast[_index[name]].Household;

    /// <summary>A household's weekly costs: groceries at its shop's price.</summary>
    private double WeekCost(string household)
    {
        int people = _cast.Count(v => v.Household == household);
        double price = ShopOf(household) == "Mart" ? 1 - _mo.MartDiscount : 1;
        return people * _mo.GroceriesPerPerson * price;
    }

    private string ShopOf(string household)
        => _shopOf.TryGetValue(household, out var s) ? s.Shop : _economy!.GroceriesAt.GetValueOrDefault(household, "Store");

    /// <summary>Monday 00:00: wages and pensions, allowances, groceries and restocking.</summary>
    private void Payday(int m)
    {
        Economy e = _economy!;
        if (Steering)
            ShopChoice(m);
        FromOutside(Town, e.TownStipend);
        foreach (var (who, perWeek, from) in e.Incomes.OrderBy(i => i.Who, StringComparer.Ordinal))
        {
            if (!_index.ContainsKey(who))
                continue;
            string home = HouseholdOf(who);
            if (from is null) FromOutside(home, perWeek);
            else Move(from, home, perWeek);
            double keep = perWeek * _mo.EarnerPocketShare;
            _purse[home] -= keep;
            _pocket[who] += keep;
        }
        foreach (var (who, perWeek) in e.Allowances.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            if (!_index.ContainsKey(who))
                continue;
            string home = HouseholdOf(who);
            double give = Math.Min(perWeek, Math.Max(0, _purse[home]));
            _purse[home] -= give;
            _pocket[who] += give;
        }
        foreach (string h in _cast.Select(v => v.Household).Distinct().OrderBy(h => h, StringComparer.Ordinal))
        {
            double cost = WeekCost(h);
            string shop = ShopOf(h);
            string? keeper = shop == "Store" ? _ao.Keepers.GetValueOrDefault("Store") : null;
            if (keeper is not null && HouseholdOf(keeper) == h)
                ToOutside(h, cost * _mo.StoreRestock);     // the shop's own family eats at cost
            else if (keeper is not null)
            {
                Move(h, HouseholdOf(keeper), cost);
                ToOutside(HouseholdOf(keeper), cost * _mo.StoreRestock);
            }
            else
                ToOutside(h, cost);                          // the chain's takings leave town
        }
        Spending();
        _log.Add($"{m} payday town {TownCash():0}");
    }

    /// <summary>Everyday spending (clothes, tools, outings, repairs): what a household holds beyond
    /// a few weeks of costs is half spent, half at Pierre's store and half out of town.</summary>
    private void Spending()
    {
        string? store = _ao.Keepers.GetValueOrDefault("Store");
        string? shopHome = store is not null && _index.ContainsKey(store) ? HouseholdOf(store) : null;
        foreach (string h in _purse.Keys.Where(k => k != Town).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            double spare = _purse[h] - _mo.BufferWeeks * Math.Max(WeekCost(h), _mo.GroceriesPerPerson);
            if (spare <= 0)
                continue;
            double spend = spare * _mo.SpendShare;
            if (shopHome is not null && shopHome != h)
            {
                Move(h, shopHome, spend / 2);
                ToOutside(shopHome, spend / 2 * _mo.StoreRestock);
                ToOutside(h, spend / 2);
            }
            else
                ToOutside(h, spend);
        }
        if (_purse[Town] > _mo.TownReserve)
            ToOutside(Town, (_purse[Town] - _mo.TownReserve) * _mo.SpendShare);
        foreach (string n in _names)
        {
            double keep = Math.Max(_mo.PocketBuffer, _want.TryGetValue(n, out var w) ? w.Price : 0);
            double spare = _pocket[n] - keep;
            if (spare <= 0)
                continue;
            double spend = spare * _mo.SpendShare;
            _pocket[n] -= spend;
            if (shopHome is not null)
            {
                _purse[shopHome] += spend / 2;
                ToOutside(shopHome, spend / 2 * _mo.StoreRestock);
                _outsideOut += spend / 2;
            }
            else
                _outsideOut += spend;
        }
    }

    /// <summary>Each tick: an adult at the saloon in the evening buys a drink, once a day.</summary>
    private void Drinks(int m)
    {
        int t = Clock.OfDay(m);
        if (t < Clock.At(18) || !_ao.Keepers.TryGetValue("Saloon", out string? gus) || !_index.ContainsKey(gus))
            return;
        string bar = HouseholdOf(gus);
        foreach (Person p in _people)
        {
            if (p.Asleep || p.Place != "Saloon" || p.V.Age < 18 || p.V.Job?.Place == "Saloon" || !_drank.Add((p.V.Name, Clock.Day(m))))
                continue;
            string home = HouseholdOf(p.V.Name);
            if (home == bar)
                continue;
            Move(home, bar, _mo.SaloonDrink);
            ToOutside(bar, _mo.SaloonDrink * _mo.SaloonRestock);
        }
    }

    /// <summary>Midnight: wants come and go; anyone who can afford theirs buys it at the store.</summary>
    private void Wants(int m)
    {
        Economy e = _economy!;
        string? store = _ao.Keepers.GetValueOrDefault("Store");
        foreach (string n in _names)
        {
            if (_want.TryGetValue(n, out var w))
            {
                if (_pocket[n] >= w.Price)
                {
                    _pocket[n] -= w.Price;
                    if (store is not null && _index.ContainsKey(store))
                    {
                        _purse[HouseholdOf(store)] += w.Price;
                        ToOutside(HouseholdOf(store), w.Price * _mo.StoreRestock);
                    }
                    else
                        _outsideOut += w.Price;
                    _want.Remove(n);
                    _log.Add($"{m} bought {n} {w.Price:0}");
                }
                continue;
            }
            if (e.Wants.TryGetValue(n, out var range) && Rng.Unit(_seed, "want", n, Clock.Day(m).ToString()) < _mo.WantChancePerDay)
            {
                double price = Math.Round(range.Min + (range.Max - range.Min) * Rng.Unit(_seed, "price", n, Clock.Day(m).ToString()));
                _want[n] = (price, m);
                _log.Add($"{m} wants {n} {price:0}");
            }
        }
    }

    /// <summary>How hard an unmet want pulls: the share still unaffordable, growing with the wait.</summary>
    private double WantPressure(string n, int m)
        => _want.TryGetValue(n, out var w)
            ? Math.Min(1, (m - w.Since) / (double)(_mo.WantPatienceDays * Clock.MinutesPerDay)) * Math.Max(0, (w.Price - _pocket[n]) / w.Price)
            : 0;

    /// <summary>How short the household is of a week's groceries.</summary>
    private double NeedPressure(string n)
    {
        string h = HouseholdOf(n);
        double cost = WeekCost(h);
        return cost <= 0 ? 0 : Math.Clamp((cost - _purse[h]) / cost, 0, 1);
    }

    /// <summary>
    /// Temptation (rules 15, 16 and 17): each tick, someone with the vice, free and somewhere it
    /// can happen, weighs their motive (an unmet want, a household in need, the thrill of it for
    /// the bold) against the risk they believe they run: how many people they can see now, how
    /// bad the next step on the ladder would be for them, and how much they fear it. The excess
    /// gives the chance of doing it. Nobody robs the shop they work in.
    /// </summary>
    private void Temptation(int m)
    {
        foreach (Person p in _people)
        {
            Villager v = p.V;
            foreach (string kindName in new[] { RummagedInBin, Stole })
            {
                if (!v.Acts.ContainsKey(kindName) || !Free(p, m) || _kinds.FirstOrDefault(k => k.Name == kindName) is not { } kind
                    || !kind.FitsAge(v.Age) || kind.Allowed.Count > 0 && !kind.Allowed.Contains(p.Place) || v.Job?.Place == p.Place)
                    continue;
                double want = kindName == Stole ? _mo.WantWeight * WantPressure(v.Name, m) : 0;
                double need = _mo.NeedWeight * NeedPressure(v.Name);
                double thrill = _mo.ThrillWeight * CharacterOf(_index[v.Name]).Boldness;
                // S8 (rule 17): a grudge against the keeper is a motive too. Kin are not exempt.
                string? keeper = _ao.Keepers.GetValueOrDefault(p.Place);
                double grievance = Steering && keeper is not null && keeper != v.Name && _index.ContainsKey(keeper)
                    ? _fo.GrievanceWeight * Math.Max(0, -St(v.Name, keeper) - _fo.GrievanceAt)
                    : 0;
                double motive = want + need + thrill + grievance;
                int inSight = _people.Count(o => o != p && !o.Asleep && o.Place == p.Place && o.At.Chebyshev(p.At) <= _po.FarTiles
                                                 && Perception.LineOfSight(_places[p.Place], p.At, o.At, _po) > 0);
                double severity = 1 + (int)Authority.StepFor(_record.GetValueOrDefault(v.Name));
                double risk = (inSight + 0.5) * severity * (1.2 - CharacterOf(_index[v.Name]).Boldness) * _mo.RiskScale;
                if (motive <= risk || Rng.Unit(_seed, "tempt", kindName, v.Name, m.ToString()) >= _mo.StealBase * (motive - risk))
                    continue;
                string why = grievance > want && grievance > need && grievance > thrill ? "grievance"
                    : want >= need && want >= thrill ? $"want {_want[v.Name].Price:0}g" : need >= thrill ? "need" : "thrill";
                Begin(m, kind, p, injected: false);
                if (why == "grievance")
                    Why(m, v.Name, kindName, keeper!);
                _motives.Add((_acts[^1].Id, v.Name, why));
                _log.Add($"{m} motive {v.Name} {kindName} {why} (motive {motive:0.00}, risk {risk:0.00}, {inSight} in sight)");
                break;
            }
        }
    }

    /// <summary>What a theft or a rummage gains, and loses its victim (called as it begins).</summary>
    private void Gains(Act act, Person actor)
    {
        if (!HasMoney)
            return;
        if (act.Kind == Stole)
        {
            var (lo, hi) = _mo.TheftValue;
            double value = Math.Round(lo + (hi - lo) * Rng.Unit(_seed, "value", act.Id.ToString()));
            _theftValue[act.Id] = value;
            // Goods taken: their worth comes into town (they were bought from outside); the shop pays
            // to replace them, unless it is the chain, whose head office absorbs the loss.
            _pocket[actor.V.Name] += value;
            _outsideIn += value;
            if (act.Location != "Mart" && _ao.Keepers.TryGetValue(act.Location, out string? keeper) && _index.ContainsKey(keeper))
                ToOutside(HouseholdOf(keeper), value * _mo.StoreRestock);
            if (_want.ContainsKey(actor.V.Name))
                _want[actor.V.Name] = _want[actor.V.Name] with { Since = act.Tick }; // sated for a while
        }
        else if (act.Kind == RummagedInBin)
        {
            _purse[HouseholdOf(actor.V.Name)] += _mo.BinValue;
            _outsideIn += _mo.BinValue; // scraps were nobody's money
        }
    }

    /// <summary>The second step of the ladder: pay back the goods and a fine to the town. Whatever
    /// can't be paid becomes community service. Back: what was paid back to the keeper.</summary>
    private bool PayUp(int actId, string accused, int m, out double back)
    {
        back = 0;
        if (!HasMoney)
            return false;
        double owed = _theftValue.GetValueOrDefault(actId) + _mo.Fine;
        string home = HouseholdOf(accused);
        double fromPocket = Math.Min(owed, Math.Max(0, _pocket[accused]));
        _pocket[accused] -= fromPocket;
        double fromPurse = Math.Min(owed - fromPocket, Math.Max(0, _purse[home]));
        _purse[home] -= fromPurse;
        double paid = fromPocket + fromPurse;
        back = Math.Min(paid, _theftValue.GetValueOrDefault(actId));
        Act act = _acts[actId];
        if (back > 0 && _ao.Keepers.TryGetValue(act.Location, out string? keeper) && _index.ContainsKey(keeper) && act.Location != "Mart")
            _purse[HouseholdOf(keeper)] += back;
        else
        {
            _outsideOut += back; // the chain's head office is repaid
            back = 0;            // nothing came back to a keeper in town
        }
        _purse[Town] += paid - Math.Min(paid, _theftValue.GetValueOrDefault(actId));
        _log.Add($"{m} paid {accused} {paid:0} of {owed:0} for {actId}");
        return paid >= owed;
    }

    private void CloseMoneyDay()
    {
        if (HasMoney)
            _townCash.Add(TownCash());
    }
}
