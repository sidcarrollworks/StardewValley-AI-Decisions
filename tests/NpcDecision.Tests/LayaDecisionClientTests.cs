using System.Net;
using System.Text;
using System.Text.Json;
using NpcDecision;
using Xunit;

namespace NpcDecision.Tests;

public class LayaDecisionClientTests
{
    // ---- request shape ----

    [Fact]
    public void Choose_PostsChoiceQuestion_WithSyntheticLabelsAndOptionTexts()
    {
        var stub = StubHandler.Json(ChoiceAnswer(("o0", 0.2), ("o1", 0.7), ("o2", 0.1)));
        using var client = Client(stub);

        client.Choose(new[] { "go fishing", "go home", "go fishing" }, "Laya is bored");

        var req = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("http://127.0.0.1:8000/v1/systemone", req.Uri);
        Assert.StartsWith("application/json", req.ContentType);

        using var doc = JsonDocument.Parse(req.Body!);
        var root = doc.RootElement;
        Assert.Equal("Laya is bored", root.GetProperty("state").GetString());
        Assert.Equal("typed-decisions", root.GetProperty("model").GetString());

        var q = Assert.Single(root.GetProperty("questions").EnumerateObject()).Value;
        Assert.Equal("choice", q.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(q.GetProperty("instructions").GetString()));
        var criteria = q.GetProperty("criteria").EnumerateObject().ToArray();
        Assert.Equal(new[] { "o0", "o1", "o2" }, criteria.Select(c => c.Name));
        Assert.Equal(new[] { "go fishing", "go home", "go fishing" }, criteria.Select(c => c.Value.GetString()));
    }

    [Fact]
    public void Score_PostsScoreQuestion_WithFiveAscendingLevels()
    {
        var stub = StubHandler.Json(ScoreAnswer(2.0));
        using var client = Client(stub);

        client.Score("ctx", 0, 1);

        using var doc = JsonDocument.Parse(stub.Requests.Single().Body!);
        var q = doc.RootElement.GetProperty("questions").EnumerateObject().Single().Value;
        Assert.Equal("score", q.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(q.GetProperty("instructions").GetString()));
        Assert.Equal(new[] { "very low", "low", "medium", "high", "very high" },
            q.GetProperty("criteria").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void YesNo_PostsNoulQuestion_WithPropositionAsInstructions()
    {
        var stub = StubHandler.Json(NoulAnswer(0.6));
        using var client = Client(stub);

        client.YesNo("Bob insulted Laya", "Laya is upset with Bob");

        using var doc = JsonDocument.Parse(stub.Requests.Single().Body!);
        Assert.Equal("Bob insulted Laya", doc.RootElement.GetProperty("state").GetString());
        var q = doc.RootElement.GetProperty("questions").EnumerateObject().Single().Value;
        Assert.Equal("noul", q.GetProperty("type").GetString());
        Assert.Equal("Laya is upset with Bob", q.GetProperty("instructions").GetString());
        Assert.False(q.TryGetProperty("criteria", out _));
    }

    [Fact]
    public void Model_OmittedWhenNull_AndBaseUrlTrailingSlashHandled()
    {
        var stub = StubHandler.Json(NoulAnswer(0.5));
        using var client = Client(stub, new LayaOptions { Model = null, BaseUrl = "http://localhost:9000/laya/" });

        client.YesNo("ctx", "p");

        var req = stub.Requests.Single();
        Assert.Equal("http://localhost:9000/laya/v1/systemone", req.Uri);
        using var doc = JsonDocument.Parse(req.Body!);
        Assert.False(doc.RootElement.TryGetProperty("model", out _));
    }

    [Fact]
    public void BearerHeader_OnlyWhenApiKeySet()
    {
        var without = StubHandler.Json(NoulAnswer(0.5));
        using (var c = Client(without))
            c.YesNo("ctx", "p");
        Assert.Null(without.Requests.Single().Authorization);

        var with = StubHandler.Json(NoulAnswer(0.5));
        using (var c = Client(with, new LayaOptions { ApiKey = "sekrit" }))
            c.YesNo("ctx", "p");
        Assert.Equal("Bearer sekrit", with.Requests.Single().Authorization);
    }

    [Fact]
    public void State_IsTruncatedKeepingTheStart()
    {
        var stub = StubHandler.Json(NoulAnswer(0.5));
        using var client = Client(stub, new LayaOptions { MaxStateChars = 10 });

        client.YesNo("0123456789ABCDEF", "p");

        using var doc = JsonDocument.Parse(stub.Requests.Single().Body!);
        Assert.Equal("0123456789", doc.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void State_TruncationDoesNotSplitSurrogatePair()
    {
        var stub = StubHandler.Json(NoulAnswer(0.5));
        using var client = Client(stub, new LayaOptions { MaxStateChars = 4 });

        client.YesNo("abc\U0001F41Fxyz", "p"); // fish emoji straddles the cut

        using var doc = JsonDocument.Parse(stub.Requests.Single().Body!);
        Assert.Equal("abc", doc.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void State_NullContextSentAsEmptyString()
    {
        var stub = StubHandler.Json(NoulAnswer(0.5));
        using var client = Client(stub);

        client.YesNo(null!, "p");

        using var doc = JsonDocument.Parse(stub.Requests.Single().Body!);
        Assert.Equal("", doc.RootElement.GetProperty("state").GetString());
    }

    // ---- response parsing ----

    [Fact]
    public void Choose_ReturnsProbabilitiesInOptionOrder_MissingLabelIsZero()
    {
        // Server's key order differs from option order; o3 is missing.
        var stub = StubHandler.Json(ChoiceAnswer(("o2", 0.5), ("o0", 0.3), ("o1", 0.2)));
        using var client = Client(stub);

        var probs = client.Choose(new[] { "a", "b", "c", "d" }, "ctx");

        Assert.Equal(new[] { 0.3, 0.2, 0.5, 0.0 }, probs);
    }

    [Theory]
    [InlineData(0.0, 1, 5, 1.0)]
    [InlineData(4.0, 1, 5, 5.0)]
    [InlineData(2.0, 1, 5, 3.0)]
    [InlineData(1.0, -10, 10, -5.0)]
    [InlineData(2.6389, 0, 4, 2.6389)]
    [InlineData(9.0, 0, 1, 1.0)]   // out-of-range score clamped
    [InlineData(-1.0, 0, 1, 0.0)]
    public void Score_MapsExpectedLevelLinearlyOntoRange(double serverScore, double min, double max, double expected)
    {
        using var client = Client(StubHandler.Json(ScoreAnswer(serverScore)));
        Assert.Equal(expected, client.Score("ctx", min, max), 6);
    }

    [Theory]
    [InlineData(0.8727, 0.8727)]
    [InlineData(1.2, 1.0)]
    [InlineData(-0.1, 0.0)]
    public void YesNo_ReturnsNoulClamped(double noul, double expected)
    {
        using var client = Client(StubHandler.Json(NoulAnswer(noul)));
        Assert.Equal(expected, client.YesNo("ctx", "p"), 10);
    }

    // ---- no request / argument checks ----

    [Fact]
    public void Choose_EmptyOptions_MakesNoRequest()
    {
        var stub = StubHandler.Json(NoulAnswer(0.5));
        using var client = Client(stub);

        Assert.Empty(client.Choose(Array.Empty<string>(), "ctx"));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public void Choose_MoreThan100Options_Throws()
    {
        var stub = StubHandler.Json(NoulAnswer(0.5));
        using var client = Client(stub);

        var options = Enumerable.Range(0, 101).Select(i => "opt" + i).ToArray();
        Assert.Throws<ArgumentException>(() => client.Choose(options, "ctx"));
        Assert.Empty(stub.Requests);
    }

    // ---- failures ----

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void ErrorStatus_Throws(HttpStatusCode status)
    {
        using var client = Client(new StubHandler(_ => Respond(status, "{\"detail\":\"nope\"}")));

        var ex = Assert.Throws<LayaException>(() => client.YesNo("ctx", "p"));
        Assert.Equal((int)status, ex.StatusCode);
        Assert.Contains("nope", ex.Message);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"answers\":")]
    [InlineData("[]")]
    [InlineData("{\"answers\":{}}")]
    [InlineData("{\"answers\":{\"q\":{\"type\":\"noul\"}}}")]
    [InlineData("{\"answers\":{\"q\":{\"type\":\"noul\",\"noul\":\"high\"}}}")]
    public void MalformedOrIncompleteResponse_Throws(string body)
    {
        using var client = Client(StubHandler.Json(body));
        Assert.Throws<LayaException>(() => client.YesNo("ctx", "p"));
    }

    [Fact]
    public void Choose_MissingProbabilities_Throws()
    {
        using var client = Client(StubHandler.Json("{\"answers\":{\"q\":{\"type\":\"choice\",\"choice\":\"o0\"}}}"));
        Assert.Throws<LayaException>(() => client.Choose(new[] { "a", "b" }, "ctx"));
    }

    [Fact]
    public void TransportFailure_Throws()
    {
        using var client = Client(new StubHandler(_ => throw new HttpRequestException("connection refused")));
        Assert.Throws<LayaException>(() => client.YesNo("ctx", "p"));
    }

    [Fact]
    public void Timeout_CancelsTheCallAndThrows()
    {
        bool sawCancellation = false;
        var stub = new StubHandler(token =>
        {
            try
            {
                Task.Delay(TimeSpan.FromSeconds(30), token).Wait();
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                sawCancellation = true;
                token.ThrowIfCancellationRequested();
            }
            return Respond(HttpStatusCode.OK, NoulAnswer(0.9));
        });
        using var client = Client(stub, new LayaOptions { Timeout = TimeSpan.FromMilliseconds(100) });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = Assert.Throws<LayaException>(() => client.YesNo("ctx", "p"));
        sw.Stop();

        Assert.IsType<TimeoutException>(ex.InnerException);
        Assert.True(sawCancellation, "handler's token was not cancelled");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"call took {sw.Elapsed}");
    }

    [Fact]
    public void ExternalBudgetToken_CancelsWithOperationCanceled()
    {
        using var budget = new CancellationTokenSource();
        budget.Cancel();
        var stub = new StubHandler(token =>
        {
            token.ThrowIfCancellationRequested();
            return Respond(HttpStatusCode.OK, NoulAnswer(0.9));
        });
        using var client = new LayaDecisionClient(stub, new LayaOptions(), budget.Token);

        Assert.ThrowsAny<OperationCanceledException>(() => client.YesNo("ctx", "p"));
    }

    [Fact]
    public void Resilient_FallsBackOnLayaFailure()
    {
        using var laya = Client(new StubHandler(_ => Respond(HttpStatusCode.ServiceUnavailable, "{\"detail\":\"server busy, try again later\"}")));
        var resilient = new ResilientDecisionClient(laya);

        Assert.Equal(0.5, resilient.YesNo("ctx", "p"), 10);
        Assert.Equal(new[] { 0.5, 0.5 }, resilient.Choose(new[] { "a", "b" }, "ctx"));
    }

    // ---- health ----

    [Fact]
    public void IsHealthy_TrueOnStatusOk()
    {
        var stub = StubHandler.Json("{\"status\":\"ok\",\"loaded\":[\"typed-decisions\"],\"device\":\"auto\"}");
        using var client = Client(stub);

        Assert.True(client.IsHealthy());
        var req = stub.Requests.Single();
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("http://127.0.0.1:8000/health", req.Uri);
    }

    [Fact]
    public void IsHealthy_FalseOnErrorBadBodyOrTransportFailure()
    {
        using (var c = Client(new StubHandler(_ => Respond(HttpStatusCode.InternalServerError, "{}"))))
            Assert.False(c.IsHealthy());
        using (var c = Client(StubHandler.Json("{\"status\":\"starting\"}")))
            Assert.False(c.IsHealthy());
        using (var c = Client(StubHandler.Json("garbage")))
            Assert.False(c.IsHealthy());
        using (var c = Client(new StubHandler(_ => throw new HttpRequestException("refused"))))
            Assert.False(c.IsHealthy());
    }

    // ---- helpers ----

    private static LayaDecisionClient Client(StubHandler stub, LayaOptions? options = null)
        => new(stub, options ?? new LayaOptions());

    private static string ChoiceAnswer(params (string Label, double P)[] probs)
    {
        string p = string.Join(",", probs.Select(x => $"\"{x.Label}\":{x.P.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        string top = probs.OrderByDescending(x => x.P).First().Label;
        return $"{{\"model\":\"laya-rl-agent\",\"answers\":{{\"q\":{{\"type\":\"choice\",\"choice\":\"{top}\",\"probabilities\":{{{p}}},\"confidence\":0.4,\"answer_confidence\":0.7}}}},\"usage\":{{\"input_tokens\":10,\"output_tokens\":0}}}}";
    }

    private static string ScoreAnswer(double score)
        => "{\"model\":\"laya-rl-agent\",\"answers\":{\"q\":{\"type\":\"score\",\"score\":"
           + score.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"legend\":{\"0\":\"very low\",\"1\":\"low\",\"2\":\"medium\",\"3\":\"high\",\"4\":\"very high\"},\"probabilities\":{\"0\":0.2,\"1\":0.2,\"2\":0.2,\"3\":0.2,\"4\":0.2}}}}";

    private static string NoulAnswer(double noul)
        => "{\"model\":\"laya-rl-agent\",\"answers\":{\"q\":{\"type\":\"noul\",\"noul\":"
           + noul.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"confidence\":0.9,\"answer_confidence\":0.9}}}";

    [Fact]
    public void FailuresInARowAndTheLastReasonAreKept_ASuccessResetsTheCount()
    {
        // Playtest 2026-10-02: /health said ok while every question failed in 4 ms.
        bool fail = true;
        var stub = new StubHandler(_ => fail
            ? Respond(HttpStatusCode.InternalServerError, "{\"detail\":\"CUDA out of memory\"}")
            : Respond(HttpStatusCode.OK, "{\"answers\":{\"q\":{\"type\":\"noul\",\"noul\":0.7,\"confidence\":0.9,\"answer_confidence\":0.9}}}"));
        using var client = new LayaDecisionClient(stub, new LayaOptions());
        Assert.Null(client.LastError);
        for (int i = 0; i < 3; i++)
            Assert.ThrowsAny<Exception>(() => client.YesNo("npc: Sam", "is it?"));
        Assert.Equal(3, client.ConsecutiveFailures);
        Assert.Contains("500", client.LastError);

        fail = false;
        client.YesNo("npc: Sam", "is it?");
        Assert.Equal(0, client.ConsecutiveFailures);
        Assert.NotNull(client.LastError); // the newest failure stays readable
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record Captured(HttpMethod Method, string Uri, string? Authorization, string? ContentType, string? Body);

    /// <summary>Records requests; supports the synchronous Send path HttpClient.Send uses.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, HttpResponseMessage> _respond;
        public List<Captured> Requests { get; } = new();

        public StubHandler(Func<CancellationToken, HttpResponseMessage> respond) => _respond = respond;

        public static StubHandler Json(string body) => new(_ => Respond(HttpStatusCode.OK, body));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null
                ? null
                : new StreamReader(request.Content.ReadAsStream(cancellationToken), Encoding.UTF8).ReadToEnd();
            lock (Requests)
            {
                Requests.Add(new Captured(request.Method, request.RequestUri!.ToString(),
                    request.Headers.Authorization?.ToString(),
                    request.Content?.Headers.ContentType?.ToString(), body));
            }
            return _respond(cancellationToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}
