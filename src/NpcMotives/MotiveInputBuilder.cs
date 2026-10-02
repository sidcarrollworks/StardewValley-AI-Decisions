using NpcMemory;
using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// Builds one NPC's <see cref="MotiveInputs"/> on the game thread, from memory only (AGENTS.md
/// rule 2): its own ledger view of the player, its lead, its diary and its regard. Everything is
/// copied, so the result can go to the worker. The runner fills in its own pacing facts (greeted,
/// news shared, frustration, attempts left), so they are left at their defaults here.
/// </summary>
public static class MotiveInputBuilder
{
    /// <summary>Days since the player was last seen or reported when there is no view at all.</summary>
    public const int NeverSeenDays = 999;

    /// <param name="metInGame">The game's own record that the player has met this NPC (the mod
    /// passes it; a <c>Talked</c> entry in the diary counts too).</param>
    /// <param name="newsScore">The planner's best news score for this NPC today, 0 if none.</param>
    /// <param name="card">The NPC card (<c>NpcCard.Render</c>), for the model's state.</param>
    public static MotiveInputs Build(
        string npc, int now, Temperament temperament, int hearts, IEnumerable<DiaryEntry> diary,
        double regardForPlayer, LedgerView? view, Whereabouts? lead, double newsScore, int seed,
        bool metInGame = false, string? card = null, bool newcomerWeek = false)
    {
        List<DiaryEntry> entries = diary.ToList();
        bool talkedEver = entries.Any(e => e.Kind == "Talked"
            && string.Equals(e.Subject, MotivesEngine.Player, StringComparison.OrdinalIgnoreCase));
        int daysSince = view is null ? NeverSeenDays : view.AgeTicks / GameClock.TicksPerDay;
        bool hasLead = lead is { HasPlace: true, Source: WhereaboutsSource.SeenToday or WhereaboutsSource.Told or WhereaboutsSource.Habit };
        return new MotiveInputs(
            npc,
            now,
            temperament,
            Math.Clamp(hearts, 0, 14),
            entries,
            regardForPlayer,
            PlayerNear: IsNear(view),
            SeenPlayerToday: IsSeenToday(view),
            HasMetPlayer: metInGame || talkedEver,
            KnowsOfPlayer: view is not null,
            BestNewsScore: Math.Max(0, newsScore),
            NewsShared: false,
            GreetedToday: false,
            DaysSinceSighting: daysSince,
            IgnoredToday: 0,
            AttemptsLeftToday: int.MaxValue,
            Seed: seed,
            NewcomerWeek: newcomerWeek,
            HasLead: hasLead,
            Card: card,
            LeadPlace: hasLead ? lead!.Place : null);
    }

    /// <summary>The NPC itself saw the player at a named spot this very tick (the ladder's
    /// <c>IsNear</c>: a memory-only fact, never a live position).</summary>
    public static bool IsNear(LedgerView? view)
        => view is not null && view.HopCount == 0 && view.AgeTicks == 0 && view.Detail == LedgerDetail.NamedSpot;

    /// <summary>The NPC itself saw the player today (any detail short of Gone); hearsay never counts.</summary>
    public static bool IsSeenToday(LedgerView? view)
        => view is not null && view.HopCount == 0
           && view.Detail is LedgerDetail.NamedSpot or LedgerDetail.Location or LedgerDetail.Region or LedgerDetail.EarlierToday;
}
