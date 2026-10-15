using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class LivesLayaTests
{
    private static readonly LivesLayaOptions Options = new() { BaseUrl = "http://127.0.0.1:8000", Timeout = TimeSpan.FromSeconds(2) };
    private const string Good = "{\"model\":\"test-revision\",\"answers\":{\"q\":{\"probabilities\":{\"o0\":0.1,\"o1\":0.7,\"o2\":0.2}}}}";
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fn) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return fn(request, cancellationToken); }
    }

    [Fact]
    public async Task OneTypedCallHasAllActualLinesReasonsAndBothSidesWithinLimit()
    {
        var request = LivesDecisionTests.Request();
        string? body = null;
        using var handler = new Handler(async (r, ct) =>
        {
            Assert.Equal("/v1/systemone", r.RequestUri!.AbsolutePath);
            body = await r.Content!.ReadAsStringAsync(ct);
            return Json(Good);
        });
        using var mind = new LivesLayaMind(handler, Options);
        var answer = await mind.EvaluateAsync(request);
        Assert.Equal("laya", answer.Backend);
        Assert.Equal(1, handler.Calls);
        Assert.True(answer.Called);
        Assert.Equal(Good, answer.Response);
        Assert.Equal("test-revision", answer.ReportedModel);
        Assert.InRange(body!.Length, 1, LivesLayaMind.TextLimit);
        using var json = JsonDocument.Parse(body);
        var criteria = json.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria");
        Assert.Equal(3, criteria.EnumerateObject().Count());
        for (int i = 0; i < 3; i++) Assert.Contains(request.Fallback.Alternatives[i].Option.Line, criteria.GetProperty("o" + i).GetString());
        Assert.Contains("+warm=0.15", body);
        Assert.Contains("-cold=0.12", body);
        Assert.Equal("later", LivesDecisionEngine.Apply(request, answer));
        Assert.Equal(new[] { "yes", "later", "no" }, answer.Packet!.Labels.Values);
    }

    [Fact]
    public void OversizeRecallIsOmittedWholeAndMarkedButLinesStayComplete()
    {
        var request = LivesDecisionTests.Request();
        var recall = request.Fallback.Recall.Items.Select(m => m with { Text = new string('x', 1500) }).ToArray();
        request = request with { Fallback = request.Fallback with { Recall = request.Fallback.Recall with { Items = recall } } };
        var packet = LivesLayaMind.BuildPacket(request);
        Assert.Equal(recall.Select(m => m.Id), packet.Omitted);
        Assert.Contains("recalled text(s) omitted", packet.Json);
        Assert.DoesNotContain("xxxxx", packet.Json);
        Assert.Equal(packet.Json.Length, packet.Characters);
        Assert.InRange(packet.Characters, 1, 1250);
        foreach (var alternative in request.Fallback.Alternatives) Assert.Contains(alternative.Option.Line, packet.Json);
    }

    [Fact]
    public async Task MandatoryOverflowFallsBackWithoutSendingOrCuttingALine()
    {
        var request = LivesDecisionTests.Request() with { Context = new string('x', 2000) };
        using var handler = new Handler((_, _) => throw new Exception("No call allowed"));
        using var mind = new LivesLayaMind(handler, Options);
        var answer = await mind.EvaluateAsync(request);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Equal(0, handler.Calls);
        Assert.False(answer.Called);
        Assert.Equal(request.Fallback.SelectedId, LivesDecisionEngine.Apply(request, answer));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"answers\":{\"q\":{\"probabilities\":{\"o0\":1}}}}")]
    [InlineData("{\"answers\":{\"q\":{\"probabilities\":{\"o0\":0,\"o1\":0,\"o2\":0}}}}")]
    [InlineData("{\"answers\":{\"q\":{\"probabilities\":{\"o0\":-1,\"o1\":1,\"o2\":1}}}}")]
    [InlineData("{\"answers\":{\"q\":{\"probabilities\":{\"o0\":1,\"o1\":1,\"o2\":\"yes\"}}}}")]
    [InlineData("{\"answers\":{\"q\":{\"probabilities\":{\"o0\":1,\"o1\":1,\"invented\":1}}}}")]
    [InlineData("{\"answers\":{\"q\":{\"probabilities\":{\"o0\":1,\"o0\":1,\"o1\":1,\"o2\":1}}}}")]
    [InlineData("{\"usage\":{\"truncated\":true},\"answers\":{\"q\":{\"probabilities\":{\"o0\":1,\"o1\":1,\"o2\":1}}}}")]
    [InlineData("{\"usage\":{\"state_tokens_dropped\":1},\"answers\":{\"q\":{\"probabilities\":{\"o0\":1,\"o1\":1,\"o2\":1}}}}")]
    public async Task InvalidWeightsOrTruncatedReadingsUseTheSameFallback(string response)
    {
        var request = LivesDecisionTests.Request();
        using var handler = new Handler((_, _) => Task.FromResult(Json(response)));
        using var mind = new LivesLayaMind(handler, Options);
        var answer = await mind.EvaluateAsync(request);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Equal(response, answer.Response);
        Assert.True(answer.Called);
        Assert.NotNull(answer.Packet);
        Assert.Equal(request.Fallback.SelectedId, LivesDecisionEngine.Apply(request, answer));
    }

    [Fact]
    public async Task TimeoutUsesFallbackButCallerCancellationPropagates()
    {
        using var handler = new Handler(async (_, ct) => { await Task.Delay(5000, ct); return Json(Good); });
        using var mind = new LivesLayaMind(handler, Options with { Timeout = TimeSpan.FromMilliseconds(20) });
        var answer = await mind.EvaluateAsync(LivesDecisionTests.Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("timed out", answer.Note);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mind.EvaluateAsync(LivesDecisionTests.Request(), cancel.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NetworkErrorAndOversizeResponseFallBack()
    {
        using var failed = new Handler((_, _) => throw new HttpRequestException("offline"));
        using var offline = new LivesLayaMind(failed, Options);
        Assert.Equal("authored-fallback", (await offline.EvaluateAsync(LivesDecisionTests.Request())).Backend);
        using var large = new Handler((_, _) => Task.FromResult(Json(new string('x', 65537))));
        using var oversize = new LivesLayaMind(large, Options);
        Assert.Equal("authored-fallback", (await oversize.EvaluateAsync(LivesDecisionTests.Request())).Backend);
    }

    [Fact]
    public async Task UnconfiguredMindNeverCallsAndRemoteOrIdentityMismatchIsRejected()
    {
        using var handler = new Handler((_, _) => throw new Exception("No call allowed"));
        using var mind = new LivesLayaMind(handler);
        Assert.Equal("authored", (await mind.EvaluateAsync(LivesDecisionTests.Request())).Backend);
        Assert.Equal(0, handler.Calls);
        Assert.Throws<ArgumentException>(() => new LivesLayaMind(Options with { BaseUrl = "https://example.com" }));
        await Assert.ThrowsAsync<ArgumentException>(() => mind.EvaluateAsync(LivesDecisionTests.Request() with { Model = "wrong" }));
    }
}
