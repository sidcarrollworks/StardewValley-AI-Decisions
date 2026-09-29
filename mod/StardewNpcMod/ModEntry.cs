using NpcSchedules;
using NpcShadow;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewNpcMod;

/// <summary>
/// Shadow-mode skeleton. On each new day it loads a spread of real villager schedules, runs the
/// NpcShadow simulator (a stationary observer watching them come and go), and writes the shadow
/// log to a file — it changes NO game state. This is the hook point where the decision layer
/// (Jev/Laya) and the memory layer will plug in later.
/// </summary>
public class ModEntry : Mod
{
    // A spread of villagers who visit different regions, so co-location (and its decay) shows up.
    private static readonly string[] ShadowNpcs =
    {
        "Abigail", "Sebastian", "Willy", "Lewis", "Penny", "Linus", "Haley", "Clint", "Robin",
    };

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
            foreach (string npc in ShadowNpcs)
            {
                var schedules = Helper.GameContent.Load<Dictionary<string, string>>($"Characters/schedules/{npc}");
                simulator.AddSubject(npc, schedules);
            }

            var options = new ExtractorOptions { Hearts = 0 };
            ShadowLog log = simulator.Run(Game1.currentSeason, Game1.dayOfMonth, dayCount: 2, options, seed: 1234);

            string path = Path.Combine(Helper.DirectoryPath, "shadow-log.txt");
            File.WriteAllText(path, log.RenderText());
            Monitor.Log($"Shadow run {Game1.currentSeason} day {Game1.dayOfMonth}: {ShadowNpcs.Length} NPCs, {log.Events.Count} events -> shadow-log.txt", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow run failed: {ex}", LogLevel.Error);
        }
    }
}
