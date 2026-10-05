namespace UnderGlass.Sim;

/// <summary>A tile position inside a location.</summary>
public readonly record struct Tile(int X, int Y)
{
    public int Chebyshev(Tile o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Y - o.Y));
}

/// <summary>
/// A place. Rows are strings: '.' open, '#' blocks sight (wall), '+' halves sight (fence, bush,
/// shelf). Outdoor places are darker at night.
/// </summary>
public sealed record Location(string Name, bool Outdoor, IReadOnlyList<string> Rows)
{
    public int Width => Rows[0].Length;
    public int Height => Rows.Count;
    public char At(Tile t) => t.Y < 0 || t.Y >= Height || t.X < 0 || t.X >= Width ? '#' : Rows[t.Y][t.X];
    public bool Walkable(Tile t) => At(t) == '.';
}

/// <summary>
/// The traits that set how strongly each law acts on a person (design 3a, law 12). All 0..1.
/// SelfRegard is Spinoza's self-esteem: low self-regard with high boldness gives confident
/// wrong guesses (design 11a).
/// </summary>
public sealed record Temperament(double Chattiness, double Boldness, double Understanding, double SelfRegard);

/// <summary>One block of a day: from this tick, be at this spot.</summary>
public sealed record RoutineStep(int FromTick, string Location, Tile Spot);

/// <summary>
/// A villager. Household and friends seed familiarity. Vices are act kinds the villager may
/// commit, with a weight (design rule 15).
/// </summary>
public sealed record Villager(
    string Name,
    string Household,
    string Kind,
    Temperament Temperament,
    IReadOnlyList<RoutineStep> Routine,
    IReadOnlyDictionary<string, double> Acts,
    IReadOnlyList<string> Friends);

/// <summary>
/// A kind of act and how it is perceived (design rule 4 row, reduced for phase 0a).
/// ReadTicks: how long it takes to understand what is happening (a glance is 1).
/// Allowed: the locations it can happen in (empty: anywhere).
/// </summary>
public sealed record ActKind(
    string Name,
    double Juiciness,
    int Valence,
    int ReadTicks,
    int Duration,
    double PerDay,
    IReadOnlyList<string> Allowed)
{
    /// <summary>A bad act at base juiciness 4 or more (D34; design rule 9).</summary>
    public bool IsScandal => Valence < 0 && Juiciness >= 4;
}

/// <summary>Something that happened.</summary>
public sealed record Act(int Id, int Tick, string Actor, string Kind, string Location, Tile At);

/// <summary>Where a belief came from.</summary>
public enum Source { Witnessed, Told }

/// <summary>
/// What one villager believes about one act. Actor is who they think did it, or null for
/// "someone". Juiciness is the value when they got it; it fades from the act's tick.
/// Chain lists the tellers, nearest first, empty for a witness.
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
    IReadOnlyList<string> Chain);

/// <summary>A villager confronted someone over a scandal (design rule 9).</summary>
public sealed record Confrontation(int ActId, int Tick, string By, string Target, bool Correct);
