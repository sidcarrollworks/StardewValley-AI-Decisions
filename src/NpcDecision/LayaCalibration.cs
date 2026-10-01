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
