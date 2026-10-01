namespace NpcMinds;

/// <summary>
/// The per-day answer table behind the viewer's model spread panel
/// ([docs/spec/debug-tools.md], "model spread panel"): one aggregate per (question template, NPC)
/// — a count and a mean answer. <see cref="RecordingDecisionClient"/> writes on the model workers,
/// the game thread reads a <see cref="Copy"/> for the snapshot and calls <see cref="Reset"/> at
/// the 6:00 tick; everything is guarded by one lock. Recording never changes an answer: the table
/// only observes values the inner client already returned.
/// </summary>
public sealed class SpreadTable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _sums = new(StringComparer.Ordinal);

    /// <summary>Record one answer under (template, npc). NaN or a missing NPC is dropped.</summary>
    public void Record(string template, string? npc, double value)
    {
        if (string.IsNullOrEmpty(npc) || double.IsNaN(value))
            return;
        lock (_lock)
        {
            string key = template + "|" + npc;
            _counts[key] = _counts.GetValueOrDefault(key) + 1;
            _sums[key] = _sums.GetValueOrDefault(key) + value;
        }
    }

    /// <summary>A consistent copy of the table, ordered by template then NPC name. Safe to hand
    /// to the server thread; the caller's snapshot builder computes the rows from it.</summary>
    public IReadOnlyList<SpreadEntry> Copy()
    {
        lock (_lock)
        {
            return _counts
                .Select(kv => new SpreadEntry(TemplateOf(kv.Key), NpcOf(kv.Key), kv.Value,
                    _sums[kv.Key] / kv.Value))
                .OrderBy(e => e.Template, StringComparer.Ordinal)
                .ThenBy(e => e.Npc, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>Drops the day's table (the 6:00 tick).</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _counts.Clear();
            _sums.Clear();
        }
    }

    private static string TemplateOf(string key) => key[..key.IndexOf('|')];
    private static string NpcOf(string key) => key[(key.IndexOf('|') + 1)..];

    /// <summary>The question with the NPC's name replaced by <c>&lt;npc&gt;</c>, so the same
    /// question groups across villagers. Uses the same word-boundary replacement as the call
    /// log's name detection; the first occurrence of the name in the question wins. Returns the
    /// question unchanged when the name is not there.</summary>
    public static string ReplaceNpc(string question, string? npc)
    {
        if (string.IsNullOrEmpty(npc))
            return question;
        var regex = new System.Text.RegularExpressions.Regex(
            @"\b" + System.Text.RegularExpressions.Regex.Escape(npc) + @"\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return regex.Replace(question, "<npc>", 1);
    }
}

/// <summary>One (template, NPC) aggregate: how many answers and their mean.</summary>
public sealed record SpreadEntry(string Template, string Npc, int Count, double Mean);

/// <summary>Tuning for the spread panel (code defaults, never saved;
/// docs/spec/debug-tools.md, "Tuning constants").</summary>
public sealed class SpreadOptions
{
    public double FlatSpread { get; init; } = 0.05;
    public int MinNpcsForSpread { get; init; } = 6;
    public double FollowsTraitMin { get; init; } = 0.3;
}
