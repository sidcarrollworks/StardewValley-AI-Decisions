using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Invented households and owned expectations isolate the observer's knowledge.
/// No test rearranges the real pilot's routines to make a particular outcome happen.</summary>
public sealed class LivesEveningExpectationTests
{
    private const string Home = "Home:Watch";
    private const string OtherHome = "Home:Other";
    private static readonly ExpectationKey Expected = CompanyPolicy.WhereaboutsKey("Bea", Home, 18 * 60);

    private static Villager Person(string name, string house, Job? job = null, IReadOnlyList<Haunt>? haunts = null)
        => new(name, house, "villager", new Temperament(.5, .5, .5, .5), new Body(100, -1),
            job, haunts ?? Array.Empty<Haunt>(), new Dictionary<string, double>(), Array.Empty<string>());

    private static TownData Town(bool together = false, Job? watcherJob = null, Job? visitorJob = null,
        IReadOnlyList<Haunt>? visitorHaunts = null)
    {
        var feelings = new FeelingOptions();
        feelings.Acts.With("company,invitations");
        return new()
        {
            Cast = new[] { Person("Ann", "Watch", watcherJob), Person("Bea", together ? "Watch" : "Other", visitorJob, visitorHaunts) },
            Places = new[] { new Location(Home, false, Enumerable.Repeat(new string('.', 12), 10).ToArray()),
                new Location(OtherHome, false, Enumerable.Repeat(new string('.', 12), 10).ToArray()) },
            Links = new[] { new Link(Home, new(5, 7), OtherHome, new(5, 7)) },
            Gatherings = Array.Empty<Gathering>(),
            Acts = ActCatalog.Kinds(feelings.Acts, new[] { DefaultTown.Acts().Single(k => k.Name == "HelpedSomeone") with { PerDay = 0 } }),
            Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000, CircadianWeight = 0 },
            Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static HistoryPack History(bool positive = true, bool seed = true) => new(1,
        new[] { new HistorySource("source", "synthetic:Ann/regular", HistorySourceKind.Canon) },
        new[] { new HistoryFact("practice", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "practice", "Ann", "Bea often visits in the evening.", KnowledgeKind.Witnessed,
            new[] { "source" }, new[] { new HistoryCue("person:Bea") }) },
        seed ? new[] { new HistoryReading("expected", "account", new(MemoryObjectKind.Person, "Bea"), ReadingStance.Expects,
            "I expect Bea here in the evening.", ReadingOrigin.CanonStated, AdmissionRule.R2,
            new(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15"), new[] { "source" },
            new[] { new HistoryCue("person:Bea") }, Expectation: Expected, ExpectedSuccess: positive) }
            : Array.Empty<HistoryReading>());

    private static Simulation Simulation(TownData? town = null, HistoryPack? history = null, bool observe = true,
        bool record = true, IReadOnlyList<string>? participants = null, ILivesDecisionMind? mind = null)
    {
        var sim = new Simulation(7, town ?? Town());
        sim.ConfigureLivesRouting(new() { Enabled = true }, history ?? History());
        sim.ConfigureLivesCompany(new() { Enabled = true });
        sim.ConfigureLivesEvenings(new() { Enabled = true, Record = record, PassiveExpectations = observe,
            Participants = participants ?? new[] { "Ann", "Bea" } }, mind);
        return sim;
    }

    private sealed class KindnessMind : ILivesDecisionMind
    {
        public readonly List<LivesDecisionRequest> Requests = new();
        public Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            string selected = request.Fallback.Opportunity.Trigger switch
            { "offer" => "offer", "answer" => "yes", "follow-through" => "keep", _ => "kindness" };
            return Task.FromResult(new LivesDecisionAnswer(request.Fallback.Alternatives.ToDictionary(
                a => a.Option.Id, a => a.Option.Id == selected ? 1.0 : 0.0), "test", request.Model));
        }
    }

    [Fact]
    public void AWholeObservedMissLearnsOnlyOwnWhereaboutsWithoutInventingARefusalOrHurt()
    {
        var sim = Simulation();
        SimResult result = sim.Run(1);
        var missed = Assert.Single(result.LivesEveningOutcomes.Where(o => o.Kind == "whereabouts-missed"));
        Assert.Equal("Ann", missed.Holder);
        Assert.Equal(20 * 60 + 59, missed.At);
        Assert.Equal(0, missed.Felt);
        ExpectationSnapshot learned = sim.LivesMemoryOf("Ann")!.Expectation(Expected);
        Assert.Equal(2.0 / 3, learned.Chance, 12);
        ExpectationEntry entry = Assert.Single(learned.Entries.Where(e => !e.Seed));
        Assert.Equal(KnowledgeKind.Witnessed, entry.How);
        Assert.False(entry.Success);
        Assert.Empty(sim.LivesMemoryOf("Ann")!.Snapshot().Associations);
        Assert.Empty(sim.LivesMemoryOf("Bea")!.Snapshot().Expectations);
        Assert.DoesNotContain(result.Acts, a => a.Kind == "Refused");
        Assert.All(result.LivesEveningChoices.Where(c => c.Phase == "expectation-miss"), choice =>
            Assert.Equal(new[] { "kindness", "leave-alone" }, choice.Decision.Alternatives.Select(a => a.Option.Id)));
    }

    [Fact]
    public void ActualLocalSightProducesFoundRatherThanAMissingWindow()
    {
        var sim = Simulation(Town(together: true));
        SimResult result = sim.Run(1);
        Assert.Single(result.LivesEveningOutcomes.Where(o => o.Kind == "whereabouts-found"));
        Assert.DoesNotContain(result.LivesEveningOutcomes, o => o.Kind == "whereabouts-missed");
        Assert.True(Assert.Single(sim.LivesMemoryOf("Ann")!.Expectation(Expected).Entries.Where(e => !e.Seed)).Success);
        Assert.DoesNotContain(result.LivesEveningChoices, c => c.Phase == "expectation-miss");
    }

    [Fact]
    public void BeingAwayForPartOfTheWindowCannotEstablishAnAbsence()
    {
        var job = new Job(OtherHome, new(3, 3), 18 * 60 + 30, 20 * 60, Array.Empty<int>(), 0, Commute: 0);
        var sim = Simulation(Town(watcherJob: job));
        SimResult result = sim.Run(1);
        Assert.DoesNotContain(result.LivesEveningOutcomes, o => o.Kind == "whereabouts-missed");
        Assert.Single(sim.LivesMemoryOf("Ann")!.Expectation(Expected).Entries);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EmptyOptimisticPriorsAndAlreadyUnlikelyHistoriesDoNotStartMonitoring(bool seed, bool positive)
    {
        var sim = Simulation(history: History(seed: seed, positive: positive));
        SimResult result = sim.Run(1);
        Assert.Empty(result.LivesEveningOutcomes);
        Assert.Empty(result.LivesEveningChoices);
    }

    [Fact]
    public void ObservationSwitchAndParticipantFilterPreventPassiveLearning()
    {
        foreach (Simulation sim in new[] { Simulation(observe: false), Simulation(participants: new[] { "Bea" }) })
        {
            SimResult result = sim.Run(1);
            Assert.Empty(result.LivesEveningOutcomes);
            Assert.Single(sim.LivesMemoryOf("Ann")!.Expectation(Expected).Entries);
        }
    }

    [Fact]
    public void RecordingDoesNotFeedObservationOrTheConcernChoice()
    {
        var recorded = Simulation();
        var silent = Simulation(record: false);
        SimResult with = recorded.Run(2), without = silent.Run(2);
        Assert.Equal(Metrics.LogHash(with), Metrics.LogHash(without));
        Assert.Equal(JsonSerializer.Serialize(recorded.LivesMemoryOf("Ann")!.Snapshot()),
            JsonSerializer.Serialize(silent.LivesMemoryOf("Ann")!.Snapshot()));
        Assert.NotEmpty(with.LivesEveningOutcomes);
        Assert.Empty(without.LivesEveningOutcomes);
        Assert.Empty(without.LivesEveningChoices);
    }

    [Fact]
    public async Task DeferredConcernWaitsForALocalFreeEncounterAndKeepsItsDecisionLink()
    {
        var job = new Job(Home, new(3, 3), 21 * 60, 21 * 60 + 30, Array.Empty<int>(), 0, Commute: 0);
        var mind = new KindnessMind();
        var sim = Simulation(Town(visitorJob: job, visitorHaunts: new[] { new Haunt(Home, new(3, 3), 21 * 60 + 30, 1440, 100) }), mind: mind);
        SimResult result = await sim.RunAsync(1, new AuthoredReflectionMind());
        LivesDecisionRequest request = Assert.Single(mind.Requests);
        Assert.Equal("expectation-miss", request.Fallback.Opportunity.Trigger);
        Assert.DoesNotContain(OtherHome, request.Context);
        Assert.Contains("do not know", request.Context);
        var waiting = Assert.Single(result.LivesEveningOutcomes.Where(o => o.Kind == "concern-waiting"));
        var acted = Assert.Single(result.LivesEveningOutcomes.Where(o => o.Kind == "concern-acted"));
        Assert.True(acted.At >= 21 * 60 + 30);
        Assert.True(acted.At > waiting.At);
        Act help = Assert.Single(result.Acts.Where(a => a.Kind == "HelpedSomeone"));
        Assert.Equal("Ann", help.Actor);
        Assert.Equal("Bea", help.Target);
        Assert.Equal(Home, help.Location);
        EncounterCause cause = Assert.Single(result.EncounterCauses.Where(c => c.ActId == help.Id));
        Assert.Equal("evening-concern", cause.Method);
        Assert.Equal(request.Fallback.Opportunity.Id, cause.ChoiceId);
    }

    [Fact]
    public async Task AgreedEveningIsNotAlsoCountedAsAPassiveWhereaboutsObservation()
    {
        HistoryPack history = History();
        history = history with { Readings = history.Readings.Append(new HistoryReading("warm", "account",
            new(MemoryObjectKind.Person, "Bea"), ReadingStance.Warm, "I like spending time with Bea.",
            ReadingOrigin.CanonStated, AdmissionRule.R2, new(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15"),
            new[] { "source" }, new[] { new HistoryCue("person:Bea", "source") })).ToArray() };
        var sim = new Simulation(7, Town(together: true));
        sim.ConfigureLivesRouting(new() { Enabled = true, Choices = new(5, 0, 1) }, history);
        sim.ConfigureLivesCompany(new() { Enabled = true, OfferBase = 1e-12, SeekBase = 1e-12 });
        sim.ConfigureLivesEvenings(new() { Enabled = true, PassiveExpectations = true, PlanBase = 1000 }, new KindnessMind());
        SimResult result = await sim.RunAsync(1, new AuthoredReflectionMind());
        Assert.Contains(result.LivesEveningUpdates, update => update.Plan.Stage == EveningStage.Agreed);
        Assert.DoesNotContain(result.LivesEveningOutcomes,
            outcome => outcome.Holder == "Ann" && outcome.Kind is "whereabouts-found" or "whereabouts-missed");
        Assert.Single(sim.LivesMemoryOf("Ann")!.Expectation(Expected).Entries);
    }
}
