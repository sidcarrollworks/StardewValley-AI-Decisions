using NpcDecision;
using Xunit;

namespace NpcDecision.Tests;

public class DecisionStateTests
{
    private const int Budget = DecisionState.StateBudgetChars;

    [Fact]
    public void Build_WithNoSections_IsEmptyString()
    {
        Assert.Equal(string.Empty, new DecisionState().Build());
    }

    [Fact]
    public void Build_WithOnlyBlankSections_IsEmptyString()
    {
        var state = new DecisionState();
        state.Add(0, string.Empty);
        state.Add(5, "\n\n  \n");

        Assert.Equal(string.Empty, state.Build());
    }

    [Fact]
    public void Build_RendersHigherPriorityFirst_SeparatedByOneBlankLine()
    {
        var state = new DecisionState();
        state.Add(0, "low");
        state.Add(10, "high");
        state.Add(5, "mid");

        Assert.Equal("high\n\nmid\n\nlow", state.Build());
    }

    [Fact]
    public void Build_EqualPriorities_KeepInsertionOrder()
    {
        var state = new DecisionState();
        state.Add(3, "first");
        state.Add(1, "last");
        state.Add(3, "second");
        state.Add(3, "third");

        Assert.Equal("first\n\nsecond\n\nthird\n\nlast", state.Build());
    }

    [Fact]
    public void Build_DropsLowPriorityWholeLines_WhenOverBudget()
    {
        var state = new DecisionState();
        string[] highLines = Enumerable.Range(0, 10).Select(i => new string('a', 99) + i).ToArray();
        string[] lowLines = Enumerable.Range(0, 10).Select(i => new string('b', 99) + i).ToArray();
        state.Add(10, string.Join("\n", highLines));
        state.Add(0, string.Join("\n", lowLines));

        string result = state.Build();

        // 10 x 100 chars + 9 newlines = 1009; + blank + b0 = 1111; + b1 = 1212; b2 would need 1313.
        string expected = string.Join("\n", highLines) + "\n\n" + lowLines[0] + "\n" + lowLines[1];
        Assert.Equal(expected, result);
        Assert.Equal(1212, result.Length);
        Assert.True(result.Length <= Budget);
        Assert.All(highLines, line => Assert.Contains(line, result));
        Assert.DoesNotContain(lowLines[2], result);
    }

    [Fact]
    public void Build_CutNeverSplitsALine()
    {
        var state = new DecisionState();
        string top = new string('a', 400);
        string low = new string('b', 500);
        state.Add(10, top);
        state.Add(0, low + "\n" + low);

        string result = state.Build();

        // 400; + blank + one 500-char line = 902; the second 500-char line would need 1403.
        Assert.Equal(top + "\n\n" + low, result);
        Assert.True(result.Length <= Budget);

        // No line in the output is a fragment: every line is empty or a whole input line.
        foreach (string line in result.Split('\n'))
        {
            Assert.True(line.Length == 0 || line.Length == 400 || line.Length == 500,
                $"partial line of length {line.Length} in the cut result");
        }

        Assert.EndsWith(low, result);
    }

    [Fact]
    public void Build_NeverExceedsBudget_WithManyLongSections()
    {
        var state = new DecisionState();
        for (int i = 0; i < 50; i++)
        {
            state.Add(i, new string('x', 500) + "\n" + new string('y', 500) + "\n" + new string('z', 500));
        }

        string result = state.Build();

        // Two 500-char lines fit (1001); the third would need 1502.
        Assert.Equal(new string('x', 500) + "\n" + new string('y', 500), result);
        Assert.True(result.Length <= Budget);
        Assert.DoesNotContain("z", result);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        static DecisionState BuildIt()
        {
            var state = new DecisionState();
            state.Add(1, "alpha\n\nbeta");
            state.Add(9, "gamma");
            state.Add(1, "delta");
            state.Add(9, "epsilon");
            return state;
        }

        DecisionState state = BuildIt();
        Assert.Equal("gamma\n\nepsilon\n\nalpha\n\nbeta\n\ndelta", state.Build());

        // Same instance twice, and a fresh instance built the same way.
        Assert.Equal(state.Build(), state.Build());
        Assert.Equal(state.Build(), BuildIt().Build());
    }

    [Fact]
    public void Build_SingleLineLongerThanBudget_IsDroppedEntirely()
    {
        var state = new DecisionState();
        state.Add(10, new string('x', Budget + 1));

        Assert.Equal(string.Empty, state.Build());
    }

    [Fact]
    public void Build_LongLineAfterShortLine_DropsOnlyTheLongLine()
    {
        var state = new DecisionState();
        state.Add(10, "keep me");
        state.Add(0, new string('x', Budget * 3));

        Assert.Equal("keep me", state.Build());
    }

    [Fact]
    public void Build_CutHappensAtTheFirstLineThatDoesNotFit()
    {
        // A line over the remaining budget is the cut point: it and every line after it drop,
        // whole. A lower-priority short line after it does not leapfrog it.
        var state = new DecisionState();
        state.Add(10, new string('x', Budget + 100));
        state.Add(0, "later");

        Assert.Equal(string.Empty, state.Build());
    }

    [Fact]
    public void Build_InteriorBlankLines_ArePreserved()
    {
        var state = new DecisionState();
        state.Add(0, "a\n\nb\n\n\nc");

        Assert.Equal("a\n\nb\n\n\nc", state.Build());
    }

    [Fact]
    public void Build_LeadingAndTrailingBlankLines_Dropped_AndCrlfNormalized()
    {
        var state = new DecisionState();
        state.Add(0, "\n  \nfirst\r\n\r\nsecond\r\n  \r\n");

        Assert.Equal("first\n\nsecond", state.Build());
    }
}
