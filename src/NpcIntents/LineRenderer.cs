using NpcMemory;

namespace NpcIntents;

/// <summary>
/// Default <see cref="ILineRenderer"/>: turns a cited diary entry into a short first-person line
/// (the NPC is the speaker). Pure templating — deterministic, no <see cref="Random"/>, no network.
/// A "Saw" entry names the place last seen; anything else falls back to a neutral musing. Every
/// result passes through <see cref="LineSanitizer.Sanitize"/> so no dialogue-command characters
/// can reach a game dialogue string.
/// </summary>
public sealed class LineRenderer : ILineRenderer
{
    private const string SawKind = "Saw";
    private const string PlayerSubject = "Player";

    private readonly Func<string?, string> _placeName;

    /// <param name="placeName">Maps an internal location name to the player-facing one; defaults
    /// to <see cref="PlaceNames.Display"/>. The mod may pass the game's own display names.</param>
    public LineRenderer(Func<string?, string>? placeName = null)
        => _placeName = placeName ?? PlaceNames.Display;

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="npc"/> and <paramref name="voice"/> are accepted for interface symmetry and
    /// future per-voice phrasing; the current templates do not vary by speaker, so the line is
    /// fully determined by <paramref name="entry"/> and when it happened.
    /// </remarks>
    public string Render(string npc, string voice, DiaryEntry entry) => Render(npc, voice, entry, daysAgo: 1);

    /// <inheritdoc />
    public string Render(string npc, string voice, DiaryEntry entry, int daysAgo)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string line = string.Equals(entry.Kind, SawKind, StringComparison.OrdinalIgnoreCase)
            ? RenderSaw(entry, When(daysAgo))
            : $"I've been thinking about {entry.Subject}.";

        return LineSanitizer.Sanitize(line);
    }

    /// <summary>How long ago, in words. Only exactly one day is "yesterday".</summary>
    public static string When(int daysAgo) => daysAgo switch
    {
        <= 0 => "earlier today",
        1 => "yesterday",
        < 7 => "the other day",
        _ => "a while back",
    };

    private string RenderSaw(DiaryEntry entry, string when)
    {
        // The player is addressed in the second person: "I saw you ...", never "I saw Player ...".
        string who = string.Equals(entry.Subject, PlayerSubject, StringComparison.OrdinalIgnoreCase)
            ? "you"
            : entry.Subject;

        string place = _placeName(entry.Detail);
        return string.IsNullOrEmpty(place)
            ? $"I saw {who} {when}."
            : $"I saw {who} at {place} {when}.";
    }
}
