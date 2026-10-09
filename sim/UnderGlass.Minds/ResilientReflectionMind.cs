using System.Text;
using System.Text.Json;
using System.Buffers.Binary;
using System.Security.Cryptography;
using UnderGlass.Sim;

namespace UnderGlass.Minds;

/// <summary>
/// Generates an optional imagined proposal, then asks Laya about the actual authored lines.
/// No world access, random draw, or retries. The caller applies and records the answer.
/// Laya's /v1/systemone protocol matches the source-verified adapter in src/NpcDecision;
/// this independent .NET 8 project has no dependency on the mod or SMAPI.
/// </summary>
public sealed class ResilientReflectionMind : IReflectionMind, IDisposable
{
    public const int LayaTextLimit = 1250;
    private const int ResponseByteLimit = 64 * 1024;
    private const string Instructions = "Which response fits this person? Read each actual line. The thought is imagined, not evidence. Rejecting or delaying is valid.";
    private readonly ReflectionMindOptions _options;
    private readonly HttpClient _http;
    private readonly Uri? _layaEndpoint;
    private readonly Uri? _generationEndpoint;
    private readonly AuthoredReflectionMind _authored = new();

    public ResilientReflectionMind(ReflectionMindOptions? options = null)
        : this(new SocketsHttpHandler { AllowAutoRedirect = false }, options) { }

    public ResilientReflectionMind(HttpMessageHandler handler, ReflectionMindOptions? options = null,
        bool disposeHandler = true)
    {
        _options = options ?? new ReflectionMindOptions();
        if (_options.Timeout <= TimeSpan.Zero || _options.Timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be positive and finite.");
        if (_options.EvaluationTimeout <= TimeSpan.Zero || _options.EvaluationTimeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(options), "EvaluationTimeout must be positive and finite.");
        if (!Enum.IsDefined(_options.EvaluationMode))
            throw new ArgumentOutOfRangeException(nameof(options), "Unknown reflection evaluation mode.");
        if (_options.MaxThoughtChars is < 1 or > 300)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxThoughtChars must be in 1..300, matching the simulation's thought limit.");
        _layaEndpoint = Endpoint(_options.LayaBaseUrl, "systemone");
        _generationEndpoint = Endpoint(_options.GenerationBaseUrl, "chat/completions");
        if (_generationEndpoint is not null && _layaEndpoint is null)
            throw new ArgumentException("Generation requires a configured Laya evaluator.", nameof(options));
        if (_generationEndpoint is not null && string.IsNullOrWhiteSpace(_options.GenerationModel))
            throw new ArgumentException("GenerationModel is required for a generation endpoint.", nameof(options));
        _http = new HttpClient(handler, disposeHandler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequest(request);
        ReflectionAnswer baseline = await _authored.ReflectAsync(request, cancellationToken);
        if (_layaEndpoint is null)
            return baseline with { Note = "No Laya endpoint configured; authored baseline, no model calls." };

        string thought = baseline.Thought;
        string suggestedChoice = baseline.SuggestedChoice;
        string? generationPrompt = null;
        string? generationResponse = null;
        var evaluations = new List<ReflectionEvaluation>();
        string stage = "Laya";
        try
        {
            IReadOnlyList<EvaluationPass> passes = BuildPasses(request);
            // Do not spend a generation call on a choice packet that cannot fit even
            // before a thought is added. A generated thought is checked again below.
            foreach (EvaluationPass pass in passes) _ = BuildLayaPrompt(pass.Request, string.Empty);
            if (_generationEndpoint is not null)
            {
                stage = "Local generation";
                // Canonical modes also keep the generator's submitted option labels/order
                // independent of caller IDs. Generate once, then evaluate that fixed thought.
                ReflectionRequest generationRequest = passes[0].Request;
                generationPrompt = BuildGenerationPrompt(generationRequest);
                using JsonDocument generated = await PostAsync(_generationEndpoint, generationPrompt, cancellationToken,
                    text => generationResponse = text);
                generationResponse = generated.RootElement.GetRawText();
                (thought, string wireSuggestion) = ParseGeneration(generated.RootElement, generationRequest);
                suggestedChoice = passes[0].LabelsToChoiceIds[wireSuggestion][0];
            }
            stage = "Laya";
            IReadOnlyDictionary<string, double> weights = await EvaluateAsync(request, passes, thought, evaluations, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ReflectionEvaluation last = evaluations[^1];
            return new ReflectionAnswer(thought, suggestedChoice, weights,
                _generationEndpoint is null ? "authored+laya" : "local-llm+laya",
                (_generationEndpoint is null ? "Authored thought; " : "Local model imagined the thought; ")
                    + $"Laya evaluated the candidate lines ({_options.EvaluationMode}, {passes.Count} pass(es)).",
                last.Prompt, generationPrompt, last.Response, evaluations.AsReadOnly(), generationResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or IOException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return baseline with
            {
                Backend = "authored-fallback",
                Note = $"{stage} failed: {ex.Message} Authored fallback replaced the model proposal and weights.",
                LayaPrompt = evaluations.LastOrDefault()?.Prompt,
                GenerationPrompt = generationPrompt,
                GenerationResponse = generationResponse,
                LayaResponse = evaluations.LastOrDefault()?.Response,
                Evaluations = evaluations.Count == 0 ? null : evaluations.AsReadOnly(),
            };
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed record EvaluationPass(ReflectionRequest Request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> LabelsToChoiceIds);

    private IReadOnlyList<EvaluationPass> BuildPasses(ReflectionRequest request)
    {
        if (_options.EvaluationMode == ReflectionEvaluationMode.Raw)
            return new[] { new EvaluationPass(request, request.Choices.ToDictionary(c => c.Id,
                c => (IReadOnlyList<string>)Array.AsReadOnly(new[] { c.Id }), StringComparer.Ordinal)) };

        // Duplicate descriptions are one semantic alternative, not extra chances to be picked.
        // Split its eventual mass equally between all equivalent original IDs. Original IDs
        // are used only in receipts and never break ordering ties or enter these prompts.
        var groups = request.Choices.GroupBy(c => (c.Kind, c.Line))
            .OrderBy(g => g.Key.Kind, StringComparer.Ordinal).ThenBy(g => g.Key.Line, StringComparer.Ordinal)
            .Select(g => (g.Key.Kind, g.Key.Line, Ids: (IReadOnlyList<string>)Array.AsReadOnly(g.Select(c => c.Id)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray()))).ToArray();
        int count = _options.EvaluationMode == ReflectionEvaluationMode.Balanced ? groups.Length : 1;
        var result = new List<EvaluationPass>(count);
        for (int rotation = 0; rotation < count; rotation++)
        {
            var choices = new List<ReflectionChoice>(groups.Length);
            var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int slot = 0; slot < groups.Length; slot++)
            {
                var group = groups[(slot + rotation) % groups.Length];
                string label = "o" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
                choices.Add(new ReflectionChoice(label, group.Kind, group.Line));
                map.Add(label, group.Ids);
            }
            result.Add(new EvaluationPass(request with { Choices = choices.AsReadOnly() }, map));
        }
        return result;
    }

    private async Task<IReadOnlyDictionary<string, double>> EvaluateAsync(ReflectionRequest original,
        IReadOnlyList<EvaluationPass> passes, string thought, List<ReflectionEvaluation> receipts,
        CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(_options.EvaluationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            // Validate every complete packet before making the first evaluator call.
            string[] prompts = passes.Select(pass => BuildLayaPrompt(pass.Request, thought)).ToArray();
            for (int i = 0; i < passes.Count; i++)
            {
                linked.Token.ThrowIfCancellationRequested();
                EvaluationPass pass = passes[i];
                string? response = null;
                try
                {
                    using JsonDocument evaluated = await PostAsync(_layaEndpoint!, prompts[i], linked.Token,
                        text => response = text);
                    CheckTruncation(evaluated.RootElement);
                    IReadOnlyDictionary<string, double> wireWeights = ParseWeights(evaluated.RootElement, pass.Request);
                    var mapped = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var (label, ids) in pass.LabelsToChoiceIds)
                        foreach (string id in ids) mapped.Add(id, wireWeights[label] / ids.Count);
                    linked.Token.ThrowIfCancellationRequested();
                    receipts.Add(new ReflectionEvaluation(i + 1, prompts[i], response, pass.LabelsToChoiceIds, mapped));
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or IOException or TimeoutException or OperationCanceledException)
                {
                    receipts.Add(new ReflectionEvaluation(i + 1, prompts[i], response, pass.LabelsToChoiceIds, Error: ex.Message));
                    throw;
                }
            }
            linked.Token.ThrowIfCancellationRequested();
            if (receipts.Count == 1) return receipts[0].Weights!;
            // Average already normalized passes, rather than averaging raw scores whose
            // scale can vary with the input ordering. No preferred social result is imposed.
            var average = original.Choices.ToDictionary(c => c.Id,
                c => receipts.Sum(r => r.Weights![c.Id] / receipts.Count), StringComparer.Ordinal);
            double total = average.Values.Sum();
            return original.Choices.ToDictionary(c => c.Id, c => average[c.Id] / total, StringComparer.Ordinal);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"Laya {_options.EvaluationMode} evaluation timed out after {_options.EvaluationTimeout.TotalMilliseconds:0} ms; all {passes.Count} passes are required.");
        }
    }

    private string BuildGenerationPrompt(ReflectionRequest request)
    {
        string instructions = "Write the actor's private thought in first person, using I or my: one short, natural sentence they might think to themselves. "
            + "Imagine a concrete next thing they could do toward the subject, connected to the supplied memory and their current feelings. "
            + "It is an uncertain future possibility, not an act already done or a settled decision. "
            + "Write silent self-talk, not dialogue addressed to the subject. The supplied lines show available acts: do not copy them as the thought. "
            + "Use the subject's name when their pronouns are not supplied. "
            + "Let their context shape the motive and voice; do not narrate or analyze the character, recite personality traits or numbers, or mention choices and evaluation. "
            + "Ground the thought only in the supplied scene. Do not invent people, past events, executable acts, or another person's hidden intentions. "
            + "This thought and any earlier imagined idea are imagination, not evidence. "
            + "Return only a JSON object with two string fields: thought and suggestedChoice. "
            + $"thought must be 1..{_options.MaxThoughtChars} characters. suggestedChoice must be one of the provided executable choice IDs. "
            + "suggestedChoice identifies the act imagined in the thought; its eventual acceptance is decided separately.";
        // Context is bounded independently of the response budget; authored lines are never cut.
        var context = new
        {
            actor = request.Actor,
            subject = request.Subject,
            context = Cut(request.Context, 1000),
            memory = Cut(request.Memory, 1000),
            earlierImaginedIdea = request.Continuity?.PriorThought,
            reasonToReconsider = request.Continuity?.Reason,
            opportunity = request.Opportunity?.Kind ?? "quiet",
            choices = request.Choices.Where(c => c.Kind.Length > 0).Select(c => new { id = c.Id, kind = c.Kind, line = c.Line }),
        };
        return JsonSerializer.Serialize(new
        {
            model = _options.GenerationModel,
            messages = new[]
            {
                new { role = "system", content = instructions },
                new { role = "user", content = JsonSerializer.Serialize(context) },
            },
            temperature = 0.7,
            seed = BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(request.Id))) & int.MaxValue,
            max_tokens = 180,
            stream = false,
            response_format = new { type = "json_object" },
        });
    }

    private string BuildLayaPrompt(ReflectionRequest request, string thought)
    {
        // The user's 1250-character working limit counts all semantic text: state,
        // instructions, labels and descriptions. JSON punctuation/field names are not text.
        // Preserve all candidate IDs, act kinds and actual lines. Reserve both character
        // context and known memory before allocating any remaining text space.
        var criteria = request.Choices.ToDictionary(c => c.Id, c => $"{(c.Kind.Length == 0 ? "no act" : c.Kind)}: {c.Line}", StringComparer.Ordinal);
        // Keep the proposed action ID for the record, not as an answer hint to the evaluator.
        // The imagined thought itself supplies the possibility the character is considering.
        string state = $"Person: {request.Actor}. About: {request.Subject}.\nImagined thought: {thought}";
        if (request.Opportunity is { Kind: "dream" })
            state += "\nOccasion: waking from sleep; the dream idea is imagination, not evidence.";
        if (request.Continuity is { } continuity)
            state += $"\nEarlier imagined idea (not evidence): {continuity.PriorThought}\nReason to reconsider: {continuity.Reason}";
        int fixedChars = Instructions.Length + criteria.Sum(c => c.Key.Length + c.Value.Length) + state.Length;
        // A long thought must not crowd out the actual event that prompted reflection.
        // An overlong required packet falls back rather than silently losing its grounding.
        int requiredContext = Math.Min(request.Context.Length, 100);
        int requiredMemory = Math.Min(request.Memory.Length, 100);
        const string contextLabel = "\nCharacter context: ";
        const string memoryLabel = "\nKnown memory: ";
        int available = LayaTextLimit - fixedChars - contextLabel.Length - memoryLabel.Length;
        if (available < requiredContext + requiredMemory)
            throw new InvalidDataException($"Required context, memory, thought (including any earlier imagined idea) and complete candidate lines exceed Laya's {LayaTextLimit}-character limit.");
        string context = Cut(request.Context, Math.Min(260, available - requiredMemory));
        string memory = Cut(request.Memory, available - context.Length);
        state += contextLabel + context + memoryLabel + memory;
        var question = new { type = "choice", instructions = Instructions, criteria };
        return JsonSerializer.Serialize(new
        {
            state,
            model = _options.LayaModel,
            questions = new Dictionary<string, object> { ["q"] = question },
        });
    }

    private (string Thought, string SuggestedChoice) ParseGeneration(JsonElement root, ReflectionRequest request)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0
            || choices[0].ValueKind != JsonValueKind.Object || !choices[0].TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Generation response has no choices[0].message.content string.");
        using JsonDocument document = JsonDocument.Parse(content.GetString()!);
        JsonElement proposal = document.RootElement;
        if (proposal.ValueKind != JsonValueKind.Object || proposal.EnumerateObject().Count() != 2
            || !proposal.TryGetProperty("thought", out var thoughtValue) || thoughtValue.ValueKind != JsonValueKind.String
            || !proposal.TryGetProperty("suggestedChoice", out var suggestedValue) || suggestedValue.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Generated content must contain exactly thought and suggestedChoice strings.");
        string thought = thoughtValue.GetString()!.Trim();
        string suggested = suggestedValue.GetString()!;
        if (thought.Length == 0 || thought.Length > _options.MaxThoughtChars)
            throw new InvalidDataException($"Generated thought must be 1..{_options.MaxThoughtChars} characters.");
        if (!request.Choices.Any(c => c.Id == suggested && c.Kind.Length > 0))
            throw new InvalidDataException("Generated suggestedChoice is not a permitted executable choice.");
        return (thought, suggested);
    }

    private static void CheckTruncation(JsonElement root)
    {
        // Character budgeting cannot guarantee a particular server's tokenization. Refuse an
        // explicitly truncated reading and retain the receipt instead of treating it as complete.
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("usage", out var usage)
            || usage.ValueKind != JsonValueKind.Object) return;
        if (usage.TryGetProperty("truncated", out var truncated) && truncated.ValueKind == JsonValueKind.True
            || usage.TryGetProperty("state_tokens_dropped", out var dropped) && dropped.ValueKind == JsonValueKind.Number && dropped.TryGetInt32(out int count) && count > 0
            || usage.TryGetProperty("truncated_questions", out var questions) && questions.ValueKind == JsonValueKind.Array && questions.GetArrayLength() > 0)
            throw new InvalidDataException("Laya reported truncated input; the complete scene and lines were not evaluated.");
    }

    private static IReadOnlyDictionary<string, double> ParseWeights(JsonElement root, ReflectionRequest request)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("answers", out var answers)
            || answers.ValueKind != JsonValueKind.Object || !answers.TryGetProperty("q", out var answer)
            || answer.ValueKind != JsonValueKind.Object || !answer.TryGetProperty("probabilities", out var probabilities)
            || probabilities.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Laya response has no answers.q.probabilities object.");
        var expected = request.Choices.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (JsonProperty entry in probabilities.EnumerateObject())
        {
            if (!expected.Contains(entry.Name) || result.ContainsKey(entry.Name)
                || entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetDouble(out double weight)
                || !double.IsFinite(weight) || weight < 0)
                throw new InvalidDataException("Laya probabilities contain an unknown/duplicate choice or a non-finite/negative weight.");
            result.Add(entry.Name, weight);
        }
        if (result.Count != expected.Count)
            throw new InvalidDataException("Laya probabilities omit a candidate choice.");
        double maximum = result.Values.Max();
        if (maximum <= 0)
            throw new InvalidDataException("Laya probabilities are all zero.");
        // Scale before summing so even very large finite weights cannot overflow.
        double scaledTotal = result.Values.Sum(v => v / maximum);
        return request.Choices.ToDictionary(c => c.Id, c => (result[c.Id] / maximum) / scaledTotal, StringComparer.Ordinal);
    }

    private async Task<JsonDocument> PostAsync(Uri endpoint, string body, CancellationToken cancellationToken,
        Action<string>? responseReceived = null)
    {
        using var deadline = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            using HttpResponseMessage response = await _http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (response.Content.Headers.ContentLength > ResponseByteLimit)
                throw new InvalidDataException($"Response exceeds {ResponseByteLimit} bytes.");
            using Stream stream = await response.Content.ReadAsStreamAsync(linked.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                int read = await stream.ReadAsync(chunk.AsMemory(), linked.Token);
                if (read == 0) break;
                if (buffer.Length + read > ResponseByteLimit)
                    throw new InvalidDataException($"Response exceeds {ResponseByteLimit} bytes.");
                buffer.Write(chunk, 0, read);
            }
            linked.Token.ThrowIfCancellationRequested();
            string text = Encoding.UTF8.GetString(buffer.ToArray());
            responseReceived?.Invoke(text);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}.");
            return JsonDocument.Parse(text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"Request timed out after {_options.Timeout.TotalMilliseconds:0} ms.");
        }
    }

    private static Uri? Endpoint(string? baseUrl, string suffix)
    {
        if (baseUrl is null) return null;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https") || !uri.IsLoopback
            || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            throw new ArgumentException("Model endpoints must be explicit loopback HTTP(S) base URLs without credentials, query, or fragment.");
        string prefix = uri.AbsoluteUri.TrimEnd('/');
        return new Uri(prefix + (prefix.EndsWith("/v1", StringComparison.Ordinal) ? "/" : "/v1/") + suffix);
    }

    private static void ValidateRequest(ReflectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Choices.Count is < 1 or > 100 || !request.Choices.Any(c => c.Kind.Length > 0)
            || request.Choices.Any(c => string.IsNullOrWhiteSpace(c.Id))
            || request.Choices.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != request.Choices.Count)
            throw new ArgumentException("Reflection needs 1..100 uniquely named choices, including an executable choice.", nameof(request));
    }

    private static string Cut(string value, int maximum)
    {
        if (value.Length <= maximum) return value;
        if (maximum <= 0) return string.Empty;
        int end = maximum - 1;
        if (end > 0 && char.IsHighSurrogate(value[end - 1])) end--;
        return value[..end] + "…";
    }
}
