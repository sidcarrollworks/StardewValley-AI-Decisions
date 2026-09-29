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

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="npc"/> and <paramref name="voice"/> are accepted for interface symmetry and
    /// future per-voice phrasing; the current templates do not vary by speaker, so the line is
    /// fully determined by <paramref name="entry"/>.
    /// </remarks>
    public string Render(string npc, string voice, DiaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string line = string.Equals(entry.Kind, SawKind, StringComparison.OrdinalIgnoreCase)
            ? RenderSaw(entry)
            : $"I've been thinking about {entry.Subject}.";

        return LineSanitizer.Sanitize(line);
    }

    private static string RenderSaw(DiaryEntry entry)
    {
        // The player is addressed in the second person: "I saw you ...", never "I saw Player ...".
        string who = string.Equals(entry.Subject, PlayerSubject, StringComparison.OrdinalIgnoreCase)
            ? "you"
            : entry.Subject;

        return string.IsNullOrEmpty(entry.Detail)
            ? $"I saw {who} yesterday."
            : $"I saw {who} at {entry.Detail} yesterday.";
    }
}
