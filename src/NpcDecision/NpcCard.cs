namespace NpcDecision;

/// <summary>
/// The NPC card: the shared first section of every model state (docs/spec/laya.md, "Data model"),
/// pure so it can be built and tested without the game. The mod resolves the game's data into
/// plain words (temperament from Data/Characters Manner/SocialAnxiety/Optimism, the voice sheet
/// line, a date/weather/time line) and calls <see cref="Render"/>; the card is passed into state
/// builders as a copy. Player-facing words only, ASCII-safe, no instructions to the model.
/// </summary>
public static class NpcCard
{
    /// <summary>
    /// Renders the card. Null or whitespace temperament/voice render as "unknown"; hearts are
    /// clamped to 0..10; <paramref name="today"/> is used verbatim (the mod builds e.g.
    /// "spring 12 (Tuesday), sunny, 7:30 PM"). Deterministic.
    /// </summary>
    public static string Render(string npc, string temperament, string voice, int hearts, string today)
    {
        string words(string? s) => string.IsNullOrWhiteSpace(s) ? "unknown" : s;
        int clampedHearts = Math.Clamp(hearts, 0, 10);

        return "npc: " + npc + "\n"
            + "temperament: " + words(temperament) + "\n"
            + "voice: " + words(voice) + "\n"
            + "hearts with the player: " + clampedHearts + " of 10\n"
            + "today: " + today;
    }
}
