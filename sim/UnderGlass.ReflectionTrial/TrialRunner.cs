using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnderGlass.Minds;
using UnderGlass.Sim;

namespace UnderGlass.ReflectionTrial;

public sealed record PromptAudit(int? TextCharacters, bool? WithinBudget, bool? CompleteChoices,
    bool? FullMemory, bool? FullContext, bool? FullThought, string? SubmittedState, string? Error)
{
    public bool Complete => WithinBudget == true && CompleteChoices == true && FullMemory == true
        && FullContext == true && FullThought == true && Error is null;
}

public sealed record TrialObservation(string Role, ReflectionRequest Request, ReflectionAnswer Answer,
    IReadOnlyDictionary<string, double> Probabilities, string TopChoice, string TopLine,
    double ElapsedMilliseconds, PromptAudit Prompt, ResponseAudit Response,
    IReadOnlyList<TrialPassAudit>? Passes = null);

public sealed record TrialPassAudit(int Pass, PromptAudit Prompt, ResponseAudit Response, string? Error = null);

public sealed record ResponseAudit(string? RoutedModel, int? InputTokens, int? StateTokensDropped,
    bool? Truncated, IReadOnlyList<string> TruncatedQuestions, string? Error)
{
    public bool HasTruncation => Truncated == true || StateTokensDropped > 0 || TruncatedQuestions.Count > 0;
    public bool Verified => Error is null && InputTokens is not null && StateTokensDropped is not null && Truncated is not null;
}

public sealed record ChoiceDelta(string BaselineId, string VariantId, string BaselineLine,
    string VariantLine, double Baseline, double Variant, double Delta);

public sealed record TrialComparison(string Id, string Change, TrialObservation Baseline,
    TrialObservation Variant, TrialObservation Repeat, double ByIdTotalVariation,
    double SemanticTotalVariation, double RepeatTotalVariation,
    IReadOnlyList<ChoiceDelta> ByIdChanges, IReadOnlyList<ChoiceDelta> SemanticChanges);

public sealed record TrialResult(bool RequestedLaya, string Model, IReadOnlyList<TrialComparison> Comparisons,
    string EvaluationMode = "raw")
{
    [JsonIgnore]
    public IEnumerable<TrialObservation> Observations => Comparisons.SelectMany(c => new[] { c.Baseline, c.Variant, c.Repeat });
    public int FallbackCount => Observations.Count(o => o.Answer.Backend.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    public int NonLayaCount => Observations.Count(o => o.Answer.Backend != "authored+laya" || o.Answer.LayaPrompt is null);
    [JsonIgnore]
    public IEnumerable<TrialPassAudit> EvaluationPasses => Observations.SelectMany(o => o.Passes is { Count: > 0 }
        ? o.Passes : new[] { new TrialPassAudit(1, o.Prompt, o.Response) });
    public int EvaluationCount => Observations.Sum(o => o.Answer.Evaluations?.Count ?? (o.Answer.LayaPrompt is null ? 0 : 1));
    public int IncompletePacketCount => Observations.Count(o => o.Answer.Backend == "authored+laya"
        && (o.Passes is { Count: > 0 } ? o.Passes.Any(p => !p.Prompt.Complete || p.Error is not null) : !o.Prompt.Complete));
    public int TruncatedResponseCount => EvaluationPasses.Count(p => p.Response.HasTruncation);
    public int UnverifiedResponseCount => Observations.Count(o => o.Answer.Backend == "authored+laya"
        && (o.Passes is { Count: > 0 } ? o.Passes.Any(p => !p.Response.Verified) : !o.Response.Verified));
    public int ExitCode => RequestedLaya && NonLayaCount > 0 ? 2
        : RequestedLaya && (IncompletePacketCount > 0 || TruncatedResponseCount > 0 || UnverifiedResponseCount > 0) ? 3 : 0;
}

public static class TrialRunner
{
    public static async Task<TrialResult> RunAsync(IReflectionMind mind, bool requestedLaya, string model,
        IReadOnlyList<TrialPair>? pairs = null, CancellationToken cancellationToken = default,
        string evaluationMode = "raw")
    {
        var results = new List<TrialComparison>();
        foreach (TrialPair pair in pairs ?? TrialScenes.Create())
        {
            // The exact baseline repeats after its intervention, exposing drift/noise
            // over the same interval rather than assuming inference is repeatable.
            TrialObservation baseline = await Observe("baseline", pair.Baseline);
            TrialObservation variant = await Observe("variant", pair.Variant);
            TrialObservation repeat = await Observe("baseline-repeat", pair.Baseline);
            var identity = pair.Baseline.Choices.ToDictionary(c => c.Id, c => c.Id, StringComparer.Ordinal);
            var byId = ChangesById(baseline, variant);
            var semantic = Changes(baseline, variant, pair.SemanticMap);
            results.Add(new TrialComparison(pair.Id, pair.Change, baseline, variant, repeat,
                ByIdTotalVariation(baseline.Probabilities, variant.Probabilities),
                TotalVariation(baseline.Probabilities, variant.Probabilities, pair.SemanticMap),
                TotalVariation(baseline.Probabilities, repeat.Probabilities, identity), byId, semantic));
        }
        return new TrialResult(requestedLaya, model, results, evaluationMode);

        async Task<TrialObservation> Observe(string role, ReflectionRequest request)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            ReflectionAnswer answer = await mind.ReflectAsync(request, cancellationToken);
            timer.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            var probabilities = Normalize(answer.Weights, request.Choices.Select(c => c.Id));
            string top = probabilities.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
            TrialPassAudit[]? passes = answer.Evaluations?.Select(e => new TrialPassAudit(e.Pass,
                AuditPacket(request, answer, e.Prompt, e.LabelsToChoiceIds), AuditResponseText(e.Response), e.Error)).ToArray();
            return new TrialObservation(role, request, answer, probabilities, top,
                request.Choices.Single(c => c.Id == top).Line, timer.Elapsed.TotalMilliseconds,
                passes?.FirstOrDefault()?.Prompt ?? Audit(request, answer),
                passes?.FirstOrDefault()?.Response ?? AuditResponse(answer), passes);
        }
    }

    public static IReadOnlyDictionary<string, double> Normalize(IReadOnlyDictionary<string, double> weights,
        IEnumerable<string> expectedIds)
    {
        string[] ids = expectedIds.ToArray();
        if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || weights.Count != ids.Length
            || ids.Any(id => !weights.TryGetValue(id, out double w) || !double.IsFinite(w) || w < 0))
            throw new InvalidDataException("Trial answer must include one finite nonnegative weight for every choice ID.");
        double maximum = weights.Values.Max();
        if (maximum <= 0) throw new InvalidDataException("Trial answer cannot have all-zero weights.");
        double scaledTotal = weights.Values.Sum(w => w / maximum);
        return ids.ToDictionary(id => id, id => (weights[id] / maximum) / scaledTotal, StringComparer.Ordinal);
    }

    public static double TotalVariation(IReadOnlyDictionary<string, double> baseline,
        IReadOnlyDictionary<string, double> variant, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count != baseline.Count || variant.Count != baseline.Count
            || baseline.Keys.Any(id => !map.ContainsKey(id))
            || map.Values.Distinct(StringComparer.Ordinal).Count() != variant.Count
            || map.Values.Any(id => !variant.ContainsKey(id)))
            throw new ArgumentException("Comparison map must match every baseline and variant choice exactly once.", nameof(map));
        return 0.5 * baseline.Sum(p => Math.Abs(p.Value - variant[map[p.Key]]));
    }

    public static double ByIdTotalVariation(IReadOnlyDictionary<string, double> baseline,
        IReadOnlyDictionary<string, double> variant)
        => 0.5 * baseline.Keys.Union(variant.Keys, StringComparer.Ordinal)
            .Sum(id => Math.Abs(baseline.GetValueOrDefault(id) - variant.GetValueOrDefault(id)));

    public static PromptAudit Audit(ReflectionRequest request, ReflectionAnswer answer)
        => answer.Evaluations is { Count: > 0 } passes
            ? AuditPacket(request, answer, passes[0].Prompt, passes[0].LabelsToChoiceIds)
            : AuditPacket(request, answer, answer.LayaPrompt, null);

    private static PromptAudit AuditPacket(ReflectionRequest request, ReflectionAnswer answer, string? prompt,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? labels)
    {
        if (prompt is null) return new(null, null, null, null, null, null, null, null);
        try
        {
            using JsonDocument document = JsonDocument.Parse(prompt);
            JsonElement root = document.RootElement;
            string state = root.GetProperty("state").GetString()!;
            JsonElement question = root.GetProperty("questions").GetProperty("q");
            JsonElement criteria = question.GetProperty("criteria");
            int characters = state.Length + question.GetProperty("instructions").GetString()!.Length
                + criteria.EnumerateObject().Sum(p => p.Name.Length + p.Value.GetString()!.Length);
            labels ??= request.Choices.ToDictionary(c => c.Id,
                c => (IReadOnlyList<string>)new[] { c.Id }, StringComparer.Ordinal);
            string[] mappedIds = labels.Values.SelectMany(ids => ids).ToArray();
            bool lines = labels.Count == criteria.EnumerateObject().Count()
                && mappedIds.Length == request.Choices.Count && mappedIds.Distinct(StringComparer.Ordinal).Count() == request.Choices.Count
                && request.Choices.All(c => mappedIds.Contains(c.Id, StringComparer.Ordinal))
                && labels.All(label => label.Value.Count > 0 && criteria.TryGetProperty(label.Key, out JsonElement text)
                    && label.Value.All(id => request.Choices.Any(c => c.Id == id
                        && text.GetString() == $"{(c.Kind.Length == 0 ? "no act" : c.Kind)}: {c.Line}")));
            const string contextMarker = "\nCharacter context: ";
            const string memoryMarker = "\nKnown memory: ";
            int contextStart = state.IndexOf(contextMarker, StringComparison.Ordinal);
            int memoryStart = state.IndexOf(memoryMarker, StringComparison.Ordinal);
            bool fullContext = contextStart >= 0 && memoryStart > contextStart
                && state[(contextStart + contextMarker.Length)..memoryStart] == request.Context;
            bool fullMemory = memoryStart >= 0 && state[(memoryStart + memoryMarker.Length)..] == request.Memory;
            bool fullThought = request.Proposal is { } proposal && answer.Thought == proposal.Thought
                && answer.SuggestedChoice == proposal.SuggestedChoice
                && state.Contains("Imagined thought: " + proposal.Thought + "\n", StringComparison.Ordinal);
            return new(characters, characters <= ResilientReflectionMind.LayaTextLimit, lines,
                fullMemory, fullContext, fullThought, state, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return new(null, false, false, false, false, false, null, "Could not audit submitted prompt: " + ex.Message);
        }
    }

    public static ResponseAudit AuditResponse(ReflectionAnswer answer)
        => AuditResponseText(answer.LayaResponse);

    private static ResponseAudit AuditResponseText(string? response)
    {
        if (response is null) return new(null, null, null, null, Array.Empty<string>(), null);
        try
        {
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            JsonElement usage = root.GetProperty("usage");
            string? model = root.TryGetProperty("routing", out JsonElement routing)
                && routing.TryGetProperty("model", out JsonElement routed) ? routed.GetString() : null;
            int tokens = usage.GetProperty("input_tokens").GetInt32();
            int dropped = usage.GetProperty("state_tokens_dropped").GetInt32();
            bool truncated = usage.GetProperty("truncated").GetBoolean();
            string[] questions = usage.GetProperty("truncated_questions").EnumerateArray()
                .Select(q => q.ValueKind == JsonValueKind.String ? q.GetString()! : q.GetRawText()).ToArray();
            return new(model, tokens, dropped, truncated, questions, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            return new(null, null, null, null, Array.Empty<string>(), "Could not audit Laya usage: " + ex.Message);
        }
    }

    private static IReadOnlyList<ChoiceDelta> ChangesById(TrialObservation baseline, TrialObservation variant)
        => baseline.Request.Choices.Select(c => c.Id).Union(variant.Request.Choices.Select(c => c.Id), StringComparer.Ordinal)
            .Select(id => new ChoiceDelta(id, id,
                baseline.Request.Choices.FirstOrDefault(c => c.Id == id)?.Line ?? "(ID absent)",
                variant.Request.Choices.FirstOrDefault(c => c.Id == id)?.Line ?? "(ID absent)",
                baseline.Probabilities.GetValueOrDefault(id), variant.Probabilities.GetValueOrDefault(id),
                variant.Probabilities.GetValueOrDefault(id) - baseline.Probabilities.GetValueOrDefault(id))).ToArray();

    private static IReadOnlyList<ChoiceDelta> Changes(TrialObservation baseline, TrialObservation variant,
        IReadOnlyDictionary<string, string> map)
        => baseline.Request.Choices.Select(c => new ChoiceDelta(c.Id, map[c.Id], c.Line,
            variant.Request.Choices.Single(v => v.Id == map[c.Id]).Line,
            baseline.Probabilities[c.Id], variant.Probabilities[map[c.Id]],
            variant.Probabilities[map[c.Id]] - baseline.Probabilities[c.Id])).ToArray();
}
