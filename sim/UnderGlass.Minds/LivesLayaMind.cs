using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using UnderGlass.Sim.Lives;

namespace UnderGlass.Minds;

public sealed record LivesLayaOptions
{
    public string? BaseUrl { get; init; }
    public string Model { get; init; } = "laya";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>One typed evaluation of existing alternatives; never a generated line or extra draw.
/// The /v1/systemone transport follows the source-verified ResilientReflectionMind adapter.
/// The caller owns torn-choice eligibility and the two-calls-per-person-day budget.</summary>
public sealed class LivesLayaMind : ILivesDecisionMind, IDisposable
{
    public const int TextLimit = 1250;
    private const int ResponseLimit = 65536;
    private readonly LivesLayaOptions _options;
    private readonly HttpClient _http;
    private readonly Uri? _endpoint;
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public LivesLayaMind(LivesLayaOptions? options = null)
        : this(new SocketsHttpHandler { AllowAutoRedirect = false }, options) { }
    public LivesLayaMind(HttpMessageHandler handler, LivesLayaOptions? options = null, bool disposeHandler = true)
    {
        _options = options ?? new();
        if (string.IsNullOrWhiteSpace(_options.Model) || _options.Model.Length > 128
            || _options.Timeout <= TimeSpan.Zero || _options.Timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentException("A valid model identity and positive finite timeout are required.");
        if (_options.BaseUrl is { } baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                || !uri.IsLoopback || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
                throw new ArgumentException("Laya must use an explicit loopback HTTP(S) base URL without credentials, query or fragment.");
            string prefix = uri.AbsoluteUri.TrimEnd('/');
            _endpoint = new(prefix + (prefix.EndsWith("/v1", StringComparison.Ordinal) ? "/" : "/v1/") + "systemone");
        }
        _http = new(handler, disposeHandler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LivesDecisionEngine.Validate(request);
        if (request.Model != _options.Model) throw new ArgumentException("Configured Laya model differs from the request model identity.");
        if (_endpoint is null) return LivesDecisionEngine.Fallback(request, "No Laya endpoint configured; no model call.");
        LivesDecisionPacket? packet = null;
        string? response = null;
        string? reportedModel = null;
        bool called = false;
        using var deadline = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            packet = BuildPacket(request);
            using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            { Content = new StringContent(packet.Json, Encoding.UTF8, "application/json") };
            called = true;
            using var result = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            if (result.Content.Headers.ContentLength > ResponseLimit) throw new InvalidDataException("Laya response is too large.");
            using var stream = await result.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                int read = await stream.ReadAsync(chunk.AsMemory(), linked.Token).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > ResponseLimit) throw new InvalidDataException("Laya response is too large.");
                buffer.Write(chunk, 0, read);
            }
            response = Encoding.UTF8.GetString(buffer.ToArray());
            result.EnsureSuccessStatusCode();
            using var parsed = JsonDocument.Parse(response);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object
                && parsed.RootElement.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String)
                reportedModel = model.GetString();
            var weights = ParseWeights(parsed.RootElement, request, packet);
            linked.Token.ThrowIfCancellationRequested();
            return new(weights, "laya", request.Model, "One typed evaluation; engine keeps the original keyed draw.", packet, response, true, reportedModel);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidDataException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string reason = ex is OperationCanceledException ? "Laya evaluation timed out." : ex.Message;
            return LivesDecisionEngine.Fallback(request, reason, packet, response, called) with { Backend = "authored-fallback", ReportedModel = reportedModel };
        }
    }

    /// <summary>Full authored lines, every numeric/eligibility reason and constraint are mandatory.
    /// Recalled text is added whole, in its recorded rank order; omitted IDs are explicit. Technical
    /// citation IDs stay on the exact request/tape. The entire JSON request is <=1250 characters.</summary>
    public static LivesDecisionPacket BuildPacket(LivesDecisionRequest request)
    {
        LivesDecisionEngine.Validate(request);
        var decision = request.Fallback;
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var criteria = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < decision.Alternatives.Count; i++)
        {
            var alternative = decision.Alternatives[i];
            string label = "o" + i.ToString(CultureInfo.InvariantCulture);
            labels.Add(label, alternative.Option.Id);
            // Recall context is already supplied once for all alternatives. Repeated references
            // are citations, not additional warm/cold weight; keep each other reason separately.
            string reasons = string.Join(";", alternative.Reasons.Where(r => r.Via != ChoiceReasonVia.Recall)
                .Select(r => Reason(r)));
            criteria.Add(label, alternative.Option.Line + (reasons.Length > 0 ? " [" + reasons + "]" : ""));
        }
        string state = decision.Opportunity.Holder + ": " + request.Context;
        if (decision.Constraints.Count > 0)
            state += "\nConstraints: " + string.Join(";", decision.Constraints.Select(c => c.Code + (c.Value is { } v ? "=" + Number(v) : "")));
        const string instructions = "Weigh these actual lines for this person. Memories are their knowledge, not objective truth. Refusal or delay is valid. Return weights.";
        string Encode(string scene, int omitted) => JsonSerializer.Serialize(new
        {
            model = request.Model,
            state = scene + (omitted > 0 ? $"\n{omitted} recalled text(s) omitted; reasons retained." : ""),
            questions = new Dictionary<string, object> { ["q"] = new { type = "choice", instructions, criteria } },
        }, Json);
        var omittedIds = new List<string>();
        string mandatory = Encode(state, decision.Recall.Items.Count);
        if (mandatory.Length > TextLimit)
            throw new InvalidDataException($"Complete candidate lines, reasons and context exceed Laya's {TextLimit}-character request limit; no model call.");
        for (int i = 0; i < decision.Recall.Items.Count; i++)
        {
            var memory = decision.Recall.Items[i];
            string origin = memory.PreRun ? "before run" : "during run";
            string item = $"\nRecall ({origin}, {memory.Kind}, {memory.How}): {memory.Text}";
            // Count all not-yet-encoded memories as omitted until they fit. This reserves room
            // for the explicit notice and never cuts a memory mid-sentence.
            if (Encode(state + item, omittedIds.Count + decision.Recall.Items.Count - i - 1).Length <= TextLimit) state += item;
            else omittedIds.Add(memory.Id);
        }
        string json = Encode(state, omittedIds.Count);
        return new(json, json.Length, new ReadOnlyDictionary<string, string>(labels), Array.AsReadOnly(omittedIds.ToArray()));
    }

    private static string Reason(ChoiceReason r)
    {
        string sign = r.Kind == ChoiceReasonKind.Supports ? "+" : r.Kind == ChoiceReasonKind.Conflicts ? "-" : "";
        return sign + r.Code + (r.Value is { } value ? "=" + Number(value) : "");
    }
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, double> ParseWeights(JsonElement root, LivesDecisionRequest request, LivesDecisionPacket packet)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Laya response is not an object.");
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object
            && (usage.TryGetProperty("truncated", out var truncated) && truncated.ValueKind == JsonValueKind.True
                || usage.TryGetProperty("state_tokens_dropped", out var dropped) && dropped.ValueKind == JsonValueKind.Number && dropped.TryGetInt32(out int count) && count > 0
                || usage.TryGetProperty("truncated_questions", out var questions) && questions.ValueKind == JsonValueKind.Array && questions.GetArrayLength() > 0))
            throw new InvalidDataException("Laya reported truncated input.");
        if (!root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object
            || !answers.TryGetProperty("q", out var answer) || answer.ValueKind != JsonValueKind.Object
            || !answer.TryGetProperty("probabilities", out var probabilities) || probabilities.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Laya response has no answers.q.probabilities object.");
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var entry in probabilities.EnumerateObject())
        {
            if (!packet.Labels.TryGetValue(entry.Name, out string? id) || entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetDouble(out double weight)
                || !weights.TryAdd(id, weight)) throw new InvalidDataException("Laya returned unknown, duplicated or malformed weights.");
        }
        return LivesDecisionEngine.Normalize(request, weights);
    }

    public void Dispose() => _http.Dispose();
}
