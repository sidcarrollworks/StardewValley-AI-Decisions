namespace NpcDiaryEvents;

/// <summary>
/// Plain-input records the mod's game-side hooks hand to the producers (docs/spec/diary.md,
/// "Triggers and game hooks"). Nothing here references the game: the Harmony postfixes in
/// mod/StardewNpcMod/Patches/ translate game objects into these, and the producers turn them
/// into <see cref="NpcMemory.DiaryEntry"/> values. All strings are used verbatim except where a
/// producer documents otherwise.
/// </summary>

/// <summary>
/// A gift the player gave an NPC (captured by a read-only postfix on NPC.receiveGift).
/// <paramref name="Taste"/> is the game's gift_taste_* value (NPC.gift_taste_*: love 0, like 2,
/// dislike 4, hate 6, Stardrop Tea 7, neutral 8); the producer maps it to a label.
/// <paramref name="IsWinterStar"/> is true for the Winter Star secret gift
/// (updateGiftLimitInfo == false on receiveGift).
/// </summary>
public sealed record GiftDetails(
    string RecipientNpc,
    string QualifiedItemId,
    string DisplayName,
    int Taste,
    bool IsBirthday,
    bool IsWinterStar,
    int AbsoluteTick);

/// <summary>
/// A gift the player gave someone else, witnessed by an NPC that was co-located with the player
/// at the most recent Observe (the span tracker, never a live position).
/// </summary>
public sealed record SawGiftDetails(
    string ObserverNpc,
    string RecipientNpc,
    string DisplayName,
    int Taste,
    int AbsoluteTick);

/// <summary>
/// A quest the player completed whose target is <paramref name="TargetNpc"/>. The mod maps game
/// quest types to <paramref name="QuestLabel"/>; SocializeQuest is never passed (skipped).
/// </summary>
public sealed record QuestDetails(
    string TargetNpc,
    string QuestLabel,
    int AbsoluteTick);
