using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Fictional people and histories exercise the real immediate-company lifecycle.
/// Large offer weights and flat answer weights are branch fixtures, never recommended defaults.</summary>
public sealed class LivesCompanyTests
{
    private const string Home = "Home:House";
    private static Location Room(string name) => new(name, false,
        Enumerable.Repeat(new string('.', 20), 12).ToArray());

    private static Villager Person(string name, int age, string household = "House", Job? job = null,
        IReadOnlyList<Haunt>? haunts = null) => new(name, household, "villager",
        new Temperament(0.5, 0.5, 0.5, 0.5, Sensitivity: name == "Ann" ? 0.8 : 0.2),
        new Body(100, -1), job, haunts ?? Array.Empty<Haunt>(), new Dictionary<string, double>(),
        Array.Empty<string>(), Age: age);

    private static TownData Town()
    {
        var feelings = new FeelingOptions();
        feelings.Acts.With("company,invitations");
        return new()
        {
            Cast = new[] { Person("Ann", 22), Person("Bo", 68) },
            Places = new[] { Room(Home) }, Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = ActCatalog.Kinds(feelings.Acts, Array.Empty<ActKind>()), Feelings = feelings,
            Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000, CircadianWeight = 0 },
            Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static HistoryPack History(string holder = "Ann", string subject = "Bo") => new(1,
        new[] { new HistorySource("source", "synthetic:company", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "fact", holder, "We have spent time together.", KnowledgeKind.Lived,
            new[] { "source" }, new[] { new HistoryCue("person:" + subject) }) },
        new[] { new HistoryReading("reading", "account", new(MemoryObjectKind.Person, subject), ReadingStance.Warm,
            "I enjoy their company.", ReadingOrigin.CanonStated, AdmissionRule.R2,
            new(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15"), new[] { "source" },
            new[] { new HistoryCue("person:" + subject) }) });

    private static CompanyOptions On(bool record = true) => new()
        { Enabled = true, Record = record, OfferBase = 100, SeekBase = 100, AnswerScale = 0 };
    private sealed record RunResult(Simulation Sim, SimResult Result, IReadOnlyList<(string Ann, string Bo)> Places,
        IReadOnlyList<(double Ann, double Bo)> Moods);

    private static RunResult Run(long seed = 7, CompanyOptions? company = null, TownData? town = null,
        HistoryPack? history = null, int days = 1, bool routingRecord = true, Action<int, Simulation>? each = null,
        IReadOnlyList<string>? compare = null)
    {
        var sim = new Simulation(seed, town ?? Town());
        sim.ConfigureLivesRouting(new() { Enabled = true, Record = routingRecord,
            CompareHistory = compare ?? Array.Empty<string>() }, history ?? History());
        if (company is not null) sim.ConfigureLivesCompany(company);
        var places = new List<(string, string)>();
        var moods = new List<(double, double)>();
        SimResult result = sim.Run(days, (at, current) =>
        {
            places.Add((current.Where("Ann").Place, current.Where("Bo").Place));
            moods.Add((current.Mood("Ann"), current.Mood("Bo")));
            each?.Invoke(at, current);
        });
        return new(sim, result, places.AsReadOnly(), moods.AsReadOnly());
    }

    // A fixed matrix exercises every legal answer; no selected seed becomes a story-quality claim.
    private static readonly Lazy<RunResult[]> Branches = new(() => Enumerable.Range(1, 16)
        .Select(seed => Run(seed, On(), days: 2)).ToArray());
    private static readonly Lazy<RunResult[]> TiredBranches = new(() => Enumerable.Range(1, 16)
        .Select(seed => Run(seed, On(), Town() with
        {
            Body = new BodyOptions { AwakeHoursAtRest = 48, CircadianWeight = 0 },
        }, days: 2)).ToArray());
    private static string Json(object value) => JsonSerializer.Serialize(value);

    [Fact]
    public void DisabledCompanyLeavesTheExistingRunAndRoutingUntouched()
    {
        var plain = Run();
        var off = Run(company: new());
        Assert.Equal(Metrics.LogHash(plain.Result), Metrics.LogHash(off.Result));
        Assert.Equal(plain.Places, off.Places);
        Assert.Equal(plain.Moods, off.Moods);
        Assert.Equal(Json(plain.Result.LivesRouting), Json(off.Result.LivesRouting));
        Assert.Empty(off.Result.LivesCompanyUpdates);
        Assert.Empty(off.Result.LivesCompanyChoices);
        Assert.Empty(off.Result.LivesCompanyAnswers);
        Assert.Empty(off.Result.LivesCompanyOutcomes);
    }

    [Fact]
    public void ConfigurationRequiresRoutingAndTheExistingCompanyAndInvitationMechanisms()
    {
        Assert.Throws<ArgumentException>(() => new Simulation(7, Town()).ConfigureLivesCompany(On()));
        var noCompany = Town();
        noCompany.Feelings.Acts.Company = false;
        noCompany.Feelings.Acts.Invitations = false;
        var sim = new Simulation(7, noCompany);
        sim.ConfigureLivesRouting(new() { Enabled = true });
        Assert.Throws<ArgumentException>(() => sim.ConfigureLivesCompany(On()));
        var noInvitation = Town();
        noInvitation.Feelings.Acts.Invitations = false;
        sim = new Simulation(7, noInvitation);
        sim.ConfigureLivesRouting(new() { Enabled = true });
        Assert.Throws<ArgumentException>(() => sim.ConfigureLivesCompany(On()));
    }

    [Fact]
    public void CompanyCannotBeReconfiguredAfterRunningStarts()
    {
        var sim = new Simulation(7, Town());
        sim.ConfigureLivesRouting(new() { Enabled = true }, History());
        sim.ConfigureLivesCompany(On());
        sim.Run(1, (at, current) =>
        {
            if (at == 0) Assert.Throws<InvalidOperationException>(() => current.ConfigureLivesCompany(On()));
        });
        Assert.Throws<InvalidOperationException>(() => sim.ConfigureLivesCompany(On()));
    }

    [Fact]
    public void CompanyRejectsAStartingRecordThatCollidesWithItsLiveCauseNamespace()
    {
        HistoryPack history = History();
        history = history with { Readings = history.Readings.Select(reading => reading with { Id = "company:seed" }).ToArray() };
        var sim = new Simulation(7, Town());
        sim.ConfigureLivesRouting(new() { Enabled = true }, history);
        Assert.Contains("company:seed", sim.LivesMemoryOf("Ann")!.HistoryIds);
        Assert.Throws<ArgumentException>(() => sim.ConfigureLivesCompany(On()));
    }

    [Fact]
    public void AnOrdinaryOfferWaitsForTheNextMinuteAndAnActExistsOnlyAfterAnAnswer()
    {
        var rows = Branches.Value.SelectMany(run => run.Result.LivesCompanyAnswers.Select(answer => (run, answer))).ToArray();
        Assert.NotEmpty(rows);
        foreach (var (run, answer) in rows)
        {
            var pending = Assert.Single(run.Result.LivesCompanyUpdates.Where(u => u.Episode.Id == answer.EpisodeId
                && u.Episode.Stage == CompanyStage.Pending));
            Assert.Equal(pending.At + 1, answer.At);
            Assert.Null(pending.Episode.ActId);
            Assert.Equal(pending.Episode.Asked, answer.Holder);
            Assert.DoesNotContain(run.Result.Acts, act => act.Tick == pending.At);
            var terminal = run.Result.LivesCompanyUpdates.Last(u => u.Episode.Id == answer.EpisodeId).Episode;
            if (answer.SelectedId == "not-now") Assert.Null(terminal.ActId);
            if (answer.SelectedId == "no")
            {
                Assert.NotNull(terminal.ActId);
                Act refusal = run.Result.Acts[terminal.ActId!.Value];
                Assert.Equal("Refused", refusal.Kind);
                Assert.Equal(answer.At, refusal.Tick);
            }
        }
    }

    [Fact]
    public void HouseholdGamesDoNotNeedSimilarAgesAuthoredGameCardsOrASaloon()
    {
        var completed = Branches.Value.SelectMany(run => run.Result.LivesCompanyUpdates
            .Where(u => u.Episode.Stage == CompanyStage.Completed).Select(update => (run, update))).ToArray();
        Assert.NotEmpty(completed);
        Assert.All(Town().Cast, villager => Assert.False(villager.Acts.ContainsKey("PlayedGame")));
        foreach (var (run, update) in completed)
        {
            Act game = run.Result.Acts[update.Episode.ActId!.Value];
            Assert.Equal("PlayedGame", game.Kind);
            Assert.Equal(Home, game.Location);
            Assert.True(update.At >= game.Tick + 29);
            Assert.Contains(update.Episode.Asker, new[] { "Ann", "Bo" });
            Assert.Contains(update.Episode.Asked, new[] { "Ann", "Bo" });
            Assert.Empty(run.Result.Invitations); // the agreed act is not asked through the old path again
        }
    }

    [Fact]
    public void FixedSeedsExerciseYesNoAndNotNowWithTheirDistinctLearningKinds()
    {
        var runs = Branches.Value.Concat(TiredBranches.Value).ToArray();
        var answers = runs.SelectMany(run => run.Result.LivesCompanyAnswers).ToArray();
        Assert.Contains(answers, answer => answer.SelectedId == "yes");
        Assert.Contains(answers, answer => answer.SelectedId == "no");
        Assert.Contains(TiredBranches.Value.SelectMany(run => run.Result.LivesCompanyAnswers),
            answer => answer.SelectedId == "not-now" && answer.State.Tired > 0.25);
        foreach (var run in runs)
            foreach (CompanyAnswer answer in run.Result.LivesCompanyAnswers)
            {
                var episode = run.Result.LivesCompanyUpdates.First(u => u.Episode.Id == answer.EpisodeId).Episode;
                var changes = run.Result.LivesMemoryChanges.Where(c => c.Holder == episode.Asker && c.At == answer.At).ToArray();
                Assert.NotEmpty(changes);
                var newlyWritten = changes.SelectMany(c => c.After.Expectations.SelectMany(e => e.Entries
                    .Where(entry => entry.KnownAt == answer.At).Select(entry => (e.Key, Entry: entry)))).ToArray();
                var expected = answer.SelectedId == "not-now" ? CompanyPolicy.AvailabilityKey(episode.Asked)
                    : CompanyPolicy.AnswerKey(episode.Asked);
                Assert.Contains(newlyWritten, item => item.Key == expected && item.Entry.Success == (answer.SelectedId == "yes"));
                if (answer.SelectedId == "not-now")
                    Assert.DoesNotContain(newlyWritten, item => item.Key.Kind == ExpectationKind.Answers);
                if (answer.SelectedId == "yes")
                    Assert.Contains(newlyWritten, item => item.Key == CompanyPolicy.AvailabilityKey(episode.Asked)
                        && item.Entry.Success);
                Assert.DoesNotContain(newlyWritten, item => item.Key.Kind == ExpectationKind.FollowThrough);
            }
    }

    [Fact]
    public void PairRateLimitAndPersonalOccupationAreIndependentOfViewing()
    {
        foreach (var run in Branches.Value)
        {
            var episodes = run.Result.LivesCompanyUpdates.Select(u => u.Episode).GroupBy(e => e.Id).Select(g => g.First()).ToArray();
            Assert.All(episodes.GroupBy(e => Clock.Day(e.StartedAt)), day => Assert.Single(day));
            var active = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CompanyUpdate update in run.Result.LivesCompanyUpdates)
            {
                CompanyEpisode episode = update.Episode;
                bool terminal = episode.Stage is CompanyStage.Completed or CompanyStage.Interrupted or CompanyStage.NotFound
                    or CompanyStage.Unavailable or CompanyStage.NotNow or CompanyStage.Refused or CompanyStage.Aborted;
                foreach (string name in new[] { episode.Asker, episode.Asked })
                {
                    if (terminal)
                    {
                        if (active.GetValueOrDefault(name) == episode.Id) active.Remove(name);
                    }
                    else
                    {
                        Assert.True(!active.ContainsKey(name) || active[name] == episode.Id);
                        active[name] = episode.Id;
                    }
                }
            }
        }
    }

    [Fact]
    public void SeveralOffersConsideredInOneMinuteHaveDistinctDecisionIdentities()
    {
        var earlyTask = new Job(Home, DefaultTown.Sofa, 0, Clock.At(12),
            Array.Empty<int>(), 0, Commute: 0);
        var town = Town() with
        {
            Cast = new[] { Person("Ann", 22, job: earlyTask), Person("Bo", 68, job: earlyTask), Person("Cy", 40, job: earlyTask) },
        };
        HistoryPack history = History();
        history = history with { Readings = history.Readings.Append(history.Readings[0] with
        {
            Id = "reading-cy", Object = new MemoryObject(MemoryObjectKind.Person, "Cy"),
            Cues = new[] { new HistoryCue("person:Cy") },
        }).ToArray() };
        var run = Run(company: On() with { OfferBase = 1e-12 }, town: town, history: history);
        var choices = run.Result.LivesCompanyChoices.Where(choice => choice.Phase == "offer").ToArray();
        Assert.Contains(choices.GroupBy(choice => (choice.Decision.Opportunity.Holder, choice.Decision.Opportunity.At)),
            group => group.Count() >= 2);
        Assert.All(choices, choice => Assert.Equal("carry-on", choice.Decision.SelectedId));
        Assert.Equal(choices.Length, choices.Select(choice => choice.Decision.Opportunity.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RecordingOffKeepsTheSameActionsMovementMoodsAndMemory()
    {
        var recorded = Run(company: On(), days: 2);
        var quiet = Run(company: On(record: false), days: 2, routingRecord: false);
        var again = Run(company: On(), days: 2);
        Assert.Equal(Metrics.LogHash(recorded.Result), Metrics.LogHash(quiet.Result));
        Assert.Equal(recorded.Places, quiet.Places);
        Assert.Equal(recorded.Moods, quiet.Moods);
        Assert.Equal(Json(recorded.Result.LivesCompanyUpdates), Json(again.Result.LivesCompanyUpdates));
        Assert.Equal(Json(recorded.Result.LivesCompanyAnswers), Json(again.Result.LivesCompanyAnswers));
        foreach (string holder in new[] { "Ann", "Bo" })
            Assert.Equal(Json(recorded.Sim.LivesMemoryOf(holder)!.Snapshot()), Json(quiet.Sim.LivesMemoryOf(holder)!.Snapshot()));
        Assert.Empty(quiet.Result.LivesCompanyUpdates);
        Assert.Empty(quiet.Result.LivesCompanyChoices);
        Assert.Empty(quiet.Result.LivesCompanyAnswers);
        Assert.Empty(quiet.Result.LivesCompanyOutcomes);
    }

    [Fact]
    public void OnlyTheHolderCanRecallTheirStartingHistoryAtAnOffer()
    {
        var choices = Branches.Value.SelectMany(run => run.Result.LivesCompanyChoices).ToArray();
        Assert.NotEmpty(choices);
        Assert.Contains(choices, row => row.Decision.Opportunity.Holder == "Ann"
            && row.Decision.Recall.Items.Any(item => item.Id == "reading"));
        Assert.All(choices.Where(row => row.Decision.Opportunity.Holder == "Bo"), row =>
            Assert.DoesNotContain(row.Decision.Recall.Items, item => item.Id is "reading" or "account"));
        Assert.All(choices, row => Assert.All(row.Decision.Recall.Items,
            item => Assert.Equal(row.Decision.Opportunity.Holder, item.Holder)));
    }

    [Fact]
    public void HistoryComparisonHoldsTheActualOfferAndDrawFixedWithoutChangingItsOutcome()
    {
        var normal = Run(company: On());
        var compared = Run(company: On(), compare: new[] { "reading" });
        Assert.Equal(Metrics.LogHash(normal.Result), Metrics.LogHash(compared.Result));
        Assert.Equal(normal.Places, compared.Places);
        Assert.Equal(normal.Moods, compared.Moods);
        Assert.Equal(Json(normal.Result.LivesCompanyUpdates), Json(compared.Result.LivesCompanyUpdates));
        var pairs = compared.Result.LivesCompanyChoices.Where(row => row.Comparison is not null)
            .Select(row => row.Comparison!).ToArray();
        Assert.NotEmpty(pairs);
        // This history supplies the opening itself. Removing it can remove the offer,
        // which must be disclosed as a changed menu rather than a fake matched comparison.
        Assert.Contains(pairs, pair => !pair.Comparable && pair.NonComparableReason == "eligible-alternatives-changed");
        Assert.All(pairs, pair =>
        {
            Assert.Equal("Ann", pair.WithHistory.Opportunity.Holder);
            Assert.Equal(pair.WithHistory.Unit, pair.WithoutHistory.Unit);
            Assert.Equal(Json(pair.WithHistory.Opportunity), Json(pair.WithoutHistory.Opportunity));
            Assert.DoesNotContain(pair.WithoutHistory.Recall.Items, item => item.Id == "reading");
        });
        Assert.Contains("reading", compared.Sim.LivesMemoryOf("Ann")!.HistoryIds);
    }

    [Fact]
    public void RemovingOneOpeningLeavesTheOfferAvailableWhenAnotherWarmMemoryRemains()
    {
        HistoryPack history = History();
        history = history with
        {
            Facts = history.Facts.Append(history.Facts[0] with { Id = "other-fact" }).ToArray(),
            Accounts = history.Accounts.Append(history.Accounts[0] with { Id = "other-account", FactId = "other-fact" }).ToArray(),
            Readings = history.Readings.Append(history.Readings[0] with { Id = "other-reading", AccountId = "other-account",
                Object = new MemoryObject(MemoryObjectKind.Activity, "PlayedGame") }).ToArray(),
        };
        var run = Run(company: On(), history: history, compare: new[] { "reading" });
        var pairs = run.Result.LivesCompanyChoices.Where(row => row.Phase == "offer" && row.Comparison is not null)
            .Select(row => row.Comparison!).ToArray();
        Assert.NotEmpty(pairs);
        Assert.All(pairs, pair =>
        {
            Assert.True(pair.Comparable);
            Assert.True(pair.ProbabilitiesChanged);
            Assert.Equal(pair.WithHistory.Unit, pair.WithoutHistory.Unit);
            Assert.Contains(pair.WithHistory.Alternatives, alternative => alternative.Option.Id == "offer");
            Assert.Contains(pair.WithoutHistory.Alternatives, alternative => alternative.Option.Id == "offer");
            Assert.DoesNotContain(pair.WithoutHistory.Recall.Items, item => item.Id == "reading");
            Assert.Contains(pair.WithoutHistory.Recall.Items, item => item.Id == "other-reading");
        });
    }

    [Fact]
    public void AFeelingForSomeoneDoesNotRevealTheirUnseenHomeOrCurrentPosition()
    {
        var town = Town() with
        {
            Cast = new[] { Person("Ann", 22, "Ann"), Person("Bo", 68, "Bo") },
            Places = new[] { Room("Home:Ann"), Room("Home:Bo") },
        };
        var run = Run(company: On(), town: town);
        Assert.Contains(run.Sim.LivesMemoryOf("Ann")!.Snapshot().Associations,
            entry => entry.Object == new MemoryObject(MemoryObjectKind.Person, "Bo") && entry.Amount > 0);
        Assert.All(run.Places, pair => Assert.NotEqual(pair.Ann, pair.Bo));
        Assert.Empty(run.Result.LivesCompanyChoices);
        Assert.Empty(run.Result.LivesCompanyUpdates);
        Assert.Empty(run.Result.LivesCompanyOutcomes);
    }

    [Fact]
    public void ADeliberateMissAtTheKnownSharedHomeDoesNotRevealTheOtherPlaceOrMeanRejection()
    {
        var town = Town() with
        {
            Cast = new[] { Person("Ann", 22), Person("Bo", 68, job: new Job("Workshop", new Tile(4, 4),
                0, Clock.MinutesPerDay, Array.Empty<int>(), 0, Commute: 0)) },
            Places = new[] { Room(Home), Room("Workshop") },
        };
        var run = Run(company: On(), town: town);
        var misses = run.Result.LivesCompanyUpdates.Where(update => update.Episode.Stage == CompanyStage.NotFound).ToArray();
        Assert.NotEmpty(misses);
        Assert.Empty(run.Result.LivesCompanyAnswers);
        Assert.All(misses, miss =>
        {
            Assert.Equal("Ann", miss.Episode.Asker);
            Assert.Equal(Home, miss.Episode.Place);
            Assert.Equal("Workshop", run.Places[miss.At].Bo); // known only to this observer assertion
            Assert.Contains(run.Result.LivesCompanyUpdates, update => update.Episode.Id == miss.Episode.Id
                && update.Episode.Stage == CompanyStage.Searching && update.At < miss.At);
            Assert.Null(miss.Episode.ActId);
            var outcome = Assert.Single(run.Result.LivesCompanyOutcomes.Where(outcome => outcome.EpisodeId == miss.Episode.Id));
            Assert.Equal("not-found", outcome.Kind);
            Assert.Equal(0, outcome.Felt);
            string cause = "company:" + miss.Episode.Id + ":not-found";
            var knowledge = run.Sim.LivesMemoryOf("Ann")!.Snapshot().Expectations
                .SelectMany(expectation => expectation.Entries.Where(entry => entry.CauseId == cause)
                    .Select(entry => (expectation.Key, Entry: entry))).ToArray();
            Assert.Equal(ExpectationKind.Whereabouts, Assert.Single(knowledge).Key.Kind);
            Assert.StartsWith(Home + ":", knowledge[0].Key.Context);
            Assert.False(knowledge[0].Entry.Success);
            Assert.DoesNotContain(run.Sim.LivesMemoryOf("Ann")!.Snapshot().Associations,
                entry => entry.CauseId == cause);
        });
        Assert.DoesNotContain(run.Result.LivesCompanyChoices.SelectMany(row => row.Decision.Opportunity.Options)
            .SelectMany(option => option.Objects), obj => obj.Kind == MemoryObjectKind.Place && obj.Key == "Workshop");
    }

    [Fact]
    public void AnUnspokenWorkObligationBlocksTheActWithoutTeachingThatSomeoneSaidNotNow()
    {
        var town = Town() with
        {
            Cast = new[]
            {
                Person("Ann", 22, haunts: new[] { new Haunt("Yard", new Tile(4, 4), 0, Clock.MinutesPerDay, 100) }),
                Person("Bo", 68, job: new Job(Home, new Tile(4, 4), 0, Clock.MinutesPerDay,
                    Array.Empty<int>(), 0, Commute: 0)),
            },
            Places = new[] { Room(Home), Room("Yard") },
        };
        var run = Run(company: On(), town: town);
        var gated = run.Result.LivesCompanyUpdates.Where(update => update.Episode.Stage == CompanyStage.Unavailable).ToArray();
        Assert.NotEmpty(gated);
        Assert.Empty(run.Result.LivesCompanyAnswers);
        Assert.All(gated, update =>
        {
            var outcomes = run.Result.LivesCompanyOutcomes.Where(outcome => outcome.EpisodeId == update.Episode.Id).ToArray();
            Assert.Equal("found", Assert.Single(outcomes).Kind);
            Assert.Null(update.Episode.ActId);
        });
        Assert.DoesNotContain(run.Sim.LivesMemoryOf("Ann")!.Snapshot().Expectations,
            expectation => expectation.Key.Kind is ExpectationKind.Answers or ExpectationKind.Availability or ExpectationKind.Activity);
    }

    [Fact]
    public void CompletedGamePaysOnePersonalFeltShareOnlyAtCompletionAndLearnsActivityThen()
    {
        var sample = Branches.Value.SelectMany(run => run.Result.LivesCompanyUpdates
                .Where(u => u.Episode.Stage == CompanyStage.Completed && Clock.Day(u.At) == 0)
                .Select(update => (run, update))).First();
        var (run, update) = sample;
        Act game = run.Result.Acts[update.Episode.ActId!.Value];
        Assert.DoesNotContain(run.Result.Feelings, felt => felt.ActId == game.Id && felt.Tick < update.At);
        var shares = run.Result.Feelings.Where(felt => felt.ActId == game.Id && felt.Route == "SharedActivity").ToArray();
        Assert.Equal(2, shares.Length);
        foreach (string holder in new[] { "Ann", "Bo" })
        {
            string partner = holder == "Ann" ? "Bo" : "Ann";
            double expected = 0.08 * (0.5 + (holder == "Ann" ? 0.8 : 0.2));
            var share = Assert.Single(shares.Where(felt => felt.Holder == holder));
            Assert.Equal(update.At, share.Tick);
            Assert.Equal(expected, share.Mood, 12);
            Assert.Equal(0, holder == "Ann" ? run.Moods[update.At - 1].Ann : run.Moods[update.At - 1].Bo);
            Assert.Equal(Feelings.Squash(expected), holder == "Ann" ? run.Moods[update.At].Ann : run.Moods[update.At].Bo, 12);
            var activity = run.Result.LivesMemoryChanges.Where(change => change.Holder == holder)
                .SelectMany(change => change.After.Expectations.Where(e => e.Key == CompanyPolicy.ActivityKey(partner, Home))
                    .SelectMany(e => e.Entries)).Where(entry => !entry.Seed).ToArray();
            Assert.Contains(activity, entry => entry.KnownAt == update.At && entry.Success);
            Assert.DoesNotContain(activity, entry => entry.KnownAt < update.At);
        }
    }

    [Fact]
    public void AnActualCollapseInterruptsTheGameWithoutCompletionJoyOrSuccessLearning()
    {
        // Pick a branch from the fixed mechanism matrix, then hold its complete pre-game world fixed.
        // The only intervention is a physical exhaustion shock after the game has actually started.
        var control = Branches.Value.Select((run, i) => (Run: run, Seed: i + 1))
            .First(item => item.Run.Result.LivesCompanyUpdates.Any(u => u.Episode.Stage == CompanyStage.Completed && Clock.Day(u.At) == 0));
        var complete = control.Run.Result.LivesCompanyUpdates.First(u => u.Episode.Stage == CompanyStage.Completed && Clock.Day(u.At) == 0);
        Act game = control.Run.Result.Acts[complete.Episode.ActId!.Value];
        var town = Town();
        var interrupted = Run(control.Seed, On(), town, each: (at, _) =>
        {
            if (at == game.Tick) town.Body.AwakeHoursAtRest = 0.00001;
        });
        var stopped = Assert.Single(interrupted.Result.LivesCompanyUpdates.Where(u => u.Episode.Id == complete.Episode.Id
            && u.Episode.Stage == CompanyStage.Interrupted));
        Assert.True(stopped.At < complete.At);
        Assert.Contains(interrupted.Result.Sleeps, sleep => sleep.Collapsed);
        Assert.DoesNotContain(interrupted.Result.LivesCompanyUpdates, u => u.Episode.Id == complete.Episode.Id
            && u.Episode.Stage == CompanyStage.Completed);
        Assert.DoesNotContain(interrupted.Result.Feelings, felt => felt.ActId == stopped.Episode.ActId && felt.Route == "SharedActivity");
        foreach (string holder in new[] { "Ann", "Bo" })
        {
            string partner = holder == "Ann" ? "Bo" : "Ann";
            var learned = interrupted.Sim.LivesMemoryOf(holder)!.Expectation(CompanyPolicy.ActivityKey(partner, Home));
            Assert.False(Assert.Single(learned.Entries).Success);
            Assert.Equal(0.5, learned.Chance);
        }
    }

    [Fact]
    public void ASubsequentChoiceReadsTheEarlierAnswerAndActivityEvidence()
    {
        var later = Branches.Value.SelectMany(run => run.Result.LivesCompanyChoices)
            .Where(row => Clock.Day(row.Decision.Opportunity.At) > 0)
            .SelectMany(row => row.Decision.Alternatives.Select(alternative => (row, alternative)))
            .Where(pair => pair.alternative.Expectations.Any(e => e.Entries.Any(entry => !entry.Seed))).ToArray();
        Assert.NotEmpty(later);
        Assert.Contains(later, pair => pair.alternative.Expectations.Any(e => e.Key.Kind == ExpectationKind.Answers && e.Entries.Count > 0));
        Assert.Contains(later, pair => pair.alternative.Expectations.Any(e => e.Key.Kind == ExpectationKind.Activity && e.Entries.Count > 0));
        Assert.All(later, pair => Assert.All(pair.alternative.Expectations.SelectMany(e => e.Entries),
            entry => Assert.True(entry.KnownAt <= pair.row.Decision.Opportunity.At)));
    }

    [Fact]
    public void OrdinaryAndHistoryLedGameOffersShareThePairDayLimit()
    {
        var town = TownData.Default();
        town.Feelings.Acts.With("company,invitations");
        town = town with { Acts = ActCatalog.Kinds(town.Feelings.Acts, town.Acts),
            Cast = ActCatalog.Cards(town.Cast, town.Feelings.Acts) };
        var sim = new Simulation(7, town);
        sim.ConfigureLivesRouting(new() { Enabled = true });
        sim.ConfigureLivesCompany(new() { Enabled = true });
        var result = sim.Run(7);
        var old = result.Invitations.Where(i => i.Kind == "PlayedGame").ToArray();
        var offers = result.LivesCompanyChoices.Where(c => c.Phase == "offer").ToArray();
        Assert.NotEmpty(old);
        Assert.NotEmpty(offers);
        string Pair(string a, string b) => string.Join("/", new[] { a, b }.Order(StringComparer.Ordinal));
        var considered = old.Select(i => (Pair: Pair(i.Asker, i.Asked), Day: Clock.Day(i.Tick)))
            .Concat(offers.Select(c => (Pair: Pair(c.Decision.Opportunity.Holder,
                c.Decision.Opportunity.Options.Single(o => o.Id == "offer").Objects.Single(o => o.Kind == MemoryObjectKind.Person).Key),
                Day: Clock.Day(c.Decision.Opportunity.At))));
        Assert.All(considered.GroupBy(c => c), group => Assert.Single(group));
    }
}
