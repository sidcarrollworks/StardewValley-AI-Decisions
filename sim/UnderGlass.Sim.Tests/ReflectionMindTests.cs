using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionMindTests
{
    private static ReflectionRequest Request(string? context = null, string? memory = null) => new(
        "day1-penny", 400, "Penny", "Pam", 7,
        memory ?? "Pam refused my apology yesterday.",
        context ?? "I care about Pam but dislike a public argument.",
        new[]
        {
            new ReflectionChoice("help", "Help", "I could help Pam before speaking about yesterday."),
            new ReflectionChoice("argue", "Argue", "You refused to hear me out."),
            new ReflectionChoice("later", "", "I need more time to think."),
            new ReflectionChoice("reject", "", "No. I will leave it alone."),
        });

    private static ReflectionMindOptions LocalOptions(bool generate = false) => new()
    {
        LayaBaseUrl = "http://127.0.0.1:8000",
        GenerationBaseUrl = generate ? "http://localhost:1234/v1" : null,
        GenerationModel = "test-model",
    };

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private const string ValidWeights = "{\"answers\":{\"q\":{\"probabilities\":{\"help\":0.4,\"argue\":0.1,\"later\":0.3,\"reject\":0.2}}}}";

    private static string Generated(string thought = "Maybe I can help Pam without reopening our argument.", string suggested = "help") =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = JsonSerializer.Serialize(new { thought, suggestedChoice = suggested }) } } },
        });

    [Fact]
    public async Task UnconfiguredMindIsExplicitlyAuthoredAndNeverUsesHttp()
    {
        using var handler = new Handler((_, _) => throw new Exception("No HTTP expected."));
        using var mind = new ResilientReflectionMind(handler);
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored", answer.Backend);
        Assert.Contains("no model calls", answer.Note);
        Assert.Null(answer.LayaPrompt);
        Assert.Null(answer.GenerationPrompt);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task LayaReceivesActualLinesAndFullCandidateSetWithinTotalTextBudget()
    {
        string? sent = null;
        using var handler = new Handler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://127.0.0.1:8000/v1/systemone", request.RequestUri!.AbsoluteUri);
            sent = await request.Content!.ReadAsStringAsync(token);
            return Json(ValidWeights);
        });
        using var mind = new ResilientReflectionMind(handler, LocalOptions());
        var request = Request(new string('c', 6000), new string('m', 6000));
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored+laya", answer.Backend);
        Assert.Equal(sent, answer.LayaPrompt);
        Assert.Contains("Authored thought", answer.Note);
        using JsonDocument prompt = JsonDocument.Parse(sent!);
        JsonElement question = prompt.RootElement.GetProperty("questions").GetProperty("q");
        JsonElement criteria = question.GetProperty("criteria");
        Assert.Equal(request.Choices.Count, criteria.EnumerateObject().Count());
        foreach (var choice in request.Choices)
            Assert.EndsWith(choice.Line, criteria.GetProperty(choice.Id).GetString());
        int textChars = prompt.RootElement.GetProperty("state").GetString()!.Length
            + question.GetProperty("instructions").GetString()!.Length
            + criteria.EnumerateObject().Sum(p => p.Name.Length + p.Value.GetString()!.Length);
        Assert.InRange(textChars, 1, 1250);
        Assert.Contains("Known memory:", prompt.RootElement.GetProperty("state").GetString());
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, answer.Weights.Values.Sum(), 12);
    }

    [Fact]
    public async Task OversizedRequiredLinesFallBackBeforeAnyHttpAndAreNeverTruncated()
    {
        using var handler = new Handler((_, _) => throw new Exception("No HTTP expected."));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        var request = Request() with
        {
            Choices = new[] { new ReflectionChoice("help", "Help", new string('x', 1251)) },
        };
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("1250-character limit", answer.Note);
        Assert.Null(answer.LayaPrompt);
        Assert.Null(answer.GenerationPrompt);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task LocalGenerationIsRecordedAndEvaluatedByLayaRatherThanAcceptedDirectly()
    {
        var bodies = new List<string>();
        using var handler = new Handler(async (request, token) =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync(token));
            if (bodies.Count == 1)
            {
                Assert.Equal("http://localhost:1234/v1/chat/completions", request.RequestUri!.AbsoluteUri);
                return Json(Generated());
            }
            Assert.Equal("/v1/systemone", request.RequestUri!.AbsolutePath);
            return Json(ValidWeights);
        });
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("local-llm+laya", answer.Backend);
        Assert.Equal("Maybe I can help Pam without reopening our argument.", answer.Thought);
        Assert.Equal("help", answer.SuggestedChoice);
        Assert.Equal(0.4, answer.Weights["help"], 12);
        Assert.Equal(0.3, answer.Weights["later"], 12);
        Assert.Equal(bodies[0], answer.GenerationPrompt);
        Assert.Equal(bodies[1], answer.LayaPrompt);
        using JsonDocument generation = JsonDocument.Parse(bodies[0]);
        Assert.Equal("test-model", generation.RootElement.GetProperty("model").GetString());
        using JsonDocument generationContext = JsonDocument.Parse(generation.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var choices = generationContext.RootElement.GetProperty("choices").EnumerateArray().ToArray();
        Assert.Equal(new[] { "help", "argue" }, choices.Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(Request().Choices[0].Line, choices[0].GetProperty("line").GetString());
        using JsonDocument laya = JsonDocument.Parse(bodies[1]);
        Assert.Contains(answer.Thought, laya.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task LongGeneratedThoughtAndFiveRealChoicesRetainKnownMemoryAndCharacterContext()
    {
        // These are the five alternatives supplied by Simulation.Reflection, including
        // its longer expressive variants. This exercises a realistic hybrid packet.
        var request = Request(
            "Penny: mood -0.20; regard for Pam 0.40. Quiet, sensitive and reluctant to start another public argument. " + new string('c', 1000),
            "At Spring 1, 12:00, you remember Pam argued with you (Argued); confidence 0.90. You tried to apologise afterward. " + new string('m', 1000)) with
        {
            Choices = new[]
            {
                new ReflectionChoice("gift", "GaveGift", "I thought of you when I found this. I'd like you to have it."),
                new ReflectionChoice("help", "HelpedSomeone", "We could do this together. Want a hand?"),
                new ReflectionChoice("confront", "Argued", "I'm still upset about what happened. We need to talk about it."),
                new ReflectionChoice("defer", "", "I need to think about this another time."),
                new ReflectionChoice("reject", "", "No. I don't want to act on this thought."),
            },
        };
        string thought = "Maybe I could help Pam while we are alone, so we can acknowledge yesterday without another public argument. ".PadRight(220, 'x');
        using var handler = new Handler((http, _) => Task.FromResult(Json(http.RequestUri!.Port == 1234
            ? Generated(thought, "help")
            : JsonSerializer.Serialize(new { answers = new { q = new { probabilities = request.Choices.ToDictionary(c => c.Id, _ => 0.2) } } }))));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("local-llm+laya", answer.Backend);
        using JsonDocument prompt = JsonDocument.Parse(answer.LayaPrompt!);
        string state = prompt.RootElement.GetProperty("state").GetString()!;
        Assert.Contains(thought, state);
        Assert.Contains(request.Context[..99], state);
        Assert.Contains(request.Memory[..99], state);
        var question = prompt.RootElement.GetProperty("questions").GetProperty("q");
        var criteria = question.GetProperty("criteria");
        foreach (var choice in request.Choices)
            Assert.EndsWith(choice.Line, criteria.GetProperty(choice.Id).GetString());
        int chars = state.Length + question.GetProperty("instructions").GetString()!.Length
            + criteria.EnumerateObject().Sum(p => p.Name.Length + p.Value.GetString()!.Length);
        Assert.InRange(chars, 1, 1250);
    }

    [Fact]
    public async Task ThoughtThatWouldDisplaceMinimumMemoryFallsBackBeforeCallingLaya()
    {
        var request = Request(new string('c', 1000), new string('m', 1000)) with
        {
            Choices = new[] { new ReflectionChoice("help", "Help", new string('x', 700)) },
        };
        using var handler = new Handler((_, _) => Task.FromResult(Json(Generated(new string('t', 220), "help"))));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("context, memory, thought", answer.Note);
        Assert.NotNull(answer.GenerationPrompt);
        Assert.Null(answer.LayaPrompt);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"not json\"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}")]
    public async Task MalformedGenerationFallsBackWithoutCallingLaya(string response)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(response)));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("Local generation failed:", answer.Note);
        Assert.NotNull(answer.GenerationPrompt);
        Assert.Null(answer.LayaPrompt);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("later")]
    [InlineData("reject")]
    public async Task GeneratedSuggestionMustNameAnExistingExecutableChoice(string suggested)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(Generated(suggested: suggested))));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("not a permitted executable choice", answer.Note);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(221)]
    public async Task GeneratedThoughtMustBeNonemptyAndWithinConfiguredBound(int length)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(Generated(new string('x', length)))));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("1..220 characters", answer.Note);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"help\":1}")]
    [InlineData("{\"help\":1,\"argue\":0,\"later\":0,\"reject\":0,\"invented\":1}")]
    [InlineData("{\"help\":1,\"help\":1,\"argue\":0,\"later\":0,\"reject\":0}")]
    [InlineData("{\"help\":-1,\"argue\":1,\"later\":1,\"reject\":1}")]
    [InlineData("{\"help\":\"0.5\",\"argue\":1,\"later\":1,\"reject\":1}")]
    [InlineData("{\"help\":1e999,\"argue\":1,\"later\":1,\"reject\":1}")]
    [InlineData("{\"help\":0,\"argue\":0,\"later\":0,\"reject\":0}")]
    public async Task InvalidLayaWeightsFallBackExplicitly(string probabilities)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("{\"answers\":{\"q\":{\"probabilities\":" + probabilities + "}}}")));
        using var mind = new ResilientReflectionMind(handler, LocalOptions());
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("Laya failed:", answer.Note);
        Assert.NotNull(answer.LayaPrompt);
        Assert.Equal(3, answer.Weights["help"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"answers\":{\"q\":[]}}")]
    public async Task MissingOrMalformedLayaAnswerFallsBack(string response)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(response)));
        using var mind = new ResilientReflectionMind(handler, LocalOptions());
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("Laya failed:", answer.Note);
    }

    [Fact]
    public async Task LargeFiniteWeightsNormalizeWithoutOverflow()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("{\"answers\":{\"q\":{\"probabilities\":{\"help\":1e308,\"argue\":1e308,\"later\":1e308,\"reject\":1e308}}}}")));
        using var mind = new ResilientReflectionMind(handler, LocalOptions());
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored+laya", answer.Backend);
        Assert.All(answer.Weights.Values, weight => Assert.Equal(0.25, weight));
    }

    [Fact]
    public async Task HttpFailureIncludesStatusAndDiscardsGeneratedProposal()
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Port == 1234
            ? Json(Generated("I shall do this exact generated thing."))
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.DoesNotContain("exact generated thing", answer.Thought);
        Assert.Contains("HTTP 503", answer.Note);
        Assert.NotNull(answer.GenerationPrompt);
        Assert.NotNull(answer.LayaPrompt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineCancelsTheActualRequestAndReturnsAuthoredFallback(bool generate)
    {
        bool cancelled = false;
        using var handler = new Handler(async (_, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled = token.IsCancellationRequested; throw; }
            return Json(ValidWeights);
        });
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate) with { Timeout = TimeSpan.FromMilliseconds(30) });
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.True(cancelled);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("timed out", answer.Note);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalCancellationPropagatesAndNeverBecomesAFallback(bool generate)
    {
        using var source = new CancellationTokenSource();
        using var handler = new Handler(async (_, token) =>
        {
            source.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Json(ValidWeights);
        });
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mind.ReflectAsync(Request(), source.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task PrecancellationSendsNothingEvenForAuthoredMode()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var handler = new Handler((_, _) => throw new Exception("No HTTP expected."));
        using var mind = new ResilientReflectionMind(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mind.ReflectAsync(Request(), source.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAlsoCoversAResponseBodyThatStallsAfterHeaders(bool external)
    {
        using var source = new CancellationTokenSource();
        using var stream = new BlockingStream(() => { if (external) source.Cancel(); });
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream),
        }));
        using var mind = new ResilientReflectionMind(handler, LocalOptions() with { Timeout = TimeSpan.FromMilliseconds(100) });
        if (external)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mind.ReflectAsync(Request(), source.Token));
        else
        {
            ReflectionAnswer answer = await mind.ReflectAsync(Request(), source.Token);
            Assert.Equal("authored-fallback", answer.Backend);
            Assert.Contains("timed out", answer.Note);
        }
        Assert.True(stream.ReadStarted);
        Assert.True(stream.ReadCancelled);
    }

    [Fact]
    public async Task TransportFailureWhileReadingSuccessfulResponseBodyFallsBack()
    {
        using var stream = new BrokenResponseStream();
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Port == 1234
            ? Json(Generated())
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var mind = new ResilientReflectionMind(handler, LocalOptions(generate: true));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("Laya failed: Connection reset while reading response body.", answer.Note);
        Assert.NotNull(answer.GenerationPrompt);
        Assert.NotNull(answer.LayaPrompt);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(3, answer.Weights["help"]);
    }

    [Fact]
    public async Task ResponseSizeIsBounded()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(new string('x', 65537))));
        using var mind = new ResilientReflectionMind(handler, LocalOptions());
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("Response exceeds", answer.Note);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("file:///tmp/laya")]
    [InlineData("http://localhost:8000?token=secret")]
    [InlineData("http://user:pass@localhost:8000")]
    public void RemoteOrAmbiguousEndpointsAreRejectedBeforeUse(string url)
    {
        Assert.Throws<ArgumentException>(() => new ResilientReflectionMind(new ReflectionMindOptions { LayaBaseUrl = url }));
        Assert.Throws<ArgumentException>(() => new ResilientReflectionMind(LocalOptions() with { GenerationBaseUrl = url }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    [InlineData(400)]
    public void ThoughtLimitCannotExceedTheSimulationContract(int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResilientReflectionMind(
            new ReflectionMindOptions { MaxThoughtChars = maximum }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    public void ThoughtLimitAcceptsBothContractBoundaries(int maximum)
    {
        using var mind = new ResilientReflectionMind(new ReflectionMindOptions { MaxThoughtChars = maximum });
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }

    private sealed class BlockingStream(Action onRead) : MemoryStream
    {
        public bool ReadStarted { get; private set; }
        public bool ReadCancelled { get; private set; }
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted = true;
            onRead();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { ReadCancelled = true; throw; }
            return 0;
        }
    }

    private sealed class BrokenResponseStream : MemoryStream
    {
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Connection reset while reading response body."));
    }
}
