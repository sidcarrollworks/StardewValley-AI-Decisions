using NpcMemory;
using NpcSchedules;

namespace NpcIntents;

/// <summary>
/// How much an NPC would want to talk about one diary entry: the "news" score the intent planner
/// uses to filter and rank options before asking the model (docs/spec/diary.md, "Deterministic
/// rules: newsworthiness"; intents.md, "Deterministic rules" items 1-2). Pure and deterministic:
/// memory in, a number out. Entries under <see cref="NewsworthinessOptions.MinNews"/> are never
/// offered to the model.
/// </summary>
public sealed class Newsworthiness
{
    /// <summary>Gift taste weights for <c>GiftReceived</c> (docs/spec/diary.md kinds table).
    /// An unknown taste counts as Neutral.</summary>
    private static readonly IReadOnlyDictionary<string, double> GiftTasteWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Love"] = 5,
            ["Like"] = 3,
            ["Neutral"] = 1,
            ["Dislike"] = 3,
            ["Hate"] = 4,
        };

    private const double NeutralTasteWeight = 1.0;

    private readonly NewsworthinessOptions _options;

    public Newsworthiness(NewsworthinessOptions? options = null)
        => _options = options ?? new NewsworthinessOptions();

    public NewsworthinessOptions Options => _options;

    /// <summary>
    /// Score one entry for one observer. Rules (docs/spec/diary.md kinds table and scoring):
    /// - TriedToReach and HeldAGrudge: 0 (never shared, so no other rule adds to them either).
    /// - Fixed-weight kinds: the <see cref="NewsworthinessOptions.KindWeights"/> table.
    /// - GiftReceived: taste from the entry's Detail (key "taste"; values Love/Like/Neutral/
    ///   Dislike/Hate): Love 5, Like 3, Neutral 1, Dislike 3, Hate 4; +2 when Detail "birthday" is 1.
    /// - SawGift: 2, or 3 when Hearts >= 6. WentLooking: 4, or 2 when Detail "found" is 1.
    /// - Saw: subject is the player -> 2; subject is an NPC who lives with the observer (same
    ///   home location in <see cref="NewsContext.Homes"/>) and was seen AT that home (the entry's
    ///   Detail location) -> 0; subject is somewhere unusual for it -> 2; otherwise 0.5.
    /// - "Unusual": the observer's belief about the subject (NewsContext.Beliefs[subject]) has
    ///   total evidence of at least <see cref="NewsworthinessOptions.UnusualEvidence"/> in the
    ///   entry's 2-hour block, and the share of the entry's region (NewsContext.Regions.RegionFor
    ///   of the entry's Detail location) in that block is under UnusualShare.
    /// - +1 if the entry is about the player and Hearts >= 4.
    /// - -1 per RecentCitation with the same (Kind, Subject), case-insensitive.
    /// Entries whose kind is unknown score 0. Null context tables (Homes/Beliefs/Regions) and a
    /// null context just skip the rules that need them.
    /// </summary>
    public double Score(DiaryEntry entry, NewsContext context)
    {
        if (entry is null)
            return 0;

        // TriedToReach and HeldAGrudge are never shared and an unknown kind is not in the
        // registry: both are a flat 0, with no hearts bonus or citation penalty on top.
        if (!TryScoreBase(entry, context, out double score))
            return 0;

        // +1 when the entry is about the player and the observer has 4+ hearts with them.
        if (IsPlayer(entry.Subject) && context is not null && context.Hearts >= 4)
            score += 1;

        // -1 per time the same (kind, subject) was already cited recently.
        if (context?.RecentCitations is { } citations)
        {
            foreach ((string kind, string subject) in citations)
            {
                if (string.Equals(kind, entry.Kind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(subject, entry.Subject, StringComparison.OrdinalIgnoreCase))
                {
                    score -= 1;
                }
            }
        }

        return score;
    }

    /// <summary>The entry's base weight before the hearts bonus and citation penalty, or false
    /// when the kind is never shared (TriedToReach, HeldAGrudge) or unknown.</summary>
    private bool TryScoreBase(DiaryEntry entry, NewsContext? context, out double score)
    {
        score = 0;

        if (string.Equals(entry.Kind, "TriedToReach", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Kind, "HeldAGrudge", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(entry.Kind, "Saw", StringComparison.OrdinalIgnoreCase))
        {
            score = SawScore(entry, context);
            return true;
        }

        if (string.Equals(entry.Kind, "GiftReceived", StringComparison.OrdinalIgnoreCase))
        {
            score = GiftScore(entry);
            return true;
        }

        if (string.Equals(entry.Kind, "SawGift", StringComparison.OrdinalIgnoreCase))
        {
            score = SawGiftScore(context);
            return true;
        }

        if (string.Equals(entry.Kind, "WentLooking", StringComparison.OrdinalIgnoreCase))
        {
            score = WentLookingScore(entry);
            return true;
        }

        if (KindWeight(entry.Kind) is { } weight)
        {
            score = weight;
            return true;
        }

        return false; // not in the registry
    }

    /// <summary>A fixed kind weight, or null when the table has no entry for the kind.</summary>
    private double? KindWeight(string kind)
    {
        if (_options.KindWeights is not { } weights)
            return null;
        // The default table compares case-insensitively; a caller's dictionary might not.
        if (weights.TryGetValue(kind, out double weight))
            return weight;
        foreach ((string key, double value) in weights)
        {
            if (string.Equals(key, kind, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return null;
    }

    /// <summary>Saw: 2 for the player, 0 for a housemate seen at their shared home, 2 for an
    /// unusual sighting (see <see cref="IsUnusual"/>), else 0.5.</summary>
    private double SawScore(DiaryEntry entry, NewsContext? context)
    {
        if (IsPlayer(entry.Subject))
            return 2;

        string? subjectHome = HomeOf(context, entry.Subject);
        string? observerHome = context is null ? null : HomeOf(context, context.Observer);
        if (subjectHome is not null
            && entry.Detail is not null
            && observerHome is not null
            && string.Equals(subjectHome, entry.Detail, StringComparison.OrdinalIgnoreCase)
            && string.Equals(subjectHome, observerHome, StringComparison.OrdinalIgnoreCase))
        {
            return 0; // lives with the observer and was seen at home: never news
        }

        return IsUnusual(entry, context) ? 2 : 0.5;
    }

    /// <summary>
    /// True when the observer's belief about the subject has at least
    /// <see cref="NewsworthinessOptions.UnusualEvidence"/> in the entry's block and the entry's
    /// region holds under <see cref="NewsworthinessOptions.UnusualShare"/> of it. Fewer than that
    /// (or a missing home, belief or region map) is simply not unusual.
    /// </summary>
    private bool IsUnusual(DiaryEntry entry, NewsContext? context)
    {
        if (context?.Beliefs is not { } beliefs || context.Regions is null)
            return false;
        if (!TryGetBelief(beliefs, entry.Subject, out RoutineBelief? belief) || belief is null)
            return false;

        int block = TimeUtils.BlockIndex(entry.AbsoluteTick % TimeUtils.TicksPerDay, context.Regions.BlockMinutes);
        if (block < 0 || block >= belief.BlockCount)
            return false;

        string region = RegionOf(context.Regions, entry.Detail);

        double evidence = 0;
        double seen = 0;
        foreach ((string name, double[] column) in belief.Counts)
        {
            if (block >= column.Length)
                continue;
            evidence += column[block];
            if (string.Equals(name, region, StringComparison.OrdinalIgnoreCase))
                seen += column[block];
        }

        return evidence >= _options.UnusualEvidence
            && seen / evidence < _options.UnusualShare;
    }

    private static double GiftScore(DiaryEntry entry)
    {
        IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(entry.Detail);
        double score = detail.TryGetValue("taste", out string? taste)
            ? TasteWeight(taste)
            : NeutralTasteWeight;
        if (detail.TryGetValue("birthday", out string? birthday) && birthday is "1")
            score += 2;
        return score;
    }

    private static double TasteWeight(string? taste)
        => taste is not null && GiftTasteWeights.TryGetValue(taste, out double weight)
            ? weight
            : NeutralTasteWeight;

    private static double SawGiftScore(NewsContext? context)
        => context is not null && context.Hearts >= 6 ? 3 : 2;

    private static double WentLookingScore(DiaryEntry entry)
    {
        IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(entry.Detail);
        return detail.TryGetValue("found", out string? found) && found is "1" ? 2 : 4;
    }

    private static bool IsPlayer(string subject)
        => string.Equals(subject, MemoryStore.PlayerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>An NPC's home location, or null when unknown. Names and locations compare
    /// case-insensitively even when the caller's dictionary does not.</summary>
    private static string? HomeOf(NewsContext? context, string? npc)
    {
        if (npc is null || context?.Homes is not { } homes)
            return null;
        if (homes.TryGetValue(npc, out string? home))
            return home;
        foreach ((string name, string location) in homes)
        {
            if (string.Equals(name, npc, StringComparison.OrdinalIgnoreCase))
                return location;
        }
        return null;
    }

    /// <summary>The observer's belief about a subject, matched case-insensitively even when the
    /// caller's dictionary does not.</summary>
    private static bool TryGetBelief(
        IReadOnlyDictionary<string, RoutineBelief> beliefs, string subject, out RoutineBelief? belief)
    {
        belief = null;
        if (subject is null)
            return false;
        if (beliefs.TryGetValue(subject, out RoutineBelief? found) && found is not null)
        {
            belief = found;
            return true;
        }
        foreach ((string name, RoutineBelief value) in beliefs)
        {
            if (value is not null && string.Equals(name, subject, StringComparison.OrdinalIgnoreCase))
            {
                belief = value;
                return true;
            }
        }
        return false;
    }

    private static string RegionOf(RegionMap regions, string? location)
        => location is null ? RegionMap.OtherRegion : regions.RegionFor(location) ?? RegionMap.OtherRegion;
}
