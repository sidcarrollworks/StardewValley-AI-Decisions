using NpcMemory;

namespace NpcIntents;

/// <summary>
/// Default <see cref="ILineRenderer"/>: turns a cited diary entry into a short first-person line
/// (the NPC is the speaker). Pure templating — deterministic, no <see cref="Random"/>, no network.
/// A "Saw" entry names the place last seen; "IgnoredBy" (the initiation ladder's record of an
/// attempt the player ignored) says so; anything else falls back to a neutral musing. The player
/// is always addressed as "you", never by the diary's "Player" subject. Every
/// result passes through <see cref="LineSanitizer.Sanitize"/> so no dialogue-command characters
/// can reach a game dialogue string.
/// </summary>
public sealed class LineRenderer : ILineRenderer
{
    private const string SawKind = "Saw";
    private const string IgnoredByKind = "IgnoredBy";
    private const string TalkedKind = "Talked";
    private const string PassedByKind = "PassedBy";
    private const string BirthdayForgottenKind = "BirthdayForgotten";
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

        string line;
        if (string.Equals(entry.Kind, SawKind, StringComparison.OrdinalIgnoreCase))
            line = RenderSaw(entry, When(daysAgo));
        else if (string.Equals(entry.Kind, IgnoredByKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = $"I tried to get your attention {When(daysAgo)}. You must have been busy.";
        else if (string.Equals(entry.Kind, TalkedKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = $"It was nice talking with you {When(daysAgo)}.";
        else if (string.Equals(entry.Kind, PassedByKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = $"You walked right past me {When(daysAgo)}.";
        else if (string.Equals(entry.Kind, BirthdayForgottenKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = $"My birthday was {When(daysAgo)}, you know.";
        else
            line = $"I've been thinking about {Who(entry.Subject)}.";

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
        string who = Who(entry.Subject);
        string place = _placeName(entry.Detail);
        return string.IsNullOrEmpty(place)
            ? $"I saw {who} {when}."
            : $"I saw {who} at {place} {when}.";
    }

    /// <summary>The player is addressed in the second person ("I saw you"), never as "Player".</summary>
    private static string Who(string subject) => IsPlayer(subject) ? "you" : subject;

    private static bool IsPlayer(string subject) => string.Equals(subject, PlayerSubject, StringComparison.OrdinalIgnoreCase);
}
