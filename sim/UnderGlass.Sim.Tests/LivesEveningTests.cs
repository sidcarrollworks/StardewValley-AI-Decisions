using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Fictional housemates and scripted typed weights exercise the real plan lifecycle.
/// The fixture's generous offer/torn settings are branch controls, not the pilot defaults.</summary>
public sealed class LivesEveningTests
{
    private const string Home = "Home:House";
    private static Villager Person(string name, Job? job = null, IReadOnlyList<KeptHour>? bedtimes = null) =>
        new(name, "House", "villager", new Temperament(.5, .5, .5, .5), new Body(100, -1), job,
            Array.Empty<Haunt>(), new Dictionary<string, double>(), Array.Empty<string>(), Age: 30, Bedtimes: bedtimes);

    private static TownData Town(Job? boJob = null, IReadOnlyList<KeptHour>? annBed = null,
        IReadOnlyList<KeptHour>? boBed = null)
    {
        var feelings = new FeelingOptions();
        feelings.Acts.With("company,invitations");
        return new()
        {
            Cast = new[] { Person("Ann", bedtimes: annBed), Person("Bo", boJob, boBed) },
            Places = new[] { new Location(Home, false, Enumerable.Repeat(new string('.', 20), 12).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = ActCatalog.Kinds(feelings.Acts, Array.Empty<ActKind>()), Feelings = feelings,
            Body = new BodyOptions { AwakeHoursAtRest = 100_000, CircadianWeight = 0 },
            Gossip = new GossipOptions { ChatChance = 0 }, Authority = new AuthorityOptions { ElectConstable = false }, Wander = 0,
        };
    }

    private static HistoryPack History() => new(1,
        new[] { new HistorySource("source", "synthetic:evening", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "fact", "Ann", "We have spent time together.", KnowledgeKind.Lived,
            new[] { "source" }, new[] { new HistoryCue("person:Bo") }) },
        new[] { new HistoryReading("reading", "account", new(MemoryObjectKind.Person, "Bo"), ReadingStance.Warm,
            "I enjoy Bo's company.", ReadingOrigin.CanonStated, AdmissionRule.R2,
            new(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15"), new[] { "source" },
            new[] { new HistoryCue("person:Bo") }) });

    private static EveningOptions On(bool record = true) => new()
    {
        Enabled = true, Record = record, PlanBase = 1000, StartMinute = 18 * 60, EndMinute = 20 * 60,
        PassiveExpectations = false,
    };

    private sealed class Scripted(Func<LivesDecisionRequest, string>? choose = null, bool yield = false) : ILivesDecisionMind
    {
        public readonly List<LivesDecisionRequest> Requests = new();
        public async Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (yield) await Task.Yield();
            string selected = choose?.Invoke(request) ?? (request.Fallback.Opportunity.Trigger switch
            { "offer" => "offer", "answer" => "yes", "follow-through" => "keep", _ => "carry-on" });
            Assert.Contains(request.Fallback.Alternatives, alternative => alternative.Option.Id == selected);
            return new(request.Fallback.Alternatives.ToDictionary(a => a.Option.Id, a => a.Option.Id == selected ? 1.0 : 0),
                "scripted-test", request.Model);
        }
    }

    private sealed record RunResult(Simulation Sim, SimResult Result, string Trace);
    private static async Task<RunResult> Run(ILivesDecisionMind? mind = null, EveningOptions? options = null,
        TownData? town = null, int days = 1, long seed = 7)
    {
        var sim = new Simulation(seed, town ?? Town());
        sim.ConfigureLivesRouting(new() { Enabled = true, Choices = new(5, 0, 1) }, History());
        sim.ConfigureLivesCompany(new() { Enabled = true, OfferBase = 1e-12, SeekBase = 1e-12, AnswerScale = 0 });
        sim.ConfigureLivesEvenings(options ?? On(), mind);
        var trace = new List<object>();
        var result = await sim.RunAsync(days, new AuthoredReflectionMind(), (m, s) => trace.Add(new
        {
            m, ann = s.Where("Ann"), bo = s.Where("Bo"), annMood = s.Mood("Ann"), boMood = s.Mood("Bo"),
        }));
        return new(sim, result, JsonSerializer.Serialize(trace));
    }
    private static string Json(object value) => JsonSerializer.Serialize(value);

    [Fact]
    public async Task AcceptedPlanOnlyStartsAtItsWindowAndCompletesARealGame()
    {
        var run = await Run(new Scripted());
        var completed = Assert.Single(run.Result.LivesEveningUpdates.Where(u => u.Plan.Stage == EveningStage.Completed));
        EveningPlan plan = completed.Plan;
        var stages = run.Result.LivesEveningUpdates.Where(u => u.Plan.Id == plan.Id).ToArray();
        Assert.Contains(stages, row => row.Plan.Stage == EveningStage.Pending && row.At < plan.Start);
        Assert.Contains(stages, row => row.Plan.Stage == EveningStage.Agreed && row.At < plan.Start);
        var playing = Assert.Single(stages.Where(row => row.Plan.Stage == EveningStage.Playing));
        Assert.InRange(playing.At, plan.Start, plan.End);
        Act game = run.Result.Acts[plan.ActId!.Value];
        Assert.Equal("PlayedGame", game.Kind);
        Assert.Equal(Home, game.Location);
        Assert.Equal(playing.At, game.Tick);
        Assert.Equal(2, run.Result.Feelings.Count(f => f.ActId == game.Id && f.Route == "SharedActivity"));
        foreach (string holder in new[] { "Ann", "Bo" })
        {
            string partner = holder == "Ann" ? "Bo" : "Ann";
            var learned = run.Sim.LivesMemoryOf(holder)!.Expectation(EveningPolicy.FollowKey(partner, Home));
            Assert.Contains(learned.Entries, entry => entry.Success && entry.KnownAt >= plan.Start);
            Assert.DoesNotContain(learned.Entries, entry => entry.KnownAt < plan.Start);
        }
    }

    [Fact]
    public async Task RecorderOffAndAsyncLatencyDoNotChangeChoicesOrTheTown()
    {
        var visibleMind = new Scripted();
        var quietMind = new Scripted(yield: true);
        var visible = await Run(visibleMind);
        var quiet = await Run(quietMind, On(false));
        Assert.Equal(Metrics.LogHash(visible.Result), Metrics.LogHash(quiet.Result));
        Assert.Equal(visible.Trace, quiet.Trace);
        Assert.Equal(Json(visible.Result.Sleeps), Json(quiet.Result.Sleeps));
        Assert.Equal(Json(visibleMind.Requests), Json(quietMind.Requests));
        Assert.Equal(Json(visible.Sim.LivesMemoryOf("Ann")!.Snapshot()), Json(quiet.Sim.LivesMemoryOf("Ann")!.Snapshot()));
        Assert.Empty(quiet.Result.LivesEveningUpdates);
        Assert.Empty(quiet.Result.LivesEveningChoices);
        Assert.Empty(quiet.Result.LivesDecisions);
    }

    [Fact]
    public async Task WholeRunReplaysFromItsExactTapeWithUniqueIdsAndBoundedCalls()
    {
        var first = await Run(new Scripted(), days: 2);
        Assert.NotEmpty(first.Result.LivesDecisions);
        var ids = first.Result.LivesEveningChoices.Select(c => c.Decision.Opportunity.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(first.Result.LivesDecisions.GroupBy(r => (r.Request.Fallback.Opportunity.Holder,
            Clock.Day(r.Request.Fallback.Opportunity.At))), group => Assert.InRange(group.Count(), 1, 2));
        Assert.All(first.Result.LivesDecisions, record => Assert.True(record.Request.Fallback.Torn));
        var tape = new RecordedLivesDecisionMind(first.Result.LivesDecisions);
        var replay = await Run(tape, days: 2);
        tape.RequireConsumed();
        Assert.Equal(first.Trace, replay.Trace);
        Assert.Equal(Metrics.LogHash(first.Result), Metrics.LogHash(replay.Result));
        Assert.Equal(Json(first.Result.LivesDecisions), Json(replay.Result.LivesDecisions));
    }

    [Fact]
    public async Task RefusalLearnsAnAnswerAndDoesNotInventAPlayedGame()
    {
        var mind = new Scripted(r => r.Fallback.Opportunity.Trigger == "answer" ? "no" : "offer");
        var run = await Run(mind);
        var refused = Assert.Single(run.Result.LivesEveningUpdates.Where(u => u.Plan.Stage == EveningStage.Refused));
        Assert.Null(refused.Plan.ActId);
        var memory = run.Sim.LivesMemoryOf("Ann")!;
        Assert.Contains(memory.Expectation(EveningPolicy.AnswerKey("Bo")).Entries,
            entry => !entry.Success && entry.KnownAt == refused.At);
        Assert.Empty(memory.Expectation(EveningPolicy.FollowKey("Bo", Home)).Entries);
        Assert.DoesNotContain(run.Result.LivesEveningUpdates, u => u.Plan.Stage is EveningStage.Playing or EveningStage.Completed);
    }

    [Fact]
    public async Task NotNowLearnsAvailabilityAndKeepsRefusalSeparate()
    {
        var job = new Job(Home, DefaultTown.Sofa, 30, 90, Array.Empty<int>(), 0, Commute: 0);
        var mind = new Scripted(r => r.Fallback.Opportunity.Trigger == "answer" ? "not-now" : "offer");
        var run = await Run(mind, town: Town(boJob: job));
        var postponed = Assert.Single(run.Result.LivesEveningUpdates.Where(u => u.Plan.Stage == EveningStage.NotNow));
        Assert.Null(postponed.Plan.ActId);
        var memory = run.Sim.LivesMemoryOf("Ann")!;
        Assert.Contains(memory.Expectation(EveningPolicy.AvailabilityKey("Bo")).Entries, entry => !entry.Success);
        Assert.Empty(memory.Expectation(EveningPolicy.AnswerKey("Bo")).Entries);
        Assert.Empty(memory.Expectation(EveningPolicy.FollowKey("Bo", Home)).Entries);
    }

    [Fact]
    public async Task OwnDecisionNotToKeepDoesNotAccuseThePartnerWhoCame()
    {
        var mind = new Scripted(r => r.Fallback.Opportunity.Trigger switch
        {
            "offer" => "offer", "answer" => "yes",
            "follow-through" when r.Fallback.Opportunity.Holder == "Ann" => "carry-on", _ => "keep",
        });
        var run = await Run(mind);
        Assert.Contains(run.Result.LivesEveningUpdates, u => u.Plan.Stage == EveningStage.Missed);
        var ann = run.Sim.LivesMemoryOf("Ann")!;
        Assert.DoesNotContain(ann.Expectation(EveningPolicy.FollowKey("Bo", Home)).Entries, e => !e.Success);
        Assert.DoesNotContain(run.Result.LivesEveningOutcomes, o => o.Holder == "Ann" && o.Felt < 0);
        Assert.DoesNotContain(run.Sim.LivesMemoryOf("Bo")!.Expectation(EveningPolicy.FollowKey("Ann", Home)).Entries, e => !e.Success);
    }

    [Fact]
    public async Task StartingThenInterruptingAGameDoesNotEraseThePartnersArrival()
    {
        var job = new Job(Home, DefaultTown.Sofa, 18 * 60 + 10, 21 * 60, Array.Empty<int>(), 0, Commute: 0);
        var run = await Run(new Scripted(), town: Town(boJob: job));
        var interrupted = Assert.Single(run.Result.LivesEveningUpdates.Where(u => u.Plan.Stage == EveningStage.Interrupted));
        Assert.NotNull(interrupted.Plan.ActId);
        foreach (string holder in new[] { "Ann", "Bo" })
        {
            string partner = holder == "Ann" ? "Bo" : "Ann";
            var memory = run.Sim.LivesMemoryOf(holder)!;
            Assert.Contains(memory.Expectation(EveningPolicy.FollowKey(partner, Home)).Entries, entry => entry.Success);
            Assert.DoesNotContain(memory.Expectation(EveningPolicy.FollowKey(partner, Home)).Entries, entry => !entry.Success);
            Assert.Contains(memory.Expectation(CompanyPolicy.ActivityKey(partner, Home)).Entries, entry => !entry.Success);
        }
        Assert.DoesNotContain(run.Result.Feelings, f => f.ActId == interrupted.Plan.ActId && f.Route == "SharedActivity");
    }

    [Fact]
    public async Task EarlierHabitDoesNotBanAnEveningOptionOrExposeTheOthersBedtime()
    {
        var habit = new KeptHour(17 * 60, Array.Empty<int>(), Array.Empty<int>(), Assumption: "Synthetic habit");
        var town = Town(annBed: new[] { habit }, boBed: new[] { habit with { At = 22 * 60 } });
        var mind = new Scripted(r => "carry-on");
        var run = await Run(mind, town: town);
        var offer = Assert.Single(run.Result.LivesEveningChoices.Where(c => c.Phase == "offer"));
        Assert.Contains(offer.Decision.Alternatives, a => a.Option.Id == "offer");
        Assert.DoesNotContain("22:", Assert.Single(mind.Requests).Context);
        Assert.Equal(habit, town.Cast.Single(p => p.Name == "Ann").Bedtimes![0]);
    }

    [Fact]
    public async Task RecalledHistoryBelongsOnlyToTheDecidingHolder()
    {
        var run = await Run(new Scripted());
        var choices = run.Result.LivesEveningChoices;
        Assert.Contains(choices, c => c.Decision.Opportunity.Holder == "Ann" && c.Decision.Recall.Items.Any(m => m.Id == "reading"));
        Assert.All(choices, choice => Assert.All(choice.Decision.Recall.Items, memory =>
        {
            Assert.Equal(choice.Decision.Opportunity.Holder, memory.Holder);
            Assert.True(memory.KnownAt <= choice.Decision.Opportunity.At);
        }));
        Assert.All(choices.Where(c => c.Decision.Opportunity.Holder == "Bo"), choice =>
            Assert.DoesNotContain(choice.Decision.Recall.Items, memory => memory.Id is "account" or "reading"));
    }

    [Fact]
    public async Task FuturePlansRetainTheCompanyMinimumAgeGate()
    {
        TownData town = Town();
        town = town with { Cast = town.Cast.Select(p => p.Name == "Bo" ? p with { Age = 3 } : p).ToArray() };
        var mind = new Scripted();
        var run = await Run(mind, town: town);
        Assert.Empty(run.Result.LivesEveningChoices);
        Assert.Empty(run.Result.LivesEveningUpdates);
        Assert.Empty(mind.Requests);
    }
}
