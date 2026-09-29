using System.Diagnostics;
using NpcDecision;
using Xunit;

namespace NpcDecision.Tests;

/// <summary>The budget token and fallback accounting on ResilientDecisionClient.</summary>
public class ResilientBudgetTests
{
    private sealed class CountingClient : IDecisionClient
    {
        public int Calls;
        private readonly int _sleepMs;
        public CountingClient(int sleepMs = 0) => _sleepMs = sleepMs;

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) { Work(); return options.Select(_ => 0.0).ToArray(); }
        public double Score(string context, double min, double max) { Work(); return max; }
        public double YesNo(string context, string proposition) { Work(); return 1.0; }

        private void Work()
        {
            Interlocked.Increment(ref Calls);
            if (_sleepMs > 0) Thread.Sleep(_sleepMs);
        }
    }

    [Fact]
    public void AnExhaustedBudgetFallsBackWithoutCallingTheModel()
    {
        var inner = new CountingClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5), cts.Token);

        Assert.Equal(0.5, client.YesNo("ctx", "p"), 10);
        Assert.Equal(3.0, client.Score("ctx", 1, 5), 10);
        Assert.Equal(new[] { 0.5, 0.5 }, client.Choose(new[] { "a", "b" }, "ctx"));
        Assert.Equal(0, inner.Calls);
        Assert.Equal(3, client.Fallbacks);
    }

    [Fact]
    public void TheBudgetCutsAWaitShortEvenBeforeThePerCallTimeout()
    {
        var inner = new CountingClient(sleepMs: 5000);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var client = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(30), cts.Token);

        var watch = Stopwatch.StartNew();
        double answer = client.YesNo("ctx", "p");
        watch.Stop();

        Assert.Equal(0.5, answer, 10);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"waited {watch.ElapsedMilliseconds} ms");
        Assert.Equal(1, client.Fallbacks);
    }

    [Fact]
    public void AHealthyModelIsNotCountedAsAFallback()
    {
        var client = new ResilientDecisionClient(new CountingClient(), TimeSpan.FromSeconds(5));

        Assert.Equal(1.0, client.YesNo("ctx", "p"), 10);
        Assert.Equal(0, client.Fallbacks);
    }
}
