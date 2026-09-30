using Xunit;

namespace NpcDecision.Tests;

public class HealthGateTests
{
    [Fact]
    public void GateDown_EveryCallFallsBackWithoutTouchingTheInner()
    {
        var inner = new CountingClient();
        var gate = new ResilientDecisionClient(inner, isDown: () => true);

        Assert.Equal(0.5, gate.YesNo("state", "p?"));
        Assert.Equal(new[] { 0.5, 0.5 }, gate.Choose(new[] { "a", "b" }, "state"));
        Assert.Equal(0.5, gate.Score("state", 0, 1));
        Assert.Equal(0, inner.Calls);
        Assert.Equal(3, gate.Fallbacks); // YesNo + Choose + Score
    }

    [Fact]
    public void GateDown_BatchFallsBackPerQuestionWithoutTouchingTheInner()
    {
        var inner = new CountingClient();
        var gate = new ResilientDecisionClient(inner, isDown: () => true);
        var questions = new Question[]
        {
            new YesNoQuestion("a", "p?"),
            new ChoiceQuestion("b", new[] { "x", "y" }),
        };

        IReadOnlyList<Answer> answers = gate.Ask("state", questions);

        Assert.Equal(2, answers.Count);
        Assert.Equal(0.5, answers[0].YesNo);
        Assert.Equal(new[] { 0.5, 0.5 }, answers[1].Probabilities);
        Assert.Equal(0, inner.Calls);
        Assert.Equal(2, gate.Fallbacks); // one per question in the loop
    }

    [Fact]
    public void GateUp_CallsTheInnerNormally()
    {
        var inner = new CountingClient();
        var gate = new ResilientDecisionClient(inner, isDown: () => false);

        Assert.Equal(0.5, gate.YesNo("state", "p?"));
        Assert.Equal(1, inner.Calls);
        Assert.Equal(0, gate.Fallbacks);
    }

    private sealed class CountingClient : IDecisionClient
    {
        public int Calls;

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            Calls++;
            double p = 1.0 / options.Count;
            return options.Select(_ => p).ToArray();
        }

        public double Score(string context, double min, double max)
        {
            Calls++;
            return (min + max) / 2.0;
        }

        public double YesNo(string context, string proposition)
        {
            Calls++;
            return 0.5;
        }
    }
}
