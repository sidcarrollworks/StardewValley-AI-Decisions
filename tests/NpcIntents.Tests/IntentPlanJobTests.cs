using System.Diagnostics;
using NpcDecision;
using NpcIntents;
using NpcMemory;
using Xunit;

namespace NpcIntents.Tests;

/// <summary>
/// Overnight planning runs off the calling (game) thread: starting and polling never block, and a
/// hung model is cut off by the budget so the plan still completes with fallback answers.
/// </summary>
public class IntentPlanJobTests
{
    private sealed class HungClient : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) { Thread.Sleep(10_000); return new[] { 1.0 }; }
        public double Score(string context, double min, double max) { Thread.Sleep(10_000); return max; }
        public double YesNo(string context, string proposition) { Thread.Sleep(10_000); return 1.0; }
    }

    private static IReadOnlyList<NpcMemorySnapshot> Snapshots(int count)
        => Enumerable.Range(0, count)
            .Select(i => new NpcMemorySnapshot($"Npc{i}", "plain", new[] { new DiaryEntry(10, "Player", "Saw", "Town") }, Array.Empty<string>()))
            .ToList();

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > timeoutMs) return false;
            Thread.Sleep(10);
        }
        return true;
    }

    [Fact]
    public void AHungModelNeverBlocksTheCallerAndTheBudgetEndsThePlan()
    {
        var snapshots = Snapshots(30);
        var watch = Stopwatch.StartNew();

        using IntentPlanJob job = IntentPlanJob.Start(
            budget => new IntentPlanner(new ResilientDecisionClient(new HungClient(), TimeSpan.FromSeconds(5), budget), new LineRenderer())
                .Plan(snapshots, seed: 1, sourceDay: 0),
            TimeSpan.FromMilliseconds(200));

        Assert.False(job.TryTake(out _, out _)); // not done yet, and polling returned at once
        Assert.True(watch.ElapsedMilliseconds < 150, $"start+poll took {watch.ElapsedMilliseconds} ms");

        // 30 NPCs x 10 s per call would be 10 minutes; the budget makes it finish in about 200 ms.
        Assert.True(WaitUntil(() => job.IsCompleted, 5000), "the budget did not end planning");
        Assert.True(job.BudgetExhausted);
        Assert.True(job.TryTake(out IntentPlan plan, out Exception? error));
        Assert.Null(error);
        Assert.True(plan.Candidates.Count <= 3); // fallback 0.5 still clears the default threshold; the cap holds
    }

    [Fact]
    public void AFailedJobHandsBackAnEmptyPlanAndTheError()
    {
        using IntentPlanJob job = IntentPlanJob.Start(_ => throw new InvalidOperationException("boom"), TimeSpan.FromSeconds(5));

        Assert.True(WaitUntil(() => job.IsCompleted, 5000));
        Assert.True(job.TryTake(out IntentPlan plan, out Exception? error));
        Assert.Empty(plan.Candidates);
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void APlanIsHandedOverOnlyOnce()
    {
        var expected = new IntentPlan(new[] { new IntentCandidate("Sam", "Hi.", new DiaryEntry(0, "Player", "Saw"), "test") });
        using IntentPlanJob job = IntentPlanJob.Start(_ => expected, TimeSpan.FromSeconds(5));

        Assert.True(WaitUntil(() => job.IsCompleted, 5000));
        Assert.True(job.TryTake(out IntentPlan plan, out _));
        Assert.Same(expected, plan);
        Assert.False(job.TryTake(out _, out _));
    }
}
