using NpcSchedules;
using NpcShadow;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewNpcMod;

/// <summary>
/// Shadow-mode skeleton. On each new day it loads Abigail's real schedule, runs the
/// NpcShadow simulator, and writes the shadow log — it changes NO game state. This is the
/// hook point where the decision layer (Jev/Laya) and the memory layer will plug in later.
/// </summary>
public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.DayStarted += OnDayStarted;
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        try
        {
            RegionMap regions = RegionMap.Load(Path.Combine(Helper.DirectoryPath, "regions.json"));

            var simulator = new ShadowSimulator(regions, "Player");
            simulator.AddSubject("Abigail",
                Helper.GameContent.Load<Dictionary<string, string>>("Characters/schedules/Abigail"));

            var options = new ExtractorOptions { Hearts = 0 };
            ShadowLog log = simulator.Run(Game1.currentSeason, Game1.dayOfMonth, dayCount: 2, options, seed: 1234);

            Monitor.Log($"Shadow run {Game1.currentSeason} day {Game1.dayOfMonth}:\n{log.RenderText()}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow run failed: {ex}", LogLevel.Error);
        }
    }
}
