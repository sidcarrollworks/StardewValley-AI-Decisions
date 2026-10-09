using System.Text;
using System.Text.Json;
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
        string? layaPrompt = null;
        string stage = "Laya";
        try
        {
            // Do not spend a generation call on a choice packet that cannot fit even
            // before a thought is added. A generated thought is checked again below.
            _ = BuildLayaPrompt(request, string.Empty, suggestedChoice);
            if (_generationEndpoint is not null)
            {
                stage = "Local generation";
                generationPrompt = BuildGenerationPrompt(request);
                using JsonDocument generated = await PostAsync(_generationEndpoint, generationPrompt, cancellationToken);
                (thought, suggestedChoice) = ParseGeneration(generated.RootElement, request);
            }
            stage = "Laya";
            layaPrompt = BuildLayaPrompt(request, thought, suggestedChoice);
            using JsonDocument evaluated = await PostAsync(_layaEndpoint, layaPrompt, cancellationToken);
            IReadOnlyDictionary<string, double> weights = ParseWeights(evaluated.RootElement, request);
            cancellationToken.ThrowIfCancellationRequested();
            return new ReflectionAnswer(thought, suggestedChoice, weights,
                _generationEndpoint is null ? "authored+laya" : "local-llm+laya",
                _generationEndpoint is null ? "Authored thought; Laya evaluated the candidate lines." : "Local model imagined the thought; Laya evaluated the candidate lines.",
                layaPrompt, generationPrompt);
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
                LayaPrompt = layaPrompt,
                GenerationPrompt = generationPrompt,
            };
        }
    }

    public void Dispose() => _http.Dispose();

    private string BuildGenerationPrompt(ReflectionRequest request)
    {
        string instructions = "Imagine one possible intention for this character, grounded only in their supplied memory and context. "
            + "This is an imagined thought, never a new witnessed fact. Do not invent people, past events, or executable acts. "
            + "Return only a JSON object with two string fields: thought and suggestedChoice. "
            + $"thought must be 1..{_options.MaxThoughtChars} characters. suggestedChoice must be one of the provided executable choice IDs. "
            + "Do not choose the response for them: the character will evaluate your suggestion separately.";
        // Context is bounded independently of the response budget; authored lines are never cut.
        var context = new
        {
            actor = request.Actor,
            subject = request.Subject,
            context = Cut(request.Context, 1000),
            memory = Cut(request.Memory, 1000),
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
            temperature = 0.8,
            max_tokens = 180,
            stream = false,
            response_format = new { type = "json_object" },
        });
    }

    private string BuildLayaPrompt(ReflectionRequest request, string thought, string suggestedChoice)
    {
        // The user's 1250-character working limit counts all semantic text: state,
        // instructions, labels and descriptions. JSON punctuation/field names are not text.
        // Preserve all candidate IDs, act kinds and actual lines. Reserve both character
        // context and known memory before allocating any remaining text space.
        var criteria = request.Choices.ToDictionary(c => c.Id, c => $"{(c.Kind.Length == 0 ? "no act" : c.Kind)}: {c.Line}", StringComparer.Ordinal);
        string state = $"Person: {request.Actor}. About: {request.Subject}.\nImagined thought: {thought}\nSuggested choice: {suggestedChoice}.";
        int fixedChars = Instructions.Length + criteria.Sum(c => c.Key.Length + c.Value.Length) + state.Length;
        // A long thought must not crowd out the actual event that prompted reflection.
        // An overlong required packet falls back rather than silently losing its grounding.
        int requiredContext = Math.Min(request.Context.Length, 100);
        int requiredMemory = Math.Min(request.Memory.Length, 100);
        const string contextLabel = "\nCharacter context: ";
        const string memoryLabel = "\nKnown memory: ";
        int available = LayaTextLimit - fixedChars - contextLabel.Length - memoryLabel.Length;
        if (available < requiredContext + requiredMemory)
            throw new InvalidDataException($"Required context, memory, thought and complete candidate lines exceed Laya's {LayaTextLimit}-character limit.");
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

    private async Task<JsonDocument> PostAsync(Uri endpoint, string body, CancellationToken cancellationToken)
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
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}.");
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
            return JsonDocument.Parse(buffer.ToArray());
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
