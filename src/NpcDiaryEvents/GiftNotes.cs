using NpcMemory;

namespace NpcDiaryEvents;

/// <summary>
/// Builds GiftReceived diary entries from what the gift postfix captured (docs/spec/diary.md,
/// "Kinds", row GiftReceived). Pure: no game types, no randomness, no I/O.
/// </summary>
public static class GiftNotes
{
    /// <summary>
    /// The game's gift_taste_* value to the Detail label. Contract (verified against NPC.cs in
    /// the 1.6.15 decompile): 0 -> "Love", 2 -> "Like", 4 -> "Dislike", 6 -> "Hate",
    /// 7 -> "Love" (Stardrop Tea is universally loved), anything else -> "Neutral".
    /// </summary>
    public static string TasteLabel(int taste)
    {
        return taste switch
        {
            0 => "Love",
            2 => "Like",
            4 => "Dislike",
            6 => "Hate",
            7 => "Love",
            _ => "Neutral",
        };
    }

    /// <summary>
    /// GiftReceived entry: Subject = Player, Kind = "GiftReceived",
    /// Detail = DiaryDetail.Format(("item", QualifiedItemId), ("name", DisplayName),
    /// ("taste", TasteLabel(Taste)), ("birthday", IsBirthday ? "1" : "0")) plus
    /// ("festival", "WinterStar") when IsWinterStar. AbsoluteTick comes from the details.
    /// A null/empty RecipientNpc, QualifiedItemId or DisplayName is an ArgumentException.
    /// </summary>
    public static DiaryEntry ToDiaryEntry(GiftDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.RecipientNpc))
            throw new ArgumentException("RecipientNpc is required.", nameof(details));
        if (string.IsNullOrWhiteSpace(details.QualifiedItemId))
            throw new ArgumentException("QualifiedItemId is required.", nameof(details));
        if (string.IsNullOrWhiteSpace(details.DisplayName))
            throw new ArgumentException("DisplayName is required.", nameof(details));

        var pairs = new List<(string Key, string Value)>
        {
            ("item", details.QualifiedItemId),
            ("name", details.DisplayName),
            ("taste", TasteLabel(details.Taste)),
            ("birthday", details.IsBirthday ? "1" : "0"),
        };
        if (details.IsWinterStar)
            pairs.Add(("festival", "WinterStar"));

        return new DiaryEntry(
            details.AbsoluteTick,
            "Player",
            "GiftReceived",
            DiaryDetail.Format(pairs.ToArray()));
    }
}
