using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.ReflectionTrial;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionTrialTests
{
    [Fact]
    public void SceneSetContainsTheTwoSemanticInterventionsAndThreeControls()
    {
        Assert.Equal(new[] { "memory", "trust", "line-meaning", "choice-order", "choice-labels" },
            TrialScenes.Create().Select(p => p.Id));
    }

    [Fact]
    public void MemoryPairChangesOnlyOneVerbInTheKnownMemory()
    {
        TrialPair pair = TrialScenes.Create().Single(p => p.Id == "memory");
        Assert.Equal(pair.Baseline.Memory.Replace("praised", "mocked", StringComparison.Ordinal), pair.Variant.Memory);
        Assert.Equal(pair.Baseline, pair.Variant with { Memory = pair.Baseline.Memory });
        Assert.NotNull(pair.Baseline.Proposal);
    }

    [Fact]
    public void TrustPairHoldsMemoryProposalAndAllOtherFieldsFixed()
    {
        TrialPair pair = TrialScenes.Create().Single(p => p.Id == "trust");
        Assert.NotEqual(pair.Baseline.Context, pair.Variant.Context);
        Assert.Equal(pair.Baseline, pair.Variant with { Context = pair.Baseline.Context });
    }

    [Fact]
    public void MeaningSwapMovesOnlyLinesBetweenSameKindIdsAndKeepsProposalReferenceFixed()
    {
        TrialPair pair = TrialScenes.Create().Single(p => p.Id == "line-meaning");
        Assert.Equal(pair.Baseline, pair.Variant with { Choices = pair.Baseline.Choices });
        Assert.Equal(pair.Baseline.Choices.Select(c => (c.Id, c.Kind)), pair.Variant.Choices.Select(c => (c.Id, c.Kind)));
        Assert.Equal(pair.Baseline.Choices[0].Line, pair.Variant.Choices[1].Line);
        Assert.Equal(pair.Baseline.Choices[1].Line, pair.Variant.Choices[0].Line);
        Assert.Equal("o3", pair.Baseline.Proposal!.SuggestedChoice);
        Assert.Equal(pair.Baseline.Choices[2], pair.Variant.Choices[2]);
        foreach (ReflectionChoice choice in pair.Baseline.Choices)
        {
            ReflectionChoice mapped = pair.Variant.Choices.Single(c => c.Id == pair.SemanticMap[choice.Id]);
            Assert.Equal(choice.Line, mapped.Line);
            Assert.Equal(choice.Kind, mapped.Kind);
        }
    }

    [Fact]
    public void OrderControlChangesOnlyOrderAndLabelControlPreservesEveryLineAndItsAct()
    {
        TrialPair order = TrialScenes.Create().Single(p => p.Id == "choice-order");
        Assert.Equal(order.Baseline, order.Variant with { Choices = order.Baseline.Choices });
        Assert.Equal(order.Baseline.Choices.Reverse(), order.Variant.Choices);
        TrialPair labels = TrialScenes.Create().Single(p => p.Id == "choice-labels");
        Assert.Equal(labels.Baseline, labels.Variant with { Choices = labels.Baseline.Choices, Proposal = labels.Baseline.Proposal });
        Assert.Equal(labels.Baseline.Choices.Select(c => (c.Kind, c.Line)), labels.Variant.Choices.Select(c => (c.Kind, c.Line)));
        Assert.Empty(labels.Baseline.Choices.Select(c => c.Id).Intersect(labels.Variant.Choices.Select(c => c.Id)));
        Assert.Equal(labels.Baseline.Proposal, labels.Variant.Proposal! with { SuggestedChoice = labels.Baseline.Proposal!.SuggestedChoice });
        Assert.Equal(labels.SemanticMap[labels.Baseline.Proposal!.SuggestedChoice], labels.Variant.Proposal!.SuggestedChoice);
    }

    [Fact]
    public void SemanticMappingSeparatesTextFollowingFromIdFollowing()
    {
        var baseline = new Dictionary<string, double> { ["a"] = 0.8, ["b"] = 0.2 };
        var variant = new Dictionary<string, double> { ["a"] = 0.2, ["b"] = 0.8 };
        var swapped = new Dictionary<string, string> { ["a"] = "b", ["b"] = "a" };
        Assert.Equal(0.6, TrialRunner.ByIdTotalVariation(baseline, variant), 12);
        Assert.Equal(0, TrialRunner.TotalVariation(baseline, variant, swapped));
        var renamed = new Dictionary<string, double> { ["x"] = 0.8, ["y"] = 0.2 };
        Assert.Equal(1, TrialRunner.ByIdTotalVariation(baseline, renamed));
        Assert.Equal(0, TrialRunner.TotalVariation(baseline, renamed,
            new Dictionary<string, string> { ["a"] = "x", ["b"] = "y" }));
    }

    [Fact]
    public void SemanticMappingMustBeABijectionRatherThanDroppingAnOption()
    {
        var probabilities = new Dictionary<string, double> { ["a"] = 0.8, ["b"] = 0.2 };
        Assert.Throws<ArgumentException>(() => TrialRunner.TotalVariation(probabilities, probabilities,
            new Dictionary<string, string> { ["a"] = "a", ["b"] = "a" }));
    }

    [Fact]
    public void TrialNormalizationIsStableForVeryLargeFiniteWeights()
    {
        var probabilities = TrialRunner.Normalize(new Dictionary<string, double> { ["a"] = 1e308, ["b"] = 1e308 }, new[] { "a", "b" });
        Assert.Equal(0.5, probabilities["a"]);
        Assert.Equal(0.5, probabilities["b"]);
        Assert.Throws<InvalidDataException>(() => TrialRunner.Normalize(new Dictionary<string, double> { ["a"] = 0 }, new[] { "a" }));
        Assert.Throws<InvalidDataException>(() => TrialRunner.Normalize(new Dictionary<string, double> { ["a"] = 1 }, new[] { "a", "b" }));
    }

    [Fact]
    public async Task OfflineRunRepeatsExactPacketsAndLabelsItsLimits()
    {
        var received = new List<ReflectionRequest>();
        var mind = new DelegateMind(async request =>
        {
            received.Add(request);
            return await new AuthoredReflectionMind().ReflectAsync(request);
        });
        TrialResult result = await TrialRunner.RunAsync(mind, false, "offline");
        Assert.Equal(15, received.Count);
        foreach (int start in new[] { 0, 3, 6, 9, 12 })
            Assert.Same(received[start], received[start + 2]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(15, result.NonLayaCount);
        Assert.Equal(0, result.FallbackCount);
        Assert.All(result.Comparisons, pair => Assert.Equal(0, pair.RepeatTotalVariation));
        string report = TrialReport.Markdown(result);
        Assert.Contains("provide no evidence about Laya", report);
        Assert.Contains("ID TV is mechanically 1", report);
        Assert.Contains("Generation was disabled", report);
        Assert.Contains("(ID absent)", report);
    }

    [Fact]
    public async Task AuthoredAnswersCannotPassForARequestedLiveTrial()
    {
        TrialResult result = await TrialRunner.RunAsync(new AuthoredReflectionMind(), true, "typed-decisions");
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(15, result.NonLayaCount);
        Assert.Contains("real Laya was requested", TrialReport.Markdown(result));
    }

    [Fact]
    public async Task RepeatMeasuresInferenceVariationSeparatelyFromTheIntervention()
    {
        int call = 0;
        TrialPair pair = TrialScenes.Create()[0];
        var mind = new DelegateMind(request =>
        {
            call++;
            var weights = request.Choices.ToDictionary(c => c.Id, _ => 0.0);
            weights["o1"] = call == 3 ? 0.5 : 0.8;
            weights["o2"] = call == 3 ? 0.5 : 0.2;
            return Task.FromResult(new ReflectionAnswer(request.Proposal!.Thought, request.Proposal.SuggestedChoice, weights, "test"));
        });
        TrialResult result = await TrialRunner.RunAsync(mind, false, "test", new[] { pair });
        Assert.Equal(0, result.Comparisons[0].ByIdTotalVariation);
        Assert.Equal(0.3, result.Comparisons[0].RepeatTotalVariation, 12);
    }

    [Fact]
    public async Task AllRealAdapterPacketsPreserveTheEntireManipulatedTextAndCaptureUsage()
    {
        using var handler = new Handler(async request =>
        {
            using JsonDocument prompt = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var probabilities = prompt.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria")
                .EnumerateObject().ToDictionary(p => p.Name, _ => 1.0);
            return Response(probabilities);
        });
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions { LayaBaseUrl = "http://127.0.0.1:8000" });
        TrialResult result = await TrialRunner.RunAsync(mind, true, "typed-decisions");
        Assert.Equal(15, handler.Calls);
        Assert.Equal(0, result.ExitCode);
        Assert.All(result.Observations, observation =>
        {
            Assert.True(observation.Prompt.Complete);
            Assert.True(observation.Response.Verified);
            Assert.InRange(observation.Prompt.TextCharacters!.Value, 1, 1250);
            Assert.Equal(observation.Request.Proposal!.Thought, observation.Answer.Thought);
            Assert.Equal("typed-decisions", observation.Response.RoutedModel);
            Assert.Equal(267, observation.Response.InputTokens);
            Assert.Null(observation.Answer.GenerationPrompt);
        });
    }

    [Fact]
    public async Task TransportFallbackCountAndNonzeroStatusAreExplicit()
    {
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions { LayaBaseUrl = "http://127.0.0.1:8000" });
        TrialResult result = await TrialRunner.RunAsync(mind, true, "typed-decisions", new[] { TrialScenes.Create()[0] });
        Assert.Equal(3, result.FallbackCount);
        Assert.Equal(3, result.NonLayaCount);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("HTTP 503", TrialReport.Markdown(result));
    }

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(false, 4, false)]
    [InlineData(false, 0, true)]
    public void EveryServerTruncationSignalIsVisible(bool truncated, int dropped, bool questionTruncated)
    {
        var answer = new ReflectionAnswer("thought", "o1", new Dictionary<string, double>(), "authored+laya",
            LayaResponse: JsonSerializer.Serialize(new
            {
                usage = new { input_tokens = 500, state_tokens_dropped = dropped, truncated, truncated_questions = questionTruncated ? new[] { "q" } : Array.Empty<string>() },
                routing = new { model = "typed-decisions" },
            }));
        ResponseAudit audit = TrialRunner.AuditResponse(answer);
        Assert.True(audit.Verified);
        Assert.True(audit.HasTruncation);
    }

    [Fact]
    public void MissingOrMalformedUsageIsUnverifiedRatherThanReportedAsNoTruncation()
    {
        var answer = new ReflectionAnswer("thought", "o1", new Dictionary<string, double>(), "authored+laya", LayaResponse: "{}");
        ResponseAudit audit = TrialRunner.AuditResponse(answer);
        Assert.False(audit.Verified);
        Assert.Null(audit.Truncated);
        Assert.NotNull(audit.Error);
    }

    private static HttpResponseMessage Response(IReadOnlyDictionary<string, double> probabilities)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                answers = new { q = new { probabilities } },
                usage = new { input_tokens = 267, state_tokens_dropped = 0, truncated = false, truncated_questions = Array.Empty<string>() },
                routing = new { model = "typed-decisions" },
            }), Encoding.UTF8, "application/json"),
        };

    private sealed class DelegateMind(Func<ReflectionRequest, Task<ReflectionAnswer>> answer) : IReflectionMind
    {
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return answer(request);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return send(request);
        }
    }
}
