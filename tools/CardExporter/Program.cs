// Exports the NPC cards for the character-spread eval (docs/spec/laya.md, "Character spread").
// Two variants per villager, exactly as the mod builds them:
//   A: NpcCard.Render with the game traits from fixtures/game/temperament/characters.json and
//      the VoiceSheets line (the card the mod sends today);
//   B: card A plus a "leanings:" line in plain words from the seed temperament table, built the
//      same way the viewer summarizes it (MindsSnapshotBuilder.TemperamentOf).
// Three hearts sets: 0 (the newcomer case), 4 and 6 (the mid-game reference). The "today" line
// is fixed. Deterministic: characters in name order.
//
// Usage: dotnet run --project tools/CardExporter [output.json] [characters.json] [temperament.json] [overrides.json]
// Defaults: sidecar/eval/cards.json and the fixture paths relative to the repo root.

using System.Text.Json;
using NpcDecision;
using NpcIntents;
using NpcMinds;
using NpcTemperament;

namespace CardExporter;

public static class Program
{
    /// <summary>The fixed delivery-day line, matching the eval set's cards.</summary>
    public const string Today = "spring 12 (Tuesday), sunny, 7:30 PM";

    public static int Main(string[] args)
    {
        string repo = RepoRoot();
        string output = Arg(args, 0) ?? Path.Combine(repo, "sidecar", "eval", "cards.json");
        string charactersPath = Arg(args, 1) ?? Path.Combine(repo, "fixtures", "game", "temperament", "characters.json");
        string temperamentPath = Arg(args, 2) ?? Path.Combine(repo, "fixtures", "game", "temperament", "temperament.json");
        string? overridesPath = Arg(args, 3) ?? Path.Combine(repo, "fixtures", "game", "temperament", "temperament-overrides.json");

        var gameTraits = LoadGameTraits(charactersPath);
        TemperamentTable table = TemperamentTable.FromJson(File.ReadAllText(temperamentPath));
        if (File.Exists(overridesPath))
            table = table.WithOverrides(TemperamentTable.OverridesFromJson(File.ReadAllText(overridesPath)));

        int[] hearts = { 0, 4, 6 };
        var cards = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, string>>>(StringComparer.Ordinal);
        foreach (string npc in gameTraits.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            string words = CardText.TemperamentWords(gameTraits[npc].Manner, gameTraits[npc].Anxiety, gameTraits[npc].Optimism);
            string voice = VoiceSheets.Voice(npc);
            TemperamentView view = MindsSnapshotBuilder.TemperamentOf(table.Of(npc), seeded: true, gameTraits: words);
            var variants = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
            foreach (string variant in new[] { "A", "B" })
            {
                var byHearts = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (int heartCount in hearts)
                {
                    string card = NpcCard.Render(npc, words, voice, heartCount, Today);
                    if (variant == "B")
                        card = WithLeanings(card, view.Summary);
                    byHearts[heartCount.ToString()] = card;
                }
                variants[variant] = byHearts;
            }
            cards[npc] = variants;
        }

        var doc = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["note"] = "NPC cards for the character-spread eval, built by tools/CardExporter from "
                + "fixtures/game/temperament/ (characters.json game traits, the seed temperament.json "
                + "with overrides) and VoiceSheets, exactly as the mod builds the card. Variant A is "
                + "the card the mod sends today; variant B adds the viewer's leanings summary line.",
            ["today"] = Today,
            ["hearts"] = hearts.Select(h => h.ToString()).ToArray(),
            ["cards"] = cards,
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(doc, options) + "\n");
        Console.WriteLine($"Wrote {cards.Count} characters to {output}");
        return 0;
    }

    /// <summary>Card A plus a "leanings:" line right after the temperament line.</summary>
    public static string WithLeanings(string card, string summary)
    {
        string line = "leanings: " + summary;
        foreach (string existing in card.Split('\n'))
        {
            if (existing.StartsWith("temperament:", StringComparison.Ordinal))
                return card.Replace(existing, existing + "\n" + line);
        }
        return card; // a temperament line always exists (NpcCard.Render)
    }

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "NpcSchedules.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Run from inside the repo.");
    }

    private static string? Arg(string[] args, int index) => index < args.Length ? args[index] : null;

    private static SortedDictionary<string, (string Manner, string Anxiety, string Optimism)> LoadGameTraits(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new SortedDictionary<string, (string, string, string)>(StringComparer.Ordinal);
        foreach (JsonProperty npc in doc.RootElement.GetProperty("characters").EnumerateObject())
        {
            JsonElement data = npc.Value;
            string text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
            result[npc.Name] = (text(data.GetProperty("Manner")), text(data.GetProperty("SocialAnxiety")), text(data.GetProperty("Optimism")));
        }
        return result;
    }
}
