using Xunit;

namespace NpcDecision.Tests;

/// <summary>Every fallback says why: the budget, the health gate, the timeout, the inner client's
/// error, or a question the batch left out (playtest 2026-10-02: 249 fallbacks with no reason).</summary>
public class FallbackReasonTests
{
    private sealed class Fails : IDecisionClient, IBatchDecisionClient
    {
        private readonly Func<Exception> _error;
        public Fails(Func<Exception> error) => _error = error;
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => throw _error();
        public double Score(string context, double min, double max) => throw _error();
        public double YesNo(string context, string proposition) => throw _error();
        public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions) => throw _error();
    }

    private sealed class Slow : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => throw new NotSupportedException();
        public double Score(string context, double min, double max) => throw new NotSupportedException();
        public double YesNo(string context, string proposition) { Thread.Sleep(500); return 1; }
    }

    private sealed class LeavesOut : IBatchDecisionClient, IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => throw new NotSupportedException();
        public double Score(string context, double min, double max) => throw new NotSupportedException();
        public double YesNo(string context, string proposition) => throw new NotSupportedException();
        public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
            => new[] { Answer.FromYesNo("a", 0.9) };
    }

    [Fact]
    public void NoFallback_NoReason()
    {
        var client = new ResilientDecisionClient(new FakeDecisionClient());
        client.YesNo("state", "p?");
        Assert.Null(client.LastFallbackReason);
    }

    [Fact]
    public void TheInnerErrorIsKept_WithAndWithoutATimeout()
    {
        var plain = new ResilientDecisionClient(new Fails(() => new LayaException("Laya returned HTTP 500: CUDA out of memory", 500)));
        Assert.Equal(0.5, plain.YesNo("state", "p?"));
        Assert.Equal("LayaException: Laya returned HTTP 500: CUDA out of memory", plain.LastFallbackReason);

        // With a timeout the call runs on a task: the AggregateException is unwrapped.
        var timed = new ResilientDecisionClient(new Fails(() => new InvalidOperationException("boom")), TimeSpan.FromSeconds(5));
        timed.Choose(new[] { "x", "y" }, "state");
        Assert.Equal("InvalidOperationException: boom", timed.LastFallbackReason);

        var batch = new ResilientDecisionClient(new Fails(() => new InvalidOperationException("batch down")));
        batch.Ask("state", new Question[] { new YesNoQuestion("a", "p?") });
        Assert.Equal("InvalidOperationException: batch down", batch.LastFallbackReason);
    }

    [Fact]
    public void ALongErrorIsCut()
    {
        var client = new ResilientDecisionClient(new Fails(() => new InvalidOperationException(new string('x', 500))));
        client.YesNo("state", "p?");
        Assert.Equal(ResilientDecisionClient.MaxReasonChars, client.LastFallbackReason!.Length);
    }

    [Fact]
    public void TheTimeout_TheGateAndTheBudgetEachSaySo()
    {
        var slow = new ResilientDecisionClient(new Slow(), TimeSpan.FromMilliseconds(50));
        slow.YesNo("state", "p?");
        Assert.Equal("no answer within 50 ms", slow.LastFallbackReason);

        var down = new ResilientDecisionClient(new FakeDecisionClient(), isDown: () => true);
        down.YesNo("state", "p?");
        Assert.Equal("skipped: the model is marked down", down.LastFallbackReason);

        using var spent = new CancellationTokenSource();
        spent.Cancel();
        var budget = new ResilientDecisionClient(new FakeDecisionClient(), TimeSpan.FromSeconds(1), spent.Token);
        budget.YesNo("state", "p?");
        Assert.Equal("the time budget ran out", budget.LastFallbackReason);
        budget.Ask("state", new Question[] { new YesNoQuestion("a", "p?") });
        Assert.Equal("the time budget ran out", budget.LastFallbackReason);
    }

    [Fact]
    public void AQuestionTheBatchLeftOutSaysWhichOne()
    {
        var client = new ResilientDecisionClient(new LeavesOut());
        IReadOnlyList<Answer> answers = client.Ask("state", new Question[] { new YesNoQuestion("a", "p?"), new YesNoQuestion("b", "q?") });
        Assert.Equal(0.9, answers[0].YesNo);
        Assert.Equal(1, client.Fallbacks);
        Assert.Equal("the model's answer left out question b", client.LastFallbackReason);
    }
}
