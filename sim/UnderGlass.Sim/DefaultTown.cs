namespace UnderGlass.Sim;

/// <summary>
/// The phase-0a town: 5 public places, one home per household, 12 of Stardew's villagers
/// (private prototype only, design section 3) and the newcomer. Routines, traits and vices are
/// first guesses, coded here for speed; they move to JSON once the rules hold.
/// Ticks: 0 is 6:00, 6 ticks an hour, 120 a day.
/// </summary>
public static class DefaultTown
{
    public const string Newcomer = "Newcomer";

    private static Location Room(string name, int w, int h, bool outdoor, params (int X, int Y, char C)[] marks)
    {
        var rows = Enumerable.Range(0, h).Select(_ => new string('.', w).ToCharArray()).ToArray();
        foreach (var (x, y, c) in marks)
            rows[y][x] = c;
        return new Location(name, outdoor, rows.Select(r => new string(r)).ToList());
    }

    private static (int, int, char)[] Line(int x0, int y0, int x1, int y1, char c)
        => Enumerable.Range(0, Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1)
            .Select(i => (x0 + Math.Sign(x1 - x0) * i, y0 + Math.Sign(y1 - y0) * i, c)).ToArray();

    public static IReadOnlyList<Location> Locations()
    {
        var places = new List<Location>
        {
            // The square: a fence runs across part of it.
            Room("Square", 30, 20, true, Line(4, 9, 18, 9, '+')),
            // The saloon: a back room behind a wall with a door at x=10.
            Room("Saloon", 20, 12, false, Line(0, 7, 9, 7, '#').Concat(Line(11, 7, 19, 7, '#')).ToArray()),
            // The store: two rows of shelves.
            Room("Store", 16, 10, false, Line(2, 4, 12, 4, '+').Concat(Line(2, 7, 12, 7, '+')).ToArray()),
            Room("Beach", 30, 12, true),
            // The clinic yard with the bin at (11,5), screened by bushes.
            Room("ClinicYard", 14, 10, true, Line(7, 2, 7, 8, '+')),
        };
        foreach (string home in Cast().Select(v => v.Household).Distinct().OrderBy(h => h, StringComparer.Ordinal))
            places.Add(Room("Home:" + home, 10, 8, false));
        return places;
    }

    public static IReadOnlyList<ActKind> Acts() => new[]
    {
        new ActKind("RummagedInBin", 4.0, -1, 2, 3, 0.15, new[] { "ClinicYard", "Square" }),
        new ActKind("Stole", 4.5, -1, 2, 2, 0.08, new[] { "Store" }),
        new ActKind("DrunkScene", 3.0, -1, 1, 3, 0.3, new[] { "Saloon" }),
        new ActKind("Argued", 3.0, -1, 1, 2, 0.4, Array.Empty<string>()),
        new ActKind("HelpedSomeone", 2.0, 1, 2, 2, 0.4, Array.Empty<string>()),
        new ActKind("GaveGift", 1.5, 1, 1, 1, 1.0, Array.Empty<string>()),
        new ActKind("Stumbled", 1.0, 0, 1, 1, 0.5, Array.Empty<string>()),
    };

    private static RoutineStep S(int hour, string place, int x, int y) => new((hour - 6) * 6, place, new Tile(x, y));
    private static Dictionary<string, double> A(params (string Kind, double W)[] acts)
        => acts.ToDictionary(a => a.Kind, a => a.W);

    public static IReadOnlyList<Villager> Cast()
    {
        var everyday = ("GaveGift", 0.5);
        var trip = ("Stumbled", 0.3);
        return new List<Villager>
        {
            new("Alex", "JoshHouse", "young man", new(0.6, 0.8, 0.3, 0.3),
                new[] { S(6, "Home:JoshHouse", 3, 3), S(9, "Beach", 5, 6), S(14, "Square", 20, 12), S(20, "Saloon", 6, 4), S(24, "Home:JoshHouse", 3, 3) },
                A(everyday, trip, ("Argued", 0.5), ("Stole", 0.15)), new[] { "Haley" }),
            new("Emily", "HaleyHouse", "young woman", new(0.7, 0.5, 0.7, 0.7),
                new[] { S(6, "Home:HaleyHouse", 4, 4), S(10, "Store", 8, 5), S(13, "Home:HaleyHouse", 4, 4), S(17, "Saloon", 12, 3), S(24, "Home:HaleyHouse", 4, 4) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Gus", "Shane" }),
            new("Gus", "Saloon", "older man", new(0.8, 0.5, 0.6, 0.6),
                new[] { S(6, "Home:Saloon", 2, 2), S(10, "Saloon", 10, 2) },
                A(everyday, trip, ("HelpedSomeone", 0.3)), new[] { "Emily", "Pam" }),
            new("Haley", "HaleyHouse", "young woman", new(0.7, 0.7, 0.2, 0.3),
                new[] { S(6, "Home:HaleyHouse", 5, 4), S(11, "Square", 8, 5), S(16, "Beach", 10, 5), S(20, "Home:HaleyHouse", 5, 4) },
                A(everyday, trip, ("Argued", 0.5)), new[] { "Alex" }),
            new("Harvey", "Clinic", "older man", new(0.4, 0.3, 0.8, 0.4),
                new[] { S(6, "Home:Clinic", 2, 2), S(9, "ClinicYard", 3, 3), S(17, "Square", 14, 14), S(20, "Saloon", 3, 3), S(23, "Home:Clinic", 2, 2) },
                A(everyday, trip, ("HelpedSomeone", 0.6)), new[] { "Pierre" }),
            new("Leah", "Cottage", "young woman", new(0.5, 0.5, 0.7, 0.5),
                new[] { S(6, "Home:Cottage", 2, 2), S(11, "Beach", 20, 4), S(17, "Saloon", 14, 5), S(22, "Home:Cottage", 2, 2) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Penny" }),
            new("Lewis", "Manor", "older man", new(0.6, 0.6, 0.6, 0.6),
                new[] { S(6, "Home:Manor", 2, 2), S(8, "Square", 15, 10), S(18, "Saloon", 8, 3), S(22, "Home:Manor", 2, 2) },
                A(everyday, trip, ("Argued", 0.3)), new[] { "Marnie", "Pierre" }),
            new("Marnie", "Ranch", "older woman", new(0.7, 0.4, 0.6, 0.5),
                new[] { S(6, "Home:Ranch", 3, 3), S(10, "Store", 10, 2), S(14, "Square", 10, 15), S(19, "Home:Ranch", 3, 3) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Lewis" }),
            new("Pam", "Trailer", "older woman", new(0.8, 0.7, 0.3, 0.3),
                new[] { S(6, "Home:Trailer", 2, 2), S(11, "Square", 22, 15), S(18, "Saloon", 5, 8), S(24, "Home:Trailer", 2, 2) },
                A(everyday, trip, ("DrunkScene", 1.0), ("RummagedInBin", 0.2)), new[] { "Gus" }),
            new("Penny", "Trailer", "young woman", new(0.4, 0.2, 0.7, 0.3),
                new[] { S(6, "Home:Trailer", 4, 4), S(9, "Square", 12, 8), S(14, "Store", 5, 8), S(20, "Home:Trailer", 4, 4) },
                A(everyday, trip, ("HelpedSomeone", 0.5)), new[] { "Leah" }),
            new("Pierre", "SeedShop", "older man", new(0.6, 0.6, 0.4, 0.5),
                new[] { S(6, "Home:SeedShop", 2, 2), S(9, "Store", 3, 2), S(23, "Home:SeedShop", 2, 2) },
                A(everyday, trip, ("Argued", 0.5)), new[] { "Lewis", "Harvey" }),
            new("Shane", "Ranch", "young man", new(0.3, 0.6, 0.4, 0.2),
                new[] { S(6, "Home:Ranch", 5, 5), S(9, "Store", 12, 6), S(17, "Saloon", 16, 9), S(24, "Home:Ranch", 5, 5) },
                A(trip, ("DrunkScene", 1.0), ("Argued", 0.4), ("Stole", 0.3), ("RummagedInBin", 0.3)), new[] { "Emily" }),
            new(Newcomer, "Farm", "newcomer", new(0.3, 0.4, 0.5, 0.5),
                new[] { S(6, "Home:Farm", 2, 2), S(9, "ClinicYard", 10, 6), S(11, "Square", 25, 5), S(16, "Store", 7, 2), S(19, "Beach", 14, 8), S(21, "Saloon", 17, 4), S(24, "Home:Farm", 2, 2) },
                A(everyday, trip, ("RummagedInBin", 1.0), ("Stole", 0.5)), Array.Empty<string>()),
        };
    }
}
