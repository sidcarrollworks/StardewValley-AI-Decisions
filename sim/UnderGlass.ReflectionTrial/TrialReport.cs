using System.Globalization;
using System.Text;

namespace UnderGlass.ReflectionTrial;

public static class TrialReport
{
    public static string Markdown(TrialResult result)
    {
        var text = new StringBuilder();
        text.AppendLine("# Reflection decision trial").AppendLine();
        text.AppendLine(result.RequestedLaya
            ? $"Requested Laya model: `{result.Model}`. These are controlled hypothetical scenes, not a simulated town."
            : "**Authored/offline plumbing run. These results provide no evidence about Laya's judgment.**");
        text.AppendLine();
        text.AppendLine($"Calls: {result.Observations.Count()}; explicit fallbacks: {result.FallbackCount}; non-Laya answers: {result.NonLayaCount}; incomplete submitted packets: {result.IncompletePacketCount}; server-truncated responses: {result.TruncatedResponseCount}; unverified server usage: {result.UnverifiedResponseCount}.");
        if (result.RequestedLaya && result.NonLayaCount > 0)
            text.AppendLine("**Warning: real Laya was requested but some answers used offline material. Do not interpret those comparisons as model behavior. Exit code 2.**");
        if (result.IncompletePacketCount > 0)
            text.AppendLine("**Warning: some submitted packets lost context, memory, a thought, or a complete candidate line, or failed their budget audit. Inspect the JSON receipts before interpreting changes.**");
        if (result.TruncatedResponseCount > 0 || result.UnverifiedResponseCount > 0)
            text.AppendLine("**Warning: some server responses report truncation or lack verifiable usage metadata. Submitted text alone cannot establish what the model actually read.**");
        text.AppendLine();
        text.AppendLine("Each pair ran baseline → variant → exact baseline repeat. Total variation (TV) is half the sum of absolute probability differences: 0 means unchanged, 1 means disjoint distributions. ID TV compares the same labels; semantic TV follows the same line meaning across swapped labels. Repeat TV describes observed inference variability over one repeat; it is not a statistical confidence bound. Top-choice ties use label order, independent of submitted candidate order.");
        text.AppendLine("For the label-renaming control, ID TV is mechanically 1 because all labels changed; semantic TV is the meaningful comparison.");
        text.AppendLine();
        text.AppendLine("| Probe | ID TV | Semantic TV | Repeat TV | Top baseline → variant → repeat |");
        text.AppendLine("|---|---:|---:|---:|---|");
        foreach (TrialComparison pair in result.Comparisons)
            text.AppendLine($"| {pair.Id} | {F(pair.ByIdTotalVariation)} | {F(pair.SemanticTotalVariation)} | {F(pair.RepeatTotalVariation)} | {pair.Baseline.TopChoice} → {pair.Variant.TopChoice} → {pair.Repeat.TopChoice} |");
        text.AppendLine();
        text.AppendLine("These measurements describe sensitivity to the controlled edits. They do not establish realism, character quality, or whether a town is interesting. A preferred answer is not prescribed.");
        foreach (TrialComparison pair in result.Comparisons)
        {
            text.AppendLine().AppendLine($"## {pair.Id}").AppendLine();
            text.AppendLine(pair.Change).AppendLine();
            text.AppendLine($"Thought held fixed: {pair.Baseline.Request.Proposal!.Thought}").AppendLine();
            text.AppendLine($"Baseline memory: {pair.Baseline.Request.Memory}");
            if (pair.Variant.Request.Memory != pair.Baseline.Request.Memory)
                text.AppendLine($"Variant memory: {pair.Variant.Request.Memory}");
            text.AppendLine($"Baseline character context: {pair.Baseline.Request.Context}");
            if (pair.Variant.Request.Context != pair.Baseline.Request.Context)
                text.AppendLine($"Variant character context: {pair.Variant.Request.Context}");
            text.AppendLine();
            text.AppendLine("| Sample | Backend | ms | Semantic text chars | Full memory / context / lines / thought |");
            text.AppendLine("|---|---|---:|---:|---|");
            foreach (TrialObservation observation in new[] { pair.Baseline, pair.Variant, pair.Repeat })
            {
                PromptAudit audit = observation.Prompt;
                text.AppendLine($"| {observation.Role} | {Cell(observation.Answer.Backend)} | {F(observation.ElapsedMilliseconds)} | {audit.TextCharacters?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {Yes(audit.FullMemory)} / {Yes(audit.FullContext)} / {Yes(audit.CompleteChoices)} / {Yes(audit.FullThought)} |");
            }
            text.AppendLine();
            text.AppendLine("| Sample | Routed model | Input tokens | Dropped state tokens | Server truncated | Truncated questions |");
            text.AppendLine("|---|---|---:|---:|---|---|");
            foreach (TrialObservation observation in new[] { pair.Baseline, pair.Variant, pair.Repeat })
            {
                ResponseAudit response = observation.Response;
                text.AppendLine($"| {observation.Role} | {response.RoutedModel ?? "n/a"} | {response.InputTokens?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {response.StateTokensDropped?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {Yes(response.Truncated)} | {Cell(string.Join(", ", response.TruncatedQuestions))} |");
            }
            foreach (TrialObservation observation in new[] { pair.Baseline, pair.Variant, pair.Repeat })
            {
                if (observation.Answer.Backend.Contains("fallback", StringComparison.OrdinalIgnoreCase))
                    text.AppendLine().AppendLine($"{observation.Role} fallback: {observation.Answer.Note}");
                if (observation.Response.Error is { } error)
                    text.AppendLine().AppendLine($"{observation.Role}: {error}");
            }
            text.AppendLine();
            text.AppendLine("| ID | Baseline line | Variant line | P(base) | P(variant) | P(repeat) | Δ by ID |");
            text.AppendLine("|---|---|---|---:|---:|---:|---:|");
            foreach (ChoiceDelta delta in pair.ByIdChanges)
                text.AppendLine($"| {delta.BaselineId} | {Cell(delta.BaselineLine)} | {Cell(delta.VariantLine)} | {F(delta.Baseline)} | {F(delta.Variant)} | {F(pair.Repeat.Probabilities.GetValueOrDefault(delta.BaselineId))} | {F(delta.Delta)} |");
            if (pair.SemanticChanges.Any(d => d.BaselineId != d.VariantId))
            {
                text.AppendLine().AppendLine("Semantic alignment (same actual line):").AppendLine();
                text.AppendLine("| Base ID → variant ID | Line | P(base) | P(variant at mapped ID) | Δ semantic |");
                text.AppendLine("|---|---|---:|---:|---:|");
                foreach (ChoiceDelta delta in pair.SemanticChanges)
                    text.AppendLine($"| {delta.BaselineId} → {delta.VariantId} | {Cell(delta.BaselineLine)} | {F(delta.Baseline)} | {F(delta.Variant)} | {F(delta.Delta)} |");
            }
        }
        text.AppendLine().AppendLine("Full request, response, submitted-prompt receipts, normalized weights and timings are in the accompanying JSON file. Generation was disabled for every probe.");
        return text.ToString();
    }

    private static string F(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
    private static string Yes(bool? value) => value is null ? "n/a" : value.Value ? "yes" : "NO";
    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
