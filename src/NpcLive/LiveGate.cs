using NpcMemory;
using NpcMotives;

namespace NpcLive;

/// <summary>
/// What the game looks like at the moment a live act would be shown, gathered by the mod on the
/// game thread just before it calls the game. These are live facts, read only to say "not now"
/// (AGENTS.md rule 2's one exception, D30): the decision itself was made from memory. Positions are
/// tiles.
/// </summary>
public sealed record LiveFacts(
    bool SinglePlayer,      // Context.IsMultiplayer is false
    bool PlayerFree,        // the player can move: no menu, no dialogue, not mid-animation
    bool EventUp,           // a cutscene or event is running
    bool Festival,          // a festival is on in the player's location
    bool SameLocation,      // the villager is in the player's location
    int DistanceTiles,      // Chebyshev distance from the villager to the player
    bool NpcBusy,           // already emoting, or a bubble already above its head
    bool NpcVisible);       // drawn on the map (not hidden, not in a warp)

/// <summary>
/// The last check before a live act is shown (rollout.md, "Safety invariants"): never during
/// events, festivals, menus or while the player can't move; only in single-player; only when the
/// villager is with the player and close enough to be seen; never on top of an emote or a bubble
/// already showing; and never late. Pure.
/// </summary>
public static class LiveGate
{
    /// <summary>Null when the act can be shown now, else why not.</summary>
    /// <param name="lastShownTick">When this villager last showed a live act
    /// (<see cref="LiveLedger.LastShownTick"/>), or null.</param>
    public static string? WhyNot(LiveAct act, LiveFacts f, int nowTick, LiveOptions? options = null, int? lastShownTick = null)
    {
        LiveOptions o = options ?? new LiveOptions();
        if (!f.SinglePlayer)
            return "live acts are single-player only";
        // A tick from the future (another save loaded in the same session) holds nothing back.
        if (lastShownTick is { } shown && nowTick - shown is >= 0 and var gap && gap < o.MinTicksBetweenActs)
            return $"already showed an act {nowTick - shown} tick(s) ago";
        if (nowTick - act.DecidedTick > o.MaxDelayTicks)
            return $"decided {nowTick - act.DecidedTick} ticks ago; the moment has passed";
        if (f.EventUp)
            return "an event is running";
        if (f.Festival)
            return "a festival is on";
        if (!f.PlayerFree)
            return "the player is busy";
        if (!f.SameLocation)
            return "the player left";
        int max = act.Act == Act.Emote ? o.EmoteMaxTiles : o.BubbleMaxTiles;
        if (f.DistanceTiles > max)
            return $"too far ({f.DistanceTiles} tiles, at most {max})";
        if (!f.NpcVisible)
            return "not visible";
        if (f.NpcBusy)
            return "already emoting or speaking";
        return null;
    }
}

/// <summary>
/// A circuit breaker per live switch (rollout.md): if showing an act throws, that one switch turns
/// off for the session and the error is reported once, so a bug can't repeat every tick. The
/// console command <c>npcmod_live off</c> trips them all. Game thread only.
/// </summary>
public sealed class LiveBreaker
{
    private readonly HashSet<Act> _tripped = new();
    private bool _allOff;

    public bool IsOpen(Act act) => !_allOff && !_tripped.Contains(act);

    public bool AllOff => _allOff;

    public IReadOnlyCollection<Act> Tripped => _tripped;

    /// <summary>Runs <paramref name="show"/> unless the act's switch is tripped. Null when it ran;
    /// else the reason it didn't (tripped before, or the exception that tripped it now).</summary>
    public string? Run(Act act, Action show)
    {
        if (!IsOpen(act))
            return _allOff ? "live acts are off (npcmod_live off)" : $"{act} is off after an error";
        try
        {
            show();
            return null;
        }
        catch (Exception ex)
        {
            _tripped.Add(act);
            return $"{act} turned off for this session after an error: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary><c>npcmod_live off</c>: every live act stops for the session.</summary>
    public void TripAll() => _allOff = true;
}

/// <summary>
/// The live acts shown and not yet answered, so an ignored one leaves an <c>IgnoredBy</c> diary
/// entry (ladder.md: in shadow the player never saw the attempt, so nothing is written; once it was
/// really shown, being ignored is real). One per villager, as the runner holds one in-person
/// attempt at a time. Game thread only; not saved (an attempt shown before a reload is forgotten).
/// </summary>
public sealed class LiveLedger
{
    private readonly Dictionary<string, (Act Act, int Tick)> _shown = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lastShown = new(StringComparer.OrdinalIgnoreCase);

    public void Shown(LiveAct a, int nowTick)
    {
        _shown[a.Npc] = (a.Act, a.DecidedTick);
        _lastShown[a.Npc] = nowTick;
    }

    /// <summary>When the villager last showed a live act, or null (the gate's spacing check).</summary>
    public int? LastShownTick(string npc) => _lastShown.TryGetValue(npc, out int t) ? t : null;

    public int Count => _shown.Count;

    /// <summary>
    /// Feeds one runner event. A <c>Responded</c> to a shown act clears it; an <c>Ignored</c> one
    /// clears it and returns the diary entry to write with <c>MemoryStore.Note</c>. Events about
    /// acts that were never shown return null.
    /// </summary>
    public DiaryEntry? OnResolved(MotiveEvent ev)
    {
        if (ev.Kind is not ("Ignored" or "Responded") || ev.Act is not { } act)
            return null;
        if (!_shown.TryGetValue(ev.Npc, out var shown) || shown.Act != act || shown.Tick > ev.AbsoluteTick)
            return null;
        _shown.Remove(ev.Npc);
        return ev.Kind == "Ignored"
            ? new DiaryEntry(ev.AbsoluteTick, MemoryStore.PlayerName, "IgnoredBy", act.ToString())
            : null;
    }
}
