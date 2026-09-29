using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

public class RegionMapTests
{
    [Fact]
    public void LoadsDataRegionsJson()
    {
        string path = FindDataRegionsJson();
        Assert.True(File.Exists(path), $"regions.json not found at {path}");
        RegionMap map = RegionMap.Load(path);

        Assert.Equal(120, map.BlockMinutes);
        Assert.Equal(0, map.RainChance["winter"]);
        Assert.Equal("Town", map.RegionFor("BusStop"));   // town includes the bus stop
        Assert.Equal("Town", map.RegionFor("SeedShop"));  // and town buildings
        Assert.Equal("Farm", map.RegionFor("Backwoods")); // backwoods counts as farm
        Assert.Equal("Mountain", map.RegionFor("SebastianRoom"));
        Assert.Equal("Island", map.RegionFor("LeoTreeHouse"));
        Assert.Null(map.RegionFor("Nowhere"));
    }

    [Fact]
    public void EveryLocationInRealGameFixturesIsMapped()
    {
        // all location names used by the real 1.6 schedules must resolve to a region
        string dataDir = FindDataRegionsJson();
        string fixturesDir = Path.Combine(Path.GetDirectoryName(dataDir)!, "..", "fixtures", "game");
        Assert.True(Directory.Exists(fixturesDir), $"fixtures/game not found at {fixturesDir}");
        RegionMap map = RegionMap.Load(dataDir);

        var unmapped = new List<string>();
        foreach (string file in Directory.EnumerateFiles(fixturesDir, "*.json"))
        {
            var schedules = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
            foreach (string script in schedules.Values)
                foreach (string field in ScriptParser.SplitFields(script))
                {
                    string[] tokens = ScriptParser.SplitTokens(field);
                    if (tokens.Length < 2 || tokens[0].StartsWith("GOTO") || tokens[0].StartsWith("NOT")
                        || tokens[0].StartsWith("MAIL"))
                        continue;
                    string time = tokens[0].Length > 1 && tokens[0][0] == 'a' ? tokens[0][1..] : tokens[0];
                    if (!int.TryParse(time, out _))
                        continue;
                    string location = tokens[1];
                    if (!int.TryParse(location, out _) && location != "bed" && map.RegionFor(location) == null)
                        unmapped.Add($"{Path.GetFileNameWithoutExtension(file)}: {location}");
                }
        }
        Assert.Empty(unmapped);
    }

    private static string FindDataRegionsJson()
    {
        // tests/bin/Debug/net6.0 -> repo/data/regions.json
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "regions.json"));
        return path;
    }
}
