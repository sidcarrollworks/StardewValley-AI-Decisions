using System.Text.Json;
using NpcMemory;

namespace NpcIntents;
/// <summary>
/// Save-file shapes for the overnight plan and delivered lines (docs/spec/persistence.md, keys
/// `intents` and `recentLines`). Additive keys: no version bump. Pure JSON helpers.
/// </summary>
public static class PlanPersistence
{
    /// <summary>One planned line with its day, saved under the `intents` key.</summary>
    public sealed record SavedPlan(int Day, bool Delivered, IReadOnlyList<IntentCandidate> Candidates)
    {
        public string ToJson() => JsonSerializer.Serialize(this);

        public static SavedPlan? FromJson(string json)
        {
            SavedPlan? plan = JsonSerializer.Deserialize<SavedPlan>(json);
            return plan is { Candidates: not null } ? plan : null;
        }
    }

    /// <summary>A line an NPC has already delivered (step 6; in shadow mode the list stays empty),
    /// saved under the `recentLines` key for novelty and cite cooldowns.</summary>
    public sealed record RecentLine(string Npc, string Line, int Day, string Kind, string Subject)
    {
        public static string ToJson(IEnumerable<RecentLine> lines)
            => JsonSerializer.Serialize(lines.ToList());

        public static List<RecentLine> FromJson(string json)
            => JsonSerializer.Deserialize<List<RecentLine>>(json) ?? new List<RecentLine>();
    }

    /// <summary>Appends a delivered line to the per-NPC lists, keeping the newest
    /// <paramref name="keep"/> per NPC (docs/spec/intents.md, `RecentLinesKept`). Pure.</summary>
    public static void Append(Dictionary<string, List<RecentLine>> lines, RecentLine line, int keep)
    {
        if (!lines.TryGetValue(line.Npc, out List<RecentLine>? mine))
            lines[line.Npc] = mine = new List<RecentLine>();
        mine.Add(line);
        if (mine.Count > keep)
            mine.RemoveRange(0, mine.Count - keep);
    }

    /// <summary>The cited (kind, subject) pairs this NPC delivered within the last
    /// <paramref name="cooldownDays"/> days, for the news cite cooldown (intents.md).</summary>
    public static IReadOnlyList<(string Kind, string Subject)> RecentCitations(
        IReadOnlyList<RecentLine>? lines, int today, int cooldownDays)
    {
        if (lines is null || lines.Count == 0)
            return Array.Empty<(string, string)>();

        var pairs = new List<(string, string)>();
        foreach (RecentLine line in lines)
        {
            if (today - line.Day > cooldownDays)
                continue;
            if (!string.IsNullOrEmpty(line.Kind))
                pairs.Add((line.Kind, line.Subject));
        }
        return pairs;
    }

    /// <summary>The delivered lines themselves (for the planner's equality check), oldest first.</summary>
    public static IReadOnlyList<string> LinesFor(IReadOnlyList<RecentLine>? lines)
        => lines is null ? Array.Empty<string>() : lines.Select(l => l.Line).ToList();
}
