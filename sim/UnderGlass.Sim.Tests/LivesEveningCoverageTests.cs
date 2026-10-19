using UnderGlass.Minds;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed partial class LivesEveningTests
{
    private static EveningOptions AllChoices(bool record = true) => On(record) with
    { Evaluation = EveningEvaluationPolicy.All, ModelCallsPerDay = 0 };

    [Fact]
    public async Task AllEvaluatesUntornChoicesEvenWithZeroSelectiveBudget()
    {
        var mind = new Scripted();
        var run = await Run(mind, AllChoices(), choices: new ChoiceSettings());
        Assert.NotEmpty(mind.Requests);
        Assert.Contains(mind.Requests, r => !r.Fallback.Torn);
        Assert.Equal(run.Result.LivesEveningChoices.Count, mind.Requests.Count);
        Assert.All(run.Result.LivesEveningChoices, c => Assert.NotNull(c.Evaluation));
        Assert.Contains(run.Result.LivesEveningUpdates, u => u.Plan.Stage == EveningStage.Completed);
    }

    [Fact]
    public async Task AllEvaluatesThirdChoiceForSamePersonInOneDay()
    {
        var town = Town() with { Cast = new[] { Person("Ann"), Person("Bo"), Person("Cy"), Person("Dee") } };
        var history = History();
        history = history with
        {
            Readings = new[] { "Bo", "Cy", "Dee" }.Select(name => history.Readings[0] with
            {
                Id = "warm-" + name, Object = new(MemoryObjectKind.Person, name),
                Text = "I enjoy " + name + "'s company.", Cues = new[] { new HistoryCue("person:" + name) },
            }).ToArray(),
        };
        var mind = new Scripted(r => r.Fallback.Opportunity.Trigger == "answer" ? "no" : "offer");
        var run = await Run(mind, AllChoices(), town, history: history);
        Assert.True(mind.Requests.Count(r => r.Fallback.Opportunity.Holder == "Ann") >= 3);
        Assert.All(run.Result.LivesEveningChoices, c => Assert.NotNull(c.Evaluation));
        Assert.DoesNotContain(run.Result.LivesEveningUpdates, u => u.Plan.Stage == EveningStage.Completed);
    }

    [Fact]
    public async Task AllReplaysExactUntornChoicesWithoutNetwork()
    {
        var first = await Run(new Scripted(), AllChoices(), days: 2, choices: new ChoiceSettings());
        var tape = new RecordedLivesDecisionMind(first.Result.LivesDecisions);
        var replay = await Run(tape, AllChoices(), days: 2, choices: new ChoiceSettings());
        tape.RequireConsumed();
        Assert.Equal(first.Trace, replay.Trace);
        Assert.Equal(Metrics.LogHash(first.Result), Metrics.LogHash(replay.Result));
        Assert.Equal(Json(first.Result.LivesEveningChoices), Json(replay.Result.LivesEveningChoices));
        Assert.Equal(Json(first.Result.LivesDecisions), Json(replay.Result.LivesDecisions));
    }

    private sealed class FailedDecisionMind : ILivesDecisionMind
    {
        public Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(LivesDecisionEngine.Fallback(request, "Synthetic HTTP failure.", called: true));
    }

    [Fact]
    public async Task AllRecordsFailureAndKeepsTheAuthoredDrawAndOutcome()
    {
        var baseline = await Run(options: On());
        var failed = await Run(new FailedDecisionMind(), AllChoices());
        Assert.NotEmpty(failed.Result.LivesDecisions);
        Assert.All(failed.Result.LivesDecisions, r =>
        {
            Assert.Equal("authored-fallback", r.Answer.Backend);
            Assert.Equal(r.Request.Fallback.SelectedId, r.SelectedId);
        });
        Assert.Equal(baseline.Trace, failed.Trace);
        Assert.Equal(Metrics.LogHash(baseline.Result), Metrics.LogHash(failed.Result));
    }

    [Fact]
    public async Task SelectiveZeroBudgetStillCallsNoModel()
    {
        var mind = new Scripted();
        var run = await Run(mind, On() with { ModelCallsPerDay = 0 });
        Assert.NotEmpty(run.Result.LivesEveningChoices);
        Assert.Empty(mind.Requests);
        Assert.All(run.Result.LivesEveningChoices, c => Assert.Null(c.Evaluation));
    }

    [Fact]
    public void AllCannotSilentlyRunWithoutAnEvaluatorOrThroughSynchronousRun()
    {
        var sim = new Simulation(7, Town());
        sim.ConfigureLivesRouting(new() { Enabled = true }, History());
        sim.ConfigureLivesCompany(new() { Enabled = true });
        Assert.Throws<ArgumentException>(() => sim.ConfigureLivesEvenings(AllChoices()));
        sim.ConfigureLivesEvenings(AllChoices(), new Scripted());
        Assert.Throws<InvalidOperationException>(() => sim.Run(1));
        Assert.Throws<ArgumentException>(() => (On() with { Evaluation = (EveningEvaluationPolicy)99 }).CopyValidated());
    }

    [Fact]
    public void DefaultPolicyPreservesOldOptionsShapeAndAllIsExplicit()
    {
        Assert.DoesNotContain("Evaluation", Json(On()));
        Assert.Contains("\"Evaluation\":1", Json(AllChoices()));
    }

    private sealed class CancelDecisionMind(CancellationTokenSource source) : ILivesDecisionMind
    {
        public int? AskedAt { get; private set; }
        public Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
        {
            AskedAt = request.Fallback.Opportunity.At;
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The run must pass its cancellation token to the mind.");
        }
    }

    [Fact]
    public async Task AllCancellationStopsBeforeApplyingThePendingAnswer()
    {
        using var source = new CancellationTokenSource();
        var sim = new Simulation(7, Town());
        sim.ConfigureLivesRouting(new() { Enabled = true }, History());
        sim.ConfigureLivesCompany(new() { Enabled = true, OfferBase = 1e-12, SeekBase = 1e-12 });
        var mind = new CancelDecisionMind(source);
        sim.ConfigureLivesEvenings(AllChoices(), mind);
        int lastObserved = -1;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sim.RunAsync(1, new AuthoredReflectionMind(),
            (at, _) => lastObserved = at, source.Token));
        Assert.NotNull(mind.AskedAt);
        Assert.Equal(mind.AskedAt, lastObserved); // No next minute in which an answer could apply.
    }
}
