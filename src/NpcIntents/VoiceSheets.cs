namespace NpcIntents;

/// <summary>
/// Short per-NPC voice anchors used to keep distinct personalities from blurring when one model
/// writes every NPC (a risk called out in the brief). A few entries to start; the fallback is a
/// neutral friendly voice. Editable data, not logic.
/// </summary>
public static class VoiceSheets
{
    private static readonly Dictionary<string, string> Sheets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Abigail"] = "spirited and playful, a little morbid",
        ["Sebastian"] = "reserved and dry, nocturnal",
        ["Sam"] = "upbeat and energetic",
        ["Leah"] = "warm and artistic",
        ["Elliott"] = "flowery and theatrical",
        ["Penny"] = "gentle and bookish",
        ["Maru"] = "bright and scientific",
        ["Alex"] = "confident and sporty",
        ["Haley"] = "breezy and a bit vain",
        ["Emily"] = "whimsical and spiritual",
        ["Harvey"] = "anxious and conscientious",
        ["Shane"] = "gruff and guarded",
        ["Linus"] = "humble and at peace outdoors",
        ["Robin"] = "practical and warm",
        ["Pierre"] = "businesslike and chatty",
        ["Gus"] = "generous and easygoing",
        ["Clint"] = "shy and self-deprecating",
        ["Willy"] = "gruff and sea-worn",
    };

    public static string Voice(string npc)
        => Sheets.TryGetValue(npc, out string? voice) ? voice : "friendly and plain-spoken";
}
