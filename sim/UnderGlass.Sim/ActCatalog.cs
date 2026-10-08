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
            rows.AddRange(Returns);
        // acts-2 (Company): PlayedGame, TreatedToDrink. acts-3 (Welcome): Welcomed. acts-4 (Repair):
        // Apologised. acts-5 (Sides): Comforted, Mocked, StoodUpFor. acts-6 (Late): LateForWork.
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

    /// <summary>The switch of the slice a catalog row belongs to (a name in <see cref="ActOptions.Switches"/>);
    /// null for a kind that isn't the catalog's (every shipped row).</summary>
    public static string? SliceOf(string kind) => kind switch
    {
        "Thanked" or "Complimented" or "Joked" => nameof(ActOptions.Returns),
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

    /// <summary>(kind, who, weight) for the slices that are on. Empty in acts-0; the cast readings
    /// that come with acts-2 and acts-5 are guesses (VERIFY, acts spec question 4).</summary>
    private static IReadOnlyList<(string Kind, string Who, double Weight)> CardsOf(ActOptions o)
        => Array.Empty<(string, string, double)>();
}
