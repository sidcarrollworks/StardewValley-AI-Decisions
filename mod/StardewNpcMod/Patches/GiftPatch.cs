using HarmonyLib;
using NpcDiaryEvents;
using StardewModdingAPI;
using StardewValley;

namespace StardewNpcMod.Patches;

/// <summary>
/// Read-only postfix on <c>NPC.receiveGift(Object, Farmer, bool, float, bool)</c>
/// (1.6.15, NPC.cs:4899) — the one place a gift from the player lands (docs/spec/diary.md,
/// "Triggers and game hooks"). It only reads: the recipient, the item's qualified ID and display
/// name, the taste and the birthday, and queues a GiftReceived record. SawGift witnesses are
/// chosen at drain time by <see cref="DiaryEventQueue"/>. Never changes arguments or results.
/// </summary>
public static class GiftPatch
{
    internal static DiaryEventQueue Queue = null!;
    internal static IMonitor Log = null!;

    public static void Postfix(NPC __instance, StardewValley.Object o, Farmer giver, bool updateGiftLimitInfo)
    {
        try
        {
            if (!Context.IsMainPlayer)
                return; // host's main screen only (HostOnly): no queue to drain elsewhere
            if (__instance is null || o is null || giver is null || !giver.IsLocalPlayer || !__instance.CanReceiveGifts())
                return;
            // Stardrop Tea also passes updateGiftLimitInfo: false (1.6.15, NPC.cs:2403); only the
            // Winter Star secret gift is a festival gift.
            bool isWinterStar = !updateGiftLimitInfo
                && !string.Equals(o.QualifiedItemId, "(O)StardropTea", StringComparison.OrdinalIgnoreCase);
            Queue.EnqueueGift(new GiftDetails(
                __instance.Name,
                o.QualifiedItemId,
                o.DisplayName,
                __instance.getGiftTasteForThisItem(o),
                __instance.isBirthday(),
                isWinterStar,
                AbsoluteTick: 0)); // stamped at drain time
        }
        catch (Exception ex)
        {
            Log.Log($"StardewNpcMod: Failed in NPC.receiveGift postfix: {ex}", LogLevel.Error);
        }
    }
}
