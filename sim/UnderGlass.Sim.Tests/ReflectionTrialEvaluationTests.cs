using System.Text.Json;
using UnderGlass.ReflectionTrial;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionTrialEvaluationTests
{
    [Fact]
    public async Task EveryMappedPassIsAuditedEvenWhenTheFirstReceiptIsComplete()
    {
        TrialPair pair = TrialScenes.Create()[0];
        var good = await TrialRunner.RunAsync(new ReceiptsMind(), true, "typed-decisions", new[] { pair }, evaluationMode: "balanced");
        Assert.Equal(0, good.ExitCode);
        Assert.Equal(6, good.EvaluationCount);
        Assert.All(good.Observations, o => Assert.All(o.Passes!, p => Assert.True(p.Prompt.Complete)));

        var clipped = await TrialRunner.RunAsync(new ReceiptsMind(clipSecond: true), true, "typed-decisions", new[] { pair }, evaluationMode: "balanced");
        Assert.Equal(3, clipped.ExitCode);
        Assert.Equal(3, clipped.IncompletePacketCount);
        Assert.All(clipped.Observations, o =>
        {
            Assert.True(o.Prompt.Complete); // First receipt alone would miss the failure.
            Assert.False(o.Passes![1].Prompt.Complete);
        });
    }

    [Fact]
    public async Task ATruncatedOrMissingLaterResponseCannotPassTheLiveTrialAudit()
    {
        TrialPair pair = TrialScenes.Create()[0];
        var truncated = await TrialRunner.RunAsync(new ReceiptsMind(truncateSecond: true), true, "typed-decisions", new[] { pair });
        Assert.Equal(3, truncated.ExitCode);
        Assert.Equal(3, truncated.TruncatedResponseCount);
        Assert.All(truncated.Observations, o => Assert.False(o.Response.HasTruncation));

        var missing = await TrialRunner.RunAsync(new ReceiptsMind(missingSecond: true), true, "typed-decisions", new[] { pair });
        Assert.Equal(3, missing.ExitCode);
        Assert.Equal(3, missing.UnverifiedResponseCount);
    }

    [Fact]
    public async Task AMisleadingChoiceMapCannotMakeAMissingLineAppearComplete()
    {
        var result = await TrialRunner.RunAsync(new ReceiptsMind(badMap: true), true, "typed-decisions", new[] { TrialScenes.Create()[0] });
        Assert.Equal(3, result.ExitCode);
        Assert.Equal(3, result.IncompletePacketCount);
        Assert.All(result.Observations, o => Assert.False(o.Passes![1].Prompt.Complete));
    }

    [Fact]
    public void ExpandedScenesChangeOnlyTheirDeclaredPerspectiveOrImaginedProposal()
    {
        IReadOnlyList<TrialPair> pairs = TrialScenes.CreateSocial();
        Assert.Equal(17, pairs.Count);
        Assert.Equal(4, pairs.Count(p => p.Id.EndsWith("-perspective", StringComparison.Ordinal)));
        foreach (TrialPair pair in pairs.Where(p => p.Id.EndsWith("-perspective", StringComparison.Ordinal)))
        {
            Assert.NotEqual(pair.Baseline.Context, pair.Variant.Context);
            Assert.Equal(pair.Baseline, pair.Variant with { Context = pair.Baseline.Context });
        }
        foreach (TrialPair pair in pairs.Where(p => p.Id.EndsWith("-inspiration", StringComparison.Ordinal)))
        {
            Assert.NotEqual(pair.Baseline.Proposal!.Thought, pair.Variant.Proposal!.Thought);
            Assert.Equal(pair.Baseline, pair.Variant with { Proposal = pair.Baseline.Proposal });
            Assert.Equal(pair.Baseline.Memory, pair.Variant.Memory);
        }
    }

    private sealed class ReceiptsMind(bool clipSecond = false, bool truncateSecond = false,
        bool missingSecond = false, bool badMap = false) : IReflectionMind
    {
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var weights = request.Choices.ToDictionary(c => c.Id, _ => 1.0 / request.Choices.Count);
            ReflectionEvaluation[] passes = { Pass(1, false), Pass(2, true) };
            return Task.FromResult(new ReflectionAnswer(request.Proposal!.Thought, request.Proposal.SuggestedChoice,
                weights, "authored+laya", LayaPrompt: passes[0].Prompt, LayaResponse: passes[0].Response, Evaluations: passes));

            ReflectionEvaluation Pass(int pass, bool second)
            {
                var ordered = request.Choices.Reverse().ToArray();
                var labels = ordered.Select((c, i) => (Label: "o" + i, Choice: c)).ToDictionary(x => x.Label,
                    x => (IReadOnlyList<string>)new[] { x.Choice.Id });
                var criteria = ordered.Select((c, i) => (Label: "o" + i, Choice: c)).ToDictionary(x => x.Label,
                    x => $"{(x.Choice.Kind.Length == 0 ? "no act" : x.Choice.Kind)}: {x.Choice.Line}");
                if (second && badMap) labels["o0"] = labels["o1"];
                string context = second && clipSecond ? request.Context[..12] : request.Context;
                string state = $"Person: {request.Actor}. About: {request.Subject}.\nImagined thought: {request.Proposal!.Thought}\nCharacter context: {context}\nKnown memory: {request.Memory}";
                string prompt = JsonSerializer.Serialize(new { state, questions = new { q = new { type = "choice", instructions = "Read the actual lines.", criteria } } });
                string? response = second && missingSecond ? null : JsonSerializer.Serialize(new
                {
                    usage = new { input_tokens = 200, state_tokens_dropped = 0, truncated = second && truncateSecond, truncated_questions = Array.Empty<string>() },
                    routing = new { model = "typed-decisions" },
                });
                return new(pass, prompt, response, labels, weights);
            }
        }
    }
}
