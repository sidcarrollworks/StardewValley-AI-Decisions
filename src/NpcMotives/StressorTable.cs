using NpcMemory;

namespace NpcMotives;

/// <summary>
/// How one kind of diary entry stirs a character (docs/spec/motives.md, "Stressor profiles").
/// <see cref="Magnitude"/> is before sensitivity; <see cref="ElasticDecay"/> is a per-day
/// multiplier on the fading part; <see cref="Plastic"/> is the share that becomes a lasting mark
/// in regard. <see cref="Yields"/> kinds leave no mark once, but repeated ones do (the yield
/// point).
/// </summary>
public sealed record StressorProfile(
    Motive Motive,
    int Valence,
    double Magnitude,
    double ElasticDecay,
    double Plastic,
    double Juiciness,
    string Emotion,
    bool Yields = false);

/// <summary>
/// The stressor table: one place that says what each diary kind means emotionally and as gossip.
/// First guesses (spec table); the playtest log is how they get tuned. Kinds not listed stir
/// nothing (a plain <c>Saw</c>, <c>TriedToReach</c>, <c>TownNews</c>...).
/// </summary>
public static class StressorTable
{
    /// <summary>The profile for an entry, or null when the entry stirs no feeling. Some kinds
    /// depend on their detail (a gift's taste, a stand-up seen, a dance accepted).</summary>
    public static StressorProfile? Of(DiaryEntry entry)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(entry.Detail);
        string? get(string key) => d.TryGetValue(key, out string? v) ? v : null;

        switch (entry.Kind)
        {
            case "GiftReceived":
                return get("taste") switch
                {
                    "Love" => new(Motive.Grateful, +1, 0.6, 0.8, 0.3, 2, "happiness"),
                    "Like" => new(Motive.Grateful, +1, 0.3, 0.7, 0.1, 1, "happiness"),
                    "Dislike" => new(Motive.Hurt, -1, 0.3, 0.7, 0.1, 2, "anger"),
                    "Hate" => new(Motive.Hurt, -1, 0.6, 0.8, 0.3, 3, "disgust"),
                    _ => null, // neutral gifts stir nothing
                };
            case "QuestHelped":
                return new(Motive.Grateful, +1, 0.5, 0.8, 0.3, 2, "happiness");
            case "AcceptedInvite":
                return new(Motive.Grateful, +1, 0.4, 0.8, 0.2, 1, "happiness");
            case "FarmVisited":
                return new(Motive.Grateful, +1, 0.4, 0.8, 0.2, 1, "happiness");
            case "Talked":
                return new(Motive.Grateful, +1, 0.1, 0.5, 0.02, 0, "happiness");
            case "Praised":
                return new(Motive.Grateful, +1, 0.3, 0.7, 0.1, 1, "happiness");
            case "HeartEvent":
                return new(Motive.Grateful, +1, 0.6, 0.9, 0.5, get("with") is null ? 1 : 3, "happiness");
            case "MovieTogether":
                return get("liked") == "dislike"
                    ? new(Motive.Hurt, -1, 0.15, 0.8, 0, 2, "disgust")
                    : new(Motive.Grateful, +1, 0.4, 0.8, 0.2, 2, "happiness");
            case "DanceAsked":
                return get("accepted") == "0"
                    ? new(Motive.Hurt, -1, 0.4, 0.85, 0.2, 3, "sadness")
                    : new(Motive.Grateful, +1, 0.5, 0.85, 0.3, 3, "happiness");
            case "StoodUp":
                return get("seen") == "1"
                    ? new(Motive.Hurt, -1, 0.85, 0.85, 0.6, 3, "anger")
                    : new(Motive.Hurt, -1, 0.7, 0.85, 0.5, 3, "sadness");
            case "BirthdayForgotten":
                return new(Motive.Hurt, -1, 0.6, 0.85, 0.4, 2, "sadness");
            case "MissedVisit":
                return new(Motive.Hurt, -1, 0.4, 0.8, 0.2, 1, "sadness");
            case "IgnoredBy":
                return new(Motive.Hurt, -1, 0.2, 0.5, 0, 1, "anger", Yields: true);
            case "PassedBy":
                return new(Motive.Hurt, -1, 0.15, 0.5, 0, 0, "sadness", Yields: true);
            case "BrushedOff":
                return new(Motive.Hurt, -1, 0.15, 0.5, 0, 0, "sadness", Yields: true);
            case "Criticized":
                return new(Motive.Hurt, -1, 0.4, 0.8, 0.3, 2, "anger");
            case "SawRummaging":
                return new(Motive.Hurt, -1, 0.3, 0.8, 0.1, 4, "disgust");
            case "Argued":
                return new(Motive.Hurt, -1, 0.4, 0.8, 0.3, 3, "anger");
            case "ChattedWith":
                return new(Motive.Grateful, +1, 0.05, 0.5, 0.02, 0, "happiness");
            default:
                return null;
        }
    }

    /// <summary>
    /// How gossip-worthy an entry is (docs/spec/ledger-gossip.md, "Juiciness"; D25): the profile's
    /// juiciness, or for kinds that stir no feeling of their own, their gossip value alone (a gift
    /// seen by taste, town news, a festival). Null: not gossip at all (a plain <c>Saw</c>). The
    /// mod passes this to <c>MemoryStore.Chat</c>.
    /// </summary>
    public static double? JuicinessOf(DiaryEntry entry)
    {
        if (Of(entry) is { } p)
            return p.Juiciness;
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(entry.Detail);
        return entry.Kind switch
        {
            "SawGift" => (d.TryGetValue("taste", out string? taste) ? taste : null) switch
            {
                "Love" => 2,
                "Like" => 1.5,
                "Dislike" => 2,
                "Hate" => 3,
                _ => 1,
            },
            "TownNews" => 2,
            "Festival" => 1,
            _ => null,
        };
    }

    /// <summary>Kinds that lift the mood and leave warmth in regard but are no reason to act on
    /// their own: a plain chat is not something to thank anyone for (playtest 2026-10-02: Haley
    /// wrote "thank you" letters after ordinary talks).</summary>
    public static bool IsMoodOnly(string kind) => kind is "Talked" or "ChattedWith";

    /// <summary>Who an entry's feeling is about. Most kinds are about their subject; a
    /// <c>SawGift</c> is about the giver.</summary>
    public static string TargetOf(DiaryEntry entry)
    {
        if (entry.Kind == "SawGift")
        {
            IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(entry.Detail);
            if (d.TryGetValue("giver", out string? giver))
                return giver;
        }
        return entry.Subject;
    }
}
