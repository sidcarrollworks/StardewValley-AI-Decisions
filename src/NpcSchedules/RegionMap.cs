using System.Text.Json;
using System.Text.Json.Serialization;

namespace NpcSchedules;

/// <summary>
/// Location-to-region map loaded from <c>regions.json</c>. Editable without rebuilding.
/// Regions: Farm, Town, Mountain, Forest, Beach, Desert, Island, plus the implicit
/// <see cref="OtherRegion"/> for anything unmapped. Town includes BusStop and every town
/// building; Backwoods counts as Farm (project brief).
/// </summary>
public sealed class RegionMap
{
    public const string OtherRegion = "Other";

    public int BlockMinutes { get; set; } = 120;
    public Dictionary<string, double> RainChance { get; set; } = new()
    {
        ["spring"] = 0.18, ["summer"] = 0.12, ["fall"] = 0.18, ["winter"] = 0.0
    };
    public Dictionary<string, string[]> Regions { get; set; } = new();
    public Dictionary<string, string> Homes { get; set; } = new();

    private readonly Dictionary<string, string> locationToRegion = new(StringComparer.OrdinalIgnoreCase);

    public void Rebuild()
    {
        locationToRegion.Clear();
        foreach ((string region, string[] locations) in Regions)
            foreach (string location in locations)
                locationToRegion[location] = region;
    }

    public string? RegionFor(string location)
        => locationToRegion.TryGetValue(location, out string? region) ? region : null;

    /// <summary>Home location override for an NPC, or null if none is configured.</summary>
    public string? HomeOverride(string npc)
        => Homes.TryGetValue(npc, out string? location) ? location : null;

    public static RegionMap Load(string path)
    {
        var map = JsonSerializer.Deserialize<RegionMap>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"regions.json at '{path}' is empty or invalid.");
        if (map.BlockMinutes <= 0 || 1200 % map.BlockMinutes != 0 || map.BlockMinutes % 10 != 0)
            throw new InvalidDataException($"blockMinutes must be a multiple of 10 that divides 1200 (got {map.BlockMinutes}).");
        map.Rebuild();
        return map;
    }
}
