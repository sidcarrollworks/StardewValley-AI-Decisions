using NpcDecision;
using NpcIntents;
using NpcMemory;
using Xunit;

namespace NpcIntents.Tests;

public class IntentPlannerTests
{
    // ---------- helpers ----------

    private static DiaryEntry Entry(int tick, string subject, string kind, string? detail = null)
        => new(tick, subject, kind, detail);

    private static NpcMemorySnapshot Snapshot(string npc, params DiaryEntry[] diary)
        => Snapshot(npc, Array.Empty<string>(), diary);

    private static NpcMemorySnapshot Snapshot(string npc, IReadOnlyList<string> recentLines, params DiaryEntry[] diary)
        => new(npc, VoiceSheets.Voice(npc), diary, recentLines);

    private static IntentPlanner Planner(
        IDecisionClient decision,
        IntentPlannerOptions? options = null)
        => new(decision, new StubRenderer(), null, options);

    /// <summary>Templated line that exposes which entry was rendered.</summary>
    private sealed class StubRenderer : ILineRenderer
    {
        public string Render(string npc, string voice, DiaryEntry entry)
            => $"LINE:{entry.Kind}:{entry.Subject}:{entry.Detail}";
    }

    /// <summary>Custom stub: yes/no is a function of the context; choose is a function of options.</summary>
    private sealed class StubDecisionClient : IDecisionClient
    {
        private readonly Func<string, double> _yesNo;
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<double>> _choose;

        public StubDecisionClient(
            Func<string, double> yesNo,
            Func<IReadOnlyList<string>, IReadOnlyList<double>>? choose = null)
        {
            _yesNo = yesNo;
            _choose = choose ?? Uniform;
        }

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => _choose(options);
        public double Score(string context, double min, double max) => (min + max) / 2.0;
        public double YesNo(string context, string proposition) => _yesNo(context);

        private static IReadOnlyList<double> Uniform(IReadOnlyList<string> options)
            => options.Count == 0 ? Array.Empty<double>() : options.Select(_ => 1.0 / options.Count).ToArray();
    }

    /// <summary>Stub that records every call, for asserting what the planner asked.</summary>
    private sealed class RecordingDecisionClient : IDecisionClient
    {
        private readonly double _yesNo;

        public RecordingDecisionClient(double yesNo = 0.9) => _yesNo = yesNo;

        public List<(IReadOnlyList<string> Options, string Context)> ChooseCalls { get; } = new();
        public List<string> YesNoContexts { get; } = new();

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            ChooseCalls.Add((options, context));
            return options.Count == 0 ? Array.Empty<double>() : options.Select(_ => 1.0 / options.Count).ToArray();
        }

        public double Score(string context, double min, double max) => (min + max) / 2.0;

        public double YesNo(string context, string proposition)
        {
            YesNoContexts.Add(context);
            return _yesNo;
        }
    }

    // ---------- tests ----------

    [Fact]
    public void EmptySnapshots_ProducesEmptyPlan()
    {
        IntentPlan plan = Planner(new FakeDecisionClient())
            .Plan(Array.Empty<NpcMemorySnapshot>(), seed: 1);

        Assert.Empty(plan.Candidates);
    }

    [Fact]
    public void NpcWithEmptyDiary_DoesNotSpeak()
    {
        // Even a client that always says yes cannot produce a candidate with no diary to cite.
        var planner = Planner(new StubDecisionClient(_ => 0.9));

        IntentPlan plan = planner.Plan(new[] { Snapshot("Abigail") }, seed: 1);

        Assert.Empty(plan.Candidates);
    }

    [Fact]
    public void Cap_KeepsAtMostMaxNpcsPerDay()
    {
        var snapshots = new[]
        {
            Snapshot("Abigail", Entry(1, "Player", "Saw")),
            Snapshot("Penny", Entry(1, "Player", "Saw")),
            Snapshot("Sam", Entry(1, "Player", "Saw")),
            Snapshot("Leah", Entry(1, "Player", "Saw")),
            Snapshot("Sebastian", Entry(1, "Player", "Saw")),
        };

        // FakeDecisionClient: yes/no = 0.5 (>= threshold), uniform choice over one option.
        IntentPlan plan = Planner(new FakeDecisionClient(), new IntentPlannerOptions { MaxNpcsPerDay = 3 })
            .Plan(snapshots, seed: 5);

        Assert.Equal(3, plan.Candidates.Count);
        Assert.Equal(3, plan.Candidates.Select(c => c.Npc).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Dedupe_SkipsLineAlreadySaid()
    {
        // The single entry renders to "LINE:Saw:Player:" — already said (case-insensitively).
        var snapshot = new NpcMemorySnapshot(
            "Abigail",
            VoiceSheets.Voice("Abigail"),
            new[] { Entry(1, "Player", "Saw") },
            new[] { "line:saw:player:" });

        IntentPlan plan = Planner(new StubDecisionClient(_ => 0.9)).Plan(new[] { snapshot }, seed: 2);

        Assert.Empty(plan.Candidates);
    }

    [Fact]
    public void NonDuplicateRecentLine_StillSpeaks()
    {
        var snapshot = new NpcMemorySnapshot(
            "Abigail",
            VoiceSheets.Voice("Abigail"),
            new[] { Entry(1, "Player", "Saw") },
            new[] { "something else entirely" });

        IntentPlan plan = Planner(new StubDecisionClient(_ => 0.9)).Plan(new[] { snapshot }, seed: 2);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("LINE:Saw:Player:", candidate.Line);
    }

    [Fact]
    public void Determinism_SameSeedSameSnapshots_SamePlan()
    {
        var snapshots = new[]
        {
            Snapshot("Abigail",
                Entry(10, "Player", "Saw", "SeedShop"),
                Entry(11, "Robin", "SpokeWith"),
                Entry(12, "Player", "ReceivedGift", "Amethyst")),
            Snapshot("Sebastian",
                Entry(10, "Sam", "SpokeWith"),
                Entry(12, "Player", "Saw", "Mountain")),
            Snapshot("Leah",
                Entry(11, "Player", "Saw", "Forest")),
        };

        var planner = Planner(new FakeDecisionClient());

        IntentPlan first = planner.Plan(snapshots, seed: 42);
        IntentPlan second = planner.Plan(snapshots, seed: 42);

        Assert.Equal(
            first.Candidates.Select(c => (c.Npc, c.Line, c.Source, c.Reason)),
            second.Candidates.Select(c => (c.Npc, c.Line, c.Source, c.Reason)));
        Assert.NotEmpty(first.Candidates);
    }

    [Fact]
    public void Threshold_OnlyHighProbabilityNpcSpeaks()
    {
        // Context carries the voice; Abigail's is "spirited...", Sebastian's is "reserved...".
        var planner = Planner(new StubDecisionClient(
            ctx => ctx.Contains("spirited", StringComparison.OrdinalIgnoreCase) ? 0.9 : 0.1));

        var snapshots = new[]
        {
            Snapshot("Abigail", Entry(1, "Player", "Saw")),
            Snapshot("Sebastian", Entry(1, "Player", "Saw")),
        };

        IntentPlan plan = planner.Plan(snapshots, seed: 3);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("Abigail", candidate.Npc);
    }

    [Fact]
    public void Reason_MentionsCitedEntryAndProbability()
    {
        var planner = Planner(new FakeDecisionClient());

        IntentPlan plan = planner.Plan(
            new[] { Snapshot("Abigail", Entry(7, "Player", "Saw", "SeedShop")) }, seed: 4);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("LINE:Saw:Player:SeedShop", candidate.Line);
        Assert.Equal(new DiaryEntry(7, "Player", "Saw", "SeedShop"), candidate.Source);
        Assert.Contains("saw the player", candidate.Reason);
        Assert.Contains("p=", candidate.Reason);
    }

    [Fact]
    public void Sampling_PicksNonArgmaxForSomeSeeds()
    {
        // Two options with a heavily skewed distribution: argmax would always pick "Player".
        // Sampling must sometimes pick the 0.1 option across seeds.
        var planner = Planner(new StubDecisionClient(
            _ => 0.9,
            options => options.Count == 2 ? new[] { 0.9, 0.1 } : new[] { 1.0 }));

        var snapshot = Snapshot("Abigail",
            Entry(1, "Player", "Saw"),
            Entry(2, "Robin", "SpokeWith"));

        var picks = new HashSet<string>();
        for (int seed = 0; seed < 100; seed++)
        {
            IntentPlan plan = planner.Plan(new[] { snapshot }, seed);
            IntentCandidate candidate = Assert.Single(plan.Candidates);
            picks.Add(candidate.Source.Subject);
        }

        Assert.Contains("Player", picks);
        Assert.Contains("Robin", picks); // proves it is sampling, not argmax
    }

    [Fact]
    public void ChooseZeroLengthProbabilities_FallsBackToUniformPick()
    {
        var planner = Planner(new StubDecisionClient(_ => 0.9, _ => Array.Empty<double>()));

        IntentPlan plan = planner.Plan(
            new[] { Snapshot("Abigail", Entry(1, "Player", "Saw"), Entry(2, "Robin", "SpokeWith")) }, seed: 9);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Contains(candidate.Source.Subject, new[] { "Player", "Robin" });
    }

    [Fact]
    public void ChooseAllZeroProbabilities_FallsBackToUniformPick()
    {
        var planner = Planner(new StubDecisionClient(_ => 0.9, options => options.Select(_ => 0.0).ToArray()));

        IntentPlan plan = planner.Plan(
            new[] { Snapshot("Abigail", Entry(1, "Player", "Saw"), Entry(2, "Robin", "SpokeWith")) }, seed: 11);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Contains(candidate.Source.Subject, new[] { "Player", "Robin" });
    }

    [Fact]
    public void Ordering_HigherProbabilityFirst()
    {
        var planner = Planner(new StubDecisionClient(ctx =>
            ctx.Contains("spirited", StringComparison.OrdinalIgnoreCase) ? 0.9 :
            ctx.Contains("reserved", StringComparison.OrdinalIgnoreCase) ? 0.7 : 0.5));

        var snapshots = new[]
        {
            Snapshot("Sam", Entry(1, "Player", "Saw")),        // 0.5
            Snapshot("Abigail", Entry(1, "Player", "Saw")),    // 0.9
            Snapshot("Sebastian", Entry(1, "Player", "Saw")),  // 0.7
        };

        IntentPlan plan = planner.Plan(snapshots, seed: 1);

        Assert.Equal(new[] { "Abigail", "Sebastian", "Sam" }, plan.Candidates.Select(c => c.Npc));
    }

    [Fact]
    public void Ordering_TiesBreakByNameAscending()
    {
        // All three have the same yes/no probability, so names decide the order (ordinal, ignore case).
        var planner = Planner(new StubDecisionClient(_ => 0.8));

        var snapshots = new[]
        {
            Snapshot("Zed", Entry(1, "Player", "Saw")),
            Snapshot("abigail", Entry(1, "Player", "Saw")),
            Snapshot("Marnie", Entry(1, "Player", "Saw")),
        };

        IntentPlan plan = planner.Plan(snapshots, seed: 1);

        Assert.Equal(new[] { "abigail", "Marnie", "Zed" }, plan.Candidates.Select(c => c.Npc));
    }

    [Fact]
    public void UsesOnlyNewestMaxRecentDiaryEntries_AndBriefContext()
    {
        var recorder = new RecordingDecisionClient(yesNo: 0.9);
        var planner = Planner(recorder, new IntentPlannerOptions { MaxRecentDiaryEntries = 3, MaxNpcsPerDay = 3 });

        var diary = Enumerable.Range(1, 7)
            .Select(i => Entry(i, $"S{i}", "Saw"))
            .ToArray();

        IntentPlan plan = planner.Plan(new[] { Snapshot("Abigail", diary) }, seed: 3);

        Assert.Single(recorder.ChooseCalls);
        IReadOnlyList<string> options = recorder.ChooseCalls[0].Options;
        Assert.Equal(3, options.Count);
        Assert.Contains("S5", options[0]);
        Assert.Contains("S7", options[2]);
        Assert.DoesNotContain(options, o => o.Contains("S4"));

        // Context must carry the voice and a diary summary, and stay short.
        string context = recorder.YesNoContexts.Single();
        Assert.Contains("spirited", context);
        Assert.Contains("S7", context);
        Assert.True(context.Length < 512, "context should stay compact");

        Assert.Single(plan.Candidates);
    }
}
