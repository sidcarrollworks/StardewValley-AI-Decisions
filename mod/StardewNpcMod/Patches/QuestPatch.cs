using HarmonyLib;
using NpcDiaryEvents;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace StardewNpcMod.Patches;

/// <summary>
/// Read-only postfix on <c>Quest.questComplete()</c> (1.6.15, Quests/Quest.cs:581) — the one
/// place every quest completes (docs/spec/diary.md, "Triggers and game hooks"). It resolves the
/// NPC the quest was for (target or npcName by type; SocializeQuest has none and is skipped) and
/// queues a QuestHelped record. An identity set makes each quest record once, even if another mod
/// calls questComplete again. Never changes arguments or results.
/// </summary>
public static class QuestPatch
{
    internal static DiaryEventQueue Queue = null!;
    internal static IMonitor Log = null!;

    private static readonly HashSet<Quest> Recorded = new();

    public static void Postfix(Quest __instance)
    {
        try
        {
            if (__instance is null || !Recorded.Add(__instance))
                return;

            (string? npc, string? label) = __instance switch
            {
                ItemDeliveryQuest q => (q.target.Value, QuestNotes.ItemDelivery),
                SlayMonsterQuest q => (q.target.Value, QuestNotes.SlayMonster),
                FishingQuest q => (q.target.Value, QuestNotes.Fishing),
                ResourceCollectionQuest q => (q.target.Value, QuestNotes.ResourceCollection),
                LostItemQuest q => (q.npcName.Value, QuestNotes.LostItem),
                SecretLostItemQuest q => (q.npcName.Value, QuestNotes.LostItem),
                _ => (null, null), // SocializeQuest and anything else: no single target
            };

            // The game uses the literal string "null" for "no target" (e.g. Marlon's mine
            // initiation SlayMonsterQuest); only a known villager gets a diary entry.
            if (string.IsNullOrWhiteSpace(npc) || label is null
                || string.Equals(npc, "null", StringComparison.OrdinalIgnoreCase)
                || Game1.characterData is null || !Game1.characterData.ContainsKey(npc))
                return;
            Queue.EnqueueQuest(new QuestDetails(npc, label, AbsoluteTick: 0)); // stamped at drain time
        }
        catch (Exception ex)
        {
            Log.Log($"StardewNpcMod: Failed in Quest.questComplete postfix: {ex}", LogLevel.Error);
        }
    }

    internal static void Reset() => Recorded.Clear();
}
