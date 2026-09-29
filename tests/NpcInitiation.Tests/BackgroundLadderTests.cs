using System.Diagnostics;
using NpcDecision;
using NpcMemory;
using Xunit;

namespace NpcInitiation.Tests;

/// <summary>The ladder's model calls run on a background worker; the game thread never waits.</summary>
public sealed class BackgroundLadderTests
{
    private sealed class SlowYes : IDecisionClient
    {
        private readonly int _sleepMs;
        public SlowYes(int sleepMs) => _sleepMs = sleepMs;
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => options.Select(_ => 1.0).ToArray();
        public double Score(string context, double min, double max) => max;
        public double YesNo(string context, string proposition) { Thread.Sleep(_sleepMs); return 1.0; }
    }

    private static InitiationInput Near(string npc, int tick)
        => new(npc, new LedgerView(npc, "Player", LedgerDetail.NamedSpot, "Farm", 0, 0, tick, "1,1"), false, 0);

    private static InitiationOptions Eager() => new() { BaseGainPerTick = 0.5, HeartsGainPerTick = 0 };

    [Fact]
    public void EnqueueNeverWaitsOnTheModel()
    {
        var runner = new BackgroundLadder(new InitiationLadder(new SlowYes(500), seed: 1, Eager()));

        var watch = Stopwatch.StartNew();
        Assert.True(runner.EnqueueTick(10, new[] { Near("Sam", 10) }));
        Assert.Empty(runner.Drain());
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 200, $"enqueue+drain took {watch.ElapsedMilliseconds} ms");
        Assert.True(runner.WaitIdle(TimeSpan.FromSeconds(10)));
        BackgroundLadder.Result result = Assert.Single(runner.Drain());
        InitiationEvent attempt = Assert.Single(result.Events);
        Assert.Equal("Attempt", attempt.Kind);
        Assert.Equal(InitiationStep.Emote, attempt.Step);
        (string npc, DiaryEntry entry) = Assert.Single(result.DiaryLines);
        Assert.Equal("Sam", npc);
        Assert.Equal("TriedToReach", entry.Kind);
    }

    [Fact]
    public void ABacklogDropsTicksInsteadOfQueueingForever()
    {
        var runner = new BackgroundLadder(new InitiationLadder(new SlowYes(300), seed: 1, Eager()), maxBacklog: 2);

        int accepted = 0;
        for (int t = 0; t < 10; t++)
            if (runner.EnqueueTick(t, new[] { Near($"Npc{t}", t) }))
                accepted++;

        Assert.True(accepted <= 3, $"accepted {accepted}");
        Assert.True(runner.Dropped >= 7);
        Assert.True(runner.WaitIdle(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void ResponsesAndStateSnapshotsFlowThroughTheWorker()
    {
        var runner = new BackgroundLadder(new InitiationLadder(new SlowYes(0), seed: 1, Eager()));

        runner.EnqueueTick(10, new[] { Near("Sam", 10) });
        runner.EnqueueResponse("Sam", 11);
        Assert.True(runner.WaitIdle(TimeSpan.FromSeconds(10)));

        var kinds = runner.Drain().SelectMany(r => r.Events).Select(e => e.Kind).ToList();
        Assert.Equal(new[] { "Attempt", "Responded" }, kinds);
        var restored = InitiationLadder.FromJson(runner.LatestJson, new SlowYes(0), seed: 1);
        Assert.Equal(0, restored.Rung("Sam"));
        Assert.True(restored.Urge("Sam") > 0);
    }
}
