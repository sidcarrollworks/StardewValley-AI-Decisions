namespace UnderGlass.Sim.Lives;

/// <summary>
/// Immutable decision-time view using the personal-memory kernel's production recall, feelings,
/// and expectation queries. No acquisition receipts, mutation methods, models or observer sink.
/// Query time must remain in the captured day and cannot precede its latest known evidence.
/// </summary>
public sealed class MemoryFrame
{
    private readonly PersonalMemory _memory;

    internal MemoryFrame(PersonalMemory memory, IEnumerable<string>? removedHistory = null)
    {
        _memory = memory.CopyForFrame(removedHistory ?? Array.Empty<string>(), out var removed);
        RemovedIds = removed;
        HistoryIds = _memory.FrameHistoryIds;
    }

    public string Holder => _memory.Holder;

    /// <summary>Original own fact, account and reading IDs eligible for removal, including any
    /// already removed IDs. Source IDs and other holders' private account/reading IDs are excluded.</summary>
    public IReadOnlyList<string> HistoryIds { get; }

    /// <summary>Cumulative removed IDs, including dependent accounts and readings, in ordinal order.</summary>
    public IReadOnlyList<string> RemovedIds { get; }

    public MemorySnapshot Snapshot() => _memory.Snapshot();
    public RecallSnapshot Recall(IEnumerable<string> cues, int at, bool lowMood = false) => _memory.Recall(cues, at, lowMood);
    public MemorySides Sides(IEnumerable<MemoryObject> objects) => _memory.Sides(objects);
    public ExpectationSnapshot Expectation(ExpectationKey key) => _memory.Expectation(key);

    /// <summary>
    /// Remove only the requested owned history and its dependencies in a new frame. A fact removes
    /// its own accounts and readings; an account removes its readings; a reading removes only itself
    /// and its numeric seed. Independent lived evidence remains. Repeated removal is idempotent;
    /// unknown or other-holder IDs fail atomically. The live memory and source frame never change.
    /// </summary>
    public MemoryFrame WithoutHistory(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return new(_memory, RemovedIds.Concat(ids));
    }
}
