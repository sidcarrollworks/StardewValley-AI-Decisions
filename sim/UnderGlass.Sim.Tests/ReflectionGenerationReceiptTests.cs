using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionGenerationReceiptTests
{
    [Fact]
    public async Task GeneratedInspirationHasAStablePerRequestSeedAndARawReceipt()
    {
        var handler = new GeneratorHandler();
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions
        { LayaBaseUrl = "http://localhost:8000", GenerationBaseUrl = "http://localhost:8080", GenerationModel = "test-model" });
        ReflectionAnswer first = await mind.ReflectAsync(Request());
        ReflectionAnswer repeated = await mind.ReflectAsync(Request());
        ReflectionAnswer another = await mind.ReflectAsync(Request() with { Id = "another-request" });
        Assert.Equal("local-llm+laya", first.Backend);
        Assert.Equal(handler.GenerationResponse, first.GenerationResponse);
        Assert.Equal(first.GenerationPrompt, repeated.GenerationPrompt);
        using var a = JsonDocument.Parse(first.GenerationPrompt!);
        using var b = JsonDocument.Parse(another.GenerationPrompt!);
        Assert.True(a.RootElement.GetProperty("seed").GetInt32() >= 0);
        Assert.NotEqual(a.RootElement.GetProperty("seed").GetInt32(), b.RootElement.GetProperty("seed").GetInt32());
        Assert.Equal(0.7, a.RootElement.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task InvalidGeneratedContentIsRetainedWhenTheAuthoredFallbackReplacesIt()
    {
        var handler = new GeneratorHandler("This is not the required JSON thought.");
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions
        { LayaBaseUrl = "http://localhost:8000", GenerationBaseUrl = "http://localhost:8080" });
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Equal(handler.GenerationResponse, answer.GenerationResponse);
        Assert.Equal(1, handler.GenerationCalls);
        Assert.Equal(0, handler.EvaluationCalls);
        Assert.DoesNotContain("This is not", answer.Thought);
    }

    [Fact]
    public async Task ReconsiderationSuppliesTheEarlierImaginedIdeaWithoutMakingItAKnownMemory()
    {
        using var mind = new ResilientReflectionMind(new GeneratorHandler(), new ReflectionMindOptions
        { LayaBaseUrl = "http://localhost:8000", GenerationBaseUrl = "http://localhost:8080" });
        var request = Request() with { Continuity = new("root", "prior", 1, 3, "Perhaps I could help.", "A later known gift encounter.") };
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        using var packet = JsonDocument.Parse(answer.GenerationPrompt!);
        using var context = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(request.Memory, context.RootElement.GetProperty("memory").GetString());
        Assert.Equal(request.Continuity.PriorThought, context.RootElement.GetProperty("earlierImaginedIdea").GetString());
        Assert.Contains("not evidence", packet.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    private static ReflectionRequest Request() => new("seed-7:scene", 720, "Ann", "Bob", 3,
        "Bob gave you a gift yesterday.", "Ann feels grateful toward Bob.", new[]
        {
            new ReflectionChoice("gift", "GaveGift", "This is for you."),
            new ReflectionChoice("defer", "", "I need more time."),
            new ReflectionChoice("reject", "", "I don't want to act on this."),
        }, Proposal: new("test", "I could bring Bob a gift.", "gift", new[] { "test" }));

    private sealed class GeneratorHandler(string content = "{\"thought\":\"I could thank Bob with a small gift.\",\"suggestedChoice\":\"gift\"}") : HttpMessageHandler
    {
        public int GenerationCalls { get; private set; }
        public int EvaluationCalls { get; private set; }
        public string GenerationResponse => JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } }, usage = new { seed = 7, elapsed_ms = 12 } });
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool generation = request.RequestUri!.AbsolutePath.EndsWith("chat/completions", StringComparison.Ordinal);
            if (generation) GenerationCalls++; else EvaluationCalls++;
            string json = generation ? GenerationResponse : "{\"answers\":{\"q\":{\"probabilities\":{\"gift\":0.5,\"defer\":0.3,\"reject\":0.2}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
