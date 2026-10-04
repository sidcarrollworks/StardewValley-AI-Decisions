namespace NpcMemory;

/// <summary>Knobs for the simulated ambient chat (<see cref="MemoryStore.Chat"/>) and gossip
/// juiciness (<see cref="Gossip"/>, docs/spec/ledger-gossip.md). Not saved.</summary>
public sealed class ChatOptions
{
    /// <summary>Consecutive co-located ticks before a pair may chat.</summary>
    public int ChatMinTicks { get; set; } = 3;

    /// <summary>Per-span chance that a pair chats (deterministic FNV-1a, not a coin flip).</summary>
    public double ChatChance { get; set; } = 0.3;

    /// <summary>A story is volunteered only at this juiciness or more for the listener; below it,
    /// it is told only when asked (<see cref="MemoryStore.AskAround"/>).</summary>
    public double VolunteerLevel { get; set; } = 2;

    /// <summary>Added for a listener who knows someone in the story (regard or a ledger entry).</summary>
    public double KnowsSomeoneBonus { get; set; } = 0.5;

    /// <summary>Each retelling passes the story on at this share of the teller's juiciness.</summary>
    public double RetellFactor { get; set; } = 0.7;

    /// <summary>Juiciness lost per whole day since the teller got the story.</summary>
    public double FadePerDay { get; set; } = 0.5;

    /// <summary>The fade for scandals (base juiciness at <see cref="ScandalBase"/> or more).</summary>
    public double ScandalFadePerDay { get; set; } = 0.8;

    public double ScandalBase { get; set; } = 4;

    /// <summary>A teller tells the same story to at most this many listeners a day.</summary>
    public int RetellsPerDay { get; set; } = 3;
}
