namespace UnderGlass.Sim;

/// <summary>
/// The act catalog, batch 1 (acts spec 4; Sid, 2026-10-08): the rows each slice adds to the town's
/// act kinds, and the card weights that go with them. The rows come after the town's own, so every
/// shipped kind keeps its index (the order StartActs draws in, and the replay's kind indices).
/// Slice acts-0 builds the seams only: no slice has rows yet. acts-1 adds Thanked, Complimented and
/// Joked behind Returns, and the later slices add theirs behind their own switches.
/// </summary>
public static class ActCatalog
{
    /// <summary>The rows of the slices that are on, in catalog order. Empty in acts-0.</summary>
    public static IReadOnlyList<ActKind> Batch1(ActOptions o)
    {
        var rows = new List<ActKind>();
        // acts-1 (Returns): Thanked, Complimented, Joked. acts-2 (Company): PlayedGame, TreatedToDrink.
        // acts-3 (Welcome): Welcomed. acts-4 (Repair): Apologised. acts-5 (Sides): Comforted, Mocked,
        // StoodUpFor. acts-6 (Late): LateForWork.
        return rows;
    }

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
