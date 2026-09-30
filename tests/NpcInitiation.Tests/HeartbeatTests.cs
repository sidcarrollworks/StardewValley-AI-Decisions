using Xunit;

namespace NpcInitiation.Tests;

public class HeartbeatTests
{
    [Fact]
    public void ShouldFire_EveryTwelveTicks()
    {
        Assert.True(Heartbeat.ShouldFire(0));
        Assert.True(Heartbeat.ShouldFire(12));
        Assert.True(Heartbeat.ShouldFire(24));
        Assert.True(Heartbeat.ShouldFire(108));
    }

    [Fact]
    public void ShouldFire_OffBeatTicks_AreFalse()
    {
        Assert.False(Heartbeat.ShouldFire(1));
        Assert.False(Heartbeat.ShouldFire(13));
        Assert.False(Heartbeat.ShouldFire(119));
    }

    [Fact]
    public void Format_TimeLabelMatchesTheGameClock()
    {
        string line = Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.None);
        Assert.StartsWith("[shadow] 0600:", line);

        line = Heartbeat.Format(12, 1, Empty(), 0, 0, Heartbeat.PlanState.None);
        Assert.StartsWith("[shadow] 0800:", line);

        line = Heartbeat.Format(60, 1, Empty(), 0, 0, Heartbeat.PlanState.None);
        Assert.StartsWith("[shadow] 1600:", line);
    }

    [Fact]
    public void Format_NoUrges_ShowsNone()
    {
        string line = Heartbeat.Format(12, 29, Empty(), 1, 0, Heartbeat.PlanState.None);
        Assert.Contains("max urge 0.00 (none)", line);
    }

    [Fact]
    public void Format_HighestUrgeWins_RegardlessOfDictionaryOrder()
    {
        // Insertion order puts Shane first, but Willy's urge is higher and must win.
        var urges = new Dictionary<string, double> { ["Shane"] = 0.2, ["Willy"] = 0.5, ["Alex"] = 0.3 };
        string line = Heartbeat.Format(12, 1, urges, 0, 0, Heartbeat.PlanState.None);
        Assert.Contains("max urge 0.50 (Willy)", line);
    }

    [Fact]
    public void Format_TiedUrges_BreakByNameAscending()
    {
        // Same urge: the alphabetically first name wins, not the dictionary's insertion order.
        var urges = new Dictionary<string, double> { ["Shane"] = 0.5, ["Abigail"] = 0.5 };
        string line = Heartbeat.Format(12, 1, urges, 0, 0, Heartbeat.PlanState.None);
        Assert.Contains("max urge 0.50 (Abigail)", line);
    }

    [Fact]
    public void Format_PlanStateWords()
    {
        Assert.Contains("overnight plan none",
            Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.None));
        Assert.Contains("overnight plan running",
            Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.Running));
        Assert.Contains("overnight plan ready",
            Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.Ready));
    }

    [Fact]
    public void Format_CarriesBacklogAndDropped()
    {
        string line = Heartbeat.Format(0, 7, Empty(), 3, 2, Heartbeat.PlanState.None);
        Assert.Contains("7 NPC diaries", line);
        Assert.Contains("backlog 3/dropped 2", line);
    }

    [Fact]
    public void Format_ModelCounters_AppearOnlyWhenSupplied()
    {
        string with = Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.None,
            modelCalls: 42, modelFallbacks: 3, modelMedianMs: 47, modelP95Ms: 61);
        Assert.Contains("model calls 42/fallback 3, median 47 ms, p95 61 ms", with);

        string without = Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.None);
        Assert.DoesNotContain("model calls", without);

        string noLatency = Heartbeat.Format(0, 1, Empty(), 0, 0, Heartbeat.PlanState.None,
            modelCalls: 1, modelFallbacks: 0);
        Assert.Contains("model calls 1/fallback 0", noLatency);
        Assert.DoesNotContain("median", noLatency);
    }

    [Fact]
    public void Format_CollectedPlan_WordingReplacesNone()
    {
        // After a plan is collected the job is gone; the heartbeat should say "3 lines", not
        // "none" (week review, finding 10).
        string collected = Heartbeat.Format(6, 1, Empty(), 0, 0, Heartbeat.PlanState.None,
            planCollectedLines: 3);
        Assert.Contains("overnight plan 3 lines", collected);
        Assert.DoesNotContain("overnight plan none", collected);

        string none = Heartbeat.Format(6, 1, Empty(), 0, 0, Heartbeat.PlanState.None);
        Assert.Contains("overnight plan none", none);

        string zero = Heartbeat.Format(6, 1, Empty(), 0, 0, Heartbeat.PlanState.None,
            planCollectedLines: 0);
        Assert.Contains("overnight plan 0 lines", zero);
    }

    private static IReadOnlyDictionary<string, double> Empty()
        => new Dictionary<string, double>();
}
