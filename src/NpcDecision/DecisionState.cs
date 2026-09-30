namespace NpcDecision;

/// <summary>
/// Builds a model state string from priority-ordered sections, cut by WHOLE LINES to a character
/// budget (docs/spec/laya.md, "Token budget"): the least important facts drop first instead of
/// whatever was written last, and no line is ever split. Pure and deterministic: no randomness,
/// no I/O.
/// </summary>
public sealed class DecisionState
{
    /// <summary>Character budget for the built state (docs/spec/laya.md: ~4 characters per English
    /// token against the smallest, 512-token checkpoint).</summary>
    public const int StateBudgetChars = 1250;

    private readonly List<Section> _sections = new();
    private int _nextSequence;

    /// <summary>
    /// Add a section. Higher priority is kept first when the budget cuts; equal priorities keep
    /// insertion order. The section may contain several lines; a blank line inside is preserved
    /// as a paragraph break, and leading/trailing blank lines are dropped.
    /// </summary>
    public void Add(int priority, string section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _sections.Add(new Section(priority, _nextSequence++, NormalizeLines(section)));
    }

    /// <summary>
    /// The state string. Rules:
    /// - Sections render sorted by priority descending (ties: insertion order), separated by one
    ///   blank line.
    /// - The result is then cut to <see cref="StateBudgetChars"/> characters, keeping whole lines
    ///   from the start: the cut drops whole lines from the end (the lowest-priority facts) and
    ///   never splits a line. A line longer than the remaining budget is dropped whole.
    /// - Deterministic for the same inputs; never exceeds the budget.
    /// </summary>
    public string Build()
    {
        // Priority descending; ties keep insertion order (an explicit sequence makes that the
        // rule, not a property of the sort).
        List<Section> ordered = _sections
            .OrderByDescending(section => section.Priority)
            .ThenBy(section => section.Sequence)
            .ToList();

        // Flatten to lines; one blank line between adjacent non-empty sections.
        var lines = new List<string>();
        foreach (Section section in ordered)
        {
            if (section.Lines.Count == 0)
            {
                continue; // An all-blank section contributes nothing, not even a separator.
            }

            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(section.Lines);
        }

        // Keep the longest whole-line prefix that fits the budget: walk from the start and stop at
        // the first line that does not fit, dropping it and every line after it whole.
        var kept = new List<string>();
        int length = 0;
        foreach (string line in lines)
        {
            int cost = (kept.Count == 0 ? 0 : 1) + line.Length; // +1 for the joining '\n'
            if (length + cost > StateBudgetChars)
            {
                break;
            }

            kept.Add(line);
            length += cost;
        }

        // The cut can leave the blank separator before a fully-dropped section as the last kept
        // line; trailing blank lines are dropped, same as in a section.
        while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[kept.Count - 1]))
        {
            kept.RemoveAt(kept.Count - 1);
        }

        return string.Join("\n", kept);
    }

    /// <summary>
    /// Splits a section into lines: CRLF normalized to LF, leading and trailing blank lines
    /// dropped, interior lines (blank ones included) kept verbatim.
    /// </summary>
    private static List<string> NormalizeLines(string section)
    {
        string[] raw = section.Replace("\r\n", "\n").Split('\n');

        int first = 0;
        int last = raw.Length - 1;
        while (first <= last && string.IsNullOrWhiteSpace(raw[first]))
        {
            first++;
        }

        while (last >= first && string.IsNullOrWhiteSpace(raw[last]))
        {
            last--;
        }

        var lines = new List<string>(Math.Max(0, last - first + 1));
        for (int i = first; i <= last; i++)
        {
            lines.Add(raw[i]);
        }

        return lines;
    }

    private sealed class Section
    {
        public Section(int priority, int sequence, List<string> lines)
        {
            Priority = priority;
            Sequence = sequence;
            Lines = lines;
        }

        public int Priority { get; }

        /// <summary>Insertion order; breaks priority ties.</summary>
        public int Sequence { get; }

        public List<string> Lines { get; }
    }
}
