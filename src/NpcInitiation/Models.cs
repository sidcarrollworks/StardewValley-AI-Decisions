using NpcMemory;

namespace NpcInitiation;

/// <summary>
/// What one NPC brings to a tick. <see cref="PlayerView"/> is the NPC's OWN ledger view of
/// "Player" (null = never seen or heard of). <see cref="Lead"/> is the NPC's best answer to
/// "where is the player?" (<see cref="MemoryStore.LookFor"/>: its own sighting, a neighbour's tip,
/// or its habit knowledge). Both come from memory only; nothing that decides sees a live position.
/// </summary>
public sealed record InitiationInput(
    string Npc,
    LedgerView? PlayerView,
    bool HasPendingIntent,   // the overnight planner gave this NPC a line for today
    int Hearts,              // 0..14, friendship with the player
    Whereabouts? Lead = null);
