using NpcMemory;

namespace NpcDiaryEvents;

/// <summary>
/// Builds the Festival and MissedFestival entries written at day end (docs/spec/diary.md,
/// "Kinds", rows Festival and MissedFestival). Pure: no game types, no randomness, no I/O.
/// </summary>
public static class FestivalNotes
{
    /// <summary>
    /// Day-end festival notes. Rules (docs/spec/diary.md, "Triggers and game hooks"):
    /// - A null/empty festivalId returns an empty list (the caller should not have called).
    /// - Attended: one Festival entry per distinct actor (subject Player, Kind "Festival",
    ///   Detail = DiaryDetail.Format(("festival", festivalId), ("with", talkedWith contains the
    ///   actor ? "1" : "0"))). The player's own name, null names and empty names are skipped;
    ///   actors are deduplicated case-insensitively and written in name order (ordinal,
    ///   case-insensitive).
    /// - Not attended: one MissedFestival entry per villager with
    ///   heartsFor(villager) >= options.MissedFestivalHeartsFloor (subject Player,
    ///   Kind "MissedFestival", Detail = DiaryDetail.Format(("festival", festivalId))),
    ///   written in name order (ordinal, case-insensitive).
    /// - The result is sorted by Npc name (ordinal, case-insensitive).
    /// - AbsoluteTick is every entry's tick.
    /// </summary>
    public static IReadOnlyList<(string Npc, DiaryEntry Entry)> AtDayEnd(
        string? festivalId,
        bool attended,
        IReadOnlyCollection<string> actors,
        IReadOnlySet<string> talkedWith,
        IReadOnlyCollection<string> villagers,
        Func<string, int> heartsFor,
        DiaryOptions options,
        int absoluteTick)
    {
        var entries = new List<(string Npc, DiaryEntry Entry)>();
        if (string.IsNullOrEmpty(festivalId))
            return entries;

        if (attended)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actor in actors)
            {
                if (string.IsNullOrEmpty(actor))
                    continue;
                if (actor.Equals("Player", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!seen.Add(actor))
                    continue;

                bool withPlayer = talkedWith.Contains(actor);
                entries.Add((actor, new DiaryEntry(
                    absoluteTick,
                    "Player",
                    "Festival",
                    DiaryDetail.Format(("festival", festivalId), ("with", withPlayer ? "1" : "0")))));
            }
        }
        else
        {
            foreach (string villager in villagers)
            {
                if (heartsFor(villager) < options.MissedFestivalHeartsFloor)
                    continue;

                entries.Add((villager, new DiaryEntry(
                    absoluteTick,
                    "Player",
                    "MissedFestival",
                    DiaryDetail.Format(("festival", festivalId)))));
            }
        }

        entries.Sort(static (a, b) => string.Compare(a.Npc, b.Npc, StringComparison.OrdinalIgnoreCase));
        return entries;
    }
}
