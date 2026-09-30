using NpcMemory;

namespace NpcIntents;

/// <summary>
/// Plain-words sentences for the model's state and options (week review, finding 5): the model
/// sees natural news like "yesterday the player gave Haley a Sunflower (a loved gift)" instead of
/// the telegraphic "GiftReceived Player (taste=Love;name=Sunflower)". The phrasing mirrors the
/// eval set's wording (sidecar/eval/run_eval.py), which is what the checkpoint A/B was actually
/// measured on. Pure: no game types, no randomness, no I/O.
/// </summary>
public static class NewsPhrasing
{
    /// <summary>One plain sentence for the model: what happened, phrased for the NPC's diary
    /// entry. Unknown kinds fall back to the telegraphic summary.</summary>
    public static string Sentence(string npc, DiaryEntry entry, int daysAgo)
    {
        if (entry is null)
            return string.Empty;

        string when = When(daysAgo);
        string kind = entry.Kind ?? string.Empty;

        if (kind.Equals("Saw", StringComparison.OrdinalIgnoreCase))
        {
            string place = PlaceNames.Display(entry.Detail);
            return IsPlayer(entry.Subject)
                ? $"{when} {npc} saw the player at {place}"
                : $"{when} {npc} saw {entry.Subject} at {place}";
        }

        if (kind.Equals("GiftReceived", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(entry.Detail);
            string adjective = detail.TryGetValue("taste", out string? taste)
                ? TasteAdjective(taste)
                : "fine";
            string birthday = detail.TryGetValue("birthday", out string? b) && b is "1"
                ? $", on {npc}'s birthday"
                : string.Empty;
            return detail.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name)
                ? $"{when} the player gave {npc} a {name} (a {adjective} gift{birthday})"
                : $"{when} the player gave {npc} a {adjective} gift{birthday}";
        }

        if (kind.Equals("SawGift", StringComparison.OrdinalIgnoreCase))
            return $"{when} {npc} saw the player give {entry.Subject} a gift";

        if (kind.Equals("QuestHelped", StringComparison.OrdinalIgnoreCase))
            return $"{when} the player completed the request for {npc}";

        if (kind.Equals("Festival", StringComparison.OrdinalIgnoreCase))
        {
            bool talked = DiaryDetail.Parse(entry.Detail).TryGetValue("with", out string? with) && with is "1";
            return talked
                ? $"{when} {npc} went to the festival with the player"
                : $"{when} {npc} was at the festival too";
        }

        if (kind.Equals("MissedFestival", StringComparison.OrdinalIgnoreCase))
            return $"{when} the player skipped the festival";

        if (kind.Equals("Talked", StringComparison.OrdinalIgnoreCase))
            return $"{when} {npc} talked with the player";

        if (kind.Equals("IgnoredBy", StringComparison.OrdinalIgnoreCase))
            return $"{when} {npc} tried to get the player's attention ({StepWords(entry.Detail)}) and got none";

        if (kind.Equals("PassedBy", StringComparison.OrdinalIgnoreCase))
            return $"{when} the player passed {npc} by without stopping";

        if (kind.Equals("BirthdayForgotten", StringComparison.OrdinalIgnoreCase))
            return $"{when} was {npc}'s birthday and the player forgot";

        return Summarize(entry);
    }

    /// <summary>The telegraphic option label: "Saw Player at Pierre's General Store" (a "Saw"
    /// detail is a place) or "IgnoredBy Player (Emote)" (any other detail is not).</summary>
    public static string Summarize(DiaryEntry entry)
    {
        string summary = $"{entry.Kind} {entry.Subject}".Trim();
        if (string.IsNullOrWhiteSpace(entry.Detail))
            return summary;
        return string.Equals(entry.Kind, "Saw", StringComparison.OrdinalIgnoreCase)
            ? $"{summary} at {PlaceNames.Display(entry.Detail)}"
            : $"{summary} ({entry.Detail})";
    }

    private static string When(int daysAgo) => daysAgo switch
    {
        1 => "yesterday",
        2 => "the day before yesterday",
        _ => $"{daysAgo} days ago",
    };

    private static string TasteAdjective(string? taste) => (taste ?? string.Empty).ToLowerInvariant() switch
    {
        "love" => "loved",
        "like" => "liked",
        "dislike" => "disliked",
        "hate" => "hated",
        _ => "fine", // Neutral and anything unrecognised
    };

    /// <summary>"Emote" -> "an emote", "Bubble" -> "a speech bubble", else the word as-is.</summary>
    private static string StepWords(string? step) => (step ?? string.Empty).ToLowerInvariant() switch
    {
        "emote" => "an emote",
        "bubble" => "a speech bubble",
        "" => "a small gesture",
        var other => other,
    };

    private static bool IsPlayer(string? subject)
        => string.Equals(subject, MemoryStore.PlayerName, StringComparison.OrdinalIgnoreCase);
}
