using System.Text;

namespace NpcIntents;

/// <summary>
/// Player-facing names for the game's internal location names, so a line says "Pierre's General
/// Store" rather than "SeedShop". The table is recalled from the vanilla 1.6 maps (verify the
/// in-game wording; <c>GameLocation.DisplayName</c> is the authoritative source in the mod).
/// Unknown names are split at capitals and underscores ("BathHouse_Pool" -> "Bath House Pool").
/// </summary>
public static class PlaceNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SeedShop"] = "Pierre's General Store",
        ["Saloon"] = "the Stardrop Saloon",
        ["ArchaeologyHouse"] = "the museum",
        ["Blacksmith"] = "the blacksmith",
        ["ScienceHouse"] = "the carpenter's shop",
        ["AnimalShop"] = "Marnie's ranch",
        ["FishShop"] = "the fish shop",
        ["Hospital"] = "the clinic",
        ["HarveyRoom"] = "Harvey's room",
        ["ManorHouse"] = "the mayor's manor",
        ["JoshHouse"] = "1 River Road",
        ["HaleyHouse"] = "2 Willow Lane",
        ["SamHouse"] = "1 Willow Lane",
        ["Trailer"] = "the trailer",
        ["Trailer_Big"] = "the trailer",
        ["ElliottHouse"] = "Elliott's cabin",
        ["LeahHouse"] = "Leah's cottage",
        ["WizardHouse"] = "the wizard's tower",
        ["Tent"] = "Linus's tent",
        ["SebastianRoom"] = "Sebastian's room",
        ["CommunityCenter"] = "the community center",
        ["JojaMart"] = "JojaMart",
        ["MovieTheater"] = "the movie theater",
        ["AdventureGuild"] = "the Adventurer's Guild",
        ["BathHouse_Entry"] = "the spa",
        ["BathHouse_Pool"] = "the spa",
        ["SandyHouse"] = "the Oasis",
        ["Desert"] = "the desert",
        ["BusStop"] = "the bus stop",
        ["Town"] = "town",
        ["Beach"] = "the beach",
        ["Forest"] = "the forest",
        ["Woods"] = "the secret woods",
        ["Mountain"] = "the mountain",
        ["Railroad"] = "the railroad",
        ["Backwoods"] = "the backwoods",
        ["Mine"] = "the mines",
        ["Farm"] = "your farm",
        ["FarmHouse"] = "your house",
        ["Greenhouse"] = "your greenhouse",
        ["IslandSouth"] = "the island",
        ["Island"] = "the island",        // region names, used when only the region is remembered
        ["Other"] = "somewhere out of the way",
    };

    public static string Display(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "";
        if (Names.TryGetValue(location, out string? name))
            return name;
        if (location.Contains(' '))
            return location; // already a phrase

        // CamelCase / snake_case -> words.
        var words = new StringBuilder();
        for (int i = 0; i < location.Length; i++)
        {
            char c = location[i];
            if (c == '_')
            {
                words.Append(' ');
                continue;
            }
            if (i > 0 && char.IsUpper(c) && char.IsLower(location[i - 1]))
                words.Append(' ');
            words.Append(c);
        }
        return string.Join(' ', words.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
