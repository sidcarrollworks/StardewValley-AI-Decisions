using NpcDecision;
using Xunit;

namespace NpcDecision.Tests;

public class DecisionClientTests
{
    [Fact]
    public void Fake_Choose_IsUniformAndSumsToOne()
    {
        var client = new FakeDecisionClient();

        IReadOnlyList<double> probs = client.Choose(new[] { "a", "b", "c", "d" }, "ctx");

        Assert.Equal(4, probs.Count);
        Assert.All(probs, p => Assert.Equal(0.25, p, 10));
        Assert.Equal(1.0, probs.Sum(), 10);
    }

    [Fact]
    public void Fake_Choose_EmptyOptions_IsEmpty()
    {
        var client = new FakeDecisionClient();
        Assert.Empty(client.Choose(Array.Empty<string>(), "ctx"));
    }

    [Fact]
    public void Fake_Score_IsMidpoint()
    {
        var client = new FakeDecisionClient();
        Assert.Equal(3.0, client.Score("loneliness", 1, 5), 10);
        Assert.Equal(0.0, client.Score("mood", -10, 10), 10);
    }

    [Fact]
    public void Fake_YesNo_IsHalf()
    {
        var client = new FakeDecisionClient();
        Assert.Equal(0.5, client.YesNo("upset with Bob", "Bob insulted them"), 10);
    }

    [Fact]
    public void Resilient_PassesThrough_WhenInnerSucceeds()
    {
        var inner = new StubClient(new[] { 0.9, 0.1 }, score: 4.0, yesNo: 0.8);
        var resilient = new ResilientDecisionClient(inner);

        Assert.Equal(new[] { 0.9, 0.1 }, resilient.Choose(new[] { "a", "b" }, "ctx"));
        Assert.Equal(4.0, resilient.Score("ctx", 1, 5), 10);
        Assert.Equal(0.8, resilient.YesNo("ctx", "p"), 10);
    }

    [Fact]
    public void Resilient_FallsBack_WhenInnerThrows()
    {
        var resilient = new ResilientDecisionClient(new ThrowingClient());

        Assert.Equal(new[] { 0.5, 0.5 }, resilient.Choose(new[] { "a", "b" }, "ctx"));
        Assert.Equal(3.0, resilient.Score("ctx", 1, 5), 10);
        Assert.Equal(0.5, resilient.YesNo("ctx", "p"), 10);
    }

    [Fact]
    public void Resilient_FallsBack_OnTimeout()
    {
        var resilient = new ResilientDecisionClient(new SlowClient(200), TimeSpan.FromMilliseconds(20));

        Assert.Equal(new[] { 1.0 }, resilient.Choose(new[] { "only" }, "ctx"));
    }

    private sealed class StubClient : IDecisionClient
    {
        private readonly double[] _choice;
        private readonly double _score;
        private readonly double _yesNo;

        public StubClient(double[] choice, double score, double yesNo)
        {
            _choice = choice;
            _score = score;
            _yesNo = yesNo;
        }

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => _choice;
        public double Score(string context, double min, double max) => _score;
        public double YesNo(string context, string proposition) => _yesNo;
    }

    private sealed class ThrowingClient : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => throw new NotImplementedException();
        public double Score(string context, double min, double max) => throw new NotImplementedException();
        public double YesNo(string context, string proposition) => throw new NotImplementedException();
    }

    private sealed class SlowClient : IDecisionClient
    {
        private readonly int _sleepMs;
        public SlowClient(int sleepMs) => _sleepMs = sleepMs;

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            Thread.Sleep(_sleepMs);
            return new[] { 0.0 };
        }

        public double Score(string context, double min, double max) { Thread.Sleep(_sleepMs); return 0; }
        public double YesNo(string context, string proposition) { Thread.Sleep(_sleepMs); return 0; }
    }
}
