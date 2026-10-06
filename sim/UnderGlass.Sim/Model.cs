namespace UnderGlass.Sim;

/// <summary>
/// The clock (design rule 1, decided 2026-10-05): 24 hours, the date changes at midnight, and
/// there is no end of the day. Time is counted in game minutes from midnight of day 0. Routines
/// and chats run on <see cref="TickMinutes"/>-minute ticks; acts and perception run every minute.
/// </summary>
public static class Clock
{
    public const int MinutesPerDay = 1440;
    public const int TickMinutes = 5;
    public const int DaysPerWeek = 7;
    public const int DaysPerSeason = 28;
    /// <summary>About 20 real minutes per game day (Sid, 2026-10-05); a parameter for later.</summary>
    public const double RealSecondsPerGameMinute = 20.0 * 60 / MinutesPerDay;

    public static int Day(int minute) => minute / MinutesPerDay;
    public static int OfDay(int minute) => minute % MinutesPerDay;
    /// <summary>0 is the first day of the week.</summary>
    public static int Weekday(int minute) => Day(minute) % DaysPerWeek;
    public static int At(int hour, int min = 0) => hour * 60 + min;
    public static string Format(int minute) => $"d{Day(minute)} {OfDay(minute) / 60:00}:{OfDay(minute) % 60:00}";
}

/// <summary>A tile position inside a location.</summary>
public readonly record struct Tile(int X, int Y)
{
    public int Chebyshev(Tile o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Y - o.Y));
}

/// <summary>
/// A place. Rows are strings: '.' open, '#' blocks sight (wall), '+' halves sight (fence, bush,
/// shelf). Only '.' can be walked on. Outdoor places are darker at night.
/// </summary>
public sealed record Location(string Name, bool Outdoor, IReadOnlyList<string> Rows)
{
    public int Width => Rows[0].Length;
    public int Height => Rows.Count;
    public char At(Tile t) => t.Y < 0 || t.Y >= Height || t.X < 0 || t.X >= Width ? '#' : Rows[t.Y][t.X];
    public bool Walkable(Tile t) => At(t) == '.';
}

/// <summary>A door between two places: stepping onto <paramref name="DoorA"/> in A puts you on
/// <paramref name="DoorB"/> in B, and back. Roads are places too, so walkers are seen on them.</summary>
public sealed record Link(string A, Tile DoorA, string B, Tile DoorB);

/// <summary>
/// The traits that set how strongly each law acts on a person (design 3a, law 12). All 0..1.
/// SelfRegard is Spinoza's self-esteem: low self-regard with high boldness gives confident
/// wrong guesses (design 11a).
/// </summary>
public sealed record Temperament(double Chattiness, double Boldness, double Understanding, double SelfRegard);

/// <summary>
/// A body (design rule 1): the energy bar's size (stamina for hard work), and how empty it gets
/// before this person feels like going to bed at a neutral hour of the body clock (10:00 or 22:00),
/// as a share of the bar; night owls are lower. A negative BedAt never feels tired, which only
/// tests use.
/// </summary>
public sealed record Body(double MaxEnergy, double BedAt);

/// <summary>A job: where, the hours (minutes of the day, End at most 1440), the weekdays off, and
/// how tiring it is (1 is an ordinary waking hour's drain at work).</summary>
public sealed record Job(string Place, Tile Spot, int Start, int End, IReadOnlyList<int> DaysOff, double Effort)
{
    public bool WorksOn(int weekday) => !DaysOff.Contains(weekday);
}

/// <summary>A free-time haunt: a spot this person likes between From and To (minutes of the day;
/// To below From runs past midnight), with a weight against their other haunts.</summary>
public sealed record Haunt(string Place, Tile Spot, int From, int To, double Weight)
{
    public bool Open(int minuteOfDay) => From <= To
        ? minuteOfDay >= From && minuteOfDay < To
        : minuteOfDay >= From || minuteOfDay < To;
}

/// <summary>
/// A villager. Household names the home (<c>Home:&lt;household&gt;</c>) and seeds familiarity.
/// The day comes from the job, the haunts and the body (design rule 1). Acts are the act kinds the
/// villager may commit, with a weight (design rule 15).
/// </summary>
public sealed record Villager(
    string Name,
    string Household,
    string Kind,
    Temperament Temperament,
    Body Body,
    Job? Job,
    IReadOnlyList<Haunt> Haunts,
    IReadOnlyDictionary<string, double> Acts,
    IReadOnlyList<string> Friends)
{
    public string Home => "Home:" + Household;
}

/// <summary>
/// A hub (design rule 11): a time when the town gathers at one place, such as noon in the square,
/// evenings at the saloon or market day. While it is on, anyone free may pick it like a haunt,
/// with this weight, and stands somewhere within Radius tiles of Center. Weekdays empty: every day.
/// OnlyDay: a one-off on that day of the run (the opening town meeting).
/// </summary>
public sealed record Gathering(string Name, string Place, Tile Center, int Radius, int From, int To,
    IReadOnlyList<int> Weekdays, double Weight, int? OnlyDay = null)
{
    public bool On(int minute) => (Weekdays.Count == 0 || Weekdays.Contains(Clock.Weekday(minute)))
        && (OnlyDay is null || Clock.Day(minute) == OnlyDay)
        && Clock.OfDay(minute) >= From && Clock.OfDay(minute) < To;
}

/// <summary>The four tiers of a story (design rule 9, decided 2026-10-05).</summary>
public enum Tier { Trivia, News, Scandal, Upheaval }

/// <summary>
/// What an act leaves behind for someone to find later (design principle 1): missing stock, a bin
/// left scattered. KeeperOnly: only the keeper of the place can notice it (a stock count); else
/// anyone who can see the spot. It lasts LastsMinutes, and each person able to notice it does so
/// with NoticePerHour x the clarity of their view (1 for a keeper's count).
/// </summary>
public sealed record TraceKind(string Name, bool KeeperOnly, int LastsMinutes, double NoticePerHour);

/// <summary>
/// A kind of act and how it is perceived (design rule 4 row, reduced for phase 0a).
/// ReadMinutes: how long it takes to understand what is happening (a glance is 1).
/// DurationMinutes: how long it lasts. PerDay: town-wide rate when someone is able to.
/// Allowed: the locations it can happen in (empty: anywhere). Trace: what it leaves behind.
/// </summary>
public sealed record ActKind(
    string Name,
    double Juiciness,
    int Valence,
    int ReadMinutes,
    int DurationMinutes,
    double PerDay,
    IReadOnlyList<string> Allowed,
    bool Upheaval = false,
    TraceKind? Trace = null)
{
    /// <summary>A bad act at base juiciness 4 or more (D34; design rule 9).</summary>
    public bool IsScandal => !Upheaval && Valence < 0 && Juiciness >= 4;

    public Tier Tier => Upheaval ? Tier.Upheaval : IsScandal ? Tier.Scandal : Juiciness >= 2 ? Tier.News : Tier.Trivia;
}

/// <summary>Something that happened, at a game minute. Injected acts were placed by the harness to
/// measure spread.</summary>
public sealed record Act(int Id, int Tick, string Actor, string Kind, string Location, Tile At, bool Injected = false);

/// <summary>Where a belief came from. Found: from a trace, after the fact (never with a name).</summary>
public enum Source { Witnessed, Told, Found }

/// <summary>
/// What one villager believes about one act. Actor is who they think did it, or null for
/// "someone". Juiciness is the value when they got it; it fades from GotTick (a game minute).
/// Chain lists the tellers, nearest first, empty for a witness. Suspects: for a "someone" story,
/// who was seen around the place at the time (pieced together from sightings, or heard with the
/// story), most suspected first; null until pieced together.
/// </summary>
public sealed record Belief(
    int ActId,
    string Kind,
    string? Actor,
    double Confidence,
    double Clarity,
    Source Source,
    double Juiciness,
    int GotTick,
    IReadOnlyList<string> Chain,
    IReadOnlyList<string>? Suspects = null);

/// <summary>A villager confronted someone over a scandal (design rule 9).</summary>
public sealed record Confrontation(int ActId, int Tick, string By, string Target, bool Correct);

/// <summary>One night's sleep (or nap): when it began and ended, and whether an alarm was slept
/// through. WokeAt is null if the run ended first.</summary>
public sealed record Sleep(string Name, int SleptAt, int? WokeAt, double EnergyAtSleep, bool MissedAlarm, bool Collapsed);

/// <summary>Who was around when an act began (a diagnostic for spread): awake within sight range
/// in the same place, awake in the same place but farther, awake elsewhere, and asleep. The actor
/// is not counted.</summary>
public sealed record Scene(int InRange, int SamePlace, int Elsewhere, int Asleep);

/// <summary>The steps of the ladder of consequences (design rule 16). Ban is the keeper's choice
/// and Watched comes with Service; both arrive with money and shops (0b).</summary>
public enum Consequence { Warning, RestitutionAndFine, Ban, Service, Watched, Detained }

/// <summary>What someone told the authority about a scandal (design rule 16): who they say did it
/// (null: "someone"), how sure they are, whether they saw it or found the trace themselves, and
/// when it reached the mayor (later than it was told, if the constable carried it). Nearby: who
/// they saw around the place at the time, when they can't name the culprit. Since and Until: when
/// they place it (-1: unknown).</summary>
public sealed record Account(int ActId, string From, string? Actor, double Confidence, bool FirstHand, int Tick,
    IReadOnlyList<string>? Nearby = null, int Since = -1, int Until = -1);

/// <summary>The mayor's decision on a scandal. LetOff: the mayor knew and went easy on someone
/// close (rare for Lewis); no consequence follows.</summary>
public sealed record Verdict(int ActId, int Tick, string By, string Accused, bool Correct, Consequence Step, bool LetOff);

/// <summary>A town vote for an office, such as the constable at the opening meeting.</summary>
public sealed record Election(string Office, int Tick, string Winner, IReadOnlyDictionary<string, int> Votes);
