using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NpcDecision;

/// <summary>Raised when a Laya call fails: non-2xx status, timeout, or an unusable response.</summary>
public sealed class LayaException : Exception
{
    /// <summary>HTTP status, when the server answered with a non-2xx status.</summary>
    public int? StatusCode { get; }

    public LayaException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// <see cref="IDecisionClient"/> over a local <c>laya-serve</c> HTTP server (Laya v0.3.22,
/// github.com/NandhaKishorM/laya; model card huggingface.co/convaiinnovations/laya).
/// <para>
/// Verified from the repo (laya/serve.py, docs/http-api.md, docs/questions-and-answers.md at
/// 0.3.22): <c>POST /v1/systemone</c> takes <c>{"state": string, "questions": {id: question},
/// "model"?: string}</c>; question types are <c>choice</c> (<c>criteria</c> = ordered object
/// label -&gt; description, max 100 options), <c>score</c> (<c>criteria</c> = ascending list of
/// 2..32 level texts) and <c>noul</c> (yes/no, <c>instructions</c> only). Answers are under
/// <c>answers.{id}</c>: choice -&gt; <c>probabilities</c> keyed by label; score -&gt; <c>score</c>,
/// the expected level index in [0, k-1]; noul -&gt; <c>noul</c> = P(true). <c>GET /health</c>
/// returns <c>{"status":"ok",...}</c>. Errors carry <c>{"detail": ...}</c> with 400/401/413/422/500,
/// and 503 + Retry-After when over <c>LAYA_MAX_CONCURRENT</c> (refused, not queued).
/// </para>
/// <para>
/// Every call is synchronous (the game's decision interface is), uses <see cref="HttpClient.Send(HttpRequestMessage, CancellationToken)"/>
/// rather than blocking on async I/O, and is cancelled on <see cref="LayaOptions.Timeout"/> or
/// the optional external budget token. Failures throw (<see cref="LayaException"/>, or
/// <see cref="OperationCanceledException"/> when the external token fires); the caller
/// (<see cref="ResilientDecisionClient"/>) turns those into a deterministic fallback.
/// Thread-safe: one shared <see cref="HttpClient"/>, no mutable state.
/// </para>
/// </summary>
public sealed class LayaDecisionClient : IDecisionClient, IDisposable
{
    /// <summary>Verified HTTP guard: at most 100 options per choice question (413 above it).</summary>
    public const int MaxChoiceOptions = 100;

    /// <summary>Ascending level texts for <see cref="Score"/>; the answer's expected index is in [0, 4].</summary>
    internal static readonly string[] ScoreLevels = { "very low", "low", "medium", "high", "very high" };

    internal const string ScoreInstructions = "How strongly does this apply?";
    private const string QuestionId = "q";

    private readonly LayaOptions _options;
    private readonly HttpClient _http;
    private readonly CancellationToken _budget;
    private readonly string _baseUrl;

    /// <summary>Client using the default socket handler.</summary>
    public LayaDecisionClient(LayaOptions? options = null, CancellationToken budget = default)
        : this(new SocketsHttpHandler(), options, budget, disposeHandler: true)
    {
    }

    /// <summary>
    /// Client over a caller-supplied handler (tests, proxies). The handler must support the
    /// synchronous <c>Send</c> path (<see cref="SocketsHttpHandler"/> and
    /// <see cref="DelegatingHandler"/> chains over it do).
    /// </summary>
    public LayaDecisionClient(HttpMessageHandler handler, LayaOptions? options = null,
        CancellationToken budget = default, bool disposeHandler = true)
    {
        _options = options ?? new LayaOptions();
        if (_options.Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be positive.");
        _baseUrl = (_options.BaseUrl ?? throw new ArgumentNullException(nameof(options), "BaseUrl is required."))
            .TrimEnd('/');
        _budget = budget;
        // Our own CancellationTokenSource enforces the timeout; HttpClient's is disabled so there
        // is exactly one timer and one exception path.
        _http = new HttpClient(handler, disposeHandler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        if (options.Count == 0)
            return Array.Empty<double>();
        if (options.Count > MaxChoiceOptions)
            throw new ArgumentException(
                $"Laya accepts at most {MaxChoiceOptions} options per choice question (got {options.Count}).",
                nameof(options));

        // Synthetic labels o0..oN keep labels unique even when option texts repeat; the text
        // goes in the description, which is what the model reads.
        string[] labels = Enumerable.Range(0, options.Count).Select(i => "o" + i).ToArray();

        JsonElement answer = Ask(context, w =>
        {
            w.WriteString("type", "choice");
            w.WriteString("instructions", "Which option fits best?");
            w.WriteStartObject("criteria");
            for (int i = 0; i < labels.Length; i++)
                w.WriteString(labels[i], options[i] ?? string.Empty);
            w.WriteEndObject();
        });

        if (!answer.TryGetProperty("probabilities", out JsonElement probs) || probs.ValueKind != JsonValueKind.Object)
            throw new LayaException("Laya choice answer has no 'probabilities' object.");

        var result = new double[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            if (probs.TryGetProperty(labels[i], out JsonElement p))
                result[i] = Clamp01(ReadNumber(p, "probabilities." + labels[i]));
        }
        return result;
    }

    public double Score(string context, double min, double max)
    {
        JsonElement answer = Ask(context, w =>
        {
            w.WriteString("type", "score");
            w.WriteString("instructions", ScoreInstructions);
            w.WriteStartArray("criteria");
            foreach (string level in ScoreLevels)
                w.WriteStringValue(level);
            w.WriteEndArray();
        });

        if (!answer.TryGetProperty("score", out JsonElement scoreEl))
            throw new LayaException("Laya score answer has no 'score'.");
        double top = ScoreLevels.Length - 1;
        double s = Math.Clamp(ReadNumber(scoreEl, "score"), 0.0, top);
        return min + (max - min) * (s / top);
    }

    public double YesNo(string context, string proposition)
    {
        JsonElement answer = Ask(context, w =>
        {
            w.WriteString("type", "noul");
            w.WriteString("instructions", proposition ?? string.Empty);
        });

        if (!answer.TryGetProperty("noul", out JsonElement noul))
            throw new LayaException("Laya noul answer has no 'noul'.");
        return Clamp01(ReadNumber(noul, "noul"));
    }

    /// <summary>
    /// True when <c>GET /health</c> answers 2xx with <c>"status": "ok"</c> within the timeout.
    /// Never throws. Note the server reports ok before lazily-loaded checkpoints are in memory
    /// when <c>LAYA_PRELOAD=0</c>; the <c>loaded</c> list is not checked here.
    /// </summary>
    public bool IsHealthy()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/health");
            using JsonDocument doc = SendForJson(request);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("status", out JsonElement status)
                && status.ValueKind == JsonValueKind.String
                && status.GetString() == "ok";
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();

    /// <summary>State as sent: null becomes empty (the server 400s on null), then cut to <see cref="LayaOptions.MaxStateChars"/>.</summary>
    internal string PrepareState(string? context)
    {
        string state = context ?? string.Empty;
        int cap = _options.MaxStateChars;
        if (cap <= 0 || state.Length <= cap)
            return state;
        // Don't split a surrogate pair: the server refuses lone surrogates.
        int cut = cap;
        if (char.IsHighSurrogate(state[cut - 1]))
            cut--;
        return state.Substring(0, cut);
    }

    private JsonElement Ask(string context, Action<Utf8JsonWriter> writeQuestionBody)
    {
        byte[] body = BuildBody(PrepareState(context), writeQuestionBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/systemone")
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        if (!string.IsNullOrEmpty(_options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using JsonDocument doc = SendForJson(request);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("answers", out JsonElement answers)
            || answers.ValueKind != JsonValueKind.Object
            || !answers.TryGetProperty(QuestionId, out JsonElement answer)
            || answer.ValueKind != JsonValueKind.Object)
            throw new LayaException("Laya response has no answer for the question.");
        return answer.Clone();
    }

    private byte[] BuildBody(string state, Action<Utf8JsonWriter> writeQuestionBody)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("state", state);
            if (!string.IsNullOrEmpty(_options.Model))
                w.WriteString("model", _options.Model);
            w.WriteStartObject("questions");
            w.WriteStartObject(QuestionId);
            writeQuestionBody(w);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return buffer.ToArray();
    }

    /// <summary>Sends synchronously under the timeout + budget, returning the parsed 2xx body.</summary>
    private JsonDocument SendForJson(HttpRequestMessage request)
    {
        using var timeout = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _budget);
        try
        {
            // Default completion option buffers the whole body inside Send, so the token
            // covers headers and body alike.
            using HttpResponseMessage response = _http.Send(request, linked.Token);
            using Stream stream = response.Content.ReadAsStream(linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                throw new LayaException($"Laya returned HTTP {code}: {ReadDetail(stream)}", code);
            }

            try
            {
                return JsonDocument.Parse(stream);
            }
            catch (JsonException ex)
            {
                throw new LayaException("Laya returned malformed JSON.", (int)response.StatusCode, ex);
            }
        }
        catch (OperationCanceledException) when (_budget.IsCancellationRequested)
        {
            throw; // external budget: let the caller see a plain cancellation
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
        {
            throw new LayaException($"Laya call timed out after {_options.Timeout.TotalMilliseconds:0} ms.",
                inner: new TimeoutException(ex.Message, ex));
        }
        catch (HttpRequestException ex)
        {
            throw new LayaException("Laya request failed: " + ex.Message, inner: ex);
        }
    }

    private static string ReadDetail(Stream stream)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("detail", out JsonElement detail))
                return detail.ValueKind == JsonValueKind.String ? detail.GetString() ?? "" : detail.GetRawText();
        }
        catch
        {
            // not JSON; fall through
        }
        return "(no detail)";
    }

    private static double ReadNumber(JsonElement el, string what)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetDouble(out double v) || double.IsNaN(v) || double.IsInfinity(v))
            throw new LayaException($"Laya answer field '{what}' is not a finite number.");
        return v;
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
