using NpcDecision;
using NpcIntents;
using NpcMemory;
using NpcSchedules;
using Xunit;

namespace NpcIntents.Tests;

public class PlannerNewsTests
{
    private static readonly Newsworthiness News = new();

    // ---------- helpers ----------

    private static RegionMap Regions()
    {
        var map = new RegionMap
        {
            BlockMinutes = 120,
            Regions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Town"] = new[] { "Town", "SeedShop", "Saloon" },
                ["Mountain"] = new[] { "Mountain", "ScienceHouse" },
            },
        };
        map.Rebuild();
        return map;
    }

    private static NewsContext Context(string observer, int hearts = 0)
        => new(observer,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Abigail"] = "SeedShop",
                ["Caroline"] = "SeedShop", // lives with Abigail
                ["Sebastian"] = "ScienceHouse",
            },
            new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase),
            Regions(),
            hearts);

    private static NpcMemorySnapshot Snap(string npc, int tick, string subject, string kind, string? detail = null)
        => new(npc, "voice", new DiaryEntry[] { new(tick, subject, kind, detail) },
            Array.Empty<string>(), Context(npc));

    private static IntentPlanner Planner(IDecisionClient decision)
        => new(decision, new StubRenderer(), News);

    private sealed class StubRenderer : ILineRenderer
    {
        public string Render(string npc, string voice, DiaryEntry entry)
            => $"LINE:{entry.Kind}:{entry.Subject}";
    }

    private sealed class RecordingDecisionClient : IDecisionClient
    {
        public int YesNoCalls;
        public int ChooseCalls;
        public readonly List<List<string>> ChooseOptions = new();
        public readonly List<string> YesNoPropositions = new();

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            ChooseCalls++;
            ChooseOptions.Add(options.ToList());
            double p = 1.0 / options.Count;
            return options.Select(_ => p).ToArray();
        }

        public double YesNo(string context, string proposition)
        {
            YesNoCalls++;
            YesNoPropositions.Add(proposition);
            return 0.5;
        }

        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }

    // ---------- tests ----------

    [Fact]
    public void ZeroNewsEntries_AreNeverOptions()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Caroline", "Saw", "SeedShop"),                                  // housemate at home: 0
            new(20, "Player", "Talked", DiaryDetail.Format(("hearts", "2"))),        // 1: under MinNews 2
            new(30, "Player", "Saw", "Town"),                                        // 2: news
        }, Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        IntentPlan plan = Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("Saw", candidate.Source.Kind);
        Assert.Equal(1, client.ChooseCalls);
        string option = Assert.Single(client.ChooseOptions[0]);
        Assert.StartsWith("yesterday Abigail saw the player at", option); // plain phrase, not "Saw Player at ..."
        Assert.Equal("does Abigail have news for the player?", Assert.Single(client.YesNoPropositions));
    }

    [Fact]
    public void SnapshotWithNothingNewsworthy_SkipsWithoutAModelCall()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice",
            new DiaryEntry[] { new(10, "Caroline", "Saw", "SeedShop") }, // 0: housemate at home
            Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        IntentPlan plan = Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Empty(plan.Candidates);
        Assert.Equal(0, client.YesNoCalls);
        Assert.Equal(0, client.ChooseCalls);
    }

    [Fact]
    public void Options_AreOrderedByNews_NotRecency()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // news 4, older
            new(20, "Player", "Saw", "Town"),                                             // news 2, newer
        }, Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Equal(2, client.ChooseOptions[0].Count);
        Assert.StartsWith("yesterday was Abigail's birthday", client.ChooseOptions[0][0]); // best news first
        Assert.StartsWith("yesterday Abigail saw the player", client.ChooseOptions[0][1]);
    }

    [Fact]
    public void FakeBackend_SpeakersAreTheBestNewsNpcs()
    {
        // Abigail's Talked (1) is under MinNews 2 now, so she never reaches the model.
        var snapshots = new[]
        {
            Snap("Abigail", 20, "Player", "Talked", DiaryDetail.Format(("hearts", "2"))),          // 1: dropped
            Snap("Alex", 20, "Player", "Saw", "Town"),                                             // 2
            Snap("Sebastian", 20, "Player", "IgnoredBy", "Emote"),                                 // 3
            Snap("Robin", 20, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4
        };

        IntentPlan plan = new IntentPlanner(new FakeDecisionClient(), new StubRenderer(), News)
            .Plan(snapshots, 42, sourceDay: 0);

        Assert.Equal(new[] { "Robin", "Sebastian", "Alex" }, plan.Candidates.Select(c => c.Npc).ToArray());
        Assert.Equal(new[] { 4.0, 3.0, 2.0 }, plan.Candidates.Select(c => c.News).ToArray());
    }

    [Fact]
    public void EqualNews_TiesBreakByName()
    {
        var snapshots = new[]
        {
            Snap("Willy", 20, "Player", "Saw", "Town"), // 2
            Snap("Alex", 20, "Player", "Saw", "Town"),  // 2
        };

        IntentPlan plan = new IntentPlanner(new FakeDecisionClient(), new StubRenderer(), News)
            .Plan(snapshots, 42, sourceDay: 0);

        Assert.Equal(new[] { "Alex", "Willy" }, plan.Candidates.Select(c => c.Npc).ToArray());
    }

    [Fact]
    public void Ranking_UsesTheBestOptionNews_NotTheSampledOne()
    {
        // Abigail has two options: news 4 and news 2; a skewed Choose makes her sample the 2.
        // Willy has a single option of news 2. The ranking key is the BEST news among the
        // options (intents.md, "Deterministic rules"), so Abigail must still rank above Willy.
        var abigail = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4
            new(20, "Player", "Saw", "Town"),                                            // 2
        }, Array.Empty<string>(), Context("Abigail"));
        var willy = Snap("Willy", 20, "Player", "Saw", "Town"); // 2

        IntentPlan plan = new IntentPlanner(new SkewedClient(), new StubRenderer(), News)
            .Plan(new[] { abigail, willy }, 42, sourceDay: 0);

        Assert.Equal(new[] { "Abigail", "Willy" }, plan.Candidates.Select(c => c.Npc).ToArray());
        Assert.Equal(new[] { 4.0, 2.0 }, plan.Candidates.Select(c => c.News).ToArray());
    }

    [Fact]
    public void Speakers_RankByNewsBeforeProbability()
    {
        // Finding 4: the narrow yes/no band (0.47-0.60 in the week) must not decide the speakers.
        // A high-news NPC with a lukewarm 0.5 answer ranks above a low-news NPC the model loves.
        var abigail = Snap("Abigail", 20, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))); // 4
        var alex = Snap("Alex", 20, "Player", "Saw", "Town");                                                  // 2

        IntentPlan plan = new IntentPlanner(new PerNpcSpeakClient(), new StubRenderer(), News)
            .Plan(new[] { alex, abigail }, 42, sourceDay: 0);

        Assert.Equal(new[] { "Abigail", "Alex" }, plan.Candidates.Select(c => c.Npc).ToArray());
    }

    [Fact]
    public void Pick_BlendsNewsIntoUniformProbabilities()
    {
        // Uniform model answer: the pick must favour the newsier entry, not be a coin flip.
        // News 4 vs 2 -> roughly 2:1 across seeds (exact counts are deterministic per seed).
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4
            new(20, "Player", "Saw", "Town"),                                            // 2
        }, Array.Empty<string>(), Context("Abigail"));

        int birthday = 0, saw = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            IntentPlan plan = Planner(new RecordingDecisionClient()).Plan(new[] { snapshot }, seed, sourceDay: 0);
            if (Assert.Single(plan.Candidates).Source.Kind == "BirthdayForgotten")
                birthday++;
            else
                saw++;
        }

        Assert.Equal(300, birthday + saw);
        Assert.True(birthday > saw, $"newsier entry should win more often: {birthday} vs {saw}");
    }

    [Fact]
    public void Pick_ModelVetoCanStillOverrideTheNews()
    {
        // The blend is p * news: a confident zero on the newsy option is a veto, not a suggestion.
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4, vetoed
            new(20, "Player", "Saw", "Town"),                                            // 2, all the mass
        }, Array.Empty<string>(), Context("Abigail"));

        IntentPlan plan = Planner(new SkewedClient()).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Equal("Saw", Assert.Single(plan.Candidates).Source.Kind);
    }

    [Fact]
    public void Pick_DegenerateAnswers_FallBackToTheNewsWeights()
    {
        // An all-zero or all-NaN answer is not a usable distribution: the pure news weights
        // decide (news 4 vs 2 -> roughly 2:1), never a uniform coin flip (review item 4).
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "BirthdayForgotten", DiaryDetail.Format(("hearts", "3"))), // 4
            new(20, "Player", "Saw", "Town"),                                            // 2
        }, Array.Empty<string>(), Context("Abigail"));

        int birthday = 0, saw = 0;
        foreach (IDecisionClient client in new IDecisionClient[]
        {
            new FixedChooseClient(_ => new[] { 0.0, 0.0 }),
            new FixedChooseClient(_ => new[] { double.NaN, double.NaN }),
        })
        {
            for (int seed = 0; seed < 300; seed++)
            {
                IntentPlan plan = Planner(client).Plan(new[] { snapshot }, seed, sourceDay: 0);
                if (Assert.Single(plan.Candidates).Source.Kind == "BirthdayForgotten")
                    birthday++;
                else
                    saw++;
            }
        }

        Assert.True(birthday > saw, $"newsier entry should win more often: {birthday} vs {saw}");
    }

    /// <summary>A client returning a fixed probability list regardless of options.</summary>
    private sealed class FixedChooseClient : IDecisionClient
    {
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<double>> _choose;

        public FixedChooseClient(Func<IReadOnlyList<string>, IReadOnlyList<double>> choose)
            => _choose = choose;

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => _choose(options);

        public double YesNo(string context, string proposition) => 0.5;

        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }

    [Fact]
    public void GiftReceived_Love_Outranks_SawOfThePlayer()
    {
        var snapshot = new NpcMemorySnapshot("Abigail", "voice", new DiaryEntry[]
        {
            new(10, "Player", "Saw", "Town"),                    // 2
            new(20, "Player", "GiftReceived", "taste=Love"),     // 5
        }, Array.Empty<string>(), Context("Abigail"));

        var client = new RecordingDecisionClient();
        Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Equal(2, client.ChooseOptions[0].Count);
        Assert.StartsWith("yesterday the player gave Abigail", client.ChooseOptions[0][0]); // best news first
        Assert.StartsWith("yesterday Abigail saw the player", client.ChooseOptions[0][1]);
    }

    /// <summary>YesNo per NPC, read from the context's legacy "npc: X" anchor.</summary>
    private sealed class PerNpcSpeakClient : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
            => options.Select(_ => 1.0 / options.Count).ToArray();

        public double YesNo(string context, string proposition)
            => context.Contains("npc: Abigail", StringComparison.OrdinalIgnoreCase) ? 0.5 : 0.9;

        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }

    [Fact]
    public void SnapshotBeliefs_AreImmuneToLaterObserves()
    {
        // The planner's context carries a snapshot; the game thread keeps observing the live
        // belief. A later observe must not change what the planner sees (AGENTS.md: two threads).
        var live = new RoutineBelief("Abigail", "Willy", 120);
        live.Observe("Town", TimeUtils.BlockIndex(100 % 120, 120), 100, 12);
        var snapshot = live.Snapshot();

        var context = new NewsContext("Abigail",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Willy"] = "Saloon" },
            new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase) { ["Willy"] = snapshot },
            Regions(), 0);
        var entry = new DiaryEntry(100, "Willy", "Saw", "Mountain");

        double before = News.Score(entry, context);
        Assert.Equal(2.0, before); // unusual: evidence 12, Mountain share 0 < 0.15

        live.Observe("Mountain", TimeUtils.BlockIndex(100 % 120, 120), 100, 100); // would flip the verdict

        Assert.Equal(before, News.Score(entry, context)); // the snapshot did not change
    }

    [Fact]
    public void Festival_TalkedAtTheFestival_GainsOne()
    {
        var context = Context("Abigail");
        var seen = new DiaryEntry(20, "Player", "Festival", "festival=spring13;with=1");
        var missed = new DiaryEntry(20, "Player", "Festival", "festival=spring13;with=0");

        Assert.Equal(3.0, News.Score(seen, context));   // 2 base + 1 talked (diary.md kinds table)
        Assert.Equal(2.0, News.Score(missed, context));
    }

    /// <summary>Chooses the LAST option (all probability mass on it), so a multi-option NPC
    /// always samples their worst ranked one.</summary>
    private sealed class SkewedClient : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            var p = new double[options.Count];
            if (options.Count > 0)
                p[^1] = 1.0;
            return p;
        }

        public double YesNo(string context, string proposition) => 0.5;
        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }

    [Fact]
    public void BatchBackend_SpeakAndPickInOneRequest()
    {
        // A batch-capable backend gets ONE Ask carrying the speak and pick questions
        // (docs/spec/laya.md, "Data model"): half the round trips per NPC.
        var snapshot = Snap("Haley", 20, "Player", "Saw", "Town"); // 2: above MinNews
        var client = new BatchRecordingClient();

        IntentPlan plan = Planner(client).Plan(new[] { snapshot }, 42, sourceDay: 0);

        Assert.Equal(1, client.AskCalls);
        Assert.Equal(2, client.LastQuestions!.Count);
        Assert.Contains(client.LastQuestions, q => q is YesNoQuestion
        {
            Proposition: "does Haley have news for the player?",
        });
        Assert.Contains(client.LastQuestions, q => q is ChoiceQuestion);
        IntentCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("Haley", candidate.Npc);
    }

    /// <summary>A batch-capable decision client: speak 0.6, uniform pick; records the asks.</summary>
    private sealed class BatchRecordingClient : IDecisionClient, IBatchDecisionClient
    {
        public int AskCalls;
        public IReadOnlyList<Question>? LastQuestions;

        public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
        {
            AskCalls++;
            LastQuestions = questions;
            var answers = new List<Answer>(questions.Count);
            foreach (Question question in questions)
            {
                answers.Add(question switch
                {
                    YesNoQuestion => Answer.FromYesNo(question.Id, 0.6),
                    ChoiceQuestion choice => Answer.FromChoice(question.Id,
                        choice.Options.Select(_ => 1.0 / choice.Options.Count).ToArray()),
                    ScoreQuestion score => Answer.FromScore(question.Id, (score.Min + score.Max) / 2.0),
                    _ => throw new ArgumentException(),
                });
            }
            return answers;
        }

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
            => options.Select(_ => 1.0 / options.Count).ToArray();

        public double YesNo(string context, string proposition) => 0.5;
        public double Score(string context, double min, double max) => (min + max) / 2.0;
    }
}
