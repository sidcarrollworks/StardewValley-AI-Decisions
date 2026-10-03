namespace NpcMotives;

/// <summary>
/// One number per villager from the motives runner's latest weighing, for the places that once
/// read the urge ladder's urge (retired 2026-10-03, D31): the heartbeat's "strongest motive" and
/// who asks around for the player. Pure; copies, safe to read on the game thread.
/// </summary>
public static class MotiveDrive
{
    /// <summary>The strength of the motive each villager would act on now (0 when none).</summary>
    public static IReadOnlyDictionary<string, double> Strongest(IReadOnlyDictionary<string, MotiveDecision> decisions)
        => decisions.ToDictionary(kv => kv.Key, kv => kv.Value.Chosen?.Strength ?? 0, StringComparer.OrdinalIgnoreCase);

    /// <summary>How much each villager wants to find the player: its strongest motive that sends
    /// it looking (missing the player, worried about them, or news to tell), 0 when none. The
    /// search asks around at <see cref="MotiveOptions.AskAroundStrength"/> and above.</summary>
    public static IReadOnlyDictionary<string, double> Seeking(IReadOnlyDictionary<string, MotiveDecision> decisions)
        => decisions.ToDictionary(kv => kv.Key,
            kv => kv.Value.Motives
                .Where(m => m.Motive is Motive.MissingYou or Motive.Worried or Motive.News && m.Subject == NpcMemory.MemoryStore.PlayerName)
                .Select(m => m.Strength).DefaultIfEmpty(0).Max(),
            StringComparer.OrdinalIgnoreCase);
}
