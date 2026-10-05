using System.Text.Json;

namespace NpcMemory;

/// <summary>
/// Per-NPC event log (the "diary" from the project brief). The game keeps only counters and
/// flags; this is the mod's record of what the NPC witnessed and did. Game-independent,
/// deterministic, JSON-serializable.
/// </summary>
public sealed class Diary
{
    /// <summary>
    /// Shared options for both directions of serialization. Nulls are written (the default),
    /// so a null <see cref="DiaryEntry.Detail"/> survives a round trip as null.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly List<DiaryEntry> _entries = new();

    /// <summary>All entries, oldest first.</summary>
    public IReadOnlyList<DiaryEntry> Entries => _entries;

    /// <summary>Append an entry. Entries may be appended in any tick order (they are kept in insertion order).</summary>
    public void Append(DiaryEntry entry) => _entries.Add(entry);

    /// <summary>Entries at or after the given absolute tick, oldest first.</summary>
    public IEnumerable<DiaryEntry> Since(int absoluteTick)
        => _entries.Where(e => e.AbsoluteTick >= absoluteTick).ToList();

    /// <summary>The most recent `count` entries, newest first.</summary>
    public IEnumerable<DiaryEntry> Recent(int count)
    {
        if (count <= 0)
            return Array.Empty<DiaryEntry>();

        // Enumerable.Reverse, not List<T>.Reverse (the latter mutates and returns void).
        return Enumerable.Reverse(_entries).Take(count).ToList();
    }

    /// <summary>Entries about a subject (case-insensitive), oldest first.</summary>
    public IEnumerable<DiaryEntry> About(string subject)
        => _entries.Where(e => string.Equals(e.Subject, subject, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Keep only the newest <paramref name="max"/> entries (by insertion order), so a
    /// long save does not grow the diary without bound.</summary>
    public void TrimTo(int max)
    {
        int excess = _entries.Count - Math.Max(0, max);
        if (excess > 0)
        {
            _entries.RemoveRange(0, excess);
            TrimmedToday += excess;
        }
    }

    /// <summary>Keep only <paramref name="keep"/> entries, chosen by <see cref="DiaryKeep"/>: the
    /// least worth remembering go first.</summary>
    public void TrimKeeping(int keep, Func<string, double> regardFor, DiaryKeepOptions options)
    {
        int before = _entries.Count;
        if (before <= Math.Max(0, keep))
            return;
        List<DiaryEntry> kept = DiaryKeep.Keep(_entries, keep, regardFor, options);
        _entries.Clear();
        _entries.AddRange(kept);
        TrimmedToday += before - kept.Count;
    }

    /// <summary>Entries dropped by <see cref="TrimTo"/> or <see cref="TrimKeeping"/> today (the playtest log reads it once a
    /// day at 6:00).</summary>
    public int TrimmedToday { get; private set; }

    /// <summary>Read and reset the day's trim count (the 6:00 memory census uses this).</summary>
    public int TakeTrimmedToday()
    {
        int trimmed = TrimmedToday;
        TrimmedToday = 0;
        return trimmed;
    }

    /// <summary>Rewrite every entry's tick (save migration, e.g. adding the year to old ticks).</summary>
    public void RemapTicks(Func<int, int> remap)
    {
        for (int i = 0; i < _entries.Count; i++)
            _entries[i] = _entries[i] with { AbsoluteTick = remap(_entries[i].AbsoluteTick) };
    }

    public string ToJson() => JsonSerializer.Serialize(_entries, JsonOptions);

    public static Diary FromJson(string json)
    {
        var entries = JsonSerializer.Deserialize<List<DiaryEntry>>(json, JsonOptions)
                      ?? new List<DiaryEntry>();
        var diary = new Diary();
        foreach (var entry in entries)
            diary.Append(entry);
        return diary;
    }
}
