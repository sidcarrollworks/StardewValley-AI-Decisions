using System.Globalization;
using NpcTemperament;

namespace NpcMotives;

/// <summary>A villager's relationship status with the player, as the mod reads it from the
/// game's <c>Friendship.Status</c> (the reader maps it; docs/spec/vanilla-sources.md, "History at
/// install").</summary>
public enum HistoryStatus
{
    None = 0,
    Dating = 1,
    Engaged = 2,
    Married = 3,
    Divorced = 4,
}

/// <summary>
/// What the game remembers of one villager's past with the player, read once per save (the
/// game's gift log, heart events seen, relationship status). Gifts are counted by the villager's
/// taste for each item; the game keeps no dates or order for them, and no conversations.
/// </summary>
public sealed record NpcHistory(
    string Npc,
    int Loved = 0,
    int Liked = 0,
    int Neutral = 0,
    int Disliked = 0,
    int Hated = 0,
    int HeartEventsSeen = 0,
    HistoryStatus Status = HistoryStatus.None)
{
    public int Gifts => Loved + Liked + Neutral + Disliked + Hated;

    /// <summary>This history without what the mod already saw itself (<paramref name="seen"/>,
    /// from <see cref="RegardHistory.SeenInDiary"/>), so nothing counts twice: those gifts and
    /// heart events already left their mark in regard. Counts never go below 0; the status stays.</summary>
    public NpcHistory Except(NpcHistory seen) => this with
    {
        Loved = Math.Max(0, Loved - seen.Loved),
        Liked = Math.Max(0, Liked - seen.Liked),
        Neutral = Math.Max(0, Neutral - seen.Neutral),
        Disliked = Math.Max(0, Disliked - seen.Disliked),
        Hated = Math.Max(0, Hated - seen.Hated),
        HeartEventsSeen = Math.Max(0, HeartEventsSeen - seen.HeartEventsSeen),
    };
}

/// <summary>One villager's seeded regard toward the player, and why.</summary>
public sealed record HistorySeed(string Npc, double Before, double After, double Raw, NpcHistory History)
{
    /// <summary>The regard after seeding.</summary>
    public double Regard => After;

    /// <summary>The shadow line: "seeded regard from history: Haley +0.42 (31 gifts: 12 loved,
    /// 15 liked, 4 neutral; 2 heart events)", or "Haley +0.10 -> +0.52 (...)" when the villager
    /// already had regard from play.</summary>
    public string Line => "seeded regard from history: " + Npc + " "
        + (Math.Abs(Before) < 0.005 ? "" : Before.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " -> ")
        + After.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + $" ({Because})";

    /// <summary>The cause, in words, for the log and the playtest record.</summary>
    public string Because
    {
        get
        {
            var parts = new List<string>();
            NpcHistory h = History;
            if (h.Gifts > 0)
            {
                var tastes = new[] { (h.Loved, "loved"), (h.Liked, "liked"), (h.Neutral, "neutral"), (h.Disliked, "disliked"), (h.Hated, "hated") }
                    .Where(x => x.Item1 > 0).Select(x => $"{x.Item1} {x.Item2}");
                parts.Add($"{h.Gifts} gift{(h.Gifts == 1 ? "" : "s")}: {string.Join(", ", tastes)}");
            }
            if (h.HeartEventsSeen > 0)
                parts.Add($"{h.HeartEventsSeen} heart event{(h.HeartEventsSeen == 1 ? "" : "s")}");
            if (h.Status != HistoryStatus.None)
                parts.Add(h.Status.ToString().ToLowerInvariant());
            return string.Join("; ", parts);
        }
    }
}

/// <summary>
/// History at install (docs/spec/vanilla-sources.md): regard toward the player seeded from what
/// the game remembers, so a mod installed mid-playthrough doesn't meet a town that has forgotten
/// years of gifts. Pure: the mod reads the game on the game thread and passes the counts in.
/// <para>
/// Each gift and heart event leaves the lasting mark the live rule would leave
/// (<see cref="RegardBook.Apply"/>: valence x magnitude x sensitivity x plastic share x retention),
/// halved by <see cref="MotiveOptions.HistoryFade"/> because their age is unknown and live regard
/// would have faded since. The sum is soft-capped: warmth approaches
/// <see cref="MotiveOptions.HistoryMaxWarmth"/>, and a grudge stops at
/// <see cref="MotiveOptions.HistoryMaxGrudge"/>, below the friendship penalty's threshold, so no
/// villager arrives already past it. Then the status: dating, engaged and married set a floor,
/// divorced a lasting grudge. Hearts are not used: familiarity already counts them.
/// </para>
/// </summary>
public static class RegardHistory
{
    /// <summary>The seed for one villager on top of the regard it already has
    /// (<paramref name="before"/>, 0 on a fresh install), or null when its history leaves no mark
    /// (no gifts with a feeling, no heart events, no status that moves it).</summary>
    public static HistorySeed? SeedOf(NpcHistory h, Temperament t, MotiveOptions? options = null, double before = 0)
    {
        MotiveOptions o = options ?? new MotiveOptions();
        double raw = Count(h.Loved, "Love", h.Npc, t, o)
                     + Count(h.Liked, "Like", h.Npc, t, o)
                     + Count(h.Disliked, "Dislike", h.Npc, t, o)
                     + Count(h.Hated, "Hate", h.Npc, t, o)
                     + Mark(StressorTable.Of(new NpcMemory.DiaryEntry(0, NpcMemory.MemoryStore.PlayerName, "HeartEvent", "")), h.Npc, t, o)
                       * Math.Max(0, h.HeartEventsSeen);
        raw *= o.HistoryFade;

        double history = raw >= 0
            ? o.HistoryMaxWarmth * Math.Tanh(raw / o.HistoryMaxWarmth)
            : -o.HistoryMaxGrudge * Math.Tanh(-raw / o.HistoryMaxGrudge);
        double regard = Math.Clamp(before + history, -1, 1);
        regard = h.Status switch
        {
            HistoryStatus.Dating => Math.Max(regard, o.HistoryDatingFloor),
            HistoryStatus.Engaged => Math.Max(regard, o.HistoryEngagedFloor),
            HistoryStatus.Married => Math.Max(regard, o.HistoryMarriedFloor),
            HistoryStatus.Divorced => Math.Min(regard, -o.HistoryDivorcedGrudge),
            _ => regard,
        };
        regard = Math.Round(regard, 4);
        return Math.Abs(regard - before) < 0.005 ? null : new HistorySeed(h.Npc, before, regard, raw, h);
    }

    /// <summary>
    /// Seeds every villager's regard toward the player from its history, in name order, on top of
    /// the regard already in the book. Pass histories with what the mod already saw removed
    /// (<see cref="NpcHistory.Except"/>), and run it once per save: the caller keeps the flag
    /// (the save-data key <c>historySeeded</c>). Returns the seeds applied.
    /// </summary>
    public static IReadOnlyList<HistorySeed> Seed(RegardBook book, IEnumerable<NpcHistory> histories,
        Func<string, Temperament> temperamentOf, MotiveOptions? options = null)
    {
        var applied = new List<HistorySeed>();
        foreach (NpcHistory h in histories.OrderBy(h => h.Npc, StringComparer.OrdinalIgnoreCase))
        {
            double before = book.Of(h.Npc, NpcMemory.MemoryStore.PlayerName);
            if (SeedOf(h, temperamentOf(h.Npc), options, before) is not { } seed)
                continue;
            book.Set(h.Npc, NpcMemory.MemoryStore.PlayerName, seed.Regard);
            applied.Add(seed);
        }
        return applied;
    }

    /// <summary>What the mod already saw itself of one villager's history: the player's gifts it
    /// noted (<c>GiftReceived</c>, by taste) and the heart events it noted (<c>HeartEvent</c>).
    /// Those left their mark in regard when they were written, so the seed leaves them out.
    /// The diary keeps the newest 500 entries, so on a long save a few old ones may have been
    /// trimmed and count again, at half strength.</summary>
    public static NpcHistory SeenInDiary(string npc, IEnumerable<NpcMemory.DiaryEntry> diary)
    {
        int loved = 0, liked = 0, neutral = 0, disliked = 0, hated = 0, events = 0;
        foreach (NpcMemory.DiaryEntry e in diary)
        {
            if (!string.Equals(e.Subject, NpcMemory.MemoryStore.PlayerName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (e.Kind == "HeartEvent")
            {
                events++;
                continue;
            }
            if (e.Kind != "GiftReceived")
                continue;
            switch (NpcMemory.DiaryDetail.Parse(e.Detail).TryGetValue("taste", out string? taste) ? taste : null)
            {
                case "Love": loved++; break;
                case "Like": liked++; break;
                case "Dislike": disliked++; break;
                case "Hate": hated++; break;
                default: neutral++; break;
            }
        }
        return new NpcHistory(npc, loved, liked, neutral, disliked, hated, events);
    }

    private static double Count(int count, string taste, string npc, Temperament t, MotiveOptions o)
        => Math.Max(0, count) * Mark(StressorTable.Of(new NpcMemory.DiaryEntry(0, NpcMemory.MemoryStore.PlayerName, "GiftReceived", "taste=" + taste)), npc, t, o);

    /// <summary>One entry's lasting mark, as <see cref="RegardBook.Apply"/> computes it (without
    /// the yield count, which none of these kinds use).</summary>
    private static double Mark(StressorProfile? p, string npc, Temperament t, MotiveOptions o)
    {
        if (p is null || p.Plastic <= 0)
            return 0;
        double magnitude = p.Magnitude * Stresses.SensitivityFactor(t);
        double retention = magnitude >= o.SevereMagnitude ? 1.0 : 0.5 + o.RetentionOf(npc);
        return p.Valence * magnitude * p.Plastic * retention;
    }
}
