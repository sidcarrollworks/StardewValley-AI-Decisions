using System.Globalization;
using System.Text;
using System.Text.Json;
using NpcTemperament;

namespace TemperamentExtractor;

/// <summary>
/// Command-line wrapper: the game's dialogue files and Data/Characters traits in, seed
/// temperaments out (temperament.json for the mod, temperament.md for review).
/// Method and inputs: docs/spec/temperament.md.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        string? dialogueDir = null, charactersPath = null, giftsPath = null, overridesPath = null, outDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help": PrintHelp(); return 0;
                case "--dialogue": dialogueDir = Next(args, ref i); break;
                case "--characters": charactersPath = Next(args, ref i); break;
                case "--gift-tastes": giftsPath = Next(args, ref i); break;
                case "--overrides": overridesPath = Next(args, ref i); break;
                case "--out": outDir = Next(args, ref i); break;
                default: Console.Error.WriteLine($"unknown argument '{args[i]}' (see --help)"); return 2;
            }
        }
        if (dialogueDir == null || charactersPath == null || outDir == null)
        {
            Console.Error.WriteLine("--dialogue, --characters and --out are required (see --help)");
            return 2;
        }

        {
            var traits = ReadTraits(charactersPath);
            var gifts = giftsPath != null ? ReadStringDictionary(giftsPath) : new Dictionary<string, string>();
            var inputs = new List<CharacterInput>();
            foreach (var (name, t) in traits.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                string file = Path.Combine(dialogueDir, name + ".json");
                var pages = new List<DialoguePage>();
                if (File.Exists(file))
                    foreach (var (_, line) in ReadStringDictionary(file).OrderBy(l => l.Key, StringComparer.Ordinal))
                        pages.AddRange(DialogueText.Pages(line));
                else
                    Console.Error.WriteLine($"no dialogue file for {name}; game traits only");
                if (gifts.TryGetValue(name, out string? taste))
                    pages.AddRange(GiftReactionLines(taste).SelectMany(DialogueText.Pages));
                var others = traits.Keys.Where(k => k != name);
                inputs.Add(new CharacterInput(name, t, DialogueFeatures.Compute(pages, others)));
            }

            var scored = TemperamentScorer.Score(inputs);
            var table = new TemperamentTable(scored.ToDictionary(s => s.Name, s => s.Seed));
            if (overridesPath != null)
                table = table.WithOverrides(TemperamentTable.OverridesFromJson(File.ReadAllText(overridesPath)));

            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "temperament.json"), table.ToJson());
            File.WriteAllText(Path.Combine(outDir, "temperament.md"), Report(scored, table));
            Console.WriteLine($"wrote {table.Rows.Count} characters to {outDir}");
            return 0;
        }
    }

    /// <summary>NPCGiftTastes value: love text/ids/like text/ids/dislike text/ids/hate text/ids/neutral text/ids.
    /// The reaction lines are the even fields (confirmed against the unpacked 1.6.15 file).</summary>
    public static IEnumerable<string> GiftReactionLines(string taste)
        => taste.Split('/').Where((_, i) => i % 2 == 0 && i < 10).Where(s => s.Length > 0);

    /// <summary>Reads an xnbcli output file (<c>{ "content": { ... } }</c>) or a flat key -> string object.</summary>
    public static Dictionary<string, string> ReadStringDictionary(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement.TryGetProperty("content", out var content) ? content : doc.RootElement;
        return root.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    public static Dictionary<string, GameTraits> ReadTraits(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<string, GameTraits>();
        foreach (var c in doc.RootElement.GetProperty("characters").EnumerateObject())
            result[c.Name] = new GameTraits(
                c.Value.GetProperty("Manner").GetString()!,
                c.Value.GetProperty("SocialAnxiety").GetString()!,
                c.Value.GetProperty("Optimism").GetString()!,
                c.Value.GetProperty("Age").GetString()!);
        return result;
    }

    private static string Report(IReadOnlyList<ScoredCharacter> scored, TemperamentTable table)
    {
        string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        string P(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine("# Seed temperaments (draft)");
        sb.AppendLine();
        sb.AppendLine("Generated by `tools/TemperamentExtractor`; do not edit by hand (use the overrides file).");
        sb.AppendLine("Method: [docs/spec/temperament.md](../../../docs/spec/temperament.md). 0.5 is the town's typical villager.");
        sb.AppendLine("Each cell is `seed (game traits alone)`: the difference is what the dialogue added.");
        sb.AppendLine();
        void Table(string title, IReadOnlyList<string> traits)
        {
            sb.AppendLine("## " + title);
            sb.AppendLine();
            sb.AppendLine("| Character | " + string.Join(" | ", traits.Select(t =>
                char.ToUpperInvariant(t[0]) + t[1..] + (TemperamentScorer.WordsOnly.Contains(t) ? "*" : ""))) + " |");
            sb.AppendLine("|---|" + string.Concat(traits.Select(_ => "---|")));
            foreach (var s in scored)
            {
                var seed = table.Of(s.Name);
                sb.Append("| ").Append(s.Name);
                foreach (string trait in traits)
                    sb.Append(" | ").Append(F(seed.Get(trait))).Append(" (").Append(F(s.FromTraits.Get(trait))).Append(')');
                sb.AppendLine(" |");
            }
            sb.AppendLine();
        }
        Table("Behaviour traits", Temperament.BehaviourTraits);
        Table("Emotion biases (Ekman)", Temperament.EmotionTraits);
        sb.AppendLine(@"\* words only: the game has no portrait for this emotion, so these are weaker numbers.");
        sb.AppendLine();
        sb.AppendLine("## Dialogue features");
        sb.AppendLine();
        sb.AppendLine("Share of pages (%) with each signal; words is the mean words per page; \"w\" columns are emotion words.");
        sb.AppendLine();
        sb.AppendLine("| Character | Pages | Happy | Sad | Angry | Love | ? | ! | ... | Words | Thanks | Sorry | Welcome | Dismiss | Gossip | Joy w | Sad w | Anger w | Fear w | Disgust w | Surprise w |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var s in scored)
        {
            var f = s.Features;
            sb.AppendLine($"| {s.Name} | {f.Pages} | {P(f.Happy)} | {P(f.Sad)} | {P(f.Angry)} | {P(f.Love)} | {P(f.Question)} | {P(f.Exclaim)} | {P(f.Trailing)} | "
                + $"{f.WordsPerPage.ToString("0.0", CultureInfo.InvariantCulture)} | {P(f.Thanks)} | {P(f.Sorry)} | {P(f.Welcome)} | {P(f.Dismiss)} | {P(f.Gossip)} | "
                + $"{P(f.HappyWords)} | {P(f.SadWords)} | {P(f.AngerWords)} | {P(f.FearWords)} | {P(f.DisgustWords)} | {P(f.SurpriseWords)} |");
        }
        return sb.ToString().Replace("\r\n", "\n"); // same bytes on every OS
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"{args[i]} needs a value");
        return args[++i];
    }

    private static void PrintHelp() => Console.WriteLine(
@"Usage: dotnet run --project tools/TemperamentExtractor -- [options]

  --dialogue <dir>      Unpacked Content/Characters/Dialogue (xnbcli output, <Name>.json). Required.
  --characters <path>   Game traits per character (fixtures/game/temperament/characters.json). Required.
  --gift-tastes <path>  Unpacked Data/NPCGiftTastes.json; adds the gift reaction lines. Optional.
  --overrides <path>    Hand edits: { ""Shane"": { ""forgiveness"": 0.3 } }, applied last. Optional.
  --out <dir>           Writes temperament.json and temperament.md. Required.

Same inputs always give the same output.");
}
