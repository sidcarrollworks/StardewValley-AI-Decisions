using System.Text.Json;

namespace NpcDecision;

/// <summary>
/// The per-question calibration table from <c>data/laya-calibration.json</c>, written by the
/// character-spread eval (<c>sidecar/eval/run_spread.py</c>) and committed. For each question
/// template the mod can ask, and each card variant (A = today's card, B = card with the leanings
/// line), it holds the town's median answer and spread over all villagers' cards. The viewer's
/// spread panel shows these beside today's live numbers ([docs/spec/debug-tools.md], "model
/// spread panel"). The corrections stay OFF: the file deliberately carries no RelativeScale or
/// w, and the mod never applies the numbers to a decision.
/// </summary>
public sealed class LayaCalibration
{
    /// <summary>One variant's row: the median and spread, or both null when the template is
    /// deliberately not calibrated.</summary>
    public sealed record VariantRow(double? Median, double? Spread)
    {
        public bool NotCalibrated => Median is null || Spread is null;
    }

    public sealed record QuestionRow(VariantRow A, VariantRow B);

    /// <summary>Every question template the mod asks today (or will ask when motives land).
    /// Tests pin that each has a calibration row or an explicit "not calibrated" one.</summary>
    public static readonly IReadOnlyList<string> KnownTemplates = new[]
    {
        "attention_emote", "attention_bubble", "attention_approach",
        "speak", "choose_bring_up", "hold_against", "close_friendly", "close_hostile",
        "welcome_newcomer",
    };

    private readonly IReadOnlyDictionary<string, QuestionRow> _questions;

    private LayaCalibration(IReadOnlyDictionary<string, QuestionRow> questions)
        => _questions = questions;

    public IReadOnlyDictionary<string, QuestionRow> Questions => _questions;

    public QuestionRow? Of(string template)
        => _questions.TryGetValue(template, out QuestionRow? row) ? row : null;

    /// <summary>The spread eval's id for a live question template (the proposition with the NPC
    /// name replaced by &lt;npc&gt;), or null when the eval did not measure that template.</summary>
    public static string? EvalIdForTemplate(string template)
    {
        if (template.StartsWith("Should <npc> try to get the player's attention with an emote", StringComparison.Ordinal))
            return "attention_emote";
        if (template.StartsWith("Should <npc> try to get the player's attention with a speech bubble", StringComparison.Ordinal))
            return "attention_bubble";
        if (template.StartsWith("Should <npc> drop what they are doing and go looking for the player", StringComparison.Ordinal))
            return "attention_approach";
        if (template.StartsWith("Does <npc> have news for the player?", StringComparison.Ordinal))
            return "speak";
        if (template.StartsWith("Would <npc> hold this against the player?", StringComparison.Ordinal))
            return "hold_against";
        if (template.StartsWith("Would <npc> go out of their way to welcome a newcomer in person?", StringComparison.Ordinal))
            return "welcome_newcomer";
        return null;
    }

    /// <summary>The trait a live question template should follow and the expected direction
    /// (+1: more of the trait means a higher answer; -1: the inverse), or null when the template
    /// has no trait mapping.</summary>
    public static (string Trait, int Direction)? TraitForTemplate(string template)
    {
        string? eval = EvalIdForTemplate(template);
        return eval switch
        {
            "attention_emote" or "attention_bubble" or "attention_approach" or
                "close_friendly" or "close_hostile" or "welcome_newcomer" => ("boldness", +1),
            "speak" => ("chattiness", +1),
            "hold_against" => ("forgiveness", -1),
            _ => null,
        };
    }

    public static LayaCalibration FromJson(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        var rows = new Dictionary<string, QuestionRow>(StringComparer.Ordinal);
        foreach (JsonProperty template in doc.RootElement.GetProperty("questions").EnumerateObject())
        {
            VariantRow ParseVariant(JsonElement value)
            {
                double? median = value.TryGetProperty("median", out JsonElement m)
                    && m.ValueKind == JsonValueKind.Number ? m.GetDouble() : null;
                double? spread = value.TryGetProperty("spread", out JsonElement s)
                    && s.ValueKind == JsonValueKind.Number ? s.GetDouble() : null;
                return new VariantRow(median, spread);
            }
            rows[template.Name] = new QuestionRow(
                ParseVariant(template.Value.GetProperty("A")),
                ParseVariant(template.Value.GetProperty("B")));
        }
        return new LayaCalibration(rows);
    }
}
