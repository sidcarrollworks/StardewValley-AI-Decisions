namespace NpcDecision;

/// <summary>
/// Shared text for the NPC card: the temperament line in plain words from the game's
/// Data/Characters enums (docs/spec/laya.md, "The NPC card"). One mapping, used by both the mod
/// (<c>ModEntry.TemperamentOf</c>) and the card exporter (<c>tools/CardExporter</c>), so a wording
/// change cannot silently make the exported cards stop matching the mod's.
/// </summary>
public static class CardText
{
    /// <summary>The card's temperament words from the raw enum names
    /// ("Polite"/"Rude"/"Neutral", "Outgoing"/"Shy"/"Neutral", "Positive"/"Negative"/"Neutral").
    /// Unknown values fall back to "neutral".</summary>
    public static string TemperamentWords(string manner, string anxiety, string optimism)
    {
        static string word(string value, string high, string low)
            => value switch { _ when value.Equals(high, StringComparison.OrdinalIgnoreCase) => high.ToLowerInvariant(),
                _ when value.Equals(low, StringComparison.OrdinalIgnoreCase) => low.ToLowerInvariant(),
                _ => "neutral" };
        return $"manners {word(manner, "Polite", "Rude")}, {word(anxiety, "Outgoing", "Shy")}, {OptimismWord(optimism)}";
    }

    /// <summary>The optimism word alone (used by the NPC card's game-traits summary).</summary>
    public static string OptimismWord(string optimism)
        => optimism.Equals("Positive", StringComparison.OrdinalIgnoreCase) ? "optimistic"
            : optimism.Equals("Negative", StringComparison.OrdinalIgnoreCase) ? "pessimistic"
            : "neutral";
}
