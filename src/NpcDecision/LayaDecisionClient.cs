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
/// <para>
/// Batching (<see cref="IBatchDecisionClient"/>): one POST carries every question under its caller
/// id (<c>questions: {id: question}</c>); answers are matched by id, and an id the server leaves
/// out is simply absent from the result (the wrapper fills the gap). Single-question calls are one
/// batch of one, under the fixed id <c>q</c>.
/// </para>
/// </summary>
public sealed class LayaDecisionClient : IDecisionClient, IBatchDecisionClient, IDisposable
{
    /// <summary>Verified HTTP guard: at most 100 options per choice question (413 above it).</summary>
    public const int MaxChoiceOptions = 100;

    /// <summary>Ascending level texts for <see cref="Score"/>; the answer's expected index is in [0, 4].</summary>
    internal static readonly string[] ScoreLevels = { "very low", "low", "medium", "high", "very high" };

    internal const string ScoreInstructions = "How strongly does this apply?";
    private const string QuestionId = "q";

    private readonly LayaOptions _options;
    private readonly HttpClient _http;
    private CancellationToken _budget;
    private readonly string _baseUrl;

    // Call counters and latency samples for the heartbeat (docs/spec/laya.md, "Sidecar
    // lifecycle"): thread-safe; the mod reads them off the game thread.
    private long _calls;
    private long _callFallbacks;
    private readonly object _latencyLock = new();
    private readonly List<double> _latencyMs = new();

    /// <summary>How many question calls were sent (a batch counts once).</summary>
    public long Calls => Interlocked.Read(ref _calls);

    /// <summary>How many question calls failed (timeouts, HTTP errors, malformed answers).</summary>
    public long CallFallbacks => Interlocked.Read(ref _callFallbacks);

    /// <summary>The median and p95 of the last 128 call latencies in milliseconds (0, 0 with no
    /// calls yet). Thread-safe snapshot.</summary>
    public (double MedianMs, double P95Ms) Latency()
    {
        double[] snapshot;
        lock (_latencyLock)
            snapshot = _latencyMs.ToArray();
        if (snapshot.Length == 0)
            return (0.0, 0.0);
        Array.Sort(snapshot);
        double median = snapshot[snapshot.Length / 2];
        double p95 = snapshot[Math.Min(snapshot.Length - 1, (int)(snapshot.Length * 0.95))];
        return (median, p95);
    }

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

    /// <summary>
    /// Swap the external budget token mid-flight (docs/spec/laya.md, "Pass the budget token"): the
    /// planning wrapper sets the session's budget before the plan job starts, so a cancelled budget
    /// aborts the in-flight HTTP call instead of only the wrapper's wait. Set before any call of
    /// that session; not safe to race with a running call.
    /// </summary>
    public void SetBudget(CancellationToken budget) => _budget = budget;

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
        var question = new ChoiceQuestion(QuestionId, options);
        return ParseAnswer(question, AskSingle(context, question)).Probabilities!;
    }

    public double Score(string context, double min, double max)
    {
        var question = new ScoreQuestion(QuestionId, min, max);
        return ParseAnswer(question, AskSingle(context, question)).Score!.Value;
    }

    public double YesNo(string context, string proposition)
    {
        var question = new YesNoQuestion(QuestionId, proposition);
        return ParseAnswer(question, AskSingle(context, question)).YesNo!.Value;
    }

    /// <summary>
    /// One <c>POST /v1/systemone</c> carrying the state and every question under its caller id
    /// (<c>questions: {id: question}</c>). Answers come back in the questions' order, matched by
    /// id; an id the server leaves out is absent from the result, not an error. Duplicate ids or
    /// more than <see cref="MaxChoiceOptions"/> options on a choice question throw before any
    /// request; an empty question list returns an empty result without a request.
    /// </summary>
    public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
    {
        if (questions is null)
            throw new ArgumentNullException(nameof(questions));
        if (questions.Count == 0)
            return Array.Empty<Answer>();
        Validate(questions);

        JsonElement answers = AskForAnswers(PrepareState(state), questions);

        var result = new List<Answer>(questions.Count);
        foreach (Question question in questions)
        {
            // An id the server skipped (or answered with something that is not an object) is
            // simply absent from the result; the resilient wrapper fills the gap.
            if (!answers.TryGetProperty(question.Id, out JsonElement answer)
                || answer.ValueKind != JsonValueKind.Object)
                continue;
            result.Add(ParseAnswer(question, answer));
        }
        return result;
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

    /// <summary>Rejects overlapping ids and choice questions the server would refuse (all before any request).</summary>
    private static void Validate(IReadOnlyList<Question> questions)
    {
        var seen = new HashSet<string>();
        foreach (Question question in questions)
        {
            if (!seen.Add(question.Id))
                throw new ArgumentException($"Duplicate question id '{question.Id}'.", nameof(questions));
            if (question is ChoiceQuestion choice)
            {
                if (choice.Options is null)
                    throw new ArgumentNullException(nameof(questions), "A choice question's options must not be null.");
                if (choice.Options.Count > MaxChoiceOptions)
                    throw new ArgumentException(
                        $"Laya accepts at most {MaxChoiceOptions} options per choice question (got {choice.Options.Count}).",
                        nameof(questions));
            }
        }
    }

    /// <summary>POSTs one body carrying all the questions and returns the response's <c>answers</c> object.</summary>
    private JsonElement AskForAnswers(string state, IReadOnlyList<Question> questions)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            JsonElement answers = SendForAnswers(state, questions);
            RecordLatency(watch.Elapsed.TotalMilliseconds, failed: false);
            return answers;
        }
        catch
        {
            RecordLatency(watch.Elapsed.TotalMilliseconds, failed: true);
            throw;
        }
    }

    private void RecordLatency(double ms, bool failed)
    {
        Interlocked.Increment(ref _calls);
        if (failed)
            Interlocked.Increment(ref _callFallbacks);
        lock (_latencyLock)
        {
            _latencyMs.Add(ms);
            if (_latencyMs.Count > 128)
                _latencyMs.RemoveAt(0);
        }
    }

    /// <summary>The raw POST: one body carrying all the questions, answers object out.</summary>
    private JsonElement SendForAnswers(string state, IReadOnlyList<Question> questions)
    {
        byte[] body = BuildBody(state, questions);

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
            || answers.ValueKind != JsonValueKind.Object)
            throw new LayaException("Laya response has no 'answers' object.");
        return answers.Clone();
    }

    /// <summary>One question posted under the fixed id <c>q</c>: the single-question call shape.</summary>
    private JsonElement AskSingle(string context, Question question)
    {
        JsonElement answers = AskForAnswers(PrepareState(context), new[] { question });
        if (!answers.TryGetProperty(QuestionId, out JsonElement answer) || answer.ValueKind != JsonValueKind.Object)
            throw new LayaException("Laya response has no answer for the question.");
        return answer;
    }

    /// <summary>Turns one <c>answers.{id}</c> object into a typed answer for its question.</summary>
    private static Answer ParseAnswer(Question question, JsonElement answer)
    {
        switch (question)
        {
            case ChoiceQuestion choice:
            {
                if (!answer.TryGetProperty("probabilities", out JsonElement probs) || probs.ValueKind != JsonValueKind.Object)
                    throw new LayaException("Laya choice answer has no 'probabilities' object.");

                var result = new double[choice.Options.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    if (probs.TryGetProperty(Label(i), out JsonElement p))
                        result[i] = Clamp01(ReadNumber(p, "probabilities." + Label(i)));
                }
                return Answer.FromChoice(choice.Id, result);
            }
            case ScoreQuestion score:
            {
                if (!answer.TryGetProperty("score", out JsonElement scoreEl))
                    throw new LayaException("Laya score answer has no 'score'.");
                double top = ScoreLevels.Length - 1;
                double s = Math.Clamp(ReadNumber(scoreEl, "score"), 0.0, top);
                return Answer.FromScore(score.Id, score.Min + (score.Max - score.Min) * (s / top));
            }
            case YesNoQuestion yesNo:
            {
                if (!answer.TryGetProperty("noul", out JsonElement noul))
                    throw new LayaException("Laya noul answer has no 'noul'.");
                return Answer.FromYesNo(yesNo.Id, Clamp01(ReadNumber(noul, "noul")));
            }
            default:
                throw new ArgumentException($"Unknown question type {question.GetType().Name}.", nameof(question));
        }
    }

    /// <summary>Writes one question body (type, instructions, criteria); shared by singles and batches.</summary>
    private static void WriteQuestion(Utf8JsonWriter w, Question question)
    {
        switch (question)
        {
            case ChoiceQuestion choice:
                w.WriteString("type", "choice");
                w.WriteString("instructions", "Which option fits best?");
                w.WriteStartObject("criteria");
                for (int i = 0; i < choice.Options.Count; i++)
                    w.WriteString(Label(i), choice.Options[i] ?? string.Empty);
                w.WriteEndObject();
                break;
            case ScoreQuestion:
                w.WriteString("type", "score");
                w.WriteString("instructions", ScoreInstructions);
                w.WriteStartArray("criteria");
                foreach (string level in ScoreLevels)
                    w.WriteStringValue(level);
                w.WriteEndArray();
                break;
            case YesNoQuestion yesNo:
                w.WriteString("type", "noul");
                w.WriteString("instructions", yesNo.Proposition ?? string.Empty);
                break;
            default:
                throw new ArgumentException($"Unknown question type {question.GetType().Name}.", nameof(question));
        }
    }

    /// <summary>The synthetic label for option <paramref name="index"/> (o0..oN), unique even for repeated texts.</summary>
    private static string Label(int index) => "o" + index;

    private byte[] BuildBody(string state, IReadOnlyList<Question> questions)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("state", state);
            if (!string.IsNullOrEmpty(_options.Model))
                w.WriteString("model", _options.Model);
            w.WriteStartObject("questions");
            foreach (Question question in questions)
            {
                w.WriteStartObject(question.Id);
                WriteQuestion(w, question);
                w.WriteEndObject();
            }
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
