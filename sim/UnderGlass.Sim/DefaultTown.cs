namespace UnderGlass.Sim;

/// <summary>
/// The phase-0a town: six public places, five roads, a farm, one home per household, Stardew's
/// families and the villagers who live alone (25, private prototype only, design section 3) and
/// the newcomer. Jobs, haunts,
/// bodies, traits and vices are first guesses, coded here for speed; they move to JSON once the
/// rules hold. Times are minutes of the day (design rule 1).
/// </summary>
public static class DefaultTown
{
    public const string Newcomer = "Newcomer";
    /// <summary>The mayor at the start of every run (design rule 16).</summary>
    public const string Mayor = "Lewis";

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
            Road("MountainPath", 24),
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
            H("TownLane", 21, 0, "SamHouse"),
            L("Square", 8, 0, "MountainPath", 0, 1),
            H("MountainPath", 23, 1, "ScienceHouse"),
            H("Saloon", 18, 10, "Saloon"),
            H("Store", 15, 0, "SeedShop"),
            H("ClinicYard", 13, 0, "Clinic"),
        };
    }

    /// <summary>The town's hubs (design rule 11): noon in the square, evenings at the saloon, and
    /// market day on Saturday. On the first day, the town meeting where the constable is voted in
    /// (design rule 16).</summary>
    public static IReadOnlyList<Gathering> Gatherings() => new[]
    {
        new Gathering("Meeting", "Square", new Tile(15, 13), 5, H(11, 30), H(12, 30), Array.Empty<int>(), 20, OnlyDay: 0),
        new Gathering("Noon", "Square", new Tile(15, 14), 4, H(11, 30), H(13, 30), Array.Empty<int>(), 3),
        new Gathering("Evening", "Saloon", new Tile(10, 4), 4, H(18), H(23), Array.Empty<int>(), 2),
        new Gathering("Market", "Square", new Tile(15, 5), 5, H(9), H(14), new[] { 5 }, 12),
    };

    // Feeling rows (phase 0c): who is pleased or hurt, how much, how much of it becomes regard
    // for its cause, how freely the cause is believed to act, at whom it is aimed, and whether the
    // joyful (+1) or the sad (-1) do it more. First guesses; see docs/under-glass/design.md 3a.
    private static readonly Affect AtKeeper = new(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper);

    public static IReadOnlyList<ActKind> Acts() => new[]
    {
        // Scandals are rare (design rule 9 tiers): about 1-2 a year town-wide, a placeholder
        // until vices and money make them come from pressure (0b).
        // Each leaves a trace (design principle 1): a scattered bin anyone can see for half a day;
        // missing stock only the keeper notices, by counting, for three days.
        // Age decides who would (design rule 17): no child steals from a till or rummages a bin.
        // Never drawn at a rate (phase 0b): they come from temptation, a motive against the
        // believed risk, for villagers with the vice (Simulation.Temptation).
        new ActKind("RummagedInBin", 4.0, -1, 1, 3, 0, new[] { "ClinicYard", "Square" },
            Trace: new TraceKind("ScatteredBin", KeeperOnly: false, LastsMinutes: 12 * 60, NoticePerHour: 0.6), MinAge: 13,
            Affect: AtKeeper with { Joy = -0.3 }),
        new ActKind("Stole", 4.5, -1, 1, 1, 0, new[] { "Store", "Mart" },
            Trace: new TraceKind("MissingStock", KeeperOnly: true, LastsMinutes: 3 * 24 * 60, NoticePerHour: 0.15), MinAge: 13,
            Affect: AtKeeper),
        new ActKind("DrunkScene", 3.0, -1, 2, 20, 0.3, new[] { "Saloon" }, MinAge: 18,
            Affect: new Affect(Patient.Onlookers, -0.15, 0.3, 0.6, Tilt: -1)),
        new ActKind("Argued", 3.0, -1, 2, 10, 0.4, Array.Empty<string>(), MinAge: 13,
            Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen)),
        new ActKind("HelpedSomeone", 2.0, 1, 2, 5, 0.4, Array.Empty<string>(), MinAge: 10,
            Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1)),
        // A child squabbles with a sibling instead (Sid: Vincent "would fight with his brother").
        // It barely dents how siblings feel about each other.
        new ActKind("Squabbled", 1.5, -1, 1, 5, 2.0, Array.Empty<string>(), MaxAge: 12, WithKin: Kin.Sibling,
            Affect: new Affect(Patient.Target, -0.15, 0.05, 0.5, TargetIs.Kin)),
        new ActKind("GaveGift", 1.5, 1, 1, 1, 3.0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1)),
        // An accident: it saddens friends and blames nobody.
        new ActKind("Stumbled", 1.0, 0, 1, 1, 1.5, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.1, 0, 0)),
        // Never drawn: the mayor's warning, being taken in, and being questioned by the constable
        // (design rule 16). The actor is the person warned, taken or questioned; the target the
        // official, who is believed to act less freely (doing their job).
        new ActKind(Authority.Warned, 3.0, -1, 1, 5, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.3, 0.4, 0.4, TargetIs.Given)),
        new ActKind(Authority.TakenIn, 3.5, -1, 1, 5, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.6, 0.4, 0.4, TargetIs.Given)),
        new ActKind(Authority.Questioned, 2.5, -1, 1, 10, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.15, 0.3, 0.4, TargetIs.Given)),
        new ActKind(Simulation.Service, 3.0, -1, 2, 30, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.4, 0.4, 0.4, TargetIs.Given)),
        // Never drawn: a keeper who learns their own kin took from them has it out at home
        // instead of reporting it (design rule 17). The actor is the one who took.
        new ActKind(Simulation.FamilyRow, 3.0, -1, 2, 15, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.3, 0.3, 1, TargetIs.Given)),
        // Never drawn: seen out at an hour you never keep, when the town is quiet (acting normal,
        // design rule 1). Not bad in itself, but worth talking about. The actor is the one seen.
        new ActKind(Simulation.OutLate, 2.0, 0, 1, 1, 0, Array.Empty<string>()),
        // Never drawn: it happens when someone's energy runs out (design rule 1).
        new ActKind(Simulation.Collapsed, 2.5, 0, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.3, 0, 0)),
        // Never drawn: light hostile acts that only the desire gate starts (rule 10): a snub, which
        // needs some daring, and turning away, the shy's cold shoulder. Seen and felt; at
        // juiciness 0.5 they are never retold.
        new ActKind(Simulation.Snubbed, 0.5, -1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, -0.15, 0.3, 1, TargetIs.Chosen)),
        new ActKind(Simulation.TurnedAway, 0.5, -1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, -0.05, 0.3, 1, TargetIs.Chosen)),
    };

    /// <summary>The town's feelings (phase 0c): seeded from households and friends, with the
    /// starting tensions, steering decisions. Every regard change counts double the rows' first
    /// guesses (PlasticScale 2): at 1, only 2.8% of pairs moved 0.1 or more in a year and every
    /// seed was a dead town; at 2, 7.9% moved, inside the 5-25% band (sweeps of 2026-10-06,
    /// sim/README.md). With the desire gate (phase 0d, rule 10): people answer, return, make up
    /// and keep away. Of its added rules (sweeps of 2026-10-07, sim/README.md), the town misses
    /// loved ones (Fond, 14 days: at 10 regard drifted up over three years), keeps a stance (hermits and brawlers can emerge), and dares by
    /// its power of acting; light snubs stay off, because they drain feuds (7.9 to 0.6 a year).</summary>
    public static FeelingOptions Feelings() => new() { Start = Tensions(), PlasticScale = 2, Desire = true,
        FondOn = true, FondDays = 14, StanceOn = true, PowerWeight = 1 };

    /// <summary>
    /// The town's starting tensions (0c question 1; Sid, 2026-10-07): who starts out disliking whom,
    /// and the first season's spark (0d question 3). Pierre and Shane both ways: the general store
    /// against the chain Shane works at. Sebastian toward Demetrius, his stepfather, and Abigail
    /// toward Pierre, her father: both feel misunderstood at home in Stardew's own stories (VERIFY
    /// against their heart events). The last two are inside a household, so the gate never acts on
    /// them; they colour how the rates aim and whom people report.
    /// </summary>
    public static readonly IReadOnlyList<(string From, string To)> TensionPairs = new[]
    {
        ("Pierre", "Shane"), ("Shane", "Pierre"), ("Sebastian", "Demetrius"), ("Abigail", "Pierre"),
    };

    /// <summary>How far each starting tension sits below the seed: below the -0.2 floor and at a
    /// feud's -0.3, so a pair seeded both ways starts in a feud and is never counted as a new one
    /// (spec section 10). A shallower depth (--tensions) starts it cool, and if it falls into a feud,
    /// that is a new one.</summary>
    public const double TensionDepth = 0.3;

    /// <summary>Regard that starts away from the seed, (from, to), and heals back there (it is the
    /// pair's baseline). <paramref name="depth"/> 0 gives none.</summary>
    public static IReadOnlyDictionary<(string From, string To), double> Tensions(double depth = TensionDepth)
        => depth == 0 ? new Dictionary<(string, string), double>() : TensionPairs.ToDictionary(p => p, _ => -depth);

    /// <summary>
    /// How strongly each villager feels joy and sadness (phase 0c; law 12), from the temperaments
    /// the mod derived from Stardew's dialogue (fixtures/game/temperament/temperament.json). A
    /// private prototype's values, to be replaced with the original cast (design section 3).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, double> Sensitivity = new Dictionary<string, double>
    {
        ["Abigail"] = 0.38, ["Alex"] = 0.41, ["Caroline"] = 0.36, ["Demetrius"] = 0.50, ["Emily"] = 0.37,
        ["Evelyn"] = 0.30, ["George"] = 0.54, ["Gus"] = 0.35, ["Haley"] = 0.37, ["Harvey"] = 0.50,
        ["Jas"] = 0.55, ["Jodi"] = 0.51, ["Kent"] = 0.54, ["Leah"] = 0.33, ["Lewis"] = 0.30,
        ["Marnie"] = 0.37, ["Maru"] = 0.42, ["Pam"] = 0.56, ["Penny"] = 0.67, ["Pierre"] = 0.36,
        ["Robin"] = 0.26, ["Sam"] = 0.40, ["Sebastian"] = 0.64, ["Shane"] = 0.74, ["Vincent"] = 0.43,
    };

    /// <summary>How long a mild feeling is kept (design rule 4: "Pam lets go, Robin keeps"). Pam's
    /// is the mod's value (src/NpcMotives/MotiveOptions.cs); Robin's is a guess (VERIFY with Sid).</summary>
    private static readonly IReadOnlyDictionary<string, double> Retention = new Dictionary<string, double>
    {
        ["Pam"] = 0.2, ["Robin"] = 0.8,
    };

    /// <summary>
    /// How much of a feeling shows (phase 0d.6; expression, the seventh trait; Sid, 2026-10-07).
    /// From the game's Data/Characters fields (fixtures/game/temperament/characters.json): 0.75, plus
    /// 0.15 for a rude manner or less 0.1 for a polite one, plus 0.05 for the outgoing or less 0.15
    /// for the shy. Sid's reading sets two (design 12.7; masking-research.md M1): Penny masks (0.25)
    /// and Pam lets it out (0.85). A guess for the private prototype (VERIFY with Sid).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, double> Expressions = new Dictionary<string, double>
    {
        ["Abigail"] = 0.95, ["Alex"] = 0.95, ["Caroline"] = 0.65, ["Demetrius"] = 0.65, ["Emily"] = 0.70,
        ["Evelyn"] = 0.70, ["George"] = 0.90, ["Gus"] = 0.80, ["Haley"] = 0.95, ["Harvey"] = 0.50,
        ["Jas"] = 0.60, ["Jodi"] = 0.65, ["Kent"] = 0.60, ["Leah"] = 0.65, ["Lewis"] = 0.80,
        ["Marnie"] = 0.70, ["Maru"] = 0.80, ["Pam"] = 0.85, ["Penny"] = 0.25, ["Pierre"] = 0.80,
        ["Robin"] = 0.80, ["Sam"] = 0.80, ["Sebastian"] = 0.75, ["Shane"] = 0.75, ["Vincent"] = 0.80,
    };

    /// <summary>Birthdays (phase 0d.6, an occasion for gifts), read from the game's Data/Characters
    /// (1.6.15) on Sid's PC on 2026-10-07. Season 0 is spring. The newcomer has none.</summary>
    private static readonly IReadOnlyDictionary<string, YearDay> Birthdays = new Dictionary<string, YearDay>
    {
        ["Kent"] = new(0, 4), ["Lewis"] = new(0, 7), ["Vincent"] = new(0, 10), ["Haley"] = new(0, 14),
        ["Pam"] = new(0, 18), ["Shane"] = new(0, 20), ["Pierre"] = new(0, 26), ["Emily"] = new(0, 27),
        ["Jas"] = new(1, 4), ["Gus"] = new(1, 8), ["Maru"] = new(1, 10), ["Alex"] = new(1, 13),
        ["Sam"] = new(1, 17), ["Demetrius"] = new(1, 19),
        ["Penny"] = new(2, 2), ["Jodi"] = new(2, 11), ["Abigail"] = new(2, 13), ["Marnie"] = new(2, 18),
        ["Robin"] = new(2, 21), ["George"] = new(2, 24),
        ["Caroline"] = new(3, 7), ["Sebastian"] = new(3, 10), ["Harvey"] = new(3, 14), ["Evelyn"] = new(3, 20),
        ["Leah"] = new(3, 23),
    };

    /// <summary>
    /// The town's authority (design rule 16): Lewis is mayor; the constable is voted in at the
    /// opening meeting. Keepers count their stock and report as victims: Pierre his store, Shane
    /// the chain store's floor, Harvey the clinic yard, Gus the saloon, and Lewis the square. The
    /// constable patrols the public places; anyone detained is held at the manor.
    /// </summary>
    public static AuthorityOptions TownAuthority() => new()
    {
        Mayor = Mayor,
        Keepers = new Dictionary<string, string>
        {
            ["Store"] = "Pierre", ["Mart"] = "Shane", ["ClinicYard"] = "Harvey", ["Saloon"] = "Gus", ["Square"] = Mayor,
        },
        Patrol = new Dictionary<string, Tile>
        {
            ["Square"] = new(15, 12), ["ClinicYard"] = new(10, 4), ["Store"] = new(8, 8), ["Mart"] = new(9, 8),
            ["Beach"] = new(15, 4), ["Saloon"] = new(10, 3),
        },
        LockupPlace = "Home:Manor",
        LockupSpot = new Tile(7, 2),
        ServicePlace = "Square",
        ServiceSpot = new Tile(12, 16),
    };

    /// <summary>
    /// The town's money (phase 0b), in g a week. Guesses scaled to Stardew's prices: groceries
    /// 100g a person; wages from 300g (Penny's teaching) to 700g (Robin's carpentry). Outside
    /// money: pensions (George, Kent), wages from away (the chain pays Shane and Sam), sales out
    /// of town (Marnie's animals, Leah's art, Robin's orders, the newcomer's crops), the clinic's
    /// fees, Demetrius's grant, Sebastian's freelance work, and the county's stipend to the town.
    /// Pam has no income and drinks most evenings: the trailer runs short.
    /// </summary>
    public static Economy TownEconomy()
    {
        var incomes = new (string, double, string?)[]
        {
            ("George", 600, null), ("Emily", 350, "Saloon"), ("Harvey", 900, null), ("Maru", 300, "Clinic"),
            ("Leah", 350, null), ("Lewis", 600, Simulation.Town), ("Penny", 300, Simulation.Town),
            ("Marnie", 700, null), ("Shane", 600, null), ("Sam", 350, null), ("Kent", 500, null),
            ("Robin", 700, null), ("Demetrius", 600, null), ("Sebastian", 300, null), (Newcomer, 450, null),
        };
        var groceries = new Dictionary<string, string>
        {
            ["JoshHouse"] = "Store", ["HaleyHouse"] = "Store", ["Saloon"] = "Store", ["Clinic"] = "Store", ["Cottage"] = "Store",
            ["Manor"] = "Store", ["Ranch"] = "Store", ["ScienceHouse"] = "Store", ["SeedShop"] = "Store",
            ["Trailer"] = "Mart", ["SamHouse"] = "Mart", ["Farm"] = "Mart",
        };
        var wants = Cast().Where(v => v.Name != Mayor).ToDictionary(v => v.Name, v => v.Stage == Stage.Child ? (20.0, 80.0) : (50.0, 300.0));
        wants["Abigail"] = (300, 900); wants["Sebastian"] = (200, 800); wants["Sam"] = (200, 600); wants["Alex"] = (150, 500);
        wants["Haley"] = (100, 400); wants[Newcomer] = (100, 400);
        var cast = Cast();
        var start = cast.Select(v => v.Household).Distinct()
            .ToDictionary(h => h, h => h == "Trailer" ? 300.0 : 200.0 + 200.0 * cast.Count(v => v.Household == h));
        return new Economy(start, incomes,
            new Dictionary<string, double> { ["Abigail"] = 60, ["Alex"] = 50, ["Haley"] = 40, ["Vincent"] = 10, ["Jas"] = 10 },
            groceries, wants, TownStipend: 1200, TownStart: 2000);
    }

    private static int H(int hour, int min = 0) => Clock.At(hour, min);
    private static Haunt At(string place, int x, int y, int from, int to, double w) => new(place, new Tile(x, y), H(from), H(to), w);
    private static Job Works(string place, int x, int y, int start, int end, double effort, params int[] daysOff)
        => new(place, new Tile(x, y), H(start), H(end), daysOff, effort);
    private static Dictionary<string, double> A(params (string Kind, double W)[] acts)
        => acts.ToDictionary(a => a.Kind, a => a.W);

    /// <summary>
    /// Family ties (design rule 17): "who is what of whom". Each tie is kept both ways, so
    /// ("Pierre", Parent, "Abigail") makes Pierre Abigail's parent and Abigail Pierre's child.
    /// Shane rents at the ranch and is not Marnie's kin.
    /// </summary>
    public static IReadOnlyList<(string Who, Kin Is, string Of)> Ties() => new[]
    {
        ("Pierre", Kin.Spouse, "Caroline"), ("Pierre", Kin.Parent, "Abigail"), ("Caroline", Kin.Parent, "Abigail"),
        ("George", Kin.Spouse, "Evelyn"), ("George", Kin.Grandparent, "Alex"), ("Evelyn", Kin.Grandparent, "Alex"),
        ("Haley", Kin.Sibling, "Emily"),
        ("Pam", Kin.Parent, "Penny"),
        ("Jodi", Kin.Spouse, "Kent"), ("Jodi", Kin.Parent, "Sam"), ("Jodi", Kin.Parent, "Vincent"),
        ("Kent", Kin.Parent, "Sam"), ("Kent", Kin.Parent, "Vincent"), ("Sam", Kin.Sibling, "Vincent"),
        ("Marnie", Kin.Guardian, "Jas"),
        ("Robin", Kin.Spouse, "Demetrius"), ("Robin", Kin.Parent, "Sebastian"), ("Robin", Kin.Parent, "Maru"),
        ("Demetrius", Kin.Parent, "Maru"), ("Demetrius", Kin.Stepparent, "Sebastian"), ("Maru", Kin.Sibling, "Sebastian"),
    };

    private static IReadOnlyDictionary<string, Kin> FamilyOf(string name)
    {
        var family = new Dictionary<string, Kin>();
        foreach (var (who, kin, of) in Ties())
        {
            if (of == name) family[who] = kin;               // who is my kin
            if (who == name) family[of] = Ages.Reverse(kin); // I am their kin, so they are the reverse to me
        }
        return family;
    }

    // Weekdays: 0 Mon, 1 Tue, 2 Wed, 3 Thu, 4 Fri, 5 Sat, 6 Sun. Ages are guesses: Stardew gives
    // few. Children have lessons with Penny on weekdays.
    public static IReadOnlyList<Villager> Cast()
    {
        var everyday = ("GaveGift", 0.5);
        var trip = ("Stumbled", 0.3);
        var lessons = Works("Square", 0, 0, 10, 14, 0.8, 5, 6);
        var list = new List<Villager>
        {
            // The Mullners: George and Evelyn raise their grandson Alex.
            new("Alex", "JoshHouse", "young man", new(0.6, 0.8, 0.3, 0.3), new(120, 0.15), null,
                new[] { At("Beach", 5, 6, 8, 13, 3), At("Square", 20, 12, 12, 19, 2), At("Saloon", 6, 4, 19, 24, 2) },
                A(everyday, trip, ("Argued", 0.5), ("Stole", 0.15)), new[] { "Haley", "Sam" }, 21),
            new("George", "JoshHouse", "old man", new(0.4, 0.6, 0.3, 0.5), new(80, 0.3), null,
                new[] { At("Square", 18, 16, 10, 12, 0.5) },
                A(trip, ("Argued", 0.6)), Array.Empty<string>(), 76),
            new("Evelyn", "JoshHouse", "old woman", new(0.7, 0.3, 0.8, 0.6), new(85, 0.3),
                Works("Square", 5, 15, 9, 12, 0.9, 6),
                new[] { At("Store", 9, 2, 13, 16, 1), At("Square", 6, 16, 13, 17, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.6)), new[] { "Caroline", "Marnie" }, 74),
            // Haley and Emily, sisters.
            new("Emily", "HaleyHouse", "young woman", new(0.7, 0.5, 0.7, 0.7), new(100, 0.08),
                Works("Saloon", 12, 3, 16, 24, 1.1, 1),
                new[] { At("Store", 8, 5, 9, 13, 2), At("Beach", 10, 8, 9, 15, 1), At("Square", 8, 14, 10, 16, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Gus", "Shane" }, 25),
            new("Haley", "HaleyHouse", "young woman", new(0.7, 0.7, 0.2, 0.3), new(95, 0.25), null,
                new[] { At("Square", 8, 5, 10, 16, 2), At("Beach", 10, 5, 13, 19, 2), At("Store", 6, 8, 10, 17, 1) },
                A(everyday, trip, ("Argued", 0.5)), new[] { "Alex" }, 21),
            // Those who live alone.
            new("Gus", "Saloon", "older man", new(0.8, 0.5, 0.6, 0.6), new(125, 0.1),
                Works("Saloon", 10, 2, 11, 24, 1.0),
                new[] { At("Square", 14, 6, 8, 11, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.3)), new[] { "Emily", "Pam" }, 50),
            new("Harvey", "Clinic", "man", new(0.4, 0.3, 0.8, 0.4), new(90, 0.25),
                Works("Home:Clinic", 4, 4, 9, 15, 1.0, 5, 6),
                new[] { At("ClinicYard", 3, 3, 15, 19, 2), At("Square", 14, 14, 16, 20, 1), At("Saloon", 3, 3, 19, 23, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.6)), new[] { "Pierre", "Maru" }, 34),
            new("Leah", "Cottage", "young woman", new(0.5, 0.5, 0.7, 0.5), new(100, 0.2), null,
                new[] { At("Beach", 20, 4, 10, 16, 2), At("Square", 5, 15, 13, 17, 1), At("Saloon", 14, 5, 18, 23, 2) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Penny" }, 27),
            new("Lewis", "Manor", "older man", new(0.6, 0.6, 0.6, 0.6), new(105, 0.2),
                Works("Square", 15, 10, 9, 17, 0.9, 5, 6),
                new[] { At("Saloon", 8, 3, 18, 22, 2), At("Store", 4, 2, 9, 17, 1) },
                A(everyday, trip, ("Argued", 0.3)), new[] { "Marnie", "Pierre", "Robin" }, 60),
            // The ranch: Marnie, her niece Jas (in her care), and Shane, who rents a room.
            new("Marnie", "Ranch", "older woman", new(0.7, 0.4, 0.6, 0.5), new(110, 0.2),
                Works("Home:Ranch", 4, 4, 9, 16, 1.2, 0, 1),
                new[] { At("Store", 10, 2, 9, 17, 1), At("Square", 10, 15, 12, 18, 2), At("Saloon", 11, 4, 18, 22, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Lewis", "Evelyn" }, 47),
            new("Jas", "Ranch", "girl", new(0.5, 0.3, 0.6, 0.5), new(90, 0.35), lessons with { Spot = new Tile(11, 7) },
                new[] { At("Square", 22, 16, 14, 18, 2), At("Beach", 6, 8, 14, 18, 1) },
                A(trip, ("GaveGift", 0.3)), new[] { "Vincent" }, 7),
            new("Shane", "Ranch", "young man", new(0.3, 0.6, 0.4, 0.2), new(95, 0.1),
                Works("Mart", 8, 5, 9, 17, 1.2, 5, 6),
                new[] { At("Saloon", 16, 9, 17, 24, 4), At("Beach", 25, 10, 17, 23, 1) },
                A(trip, ("DrunkScene", 1.0), ("Argued", 0.4), ("Stole", 0.3), ("RummagedInBin", 0.3)), new[] { "Emily" }, 30),
            // The trailer: Pam and her daughter Penny, who teaches the children.
            new("Pam", "Trailer", "older woman", new(0.8, 0.7, 0.3, 0.3), new(85, 0.1), null,
                new[] { At("Square", 22, 15, 11, 17, 2), At("Saloon", 5, 6, 15, 24, 4), At("Mart", 5, 2, 10, 16, 1) },
                A(everyday, trip, ("DrunkScene", 1.0), ("RummagedInBin", 0.2)), new[] { "Gus" }, 50),
            new("Penny", "Trailer", "young woman", new(0.4, 0.2, 0.7, 0.3), new(90, 0.25),
                Works("Square", 12, 6, 10, 14, 0.9, 5, 6),
                new[] { At("Store", 5, 8, 14, 18, 1), At("Beach", 8, 3, 14, 18, 1), At("ClinicYard", 10, 3, 14, 18, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.5)), new[] { "Leah", "Maru" }, 22),
            // The seed shop: Pierre, Caroline and their daughter Abigail.
            new("Pierre", "SeedShop", "older man", new(0.6, 0.6, 0.4, 0.5), new(105, 0.2),
                Works("Store", 3, 2, 9, 17, 1.1, 2),
                new[] { At("Square", 6, 4, 17, 20, 1), At("Saloon", 9, 4, 19, 22, 1) },
                A(everyday, trip, ("Argued", 0.5)), new[] { "Lewis", "Harvey" }, 45),
            new("Caroline", "SeedShop", "older woman", new(0.7, 0.4, 0.6, 0.5), new(100, 0.2), null,
                new[] { At("Square", 7, 15, 9, 12, 2), At("Store", 12, 2, 12, 17, 2) },
                A(everyday, trip, ("Argued", 0.3)), new[] { "Jodi", "Evelyn" }, 43),
            new("Abigail", "SeedShop", "young woman", new(0.6, 0.8, 0.4, 0.4), new(100, 0.05), null,
                new[] { At("Store", 13, 8, 12, 17, 1), At("Square", 24, 4, 14, 18, 1), At("Saloon", 15, 3, 19, 24, 3), At("Beach", 26, 3, 20, 24, 1) },
                A(everyday, trip, ("Argued", 0.5), ("Stole", 0.1)), new[] { "Sam", "Sebastian" }, 20),
            // 1 Willow Lane: Jodi, Kent and their sons Sam and Vincent.
            new("Jodi", "SamHouse", "woman", new(0.6, 0.4, 0.6, 0.4), new(95, 0.2), null,
                new[] { At("Store", 11, 5, 9, 12, 2), At("Square", 9, 16, 11, 14, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Caroline" }, 40),
            new("Kent", "SamHouse", "man", new(0.3, 0.5, 0.5, 0.4), new(110, 0.25), null,
                new[] { At("Square", 26, 12, 10, 13, 1), At("Saloon", 7, 5, 19, 22, 1) },
                A(trip, ("Argued", 0.3)), Array.Empty<string>(), 42),
            new("Sam", "SamHouse", "young man", new(0.8, 0.7, 0.5, 0.6), new(110, 0.1),
                Works("Mart", 11, 5, 9, 14, 1.1, 5, 6),
                new[] { At("Beach", 18, 9, 15, 19, 1), At("Square", 23, 13, 14, 18, 1), At("Saloon", 14, 2, 19, 24, 3) },
                A(everyday, trip, ("Argued", 0.3)), new[] { "Abigail", "Sebastian", "Alex" }, 21),
            new("Vincent", "SamHouse", "boy", new(0.9, 0.6, 0.3, 0.6), new(90, 0.35), lessons with { Spot = new Tile(13, 7) },
                new[] { At("Square", 21, 15, 14, 18, 2), At("Beach", 7, 9, 14, 18, 1) },
                A(trip, ("Squabbled", 1.0), ("GaveGift", 0.3)), new[] { "Jas" }, 7),
            // The carpenter's house on the mountain: Robin, Demetrius, Maru, and Sebastian (Robin's son).
            new("Robin", "ScienceHouse", "woman", new(0.8, 0.6, 0.6, 0.6), new(115, 0.2),
                Works("Home:ScienceHouse", 4, 4, 9, 17, 1.2, 1),
                new[] { At("Saloon", 12, 5, 19, 22, 1), At("Square", 13, 16, 17, 19, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.5)), new[] { "Lewis" }, 45),
            new("Demetrius", "ScienceHouse", "man", new(0.5, 0.5, 0.8, 0.7), new(105, 0.2),
                Works("Home:ScienceHouse", 7, 2, 9, 17, 1.0, 5, 6),
                new[] { At("Beach", 22, 7, 17, 19, 1), At("Square", 17, 13, 17, 19, 1) },
                A(everyday, trip, ("Argued", 0.3)), new[] { "Harvey" }, 46),
            new("Maru", "ScienceHouse", "young woman", new(0.6, 0.5, 0.8, 0.6), new(100, 0.15),
                Works("Home:Clinic", 6, 4, 9, 16, 1.0, 0, 2, 4, 5, 6),
                new[] { At("Square", 19, 14, 17, 19, 1), At("Saloon", 13, 4, 19, 22, 1) },
                A(everyday, trip, ("HelpedSomeone", 0.4)), new[] { "Penny", "Harvey" }, 21),
            new("Sebastian", "ScienceHouse", "young man", new(0.3, 0.5, 0.6, 0.3), new(100, 0.07), null,
                new[] { At("Saloon", 16, 4, 19, 24, 3), At("Beach", 27, 9, 20, 24, 1) },
                A(trip, ("Argued", 0.3), ("Stole", 0.05)), new[] { "Sam", "Abigail" }, 22),
            new(Newcomer, "Farm", "newcomer", new(0.3, 0.4, 0.5, 0.5), new(110, 0.2),
                Works("Farm", 10, 7, 6, 12, 1.3),
                new[]
                {
                    At("ClinicYard", 10, 6, 12, 20, 1), At("Square", 25, 5, 12, 20, 2), At("Store", 7, 2, 12, 17, 1),
                    At("Beach", 14, 8, 14, 20, 1), At("Saloon", 17, 4, 18, 24, 1), At("Mart", 12, 8, 12, 18, 1),
                },
                A(everyday, trip, ("RummagedInBin", 1.0), ("Stole", 0.5)), Array.Empty<string>(), 25),
        };
        return list.Select(v => v with
        {
            Family = FamilyOf(v.Name),
            Temperament = v.Temperament with
            {
                Sensitivity = Sensitivity.GetValueOrDefault(v.Name, 0.5),
                Retention = Retention.GetValueOrDefault(v.Name, 0.5),
                Expression = Expressions.GetValueOrDefault(v.Name, 0.75),
            },
            Birthday = Birthdays.TryGetValue(v.Name, out YearDay b) ? b : null,
        }).ToList();
    }
}
