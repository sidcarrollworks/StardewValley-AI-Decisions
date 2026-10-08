namespace UnderGlass.Sim;

/// <summary>
/// Towns grown from the shipped one (town spec; Sid's answers of 2026-10-08: grow Pelican Town,
/// starting with 60, with the 31-person step first). Each is a whole <see cref="TownData"/>; the
/// shipped town (<see cref="TownData.Default"/>) is untouched inside it.
/// </summary>
public static class Towns
{
    /// <summary>The names <see cref="Named"/> knows, for the runner's and the replay's --town.</summary>
    public static readonly IReadOnlyList<string> Names = new[] { "pelican", "pelican31" };

    /// <summary>A town by name: "pelican" (the shipped 26) or "pelican31".</summary>
    public static TownData Named(string name) => name switch
    {
        "pelican" or "pelican26" => TownData.Default(),
        "pelican31" => Pelican31(),
        _ => throw new ArgumentException($"no town called {name}: one of {string.Join(", ", Names)}"),
    };

    /// <summary>The five who live a little apart, as people who keep to themselves (town spec 4.6,
    /// T2b; Sid's answer 6). Lore is recalled, not checked (VERIFY against the game): Clint keeps
    /// the blacksmith's and is sweet on Emily; Willy keeps the fish shop on the beach; Elliott writes
    /// in his cabin by the sea; Linus lives in a tent on the mountain, forages, and is known to go
    /// through the bins; the Wizard keeps to his tower in the forest. Sensitivity and expression come
    /// from the same game data as the rest of the cast (fixtures/game/temperament): the dialogue's
    /// sensitivity, and expression 0.75 + 0.15 rude or - 0.1 polite + 0.05 outgoing or - 0.15 shy.
    /// Chattiness and boldness are the dialogue's, to a tenth; understanding, self-regard, bodies,
    /// ages, hours, haunts, friends and money are first guesses.</summary>
    public static TownData Pelican31()
    {
        TownData town = TownData.Default();
        IReadOnlyList<Villager> five = Five();
        var cast = town.Cast.Concat(five).ToList();

        var places = town.Places.ToList();
        places.Add(Room("Blacksmith", 14, 10, Line(3, 4, 10, 4, '+')));   // a counter across the shop
        places.Add(Room("FishShop", 12, 8, Line(2, 3, 8, 3, '+')));       // the counter by the pier
        places.Add(new Location("TowerPath", true, Enumerable.Repeat(new string('.', 20), 3).ToList()));
        foreach (Villager v in five)
            if (!places.Any(p => p.Name == v.Home))
                places.Add(Room(v.Home, 10, 8));

        static Link L(string a, int ax, int ay, string b, int bx, int by) => new(a, new Tile(ax, ay), b, new Tile(bx, by));
        static Link H(string from, int x, int y, string household) => L(from, x, y, "Home:" + household, DefaultTown.HomeDoor.X, DefaultTown.HomeDoor.Y);
        var links = town.Links.Concat(new[]
        {
            L("TownLane", 14, 2, "Blacksmith", 0, 5),  // on the lane (town spec 4.6); the square's (29,10) is slot E's
            H("Blacksmith", 13, 5, "Blacksmith"),       // Clint lives behind the shop
            L("Beach", 29, 10, "FishShop", 0, 4),      // the pier; the beach's (29,6) is slot SE's
            H("FishShop", 11, 4, "FishShop"),           // and Willy behind his
            H("Beach", 2, 0, "ElliottCabin"),
            H("MountainPath", 12, 2, "Tent"),
            L("ForestPath", 28, 2, "TowerPath", 0, 1),  // into the forest
            H("TowerPath", 19, 1, "Tower"),
        }).ToList();

        FeelingOptions feelings = DefaultTown.Feelings();
        var start = new Dictionary<(string, string), double>(feelings.Start)
        {
            [("Clint", "Emily")] = 0.4, // sweet on her from the start (VERIFY), one way
        };
        feelings.Start = start;

        AuthorityOptions authority = DefaultTown.TownAuthority();
        authority.Keepers = new Dictionary<string, string>(authority.Keepers) { ["Blacksmith"] = "Clint", ["FishShop"] = "Willy" };

        Economy e = town.Economy!;
        var purses = new Dictionary<string, double>(e.StartPurse)
        {
            ["Blacksmith"] = 400, ["FishShop"] = 400, ["ElliottCabin"] = 300, ["Tower"] = 600,
            ["Tent"] = 100, // Linus lives off the land and has next to nothing (VERIFY)
        };
        var incomes = e.Incomes.Concat(new (string, double, string?)[]
        {
            ("Clint", 550, null),    // tools and ore sold out of town as well as in it
            ("Willy", 450, null),    // fish sold out of town
            ("Elliott", 200, null),  // a writer's advances, small
            ("Wizard", 300, null),   // means unknown (invented)
        }).ToList(); // Linus earns nothing
        var groceries = new Dictionary<string, string>(e.GroceriesAt)
        {
            ["Blacksmith"] = "Store", ["FishShop"] = "Store", ["ElliottCabin"] = "Store", ["Tower"] = "Store", ["Tent"] = "Mart",
        };
        var wants = new Dictionary<string, (double, double)>(e.Wants);
        foreach (Villager v in five)
            wants[v.Name] = (50, 300);
        var economy = e with { StartPurse = purses, Incomes = incomes, GroceriesAt = groceries, Wants = wants };

        // At 30 people or more TellsPerDay's default jumps from 2 to 3 (Simulation.TellsPerDay), which
        // would quietly change gossip; the grown towns keep the shipped town's 2 (town spec, section 3).
        return town with
        {
            Cast = cast, Places = places, Links = links, Feelings = feelings, Authority = authority, Economy = economy,
            Gossip = new GossipOptions { TellsPerDay = 2 },
        };
    }

    private static Location Room(string name, int w, int h, params (int X, int Y, char C)[] marks)
    {
        var rows = Enumerable.Range(0, h).Select(_ => new string('.', w).ToCharArray()).ToArray();
        foreach (var (x, y, c) in marks)
            rows[y][x] = c;
        return new Location(name, false, rows.Select(r => new string(r)).ToList());
    }

    private static (int, int, char)[] Line(int x0, int y0, int x1, int y1, char c)
        => Enumerable.Range(0, Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1)
            .Select(i => (x0 + Math.Sign(x1 - x0) * i, y0 + Math.Sign(y1 - y0) * i, c)).ToArray();

    private static int H(int hour, int min = 0) => Clock.At(hour, min);
    private static Haunt At(string place, int x, int y, int from, int to, double w) => new(place, new Tile(x, y), H(from), H(to), w);
    private static Job Works(string place, int x, int y, int start, int end, double effort, params int[] daysOff)
        => new(place, new Tile(x, y), H(start), H(end), daysOff, effort);
    private static Dictionary<string, double> A(params (string Kind, double W)[] acts) => acts.ToDictionary(a => a.Kind, a => a.W);

    /// <summary>Clint, Willy, Elliott, Linus and the Wizard. Weekdays: 0 Mon ... 6 Sun.</summary>
    private static IReadOnlyList<Villager> Five()
    {
        var everyday = ("GaveGift", 0.5);
        var trip = ("Stumbled", 0.3);
        var five = new List<Villager>
        {
            // The blacksmith: at the anvil 9:00-16:00 (VERIFY his hours and day off), an evening at
            // the saloon, a quiet hour by the square. Shy and low in himself.
            new("Clint", "Blacksmith", "man", new(0.3, 0.2, 0.5, 0.2), new(120, 0.15),
                Works("Blacksmith", 6, 2, 9, 16, 1.4),
                new[] { At("Saloon", 15, 5, 19, 23, 2), At("Square", 25, 11, 16, 19, 1) },
                A(trip, ("GaveGift", 0.3)), new[] { "Gus" }, 34),
            // The fisherman: up early on the beach, the shop 9:00-17:00, closed Saturdays (VERIFY),
            // and the saloon some evenings.
            new("Willy", "FishShop", "older man", new(0.5, 0.5, 0.6, 0.6), new(105, 0.15),
                Works("FishShop", 5, 1, 9, 17, 1.0, 5),
                new[] { At("Beach", 27, 9, 5, 9, 2), At("Beach", 24, 10, 17, 20, 1), At("Saloon", 7, 5, 19, 23, 2) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Gus", "Elliott" }, 58),
            // The writer: mornings walking the beach, writing at home through the middle of the day,
            // now and then the square or the saloon.
            new("Elliott", "ElliottCabin", "young man", new(0.5, 0.4, 0.7, 0.5), new(100, 0.1),
                Works("Home:ElliottCabin", 4, 4, 10, 15, 0.9),
                new[] { At("Beach", 6, 4, 7, 10, 2), At("Square", 4, 17, 15, 18, 1), At("Saloon", 13, 6, 19, 23, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.3)), new[] { "Leah", "Willy" }, 28),
            // The forager: the mountain lake in the morning, the beach and the forest by day, and,
            // late, the square, where the bins are (VERIFY). No income: rummaging comes from need.
            new("Linus", "Tent", "old man", new(0.4, 0.4, 0.8, 0.5), new(95, 0.25), null,
                new[] { At("MountainPath", 18, 1, 6, 12, 2), At("Beach", 3, 9, 12, 17, 1), At("ForestPath", 10, 1, 14, 19, 2), At("Square", 27, 18, 20, 23, 1) },
                A(trip, ("RummagedInBin", 1.0), ("HelpedSomeone", 0.3)), Array.Empty<string>(), 60),
            // The Wizard: his tower all day and most of the night (it outweighs the market three times over),
            // and a walk on his path late.
            new("Wizard", "Tower", "old man", new(0.5, 0.5, 0.8, 0.8), new(100, 0.05), null,
                new[] { At("Home:Tower", 6, 2, 0, 24, 40), At("TowerPath", 10, 1, 21, 24, 1) },
                A(trip), Array.Empty<string>(), 70),
        };
        // Sensitivity and expression from the game data (see Pelican31); birthdays recalled (VERIFY).
        var sensitivity = new Dictionary<string, double> { ["Clint"] = 0.73, ["Willy"] = 0.50, ["Elliott"] = 0.55, ["Linus"] = 0.40, ["Wizard"] = 0.52 };
        var expression = new Dictionary<string, double> { ["Clint"] = 0.75, ["Willy"] = 0.75, ["Elliott"] = 0.65, ["Linus"] = 0.60, ["Wizard"] = 0.90 };
        var birthdays = new Dictionary<string, YearDay>
        {
            ["Clint"] = new(3, 26), ["Willy"] = new(1, 24), ["Elliott"] = new(2, 5), ["Linus"] = new(3, 3), ["Wizard"] = new(3, 17),
        };
        return five.Select(v => v with
        {
            Family = new Dictionary<string, Kin>(),
            Temperament = v.Temperament with { Sensitivity = sensitivity[v.Name], Expression = expression[v.Name] },
            Birthday = birthdays[v.Name],
        }).ToList();
    }
}
