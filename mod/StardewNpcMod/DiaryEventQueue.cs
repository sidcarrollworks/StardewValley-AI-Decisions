using NpcDiaryEvents;
using NpcMemory;
using StardewModdingAPI;

namespace StardewNpcMod;

/// <summary>
/// The game-thread queue between the read-only Harmony postfixes and memory
/// (docs/spec/diary.md, "Harmony": a postfix records, the tick applies). The next game tick drains
/// it through the producers into <see cref="MemoryStore"/>, so there is one writer and one order.
/// The SawGift witnesses are chosen at drain time from the span tracker's last Observe
/// (<see cref="MemoryStore.CoLocatedWithPlayerNow"/>), never from live positions.
/// </summary>
public sealed class DiaryEventQueue
{
    private readonly List<GiftDetails> _gifts = new();
    private readonly List<QuestDetails> _quests = new();

    public void EnqueueGift(GiftDetails details) => _gifts.Add(details);

    public void EnqueueQuest(QuestDetails details) => _quests.Add(details);

    public void Clear()
    {
        _gifts.Clear();
        _quests.Clear();
    }

    /// <summary>Writes everything queued so far into the store and logs it at Trace. Every entry
    /// gets <paramref name="absoluteTick"/> — the tick that applies the record.</summary>
    public void Drain(MemoryStore store, int absoluteTick, IMonitor monitor)
    {
        foreach (GiftDetails gift in _gifts)
        {
            GiftDetails stamped = gift with { AbsoluteTick = absoluteTick };
            store.Note(stamped.RecipientNpc, GiftNotes.ToDiaryEntry(stamped));
            monitor.Log($"[shadow] diary {stamped.RecipientNpc}: GiftReceived Player ({stamped.DisplayName})", LogLevel.Trace);

            foreach (string witness in store.CoLocatedWithPlayerNow()
                         .Where(w => !string.Equals(w, stamped.RecipientNpc, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(w => w, StringComparer.OrdinalIgnoreCase))
            {
                var saw = new SawGiftDetails(witness, stamped.RecipientNpc, stamped.DisplayName, stamped.Taste, absoluteTick);
                store.Note(witness, SawGiftNotes.ToDiaryEntry(saw));
                monitor.Log($"[shadow] diary {witness}: SawGift {stamped.RecipientNpc} ({stamped.DisplayName})", LogLevel.Trace);
            }
        }

        foreach (QuestDetails quest in _quests)
        {
            QuestDetails stamped = quest with { AbsoluteTick = absoluteTick };
            store.Note(stamped.TargetNpc, QuestNotes.ToDiaryEntry(stamped));
            monitor.Log($"[shadow] diary {stamped.TargetNpc}: QuestHelped Player ({stamped.QuestLabel})", LogLevel.Trace);
        }

        _gifts.Clear();
        _quests.Clear();
    }
}
