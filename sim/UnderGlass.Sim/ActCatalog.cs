namespace UnderGlass.Sim;

/// <summary>
/// The act catalog, batch 1 (acts spec 4; Sid, 2026-10-08): the rows each slice adds to the town's
/// act kinds, and the card weights that go with them. The rows come after the town's own, so every
/// shipped kind keeps its index (the order StartActs draws in, and the replay's kind indices).
/// acts-1 adds Thanked, Complimented and Joked behind Returns, and the later slices add theirs behind
/// their own switches. A row whose slice is off is never offered (<see cref="SliceOf"/>), so with
/// every row appended and every switch off the town is the shipped one.
/// </summary>
public static class ActCatalog
{
    /// <summary>The rows of the slices that are on, in catalog order.</summary>
    public static IReadOnlyList<ActKind> Batch1(ActOptions o)
    {
        var rows = new List<ActKind>();
        if (o.Returns)
            rows.AddRange(o.Company ? Returns.Select(PerHeadWithCompany) : Returns);
        if (o.Company)
            rows.AddRange(Company);
        if (o.Welcome)
            rows.AddRange(Welcome);
        if (o.Repair)
            rows.AddRange(Repair);
        if (o.Sides)
            rows.AddRange(Sides);
        // acts-6 (Late): LateForWork.
        return rows;
    }

    /// <summary>
    /// acts-1, Returns (acts spec 4.1-4.3): three light kind acts, so the shy can answer a kindness
    /// and the fond can be warm without a gift. Light: never retold (juiciness under 1.5), they stir
    /// no motive, settle as None, are capped at WarmPerDay an actor a day and WarmBudget of regard a
    /// pair a day, and stay out of 0d.6's kindness counts (acts spec question 2, answer c). Gate
    /// only in acts-1: Complimented and Joked are drawn per head too once acts-2 builds the draws.
    /// <list type="bullet">
    /// <item>Thanked, "Haley thanked Evelyn for the shell": rule 10's wave (0.2 / 0.02); answers
    /// Return only, so it needs a kindness from the target in the last week.</item>
    /// <item>Complimented, "Leah told Haley her photos were lovely": rule 10's chat (0.3 / 0.05);
    /// answers Return, MakeUp, Fond and Remorse; familiarity 0.2 or more; age 5 and up; read cold
    /// (flattery, -0.06) by a target who holds the actor below -0.2.</item>
    /// <item>Joked, "Sam teased Penny about her lesson plans": 0.2 / 0.05; answers Fond; the joker
    /// holds the target at 0.3 or more and knows them at 0.5 or more; age 7 and up; read cold (taken
    /// badly, -0.04) by a target who holds the joker below 0.1.</item>
    /// </list>
    /// </summary>
    public static readonly IReadOnlyList<ActKind> Returns = new[]
    {
        new ActKind("Thanked", 0.5, 1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, 0.08, 0.1, 1, TargetIs.Chosen),
            Gate: new ActGate(0.2, 0.02, new[] { DesireKind.Return }, Light: true)),
        new ActKind("Complimented", 1.0, 1, 1, 2, 0, Array.Empty<string>(), MinAge: 5,
            Affect: new Affect(Patient.Target, 0.12, 0.15, 1, TargetIs.Chosen, Tilt: 1, ReadWarmAt: -0.2),
            Gate: new ActGate(0.3, 0.05, new[] { DesireKind.Return, DesireKind.MakeUp, DesireKind.Fond, DesireKind.Remorse },
                Light: true, MinFamiliarity: 0.2)),
        new ActKind("Joked", 0.5, 1, 1, 2, 0, Array.Empty<string>(), MinAge: 7,
            Affect: new Affect(Patient.Target, 0.08, 0.1, 1, TargetIs.Chosen, Tilt: 1, ReadWarmAt: 0.1),
            Gate: new ActGate(0.2, 0.05, new[] { DesireKind.Fond }, Light: true, MinFamiliarity: 0.5, MinRegard: 0.3)),
    };

    /// <summary>Complimented and Joked are drawn per head too once the per-head draws exist (acts-2,
    /// Company): 0.08 and 0.15 a day for those who carry them on their cards.</summary>
    private static ActKind PerHeadWithCompany(ActKind k) => k.Name switch
    {
        "Complimented" => k with { PerHead = true, PerDay = 0.08 },
        "Joked" => k with { PerHead = true, PerDay = 0.15 },
        _ => k,
    };

    /// <summary>
    /// acts-2, Company (acts spec 4.4 and 4.5): time spent together, and the bar tied to money and
    /// company. Both are drawn per head (a rate for each who carries them on their card, so acts
    /// per person hold as the town grows) and answer Fond through the gate; with Company on, Fond
    /// gives a gift only on the other's birthday or a festival (acts spec question 1, answer b).
    /// <list type="bullet">
    /// <item>PlayedGame, "Sebastian and Sam played pool": light; 0.3 / 0.1; 30 minutes, both busy;
    /// familiarity 0.2 or more; age 5 and up, the two within 6 years of each other or both under 13;
    /// at the saloon from 17:00 to midnight (pool, darts, the arcade: VERIFY that the saloon has
    /// them), in the square or on the beach from 9:00 to 19:00 (children's games, catch). The target
    /// feels it as a kindness; the actor, as company.</item>
    /// <item>TreatedToDrink, "Shane bought Emily a drink": a story act (told on day 0 to those who know
    /// them); 0.3 / 0.1; at the saloon from 18:00 to 01:00, both 18 or older; it answers Return,
    /// MakeUp and Fond and stirs Return as a gift does. The actor's household pays one drink to the
    /// bar's, which restocks; it is the target's drink of the day if they had none, else a second;
    /// the bar's own household treats on the house. The purse must hold a drink.</item>
    /// </list>
    /// </summary>
    public static readonly IReadOnlyList<ActKind> Company = new[]
    {
        new ActKind("PlayedGame", 1.0, 1, 2, 30, 0.1, new[] { "Saloon", "Square", "Beach" }, MinAge: 5,
            Affect: new Affect(Patient.Target, 0.08, 0.1, 1, TargetIs.Chosen),
            Gate: new ActGate(0.3, 0.1, new[] { DesireKind.Fond }, Light: true, MinFamiliarity: 0.2),
            PerHead: true, FromMinute: Clock.At(9), ToMinute: Clock.MinutesPerDay),
        new ActKind("TreatedToDrink", 1.5, 1, 1, 2, 0.08, new[] { "Saloon" }, MinAge: 18,
            Affect: new Affect(Patient.Target, 0.15, 0.25, 1, TargetIs.Chosen, Tilt: 1),
            Gate: new ActGate(0.3, 0.1, new[] { DesireKind.Return, DesireKind.MakeUp, DesireKind.Fond }),
            PerHead: true, FromMinute: Clock.At(18), ToMinute: Clock.At(1)),
    };

    /// <summary>
    /// acts-3, Welcome (acts spec 4.6): Welcomed, "Robin showed the newcomer round the square", the
    /// answer to the Curious motive (Simulation.CuriousMotives): someone free feels curious about
    /// anyone in reach they know below NewAt and haven't welcomed. A story act (told on day 0 to those
    /// who know them); 0.3 / 0.1; in a public place, not a home; at most once for each ordered pair,
    /// ever; any age. Each comes to know the other WelcomeFamiliarity better, and it stirs Return in
    /// the one welcomed. It is keyed on familiarity, not on the name "Newcomer": in the shipped town
    /// only the newcomer starts that little known, and in a grown town it is how people from
    /// different neighbourhoods meet. (VERIFY: Lewis greets the farmer on day 1 in the game.)
    /// </summary>
    public static readonly IReadOnlyList<ActKind> Welcome = new[]
    {
        new ActKind("Welcomed", 1.5, 1, 1, 5, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, 0.15, 0.25, 1, TargetIs.Chosen),
            Gate: new ActGate(0.3, 0.1, new[] { DesireKind.Curious })),
    };

    /// <summary>
    /// acts-4, Repair (acts spec 4.7): Apologised, "Sam said sorry to Penny for the joke", the answer
    /// to the Remorse motive (Simulation.StirRemorse): the actor of an argument, or of a joke read
    /// cold, who doesn't dislike the target and wasn't in the right, wants to put it right. A story
    /// act (told on day 0 to those who know them); a walk-up (0.5 / 0.15), dearer for the proud by 0.5
    /// x (self-regard - 0.5); anywhere; age 5 and up; its About is the hurt. The target answers on the
    /// spot (Simulation.AnswerApology): accepted, they get back part of the regard the hurt cost
    /// (half the first time within ApologyDays, a quarter the second, then nothing: question 3,
    /// answer b), the fear and the grudge it left are lifted, and their stance eases; refused, the
    /// apologiser smarts and the hurt stays.
    /// </summary>
    public static readonly IReadOnlyList<ActKind> Repair = new[]
    {
        new ActKind("Apologised", 1.5, 1, 2, 5, 0, Array.Empty<string>(), MinAge: 5,
            Affect: new Affect(Patient.Target, 0.1, 0.2, 1, TargetIs.Chosen),
            Gate: new ActGate(0.5, 0.15, new[] { DesireKind.Remorse }, PrideWeight: 0.5)),
    };

    /// <summary>
    /// acts-5, Sides (acts spec 4.8-4.10): one hurt in public opens three threads, the target's
    /// answer, someone who defends them, and someone who comforts them.
    /// <list type="bullet">
    /// <item>Comforted, "Emily sat with Haley after the square": a story act (0.4 / 0.15); age 7 and up;
    /// it answers Pity (a witness's, at a hurt seen: Simulation.PityAfterHurt) and Remorse (only if
    /// the target was hurt within the hour). It eases the target's stance, cools their grudge against
    /// whoever hurt them by ComfortCools, and stirs Return.</item>
    /// <item>Mocked, "Haley laughed at Penny in front of the market": news; heavy hostile (0.55 / 0.25
    /// with the hostile surcharge); only with a card, an audience of two or more and expression 0.5 or
    /// more; in a place that isn't a home; age 10 and up. Dearer than an argument, so the gate picks
    /// it only when someone bold has a crowd: it replaces arguments rather than adding to them.</item>
    /// <item>StoodUpFor, "Marnie told Pierre to leave Shane alone": news; heavy hostile (0.5 / 0.2 with
    /// the surcharge); age 7 and up; it answers Defend (Simulation.DefendAfterHurt). Its target is the
    /// aggressor and its With the one defended, who, if they saw it, feels WithJoy toward the defender
    /// and wants to return it.</item>
    /// </list>
    /// </summary>
    public static readonly IReadOnlyList<ActKind> Sides = new[]
    {
        new ActKind("Comforted", 1.5, 1, 2, 10, 0, Array.Empty<string>(), MinAge: 7,
            Affect: new Affect(Patient.Target, 0.2, 0.25, 1, TargetIs.Chosen, Tilt: 1),
            Gate: new ActGate(0.4, 0.15, new[] { DesireKind.Pity, DesireKind.Remorse })),
        new ActKind("Mocked", 2.5, -1, 1, 3, 0, Array.Empty<string>(), MinAge: 10,
            Affect: new Affect(Patient.Target, -0.3, 0.35, 1, TargetIs.Chosen),
            Gate: new ActGate(0.55, 0.25, new[] { DesireKind.Answer, DesireKind.Retaliate }, NeedsCard: true, MinAudience: 2)),
        new ActKind("StoodUpFor", 2.5, -1, 2, 5, 0, Array.Empty<string>(), MinAge: 7,
            Affect: new Affect(Patient.Target, -0.2, 0.3, 1, TargetIs.Chosen, WithJoy: 0.2),
            Gate: new ActGate(0.5, 0.2, new[] { DesireKind.Defend })),
    };

    /// <summary>The switch of the slice a catalog row belongs to (a name in <see cref="ActOptions.Switches"/>);
    /// null for a kind that isn't the catalog's (every shipped row).</summary>
    public static string? SliceOf(string kind) => kind switch
    {
        "Thanked" or "Complimented" or "Joked" => nameof(ActOptions.Returns),
        "PlayedGame" or "TreatedToDrink" => nameof(ActOptions.Company),
        "Welcomed" => nameof(ActOptions.Welcome),
        "Apologised" => nameof(ActOptions.Repair),
        "Comforted" or "Mocked" or "StoodUpFor" => nameof(ActOptions.Sides),
        _ => null,
    };

    /// <summary>The town's act kinds (the shipped town's when none are given) with the rows of the
    /// slices that are on after them. With no rows, the town's own list.</summary>
    public static IReadOnlyList<ActKind> Kinds(ActOptions o, IReadOnlyList<ActKind>? town = null)
    {
        IReadOnlyList<ActKind> kinds = town ?? DefaultTown.Acts();
        IReadOnlyList<ActKind> rows = Batch1(o);
        return rows.Count == 0 ? kinds : kinds.Concat(rows).ToList();
    }

    /// <summary>The cast with the card weights of the slices that are on: for the kinds drawn per
    /// head and the kinds that need a card. Only each villager's Acts changes; with no cards, the
    /// cast as given.</summary>
    public static IReadOnlyList<Villager> Cards(IReadOnlyList<Villager> cast, ActOptions o)
    {
        var cards = CardsOf(o);
        if (cards.Count == 0)
            return cast;
        return cast.Select(v =>
        {
            var mine = cards.Where(c => c.Who == v.Name).ToList();
            if (mine.Count == 0)
                return v;
            var acts = new Dictionary<string, double>(v.Acts);
            foreach (var (kind, _, weight) in mine)
                acts[kind] = weight;
            return v with { Acts = acts };
        }).ToList();
    }

    /// <summary>(kind, who, weight) for the slices that are on: Mocked's (Sides), and the cards of the
    /// four kinds drawn per head, which come with Company (acts-2). Sid's answer to question 4 (b): cards only for the
    /// per-head kinds and Mocked, the gate's acts from traits. These are first readings of the cast
    /// from memory of the game, not its files (VERIFY), for Sid to correct.</summary>
    private static IReadOnlyList<(string Kind, string Who, double Weight)> CardsOf(ActOptions o)
    {
        var cards = new List<(string, string, double)>();
        if (o.Sides)
        {
            // Mocked needs a card (acts spec 4.9; question 4, answer b: cards only for the per-head kinds and Mocked).
            cards.AddRange(new[] { "Abigail", "Alex", "George", "Haley", "Pam", "Shane" }.Select(w => ("Mocked", w, 1.0)));
            cards.AddRange(new[] { "Pierre", "Sam", "Sebastian" }.Select(w => ("Mocked", w, 0.5)));
        }
        if (!o.Company)
            return cards;
        void Card(string kind, double weight, params string[] who) => cards.AddRange(who.Select(w => (kind, w, weight)));
        if (o.Returns)
        {
            Card("Complimented", 1, "Caroline", "Emily", "Evelyn", "Gus", "Harvey", "Jodi", "Leah", "Lewis", "Penny", "Robin");
            Card("Joked", 1, "Abigail", "Alex", "Gus", "Jas", "Marnie", "Robin", "Sam", "Sebastian", "Vincent");
            Card("Joked", 0.5, "Haley", "Pierre", "Shane");
        }
        Card("PlayedGame", 1, "Abigail", "Alex", "Jas", "Sam", "Sebastian", "Vincent");
        Card("PlayedGame", 0.3, "Emily", "Haley", "Maru", "Shane");
        Card("TreatedToDrink", 1, "Demetrius", "Gus", "Leah", "Lewis", "Marnie", "Pam", "Pierre", "Shane");
        return cards;
    }
}
