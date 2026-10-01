using NpcMemory;

namespace NpcInitiation;

/// <summary>
/// Decides who asks around about the player, and when (brief goal 4: "asking neighbors when
/// someone is missing"). An NPC asks the NPCs it is with right now when it wants the player
/// (urge at or above <see cref="SearchOptions.AskUrge"/>) and has no first-hand sighting from the
/// last <see cref="SearchOptions.FreshTicks"/> ticks. Answers go through the ledger's gossip rules
/// (<see cref="MemoryStore.AskAround"/>). Runs on the game thread: memory only, no model calls,
/// deterministic. Ask cooldowns are kept in memory, not saved (a reload just lets NPCs ask again).
/// </summary>
public sealed class PlayerSearch
{
    private readonly SearchOptions _options;
    private readonly Dictionary<string, int> _lastAsked = new(StringComparer.OrdinalIgnoreCase);

    public PlayerSearch(SearchOptions? options = null) => _options = options ?? new SearchOptions();

    /// <summary>
    /// Let every NPC that misses the player ask the NPCs around it. Returns one event per NPC that
    /// learned something, with what it now knows. <paramref name="urges"/> is the ladder's latest
    /// snapshot (<see cref="BackgroundLadder.LatestUrges"/>).
    /// </summary>
    public IReadOnlyList<SearchEvent> Tick(MemoryStore memory, int nowTick, IReadOnlyDictionary<string, double> urges)
    {
        var events = new List<SearchEvent>();
        foreach ((string npc, double urge) in urges.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (urge < _options.AskUrge || !Misses(memory.Ledger.View(npc, MemoryStore.PlayerName, nowTick)))
                continue;
            if (_lastAsked.TryGetValue(npc, out int last) && nowTick - last < _options.AskCooldownTicks)
                continue;

            AskResult result = memory.AskAround(npc, MemoryStore.PlayerName, nowTick);
            if (result.Asked.Count == 0)
                continue; // nobody around to ask; try again as soon as someone is
            _lastAsked[npc] = nowTick;

            if (result.Told.Count > 0 && memory.Ledger.View(npc, MemoryStore.PlayerName, nowTick) is { } learned)
                events.Add(new SearchEvent(nowTick, npc, result.Asked, learned, result.Told));
        }
        return events;
    }

    /// <summary>No first-hand sighting in the last <see cref="SearchOptions.FreshTicks"/> ticks
    /// (never seen, only heard about, forgotten overnight, or seen too long ago).</summary>
    private bool Misses(LedgerView? view)
        => view is null || view.HopCount > 0 || view.Detail == LedgerDetail.Gone || view.AgeTicks >= _options.FreshTicks;
}

/// <summary>One NPC asked around about the player and learned something (<see cref="Learned"/> is
/// its ledger view afterwards; <see cref="LedgerView.ToldBy"/> says who told it).
/// <see cref="Told"/> names the neighbours whose answer was kept.</summary>
public sealed record SearchEvent(int AbsoluteTick, string Seeker, IReadOnlyList<string> Asked, LedgerView Learned,
    IReadOnlyList<string>? Told = null); // at the end with a default (records grow at the end)

/// <summary>Tuning for <see cref="PlayerSearch"/>. Not saved.</summary>
public sealed class SearchOptions
{
    /// <summary>Only NPCs whose urge is at least this ask around (the ladder's Bubble threshold,
    /// so they have a lead by the time they are keen enough to go looking at 0.60).</summary>
    public double AskUrge { get; set; } = 0.45;

    /// <summary>A first-hand sighting younger than this many ticks means the NPC doesn't need to ask.</summary>
    public int FreshTicks { get; set; } = 6;

    /// <summary>Minimum ticks between two rounds of asking by the same NPC.</summary>
    public int AskCooldownTicks { get; set; } = 6;
}
