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
        PlanState planState)
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

        return $"[shadow] {TimeUtils.TimeOfDay(tickOfDay):0000}: {diaryCount} NPC diaries, " +
               $"max urge {maxUrge:0.00} ({top}), ladder backlog {backlog}/dropped {dropped}, " +
               $"overnight plan {plan}";
    }
}
