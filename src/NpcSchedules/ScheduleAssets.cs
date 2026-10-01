namespace NpcSchedules;

/// <summary>
/// Content asset names for the game's schedule data. In 1.6 a villager's schedule lives at
/// <c>Characters/schedules/&lt;Name&gt;</c>, NOT <c>Data/Schedules/&lt;Name&gt;</c> — verified in
/// stardew-source-notes.md, "Motives verify pass" (2026-10-01), "Schedules": each NPC loads that
/// asset via <c>NPC.TryLoadSchedule</c> (NPC.cs:5993). One helper so the name cannot drift
/// between the mod's seeding and the tests.
/// </summary>
public static class ScheduleAssets
{
    /// <summary>The asset path holding one villager's schedule, loaded as
    /// <c>Dictionary&lt;string, string&gt;</c>.</summary>
    public static string NameFor(string npc) => $"Characters/schedules/{npc}";
}
