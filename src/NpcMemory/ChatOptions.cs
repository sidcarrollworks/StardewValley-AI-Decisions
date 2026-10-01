namespace NpcMemory;

/// <summary>Knobs for the simulated ambient chat (<see cref="MemoryStore.Chat"/>). Not saved.</summary>
public sealed class ChatOptions
{
    /// <summary>Consecutive co-located ticks before a pair may chat.</summary>
    public int ChatMinTicks { get; set; } = 3;

    /// <summary>Per-span chance that a pair chats (deterministic FNV-1a, not a coin flip).</summary>
    public double ChatChance { get; set; } = 0.3;
}
