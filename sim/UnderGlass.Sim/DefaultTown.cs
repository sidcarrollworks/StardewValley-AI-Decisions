namespace UnderGlass.Sim;

/// <summary>
/// The phase-0a town: six public places, four roads, a farm, one home per household, 12 of
/// Stardew's villagers (private prototype only, design section 3) and the newcomer. Jobs, haunts,
/// bodies, traits and vices are first guesses, coded here for speed; they move to JSON once the
/// rules hold. Times are minutes of the day (design rule 1).
/// </summary>
public static class DefaultTown
{
    public const string Newcomer = "Newcomer";

    /// <summary>Where everyone sleeps and sits at home.</summary>
    public static readonly Tile Bed = new(2, 2), Sofa = new(4, 4), HomeDoor = new(5, 7);

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

    private static Location Road(string name, int length) => Room(name, length, 3, true);

    public static IReadOnlyList<Location> Locations()
    {
        var places = new List<Location>
        {
            // The square: a fence runs across part of it.
            Room("Square", 30, 20, true, Line(4, 9, 18, 9, '+')),
            // The saloon: a back room behind a wall with a door at x=10.
            Room("Saloon", 20, 12, false, Line(0, 7, 9, 7, '#').Concat(Line(11, 7, 19, 7, '#')).ToArray()),
            // Pierre's store: two rows of shelves.
            Room("Store", 16, 10, false, Line(2, 4, 12, 4, '+').Concat(Line(2, 7, 12, 7, '+')).ToArray()),
            // The chain store: two long aisles.
            Room("Mart", 18, 10, false, Line(3, 3, 14, 3, '+').Concat(Line(3, 6, 14, 6, '+')).ToArray()),
            Room("Beach", 30, 12, true),
            // The clinic yard with the bin, screened by bushes.
            Room("ClinicYard", 14, 10, true, Line(7, 2, 7, 8, '+')),
            Room("Farm", 20, 15, true),
            Road("TownLane", 26),
            Road("BeachPath", 16),
            Road("ForestPath", 32),
            Road("FarmRoad", 20),
        };
        foreach (string home in Cast().Select(v => v.Home).Distinct().OrderBy(h => h, StringComparer.Ordinal))
            places.Add(Room(home, 10, 8, false));
        return places;
    }

    public static IReadOnlyList<Link> Links()
    {
        static Link L(string a, int ax, int ay, string b, int bx, int by) => new(a, new Tile(ax, ay), b, new Tile(bx, by));
        static Link H(string road, int x, int y, string household) => L(road, x, y, "Home:" + household, HomeDoor.X, HomeDoor.Y);
        return new[]
        {
            L("Square", 29, 5, "Saloon", 10, 0),
            L("Square", 0, 5, "Store", 8, 9),
            L("Square", 15, 19, "Mart", 9, 0),
            L("Square", 29, 15, "ClinicYard", 0, 5),
            L("Square", 0, 12, "TownLane", 0, 1),
            L("Square", 15, 0, "BeachPath", 0, 1),
            L("BeachPath", 15, 1, "Beach", 15, 0),
            L("Square", 20, 0, "ForestPath", 0, 1),
            L("Square", 0, 18, "FarmRoad", 19, 1),
            L("FarmRoad", 0, 1, "Farm", 19, 7),
            H("Farm", 2, 2, "Farm"),
            H("TownLane", 5, 0, "JoshHouse"),
            H("TownLane", 11, 0, "HaleyHouse"),
            H("TownLane", 17, 0, "Manor"),
            H("TownLane", 25, 1, "Trailer"),
            H("ForestPath", 16, 0, "Ranch"),
            H("ForestPath", 31, 1, "Cottage"),
            H("Saloon", 18, 10, "Saloon"),
            H("Store", 15, 0, "SeedShop"),
            H("ClinicYard", 13, 0, "Clinic"),
        };
    }

    public static IReadOnlyList<ActKind> Acts() => new[]
    {
        // Scandals are rare (design rule 9 tiers): about 1-2 a year town-wide, a placeholder
        // until vices and money make them come from pressure (0b).
        new ActKind("RummagedInBin", 4.0, -1, 1, 3, 0.025, new[] { "ClinicYard", "Square" }),
        new ActKind("Stole", 4.5, -1, 1, 1, 0.02, new[] { "Store", "Mart" }),
        new ActKind("DrunkScene", 3.0, -1, 2, 20, 0.3, new[] { "Saloon" }),
        new ActKind("Argued", 3.0, -1, 2, 10, 0.4, Array.Empty<string>()),
        new ActKind("HelpedSomeone", 2.0, 1, 2, 5, 0.4, Array.Empty<string>()),
        new ActKind("GaveGift", 1.5, 1, 1, 1, 3.0, Array.Empty<string>()),
        new ActKind("Stumbled", 1.0, 0, 1, 1, 1.5, Array.Empty<string>()),
        // Never drawn: it happens when someone's energy runs out (design rule 1).
        new ActKind(Simulation.Collapsed, 2.5, 0, 1, 1, 0, Array.Empty<string>()),
    };

    private static int H(int hour, int min = 0) => Clock.At(hour, min);
    private static Haunt At(string place, int x, int y, int from, int to, double w) => new(place, new Tile(x, y), H(from), H(to), w);
    private static Job Works(string place, int x, int y, int start, int end, double effort, params int[] daysOff)
        => new(place, new Tile(x, y), H(start), H(end), daysOff, effort);
    private static Dictionary<string, double> A(params (string Kind, double W)[] acts)
        => acts.ToDictionary(a => a.Kind, a => a.W);

    // Weekdays: 0 Mon, 1 Tue, 2 Wed, 3 Thu, 4 Fri, 5 Sat, 6 Sun.
    public static IReadOnlyList<Villager> Cast()
    {
        var everyday = ("GaveGift", 0.5);
        var trip = ("Stumbled", 0.3);
        return new List<Villager>
        {
            new("Alex", "JoshHouse", "young man", new(0.6, 0.8, 0.3, 0.3), new(120, 0.15), null,
                new[] { At("Beach", 5, 6, 8, 13, 3), At("Square", 20, 12, 12, 19, 2), At("Saloon", 6, 4, 19, 24, 2) },
                A(everyday, trip, ("Argued", 0.5), ("Stole", 0.15)), new[] { "Haley" }),
            new("Emily", "HaleyHouse", "young woman", new(0.7, 0.5, 0.7, 0.7), new(100, 0.08),
                Works("Saloon", 12, 3, 16, 24, 1.1, 1),
                new[] { At("Store", 8, 5, 9, 13, 2), At("Beach", 10, 8, 9, 15, 1), At("Square", 8, 14, 10, 16, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Gus", "Shane" }),
            new("Gus", "Saloon", "older man", new(0.8, 0.5, 0.6, 0.6), new(125, 0.1),
                Works("Saloon", 10, 2, 11, 24, 1.0),
                new[] { At("Square", 14, 6, 8, 11, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.3)), new[] { "Emily", "Pam" }),
            new("Haley", "HaleyHouse", "young woman", new(0.7, 0.7, 0.2, 0.3), new(95, 0.25), null,
                new[] { At("Square", 8, 5, 10, 16, 2), At("Beach", 10, 5, 13, 19, 2), At("Store", 6, 8, 10, 17, 1) },
                A(everyday, trip, ("Argued", 0.5)), new[] { "Alex" }),
            new("Harvey", "Clinic", "older man", new(0.4, 0.3, 0.8, 0.4), new(90, 0.25),
                Works("Home:Clinic", 4, 4, 9, 15, 1.0, 5, 6),
                new[] { At("ClinicYard", 3, 3, 15, 19, 2), At("Square", 14, 14, 16, 20, 1), At("Saloon", 3, 3, 19, 23, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.6)), new[] { "Pierre" }),
            new("Leah", "Cottage", "young woman", new(0.5, 0.5, 0.7, 0.5), new(100, 0.2), null,
                new[] { At("Beach", 20, 4, 10, 16, 2), At("Square", 5, 15, 13, 17, 1), At("Saloon", 14, 5, 18, 23, 2) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Penny" }),
            new("Lewis", "Manor", "older man", new(0.6, 0.6, 0.6, 0.6), new(105, 0.2),
                Works("Square", 15, 10, 9, 17, 0.9, 5, 6),
                new[] { At("Saloon", 8, 3, 18, 22, 2), At("Store", 4, 2, 9, 17, 1) },
                A(everyday, trip, ("Argued", 0.3)), new[] { "Marnie", "Pierre" }),
            new("Marnie", "Ranch", "older woman", new(0.7, 0.4, 0.6, 0.5), new(110, 0.2),
                Works("Home:Ranch", 4, 4, 9, 16, 1.2, 0, 1),
                new[] { At("Store", 10, 2, 9, 17, 1), At("Square", 10, 15, 12, 18, 2), At("Saloon", 11, 4, 18, 22, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Lewis" }),
            new("Pam", "Trailer", "older woman", new(0.8, 0.7, 0.3, 0.3), new(85, 0.1), null,
                new[] { At("Square", 22, 15, 11, 17, 2), At("Saloon", 5, 6, 15, 24, 4), At("Mart", 5, 2, 10, 16, 1) },
                A(everyday, trip, ("DrunkScene", 1.0), ("RummagedInBin", 0.2)), new[] { "Gus" }),
            new("Penny", "Trailer", "young woman", new(0.4, 0.2, 0.7, 0.3), new(90, 0.25),
                Works("Square", 12, 6, 10, 14, 0.9, 5, 6),
                new[] { At("Store", 5, 8, 14, 18, 1), At("Beach", 8, 3, 14, 18, 1), At("ClinicYard", 10, 3, 14, 18, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.5)), new[] { "Leah" }),
            new("Pierre", "SeedShop", "older man", new(0.6, 0.6, 0.4, 0.5), new(105, 0.2),
                Works("Store", 3, 2, 9, 17, 1.1, 2),
                new[] { At("Square", 6, 4, 17, 20, 1), At("Saloon", 9, 4, 19, 22, 1) },
                A(everyday, trip, ("Argued", 0.5)), new[] { "Lewis", "Harvey" }),
            new("Shane", "Ranch", "young man", new(0.3, 0.6, 0.4, 0.2), new(95, 0.1),
                Works("Mart", 8, 5, 9, 17, 1.2, 5, 6),
                new[] { At("Saloon", 16, 9, 17, 24, 4), At("Beach", 25, 10, 17, 23, 1) },
                A(trip, ("DrunkScene", 1.0), ("Argued", 0.4), ("Stole", 0.3), ("RummagedInBin", 0.3)), new[] { "Emily" }),
            new(Newcomer, "Farm", "newcomer", new(0.3, 0.4, 0.5, 0.5), new(110, 0.2),
                Works("Farm", 10, 7, 6, 12, 1.3),
                new[]
                {
                    At("ClinicYard", 10, 6, 12, 20, 1), At("Square", 25, 5, 12, 20, 2), At("Store", 7, 2, 12, 17, 1),
                    At("Beach", 14, 8, 14, 20, 1), At("Saloon", 17, 4, 18, 24, 1), At("Mart", 12, 8, 12, 18, 1),
                },
                A(everyday, trip, ("RummagedInBin", 1.0), ("Stole", 0.5)), Array.Empty<string>()),
        };
    }
}
