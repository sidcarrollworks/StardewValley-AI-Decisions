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
    private const string GiftReceivedKind = "GiftReceived";
    private const string SawGiftKind = "SawGift";
    private const string QuestHelpedKind = "QuestHelped";
    private const string FestivalKind = "Festival";
    private const string MissedFestivalKind = "MissedFestival";
    private const string HeardKind = "Heard";
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
        else if (string.Equals(entry.Kind, GiftReceivedKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = RenderGift(entry, When(daysAgo));
        else if (string.Equals(entry.Kind, SawGiftKind, StringComparison.OrdinalIgnoreCase))
            line = RenderSawGift(entry, When(daysAgo));
        else if (string.Equals(entry.Kind, QuestHelpedKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = $"Thanks for helping me out {When(daysAgo)}.";
        else if (string.Equals(entry.Kind, FestivalKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = TalkedAt(entry)
                ? $"It was nice catching up with you at the festival {When(daysAgo)}."
                : $"I saw you at the festival {When(daysAgo)}.";
        else if (string.Equals(entry.Kind, MissedFestivalKind, StringComparison.OrdinalIgnoreCase) && IsPlayer(entry.Subject))
            line = $"You missed the festival {When(daysAgo)}.";
        else if (string.Equals(entry.Kind, HeardKind, StringComparison.OrdinalIgnoreCase))
            line = RenderHeard(entry, When(daysAgo));
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

    /// <summary>A gift the NPC got, by taste: thanks only for a gift it liked (playtest
    /// 2026-10-02: "Thanks again for the Daffodil" to Jodi, who hates daffodils).</summary>
    private static string RenderGift(DiaryEntry entry, string when)
    {
        string gift = ItemName(entry) is { } item ? $"the {item}" : "the gift";
        string taste = DiaryDetail.Parse(entry.Detail).TryGetValue("taste", out string? t) ? t : "";
        return taste.ToLowerInvariant() switch
        {
            "hate" => $"About {gift} you gave me {when}. Please don't do that again.",
            "dislike" => $"I'm not sure what to do with {gift} you gave me {when}.",
            "neutral" => $"Thanks for {gift} {when}.",
            _ => $"Thanks again for {gift} {when}.",
        };
    }

    /// <summary>A gift the NPC watched someone get, with who gave it and how it went down
    /// (Sid, 2026-10-02: "I saw Jodi get a Daffodil from you, and she didn't like it"). The
    /// witness saw the reaction, so the taste is part of what it saw.</summary>
    private static string RenderSawGift(DiaryEntry entry, string when)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(entry.Detail);
        string gift = d.TryGetValue("name", out string? name) ? $"a {name}" : "a gift";
        string giver = d.TryGetValue("giver", out string? g) ? $" from {Who(g)}" : "";
        string reaction = (d.TryGetValue("taste", out string? taste) ? taste.ToLowerInvariant() : "") switch
        {
            "love" => ", and it made their day",
            "like" => ", and they seemed pleased",
            "dislike" => ", and they didn't like it",
            "hate" => ", and they hated it",
            _ => "",
        };
        return $"I saw {Who(entry.Subject)} get {gift}{giver} {when}{reaction}.";
    }

    /// <summary>Hearsay in words: who told it and what happened, from the Heard entry's copy of
    /// the original (kind, its keys, and "from").</summary>
    private static string RenderHeard(DiaryEntry entry, string when)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(entry.Detail);
        string? from = d.TryGetValue("from", out string? f) ? f : null;
        string kind = d.TryGetValue("kind", out string? k) ? k : "";
        string teller = from ?? "someone";
        // Retold (D25): the story started with someone other than the teller ("of").
        string them = d.TryGetValue("of", out string? of) && !of.Equals(teller, StringComparison.OrdinalIgnoreCase) ? of : "them";
        if (IsPlayer(entry.Subject) && kind.Equals(GiftReceivedKind, StringComparison.OrdinalIgnoreCase))
        {
            string gift = d.TryGetValue("name", out string? name) ? $"a {name}" : "a gift";
            return (d.TryGetValue("taste", out string? taste) ? taste.ToLowerInvariant() : "") switch
            {
                "hate" or "dislike" => $"{teller} told me about {gift} you gave {them} {when}. "
                                       + (them == "them" ? "They weren't happy." : $"{them} wasn't happy."),
                _ => $"{teller} told me you gave {them} {gift} {when}.",
            };
        }
        if (IsPlayer(entry.Subject) && kind.Equals(QuestHelpedKind, StringComparison.OrdinalIgnoreCase))
            return $"{teller} told me you helped {them} out {when}.";
        return from is null
            ? $"I heard about {Who(entry.Subject)} {when}."
            : $"I heard about {Who(entry.Subject)} from {from} {when}.";
    }

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

    /// <summary>The item display name from a key=value Detail ("name" key), or null when absent.</summary>
    private static string? ItemName(DiaryEntry entry)
        => DiaryDetail.Parse(entry.Detail).TryGetValue("name", out string? name) ? name : null;

    /// <summary>The "with" key of a Festival Detail: 1 when the player talked to this NPC there.</summary>
    private static bool TalkedAt(DiaryEntry entry)
        => DiaryDetail.Parse(entry.Detail).TryGetValue("with", out string? with) && with == "1";
}
