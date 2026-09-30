using NpcMemory;

namespace NpcDiaryEvents;

/// <summary>
/// Builds QuestHelped diary entries for the NPC a completed quest was for (docs/spec/diary.md,
/// "Kinds", row QuestHelped). Pure: no game types, no randomness, no I/O.
/// </summary>
public static class QuestNotes
{
    /// <summary>Valid QuestLabel values (the mod maps game quest types to these).</summary>
    public const string ItemDelivery = "ItemDelivery";
    public const string Fishing = "Fishing";
    public const string SlayMonster = "SlayMonster";
    public const string ResourceCollection = "ResourceCollection";
    public const string LostItem = "LostItem";
    public const string Special = "Special";

    /// <summary>
    /// QuestHelped entry: Subject = Player, Kind = "QuestHelped",
    /// Detail = DiaryDetail.Format(("quest", QuestLabel), ("name", TargetNpc)).
    /// AbsoluteTick comes from the details.
    /// A null/empty TargetNpc is an ArgumentException; an empty QuestLabel is allowed and
    /// stored as-is (the mod always passes one of the constants above).
    /// </summary>
    public static DiaryEntry ToDiaryEntry(QuestDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.TargetNpc))
            throw new ArgumentException("TargetNpc is required.", nameof(details));

        return new DiaryEntry(
            details.AbsoluteTick,
            "Player",
            "QuestHelped",
            DiaryDetail.Format(("quest", details.QuestLabel ?? string.Empty), ("name", details.TargetNpc)));
    }
}
