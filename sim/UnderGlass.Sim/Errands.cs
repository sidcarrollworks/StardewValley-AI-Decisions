namespace UnderGlass.Sim;

/// <summary>Something sold at a counter (needs spec): its name, how it is said in a line ("some flour",
/// so a line never puts an article in front of the placeholder), where it is sold, its price on the
/// town's own scale (a drink at the Saloon is 12), and the youngest age it is sold to.</summary>
public sealed record Good(string Name, string Phrase, string Place, double Price, int MinAge = 0);

/// <summary>A counter's hours as its sign shows them: anyone can know them, as a sign on the door is
/// public, so deciding to go reads no one's true position. Buying still needs someone at work there
/// when the errand-runner arrives. To may be a whole day (the Saloon serves until midnight).</summary>
public sealed record Counter(string Place, Tile Spot, int From, int To, IReadOnlyList<int> DaysShut)
{
    public bool OpenAt(int minute)
        => !DaysShut.Contains(Clock.Weekday(minute)) && Clock.OfDay(minute) >= From && Clock.OfDay(minute) < To;
}

/// <summary>The town's errands book (needs spec): what is sold where, each counter's sign, the things
/// each person comes to need in their own life, and what someone will not fetch for anyone (Penny
/// will not buy beer, even for her mother).</summary>
public sealed record ErrandBook(IReadOnlyList<Good> Goods, IReadOnlyList<Counter> Counters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Uses, IReadOnlyDictionary<string, IReadOnlyList<string>> WontFetch)
{
    public Good Good(string name) => Goods.First(g => g.Name == name);
    public Counter CounterFor(string place) => Counters.First(c => c.Place == place);
}

/// <summary>How a need stands. Append only.</summary>
public enum NeedState { Open, Promised, Met, Lapsed }

/// <summary>A need for one named thing (needs spec): who needs it, what, when it arose, until when it
/// waits for someone to promise it, how it stands, the promise made for it, and when it was settled.</summary>
public sealed record Need(int Id, int Tick, string Holder, string Item, int Until,
    NeedState State = NeedState.Open, int Promise = -1, int Settled = -1);
