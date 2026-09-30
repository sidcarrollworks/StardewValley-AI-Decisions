using NpcMemory;

namespace NpcDiaryEvents;

/// <summary>
/// Builds SawGift diary entries for the NPCs that saw the player give someone else a gift
/// (docs/spec/diary.md, "Kinds", row SawGift). Pure: no game types, no randomness, no I/O.
/// </summary>
public static class SawGiftNotes
{
    /// <summary>
    /// SawGift entry: Subject = RecipientNpc (the NPC who RECEIVED the gift; the entry lands in
    /// the observer's diary), Kind = "SawGift",
    /// Detail = DiaryDetail.Format(("giver", "Player"), ("name", DisplayName),
    /// ("taste", GiftNotes.TasteLabel(Taste))). AbsoluteTick comes from the details.
    /// A null/empty ObserverNpc, RecipientNpc or DisplayName is an ArgumentException.
    /// </summary>
    public static DiaryEntry ToDiaryEntry(SawGiftDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.ObserverNpc))
            throw new ArgumentException("ObserverNpc is required.", nameof(details));
        if (string.IsNullOrWhiteSpace(details.RecipientNpc))
            throw new ArgumentException("RecipientNpc is required.", nameof(details));
        if (string.IsNullOrWhiteSpace(details.DisplayName))
            throw new ArgumentException("DisplayName is required.", nameof(details));

        string detail = DiaryDetail.Format(
            ("giver", "Player"),
            ("name", details.DisplayName),
            ("taste", GiftNotes.TasteLabel(details.Taste)));

        return new DiaryEntry(
            details.AbsoluteTick,
            details.RecipientNpc,
            "SawGift",
            detail);
    }
}
