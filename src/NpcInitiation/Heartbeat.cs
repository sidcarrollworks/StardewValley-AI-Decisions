using NpcSchedules;

namespace NpcInitiation;

/// <summary>
/// Pure formatting for the mod's two-hourly heartbeat log line, kept here so it is testable
/// without the game. Deterministic: the max-urge pick breaks ties by NPC name ascending
/// (AGENTS rule 4), never by dictionary order.
/// </summary>
public static class Heartbeat
{
    /// <summary>State of the overnight plan job, formatted as the log's short word.</summary>
    public enum PlanState
    {
        None,
        Running,
        Ready,
    }

    /// <summary>The heartbeat fires at ticks 0, 12, 24, ... (every 2 game hours).</summary>
    public static bool ShouldFire(int tickOfDay)
        => tickOfDay % 12 == 0;

    public static string Format(
        int tickOfDay,
        int diaryCount,
        IReadOnlyDictionary<string, double> urges,
        int backlog,
        int dropped,
        PlanState planState,
        long modelCalls = -1,
        long modelFallbacks = -1,
        double modelMedianMs = -1,
        double modelP95Ms = -1)
    {
        string plan = planState switch
        {
            PlanState.Running => "running",
            PlanState.Ready => "ready",
            _ => "none",
        };

        // Strictly-higher comparison over name-ascending order, so ties keep the
        // alphabetically first NPC regardless of the dictionary's iteration order.
        double maxUrge = 0.0;
        string top = "none";
        foreach (string npc in urges.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            double urge = urges[npc];
            if (urge > maxUrge)
            {
                maxUrge = urge;
                top = npc;
            }
        }

        string line = $"[shadow] {TimeUtils.TimeOfDay(tickOfDay):0000}: {diaryCount} NPC diaries, " +
                      $"max urge {maxUrge:0.00} ({top}), ladder backlog {backlog}/dropped {dropped}, " +
                      $"overnight plan {plan}";

        // Model counters ride at the end when the caller supplies them (docs/spec/laya.md,
        // "Counters in the heartbeat"); a negative value means "no data" and is omitted.
        if (modelCalls >= 0)
        {
            line += $", model calls {modelCalls}/fallback {modelFallbacks}";
            if (modelMedianMs >= 0)
                line += $", median {modelMedianMs:0} ms, p95 {modelP95Ms:0} ms";
        }

        return line;
    }
}
