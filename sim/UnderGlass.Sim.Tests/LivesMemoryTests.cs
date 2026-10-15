using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Synthetic people and evidence only; canon standing exercises the admission rules.</summary>
public sealed class LivesMemoryTests
{
    private static readonly MemoryObject Workshop = new(MemoryObjectKind.Place, "Workshop");
    private static readonly MemoryObject Friend = new(MemoryObjectKind.Person, "Bo");
    private static readonly ExpectationKey Answer = new(ExpectationKind.Answers, "Bo", "mending invitation");
    private static readonly HistoryPack Empty = new(1, Array.Empty<HistorySource>(), Array.Empty<HistoryFact>(),
        Array.Empty<HistoryAccount>(), Array.Empty<HistoryReading>());

    private static HistoryPack SeedPack(ReadingStance stance = ReadingStance.Warm)
    {
        return new(1,
            new[] { new HistorySource("source", "synthetic:workshop", HistorySourceKind.Canon) },
            new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
            new[] { new HistoryAccount("account-ada", "fact", "Ada", "I spend time at the workshop.",
                KnowledgeKind.Lived, new[] { "source" }, new[] { new HistoryCue("shared") }) },
            new[] { new HistoryReading("reading-ada", "account-ada", Workshop, stance,
                "This is my own reading of time there.", ReadingOrigin.CanonStated, AdmissionRule.R2,
                new HistoryReview(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15"),
                new[] { "source" }, new[] { new HistoryCue("shared") },
                Expectation: stance == ReadingStance.Expects ? Answer : null,
                ExpectedSuccess: stance == ReadingStance.Expects ? false : null) });
    }

    private static MemoryExperience Felt(long sequence, string cause, double amount,
        MemoryObject? target = null, string cue = "shared", int? at = null) => new(sequence,
        at ?? checked((int)sequence), cause, KnowledgeKind.Lived,
        new[] { new AssociationInput(target ?? Workshop, amount, new[] { cue }) }, Array.Empty<ExpectationInput>());

    private static MemoryExperience Outcome(long sequence, bool success, string? cause = null,
        ExpectationKey? key = null, int? at = null, string cue = "shared") => new(sequence,
        at ?? checked((int)sequence), cause ?? "outcome-" + sequence, KnowledgeKind.Witnessed,
        Array.Empty<AssociationInput>(), new[] { new ExpectationInput(key ?? Answer, success, new[] { cue }) });

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private sealed class Sink : ILivesSink
    {
        public List<LivesMemoryChange> Changes { get; } = new();
        public List<RecallSnapshot> Recalls { get; } = new();
        public void Changed(LivesMemoryChange change) => Changes.Add(change);
        public void Recalled(RecallSnapshot recall) => Recalls.Add(recall);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(-0.2)]
    public void ExpectationSummariesCannotCrowdOutMatchedFeelings(double amount)
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(Felt(0, "shared-evening", amount));
        for (int i = 1; i <= 5; i++)
            memory.Learn(Outcome(i, true, key: new(ExpectationKind.Answers, "Bo", "question-" + i)));
        var recalled = memory.Recall(new[] { "shared" }, 5);
        Assert.Equal(3, recalled.Items.Count);
        Assert.Contains(recalled.Items, r => r.Causes.Contains("shared-evening"));
        Assert.Contains(recalled.Items, r => r.Kind == RecallKind.Expectation);
        Assert.Equal(amount > 0, recalled.Items.Single(r => r.Kind == RecallKind.Association).Warm > 0);
        Assert.Empty(memory.Recall(new[] { "unrelated" }, 5).Items);
    }

    [Fact]
    public void SharedFactDoesNotShareAnAccountOrPrivateReadingWithAnotherHolder()
    {
        HistoryPack pack = SeedPack();
        var secondAccount = pack.Accounts[0] with { Id = "account-bo", Holder = "Bo", Text = "My time there was difficult." };
        var secondReading = pack.Readings[0] with
        {
            Id = "reading-bo", AccountId = secondAccount.Id, Stance = ReadingStance.Wary,
            Text = "I dread returning.",
        };
        pack = pack with { Accounts = pack.Accounts.Append(secondAccount).ToArray(), Readings = pack.Readings.Append(secondReading).ToArray() };
        var ada = new PersonalMemory("Ada", pack);
        var bo = new PersonalMemory("Bo", pack);
        var outsider = new PersonalMemory("Cy", pack);

        Assert.Equal(0.05, ada.Sides(new[] { Workshop }).Warm, 12);
        Assert.Equal(0, ada.Sides(new[] { Workshop }).Cold);
        Assert.Equal(0.05, bo.Sides(new[] { Workshop }).Cold, 12);
        Assert.Equal(0, bo.Sides(new[] { Workshop }).Warm);
        Assert.All(ada.Recall(new[] { "shared" }, 0).Items, item => Assert.Equal("Ada", item.Holder));
        Assert.DoesNotContain(ada.Recall(new[] { "shared" }, 0).Items, item => item.Id.EndsWith("-bo"));
        Assert.Empty(outsider.Snapshot().Associations);
        Assert.Empty(outsider.Recall(new[] { "shared" }, 0).Items);
    }

    [Fact]
    public void ProposedAndRejectedReadingsAreExcludedWhileAuthoredReadingIsRecallOnly()
    {
        HistoryPack pack = SeedPack();
        HistoryReading source = pack.Readings[0];
        pack = pack with
        {
            Readings = new[]
            {
                source with { Id = "proposed", Review = new(ReviewStatus.Proposed) },
                source with { Id = "rejected", Review = new(ReviewStatus.Rejected) },
                source with { Id = "authored", Origin = ReadingOrigin.Authored, Admission = null },
                source with { Id = "no-stance", Stance = ReadingStance.None },
            },
        };
        var memory = new PersonalMemory("Ada", pack);
        Assert.Empty(memory.Snapshot().Associations);
        Assert.Empty(memory.Snapshot().Expectations);
        var recalled = memory.Recall(new[] { "shared" }, 0).Items;
        Assert.Contains(recalled, item => item.Id == "authored" && item.Warm == 0 && item.Cold == 0);
        Assert.Contains(recalled, item => item.Id == "no-stance");
        Assert.DoesNotContain(recalled, item => item.Id is "proposed" or "rejected");
    }

    [Fact]
    public void BothSignsSurvivePerObjectPressureAndDoNotCancelEachOther()
    {
        var memory = new PersonalMemory("Ada", Empty);
        double[] felt = { 0.9, 0.8, 0.7, 0.6, -0.001, -0.002 };
        for (int i = 0; i < felt.Length; i++) memory.Learn(Felt(i, "cause-" + i, felt[i]));

        Assert.Equal(3, memory.ActiveAssociationCount);
        Assert.Equal(new[] { -0.002, 0.8, 0.9 }, memory.Snapshot().Associations.Select(a => a.Amount).Order());
        MemorySides sides = memory.Sides(new[] { Workshop });
        Assert.Equal(1.7, sides.Warm, 12);
        Assert.Equal(0.002, sides.Cold, 12);
    }

    [Fact]
    public void HolderCapEvictsAnEntireWeakObjectIncludingBothSigns()
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", Empty, new(AssociationsPerHolder: 4), sink);
        memory.Learn(Felt(0, "weak-warm", 0.2));
        memory.Learn(Felt(1, "weak-cold", -0.1));
        memory.Learn(Felt(2, "strong-warm-a", 0.8, Friend));
        memory.Learn(Felt(3, "strong-warm-b", 0.7, Friend));
        memory.Learn(Felt(4, "strong-cold", -0.6, Friend));

        Assert.Equal(3, memory.ActiveAssociationCount);
        Assert.All(memory.Snapshot().Associations, entry => Assert.Equal(Friend, entry.Object));
        Assert.Empty(memory.Sides(new[] { Workshop }).Causes);
        Assert.Equal(2, sink.Changes[^1].DroppedIds.Count);
        Assert.Equal(new[] { "weak-cold", "weak-warm" }, sink.Changes[^1].Before.Associations
            .Where(a => sink.Changes[^1].DroppedIds.Contains(a.Id)).Select(a => a.CauseId).Order());
    }

    [Fact]
    public void OneCauseAcrossSeveralObjectsCountsOnceButIndependentCausesAccumulate()
    {
        var activity = new MemoryObject(MemoryObjectKind.Activity, "Mending");
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(new(0, 0, "one-evening", KnowledgeKind.Lived, new[]
        {
            new AssociationInput(Friend, 0.2, new[] { "shared" }),
            new AssociationInput(Workshop, 0.2, new[] { "shared" }),
            new AssociationInput(activity, 0.2, new[] { "shared" }),
        }, Array.Empty<ExpectationInput>()));
        memory.Learn(Felt(1, "another-evening", 0.3, Friend));

        MemorySides sides = memory.Sides(new[] { Friend, Workshop, activity });
        Assert.Equal(0.5, sides.Warm, 12);
        Assert.Equal(2, sides.Causes.Count);
        Assert.Equal(3, sides.Causes.Single(c => c.CauseId == "one-evening").Routes.Count);
        Assert.Equal(Json(sides), Json(memory.Sides(new[] { activity, Friend, Workshop, Friend })));
    }

    [Fact]
    public void OneCauseCanHaveWarmAndColdRoutesWithoutErasingEither()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(new(0, 0, "mixed-evening", KnowledgeKind.Lived, new[]
        {
            new AssociationInput(Friend, 0.2, Array.Empty<string>()),
            new AssociationInput(Workshop, -0.1, Array.Empty<string>()),
        }, Array.Empty<ExpectationInput>()));
        MemorySides sides = memory.Sides(new[] { Friend, Workshop });
        Assert.Equal(0.2, sides.Warm);
        Assert.Equal(0.1, sides.Cold);
        Assert.Single(sides.Causes);
    }

    [Fact]
    public void FirstOutcomeMovesChanceAndContradictoryOutcomesStayInTheEstimate()
    {
        var memory = new PersonalMemory("Ada", Empty);
        Assert.Equal(1, memory.Expectation(Answer).Chance);
        memory.Learn(Outcome(0, false));
        Assert.Equal(0.5, memory.Expectation(Answer).Chance);
        Assert.Null(memory.Expectation(Answer).Summary);
        memory.Learn(Outcome(1, true));
        Assert.Equal(2.0 / 3, memory.Expectation(Answer).Chance, 12);
        Assert.Equal(new[] { false, true }, memory.Expectation(Answer).Entries.Select(e => e.Success));
        Assert.Null(memory.Expectation(Answer).Summary);
    }

    [Fact]
    public void ExpectationRetainsItsSeedAndSixLatestLivedEntries()
    {
        var memory = new PersonalMemory("Ada", SeedPack(ReadingStance.Expects));
        Assert.Equal(0.5, memory.Expectation(Answer).Chance);
        for (int i = 0; i < 9; i++) memory.Learn(Outcome(i, i % 2 == 0));
        ExpectationSnapshot expectation = memory.Expectation(Answer);
        Assert.Equal(7, expectation.Entries.Count);
        Assert.Equal("reading-ada", Assert.Single(expectation.Entries.Where(e => e.Seed)).CauseId);
        Assert.Equal(Enumerable.Range(3, 6).Select(i => "outcome-" + i), expectation.Entries.Where(e => !e.Seed).Select(e => e.CauseId));
        Assert.Equal(0.5, expectation.Chance);
        Assert.Null(expectation.Summary);
    }

    [Fact]
    public void ReceiptCannotBeReappliedAfterAssociationAndExpectationEvidenceWasEvicted()
    {
        var memory = new PersonalMemory("Ada", Empty);
        var original = new MemoryExperience(0, 0, "old", KnowledgeKind.Lived,
            new[] { new AssociationInput(Workshop, 0.01, new[] { "old-cue" }) },
            new[] { new ExpectationInput(Answer, false, new[] { "old-cue" }) });
        memory.Learn(original);
        for (int i = 1; i <= 7; i++)
            memory.Learn(new(i, i, "new-" + i, KnowledgeKind.Lived,
                new[] { new AssociationInput(Workshop, 0.1 + i / 100.0, new[] { "new-cue" }) },
                new[] { new ExpectationInput(Answer, true, new[] { "new-cue" }) }));
        Assert.DoesNotContain(memory.Snapshot().Associations, e => e.CauseId == "old");
        Assert.DoesNotContain(memory.Expectation(Answer).Entries, e => e.CauseId == "old");
        string before = Json(memory.Snapshot());
        Assert.False(memory.Learn(original));
        Assert.False(memory.Learn(original with { Sequence = 8, KnownAt = 8 }));
        Assert.Equal(before, Json(memory.Snapshot()));
        Assert.Equal(8, memory.AcquisitionReceiptCount);
        Assert.Empty(memory.Recall(new[] { "old-cue" }, 8).Items);
    }

    [Fact]
    public void SummaryThresholdChangesTheSummaryButNeverTheChanceOrEvidence()
    {
        var early = new PersonalMemory("Ada", Empty, new(DeriveAfter: 2));
        var late = new PersonalMemory("Ada", Empty, new(DeriveAfter: 4));
        for (int i = 0; i < 3; i++)
        {
            early.Learn(Outcome(i, false));
            late.Learn(Outcome(i, false));
        }
        Assert.False(early.Expectation(Answer).Summary);
        Assert.Null(late.Expectation(Answer).Summary);
        Assert.Equal(0.25, early.Expectation(Answer).Chance);
        Assert.Equal(early.Expectation(Answer).Chance, late.Expectation(Answer).Chance);
        Assert.Equal(Json(early.Expectation(Answer).Entries), Json(late.Expectation(Answer).Entries));
        Assert.Equal(new[] { "outcome-1", "outcome-2" }, early.Expectation(Answer).SummaryEvidence);
        early.Learn(Outcome(3, true));
        Assert.Null(early.Expectation(Answer).Summary);
        Assert.Equal(0.4, early.Expectation(Answer).Chance);
    }

    [Fact]
    public void RecallReservesOneOfThreePlacesForMatchedPreRunHistoryAndAddsWarmthInLowMood()
    {
        var memory = new PersonalMemory("Ada", SeedPack(ReadingStance.None));
        for (int i = 0; i < 4; i++)
            memory.Learn(Felt(i, "lived-" + i, 0.9 - i / 10.0, new(MemoryObjectKind.Activity, "task-" + i)));
        memory.Learn(Felt(4, "unrelated-comfort", 1, new(MemoryObjectKind.Activity, "Gardening"), "garden"));

        RecallSnapshot normal = memory.Recall(new[] { "shared" }, 4);
        Assert.Equal(3, normal.Items.Count);
        Assert.Single(normal.Items.Where(i => i.PreRun));
        Assert.Equal(new[] { "lived-0", "lived-1" }, normal.Items.Where(i => !i.PreRun).SelectMany(i => i.Causes));
        RecallSnapshot sad = memory.Recall(new[] { "shared" }, 4, lowMood: true);
        Assert.Equal(4, sad.Items.Count);
        Assert.Equal(normal.Items.Select(i => i.Id), sad.Items.Take(3).Select(i => i.Id));
        Assert.Contains("lived-2", sad.Items[3].Causes);
        Assert.DoesNotContain(sad.Items, i => i.Causes.Contains("unrelated-comfort"));
    }

    [Fact]
    public void RecallCannotReadFutureAcquisitionsByRewindingLiveMemory()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(Felt(0, "known-later", 0.2, at: 100));
        Assert.Throws<ArgumentException>(() => memory.Recall(new[] { "shared" }, 99));
        Assert.All(memory.Recall(new[] { "shared" }, 100).Items, item => Assert.True(item.KnownAt <= 100));
    }

    [Fact]
    public void SourcedCueCountsDoubleWithoutMakingOtherCuesExclusive()
    {
        HistoryPack pack = SeedPack(ReadingStance.None);
        pack = pack with
        {
            Readings = new[] { pack.Readings[0] with { Cues = new[] { new HistoryCue("autumn", "source"), new HistoryCue("absence") } } },
        };
        var memory = new PersonalMemory("Ada", pack);
        RecalledMemory autumn = Assert.Single(memory.Recall(new[] { "autumn" }, 0).Items);
        RecalledMemory absence = Assert.Single(memory.Recall(new[] { "absence" }, 0).Items);
        Assert.Equal(autumn.Id, absence.Id);
        Assert.Equal(2, autumn.Overlap);
        Assert.Equal(1, absence.Overlap);
    }

    [Fact]
    public void EqualScoresUseOrdinalIdsIndependentOfInputAndCueOrder()
    {
        var objects = new[] { new MemoryObject(MemoryObjectKind.Place, "z"), new(MemoryObjectKind.Place, "A"), new(MemoryObjectKind.Place, "a") };
        var inputs = objects.Select(o => new AssociationInput(o, 0.2, new[] { "b", "a" })).ToArray();
        var first = new PersonalMemory("Ada", Empty);
        var second = new PersonalMemory("Ada", Empty);
        first.Learn(new(0, 0, "cause", KnowledgeKind.Lived, inputs, Array.Empty<ExpectationInput>()));
        second.Learn(new(0, 0, "cause", KnowledgeKind.Lived, inputs.Reverse().ToArray(), Array.Empty<ExpectationInput>()));
        var recalled = first.Recall(new[] { "b", "a" }, 0);
        Assert.Equal(recalled.Items.Select(i => i.Id).Order(StringComparer.Ordinal), recalled.Items.Select(i => i.Id));
        Assert.Equal(Json(recalled), Json(second.Recall(new[] { "a", "b", "a" }, 0)));
        Assert.Equal(Json(first.Snapshot()), Json(second.Snapshot()));
    }

    [Fact]
    public void RecallBreaksEqualOverlapByStrengthAndPersonalSignificance()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(new(0, 0, "cause", KnowledgeKind.Lived, new[]
        {
            new AssociationInput(Workshop, 0.3, new[] { "shared" }),
            new AssociationInput(Friend, 0.2, new[] { "shared" }, PersonalSignificance: 1),
        }, Array.Empty<ExpectationInput>()));
        Assert.Contains(":Person:Bo", memory.Recall(new[] { "shared" }, 0).Items[0].Id);
    }

    [Fact]
    public void InputCollectionsAndEarlierSnapshotsDoNotChangeAfterLearningOrCallerEdits()
    {
        HistoryPack pack = SeedPack();
        var historyCues = new List<HistoryCue> { new("shared") };
        pack = pack with { Accounts = new[] { pack.Accounts[0] with { Cues = historyCues } } };
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", pack, sink: sink);
        var cues = new List<string> { "shared" };
        var associations = new List<AssociationInput> { new(Friend, 0.2, cues) };
        var expectations = new List<ExpectationInput> { new(Answer, false, cues) };
        memory.Learn(new(0, 0, "first", KnowledgeKind.Lived, associations, expectations));
        MemorySnapshot snapshot = memory.Snapshot();
        RecallSnapshot recall = memory.Recall(cues, 0);
        string saved = Json(snapshot), savedRecall = Json(recall), savedReceipts = Json(sink.Changes);

        historyCues.Clear();
        cues[0] = "caller-edited";
        associations.Clear();
        expectations.Clear();
        Assert.Equal(saved, Json(memory.Snapshot()));
        Assert.Equal(savedRecall, Json(memory.Recall(new[] { "shared" }, 0)));
        Assert.Equal(savedReceipts, Json(sink.Changes));

        memory.Learn(Felt(1, "second", -0.3, Friend));
        Assert.Equal(saved, Json(snapshot));
        Assert.Equal(savedRecall, Json(recall));
        Assert.Equal(saved, Json(sink.Changes[1].After));
        Assert.Throws<NotSupportedException>(() => ((IList<AssociationEntry>)snapshot.Associations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.Associations[0].Cues).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<RecalledMemory>)recall.Items).Clear());
    }

    [Fact]
    public void RecordingOnAndOffProduceIdenticalMemoryAndRecall()
    {
        var sink = new Sink();
        var recorded = new PersonalMemory("Ada", SeedPack(), sink: sink);
        var silent = new PersonalMemory("Ada", SeedPack());
        for (int i = 0; i < 8; i++)
        {
            var experience = new MemoryExperience(i, i, "cause-" + i, KnowledgeKind.Lived,
                new[] { new AssociationInput(Workshop, i % 2 == 0 ? 0.1 : -0.2, new[] { "shared" }) },
                new[] { new ExpectationInput(Answer, i % 3 == 0, new[] { "shared" }) });
            recorded.Learn(experience);
            silent.Learn(experience);
        }
        recorded.AdvanceToDay(2);
        silent.AdvanceToDay(2);
        recorded.Retract("cause-7", 2880);
        silent.Retract("cause-7", 2880);
        Assert.Equal(Json(recorded.Snapshot()), Json(silent.Snapshot()));
        Assert.Equal(Json(recorded.Recall(new[] { "shared" }, 2880, true)), Json(silent.Recall(new[] { "shared" }, 2880, true)));
        Assert.Equal(11, sink.Changes.Count);
        Assert.Single(sink.Recalls);
    }

    [Fact]
    public void NightlyDecayIsMonotonicAppliedOnceAndDoesNotReinflateDilutedSeed()
    {
        var memory = new PersonalMemory("Ada", SeedPack());
        for (int i = 0; i < 6; i++) memory.Learn(Felt(i, "lived-" + i, -0.1));
        double diluted = Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed)).Amount;
        Assert.Equal(0.05 / 7, diluted, 12);
        double cold = memory.Sides(new[] { Workshop }).Cold;
        memory.AdvanceToDay(1);
        Assert.Equal(cold * 0.975, memory.Sides(new[] { Workshop }).Cold, 12);
        string afterOne = Json(memory.Snapshot());
        memory.AdvanceToDay(1);
        Assert.Equal(afterOne, Json(memory.Snapshot()));
        memory.AdvanceToDay(50);
        Assert.Equal(cold * Math.Pow(0.975, 50), memory.Sides(new[] { Workshop }).Cold, 12);
        Assert.Equal(diluted, Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed)).Amount, 12);
        Assert.Throws<ArgumentException>(() => memory.AdvanceToDay(49));
    }

    [Fact]
    public void EvictionDoesNotReviveTheStrengthOfARecalledSeedReading()
    {
        var memory = new PersonalMemory("Ada", SeedPack());
        memory.Learn(Felt(0, "first", 0.4));
        double afterFirst = memory.Recall(new[] { "place:Workshop" }, 0).Items.Single(i => i.Id == "reading-ada").Warm;
        Assert.Equal(0.025, afterFirst, 12);
        memory.Learn(Felt(1, "second", 0.5));
        memory.Learn(Felt(2, "third", 0.6));
        Assert.DoesNotContain(memory.Snapshot().Associations, a => a.Seed);
        RecalledMemory reading = memory.Recall(new[] { "place:Workshop" }, 2).Items.Single(i => i.Id == "reading-ada");
        Assert.True(reading.Warm <= afterFirst, "Evicting active evidence must not restore the seed's original numeric authority.");
        Assert.True(reading.Strength <= afterFirst);
    }

    [Fact]
    public void RetractionRemovesOnlyTheCauseAndItsDilutionWithoutRestoringEvictedEvidence()
    {
        var memory = new PersonalMemory("Ada", SeedPack());
        for (int i = 0; i < 5; i++)
            memory.Learn(new(i, i, "cause-" + i, KnowledgeKind.Lived,
                new[] { new AssociationInput(Workshop, -0.1 - i / 100.0, new[] { "shared" }) },
                new[] { new ExpectationInput(Answer, i % 2 == 0, new[] { "shared" }) }));
        Assert.Equal(0.05 / 6, Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed)).Amount, 12);
        memory.Retract("cause-4", 5);
        Assert.DoesNotContain(memory.Snapshot().Associations, a => a.CauseId == "cause-4");
        Assert.DoesNotContain(memory.Expectation(Answer).Entries, e => e.CauseId == "cause-4");
        Assert.DoesNotContain(memory.Snapshot().Associations, a => a.CauseId == "cause-2");
        Assert.Equal(0.05 / 5, Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed)).Amount, 12);
        Assert.Equal(0.6, memory.Expectation(Answer).Chance);
        Assert.False(memory.Learn(Felt(4, "cause-4", -0.14)));
        memory.Retract("reading-ada", 6);
        Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed));
        Assert.Contains(memory.Recall(new[] { "shared" }, 6).Items, item => item.PreRun);
    }

    [Fact]
    public void InvalidProjectionIsAtomicAndDoesNotConsumeTheAcquisitionReceipt()
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", SeedPack(), sink: sink);
        var invalid = new MemoryExperience(0, 0, "cause", KnowledgeKind.Lived,
            new[] { new AssociationInput(Workshop, 0.2, new[] { "shared" }) },
            new[] { new ExpectationInput(Answer, false, new[] { "" }) });
        string before = Json(memory.Snapshot());
        Assert.Throws<ArgumentException>(() => memory.Learn(invalid));
        Assert.Equal(before, Json(memory.Snapshot()));
        Assert.Single(sink.Changes);
        Assert.True(memory.Learn(invalid with { Expectations = new[] { new ExpectationInput(Answer, false, new[] { "shared" }) } }));
        Assert.Equal(0.5, memory.Expectation(Answer).Chance);
        Assert.Equal(2, sink.Changes.Count);
    }

    [Theory]
    [InlineData(KnowledgeKind.Inferred)]
    [InlineData(KnowledgeKind.Reading)]
    public void InferenceOrReadingCannotBeSubmittedAsLearnedOutcome(KnowledgeKind kind)
    {
        var memory = new PersonalMemory("Ada", Empty);
        Assert.Throws<ArgumentException>(() => memory.Learn(Outcome(0, false) with { How = kind }));
        Assert.Empty(memory.Snapshot().Expectations);
    }

    [Fact]
    public void ToldEvidenceKeepsTellerAndDoesNotDiscountAnAlreadyFeltAmountAgain()
    {
        var memory = new PersonalMemory("Ada", Empty);
        var told = new MemoryExperience(0, 10, "reported", KnowledgeKind.Told,
            new[] { new AssociationInput(Friend, -0.1, new[] { "shared" }) },
            new[] { new ExpectationInput(Answer, false, new[] { "shared" }) }, "Cy", 0.4);
        Assert.True(memory.Learn(told));
        AssociationEntry association = Assert.Single(memory.Snapshot().Associations);
        Assert.Equal(-0.1, association.Amount);
        Assert.Equal(0.4, association.Credence);
        Assert.Equal("Cy", association.Teller);
        Assert.Equal(KnowledgeKind.Told, association.How);
        ExpectationEntry expectation = Assert.Single(memory.Expectation(Answer).Entries);
        Assert.Equal("Cy", expectation.Teller);
        Assert.Equal(0.4, expectation.Credence);
        var recalled = memory.Recall(new[] { "shared" }, 10).Items;
        RecalledMemory felt = Assert.Single(recalled.Where(i => i.Kind == RecallKind.Association));
        Assert.Equal(KnowledgeKind.Told, felt.How);
        Assert.Equal("Cy", felt.Teller);
        RecalledMemory estimate = Assert.Single(recalled.Where(i => i.Kind == RecallKind.Expectation));
        Assert.Equal(KnowledgeKind.Reading, estimate.How);
        Assert.Null(estimate.Teller);
        Assert.Contains(expectation.CauseId, estimate.Causes);
    }

    [Fact]
    public void ToldRequiresTellerAndOtherModesCannotBorrowOne()
    {
        var memory = new PersonalMemory("Ada", Empty);
        Assert.Throws<ArgumentException>(() => memory.Learn(Outcome(0, false) with { How = KnowledgeKind.Told }));
        Assert.Throws<ArgumentException>(() => memory.Learn(Outcome(0, false) with { Teller = "Cy" }));
        Assert.True(memory.Learn(Outcome(0, false)));
    }

    [Fact]
    public void NewAcquisitionsMustRespectClockAndExistingCausesMustReuseTheirReceipt()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(Felt(1, "first", 0.2, at: 10));
        Assert.Throws<ArgumentException>(() => memory.Learn(Felt(2, "past", 0.2, at: 9)));
        Assert.False(memory.Learn(Felt(2, "first", 0.2, at: 11)));
        Assert.False(memory.Learn(Felt(0, "older-receipt", 0.2, at: 12)));
        Assert.True(memory.Learn(Felt(2, "second", 0.2, at: 12)));
        memory.AdvanceToDay(1);
        Assert.Throws<ArgumentException>(() => memory.Learn(Felt(3, "before-night", 0.2, at: 1439)));
        Assert.Throws<ArgumentException>(() => memory.Retract("first", 1439));
    }

    [Fact]
    public void RetractionOfEvictedCauseUndoesItsDilutionOnceButDoesNotResurrectIt()
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", SeedPack(), sink: sink);
        for (int i = 0; i < 6; i++) memory.Learn(Felt(i, "cause-" + i, -0.1 - i / 100.0));
        Assert.DoesNotContain(memory.Snapshot().Associations, a => a.CauseId == "cause-0");
        Assert.Equal(0.05 / 7, Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed)).Amount, 12);
        memory.Retract("cause-0", 6);
        Assert.Equal(0.05 / 6, Assert.Single(memory.Snapshot().Associations.Where(a => a.Seed)).Amount, 12);
        string after = Json(memory.Snapshot());
        int receipts = sink.Changes.Count;
        memory.Retract("cause-0", 6);
        Assert.Equal(after, Json(memory.Snapshot()));
        Assert.Equal(receipts, sink.Changes.Count);
        Assert.False(memory.Learn(Felt(6, "cause-0", -0.1)));
        Assert.Equal(after, Json(memory.Snapshot()));
        Assert.Equal(6, memory.AcquisitionReceiptCount);
    }

    [Fact]
    public void ReadingsOfOnePreRunExperienceAreRoutesAndNotIndependentEvidence()
    {
        HistoryPack pack = SeedPack();
        pack = pack with
        {
            Readings = new[]
            {
                pack.Readings[0],
                pack.Readings[0] with { Id = "reading-company", Object = Friend },
                pack.Readings[0] with { Id = "reading-dread", Stance = ReadingStance.Wary },
            },
        };
        var memory = new PersonalMemory("Ada", pack);
        MemorySides sides = memory.Sides(new[] { Workshop, Friend });
        Assert.Equal(0.05, sides.Warm);
        Assert.Equal(0.05, sides.Cold);
        CauseContribution cause = Assert.Single(sides.Causes);
        Assert.Equal("account-ada", cause.CauseId);
        Assert.Equal(3, cause.Routes.Count);
    }

    [Fact]
    public void ImmediateEvictionStillRecordsBothTheAcquisitionAndDroppedEntry()
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", Empty, sink: sink);
        for (int i = 0; i < 3; i++) memory.Learn(Felt(i, "retained-" + i, 0.9 - i / 10.0));
        memory.Learn(Felt(3, "too-weak", 0.01));
        LivesMemoryChange change = sink.Changes[^1];
        AssociationEntry added = Assert.Single(change.AddedAssociations);
        Assert.Equal("too-weak", added.CauseId);
        Assert.Contains(added.Id, change.DroppedIds);
        Assert.DoesNotContain(change.After.Associations, a => a.Id == added.Id);
        Assert.Equal(3, memory.ActiveAssociationCount);
        Assert.Equal(4, memory.AcquisitionReceiptCount);
    }

    [Fact]
    public void AdvanceBeforeAcquiringLaterDayEvidencePreventsRetroactiveDecay()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(Felt(0, "yesterday", 0.2));
        Assert.Throws<ArgumentException>(() => memory.Learn(Felt(1, "today", 0.3, at: 1440)));
        memory.AdvanceToDay(1);
        Assert.True(memory.Learn(Felt(1, "today", 0.3, at: 1440)));
        Assert.Equal(0.2 * 0.975, memory.Snapshot().Associations.Single(a => a.CauseId == "yesterday").Amount, 12);
        Assert.Equal(0.3, memory.Snapshot().Associations.Single(a => a.CauseId == "today").Amount);
        string current = Json(memory.Snapshot());
        memory.AdvanceToDay(1);
        Assert.Equal(current, Json(memory.Snapshot()));
    }

    [Fact]
    public void RetractedExpectationCueCannotKeepCallingItsEvidenceToMind()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(Outcome(0, false, cue: "first-only"));
        memory.Learn(Outcome(1, true, cue: "second-only"));
        Assert.Single(memory.Recall(new[] { "first-only" }, 1).Items);
        memory.Retract("outcome-0", 2);
        Assert.Empty(memory.Recall(new[] { "first-only" }, 2).Items);
        Assert.Single(memory.Recall(new[] { "second-only" }, 2).Items);
        Assert.Equal(1, memory.Expectation(Answer).Chance);
    }

    [Fact]
    public void RemovingLastLivedExpectationRestoresTheSeedAndKeepsItsOriginalReading()
    {
        var memory = new PersonalMemory("Ada", SeedPack(ReadingStance.Expects));
        memory.Learn(Outcome(0, true));
        Assert.Equal(2.0 / 3, memory.Expectation(Answer).Chance, 12);
        memory.Retract("outcome-0", 1);
        Assert.Equal(0.5, memory.Expectation(Answer).Chance);
        Assert.True(Assert.Single(memory.Expectation(Answer).Entries).Seed);
        Assert.Contains(memory.Recall(new[] { "shared" }, 1).Items, i => i.Id == "reading-ada" && i.PreRun);
    }

    [Fact]
    public void IdenticalExperiencesAreIndependentForDifferentHolders()
    {
        var ada = new PersonalMemory("Ada", Empty);
        var bo = new PersonalMemory("Bo", Empty);
        MemoryExperience experience = Felt(0, "shared-event", 0.2);
        Assert.True(ada.Learn(experience));
        Assert.True(bo.Learn(experience with { Associations = new[] { new AssociationInput(Workshop, -0.4, new[] { "shared" }) } }));
        Assert.Equal(0.2, ada.Sides(new[] { Workshop }).Warm);
        Assert.Equal(0, ada.Sides(new[] { Workshop }).Cold);
        Assert.Equal(0.4, bo.Sides(new[] { Workshop }).Cold);
        Assert.Equal(0, bo.Sides(new[] { Workshop }).Warm);
        ada.Retract("shared-event", 1);
        Assert.Empty(ada.Snapshot().Associations);
        Assert.Single(bo.Snapshot().Associations);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0.975, 40_000)]
    public void CompleteDecayDropsVanishedEntriesAndCannotProtectAThirdZeroSign(double retention, int day)
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", Empty,
            new(NightlyRetention: retention, AssociationsPerObject: 2, AssociationsPerHolder: 2), sink);
        memory.Learn(Felt(0, "old-warm", 0.2));
        memory.Learn(Felt(1, "old-cold", -0.3));
        string[] oldIds = memory.Snapshot().Associations.Select(a => a.Id).ToArray();

        memory.AdvanceToDay(day);

        Assert.Equal(0, memory.ActiveAssociationCount);
        Assert.Empty(memory.Sides(new[] { Workshop }).Causes);
        int at = checked(day * Clock.MinutesPerDay);
        Assert.Empty(memory.Recall(new[] { "shared" }, at).Items);
        LivesMemoryChange decay = sink.Changes[^1];
        Assert.Equal("decay", decay.Reason);
        Assert.Equal(oldIds.Order(StringComparer.Ordinal), decay.DroppedIds);
        Assert.Empty(decay.After.Associations);
        Assert.Empty(decay.AddedAssociations);
        Assert.Equal(2, memory.AcquisitionReceiptCount);

        memory.Learn(Felt(2, "new-warm", 0.4, at: at));
        memory.Learn(Felt(3, "new-cold", -0.5, at: at));

        Assert.Equal(2, memory.ActiveAssociationCount);
        Assert.Equal(new[] { -0.5, 0.4 }, memory.Snapshot().Associations.Select(a => a.Amount).Order());
        Assert.Equal(0.4, memory.Sides(new[] { Workshop }).Warm);
        Assert.Equal(0.5, memory.Sides(new[] { Workshop }).Cold);
        Assert.All(memory.Snapshot().Associations, entry => Assert.NotEqual(0, entry.Amount));
        Assert.False(memory.Learn(Felt(4, "old-warm", 0.2, at: at)));
    }

    [Theory]
    [InlineData("learn")]
    [InlineData("recall")]
    [InlineData("retract")]
    public void FutureDayOperationRejectsAtomicallyUntilDayAdvance(string operation)
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", Empty, sink: sink);
        memory.Learn(Felt(0, "first", 0.2, at: 10));
        string before = Json(memory.Snapshot());
        int changes = sink.Changes.Count;

        void ApplyAtTomorrow()
        {
            switch (operation)
            {
                case "learn": memory.Learn(Felt(2, "tomorrow", 0.4, at: 1440)); break;
                case "recall": memory.Recall(new[] { "shared" }, 1440); break;
                case "retract": memory.Retract("first", 1440); break;
                default: throw new InvalidOperationException();
            }
        }

        Assert.Throws<ArgumentException>(ApplyAtTomorrow);
        Assert.Equal(before, Json(memory.Snapshot()));
        Assert.Equal(changes, sink.Changes.Count);
        Assert.Empty(sink.Recalls);
        Assert.Equal(1, memory.AcquisitionReceiptCount);
        Assert.True(memory.Learn(Felt(1, "still-today", -0.3, at: 20)));

        memory.AdvanceToDay(1);
        Assert.Equal(0.2 * 0.975, memory.Snapshot().Associations.Single(a => a.CauseId == "first").Amount, 12);
        Assert.Equal(-0.3 * 0.975, memory.Snapshot().Associations.Single(a => a.CauseId == "still-today").Amount, 12);
        ApplyAtTomorrow();
        switch (operation)
        {
            case "learn":
                Assert.Equal(0.4, memory.Snapshot().Associations.Single(a => a.CauseId == "tomorrow").Amount);
                Assert.Equal(3, memory.AcquisitionReceiptCount);
                break;
            case "recall":
                Assert.Equal(2, Assert.Single(sink.Recalls).Items.Count);
                break;
            case "retract":
                Assert.DoesNotContain(memory.Snapshot().Associations, a => a.CauseId == "first");
                Assert.False(memory.Learn(Felt(2, "first", 0.2, at: 1440)));
                break;
        }
    }

    [Theory]
    [InlineData("learn")]
    [InlineData("recall")]
    [InlineData("retract")]
    public void NegativeClockRejectsEvenBeforeFirstAcquisition(string operation)
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", SeedPack(), sink: sink);
        string before = Json(memory.Snapshot());
        Assert.Throws<ArgumentException>(() =>
        {
            switch (operation)
            {
                case "learn": memory.Learn(Felt(0, "invalid", 0.2, at: -1)); break;
                case "recall": memory.Recall(new[] { "shared" }, -1); break;
                case "retract": memory.Retract("unknown", -1); break;
                default: throw new InvalidOperationException();
            }
        });
        Assert.Equal(before, Json(memory.Snapshot()));
        Assert.Equal(0, memory.AcquisitionReceiptCount);
        Assert.Single(sink.Changes);
        Assert.Empty(sink.Recalls);
        Assert.True(memory.Learn(Felt(0, "valid", 0.2)));
    }

    [Fact]
    public void MalformedUtf16ObjectKeysRejectTheEntireAcquisitionBeforeIdsCanCollide()
    {
        var sink = new Sink();
        var memory = new PersonalMemory("Ada", Empty, sink: sink);
        var batch = new MemoryExperience(0, 0, "batch", KnowledgeKind.Lived, new[]
        {
            new AssociationInput(Workshop, 0.2, new[] { "shared" }),
            new AssociationInput(new(MemoryObjectKind.Place, "\uD800"), 0.3, new[] { "shared" }),
            new AssociationInput(new(MemoryObjectKind.Place, "\uD801"), -0.4, new[] { "shared" }),
        }, Array.Empty<ExpectationInput>());

        Assert.Throws<ArgumentException>(() => memory.Learn(batch));
        Assert.Empty(memory.Snapshot().Associations);
        Assert.Equal(0, memory.AcquisitionReceiptCount);
        Assert.Single(sink.Changes);
        Assert.True(memory.Learn(Felt(0, "batch", 0.2)));
        Assert.Single(memory.Snapshot().Associations);
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xD801)]
    public void MalformedUtf16CauseIdsRejectWithoutConsumingTheReceipt(int codeUnit)
    {
        string cause = new((char)codeUnit, 1);
        var memory = new PersonalMemory("Ada", Empty);
        Assert.Throws<ArgumentException>(() => memory.Learn(Felt(0, cause, 0.2)));
        Assert.Equal(0, memory.AcquisitionReceiptCount);
        Assert.Empty(memory.Snapshot().Associations);
        Assert.True(memory.Learn(Felt(0, "valid", 0.2)));
    }

    [Fact]
    public void WellFormedSupplementaryCharactersRemainDistinctAndRecallable()
    {
        var memory = new PersonalMemory("Ada", Empty);
        var seedling = new MemoryObject(MemoryObjectKind.Place, "Garden \U0001F331");
        var tree = new MemoryObject(MemoryObjectKind.Place, "Garden \U0001F333");
        memory.Learn(Felt(0, "cause-\U0001F331", 0.2, seedling, "cue-\U0001F331"));
        memory.Learn(Felt(1, "cause-\U0001F333", 0.3, tree, "cue-\U0001F333"));
        Assert.Equal(2, memory.ActiveAssociationCount);
        Assert.Equal(2, memory.Snapshot().Associations.Select(a => a.Id).Distinct().Count());
        Assert.Equal("cause-\U0001F331", Assert.Single(memory.Recall(new[] { "cue-\U0001F331" }, 1).Items).Causes.Single());
        Assert.Equal("cause-\U0001F333", Assert.Single(memory.Recall(new[] { "cue-\U0001F333" }, 1).Items).Causes.Single());
    }

    [Fact]
    public void MixedExpectationRecallIsAReadingAndKeepsEveryOutcomesActualProvenance()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(Outcome(0, false, cause: "reported") with { How = KnowledgeKind.Told, Teller = "Cy", Credence = 0.4 });
        memory.Learn(Outcome(1, true, cause: "witnessed"));
        RecalledMemory estimate = Assert.Single(memory.Recall(new[] { "shared" }, 1).Items);
        Assert.Equal(RecallKind.Expectation, estimate.Kind);
        Assert.Equal(KnowledgeKind.Reading, estimate.How);
        Assert.Null(estimate.Teller);
        Assert.Equal(new[] { "reported", "witnessed" }, estimate.Causes);
        ExpectationSnapshot expectation = memory.Expectation(Answer);
        ExpectationEntry reported = expectation.Entries.Single(e => e.CauseId == "reported");
        ExpectationEntry witnessed = expectation.Entries.Single(e => e.CauseId == "witnessed");
        Assert.Equal(KnowledgeKind.Told, reported.How);
        Assert.Equal("Cy", reported.Teller);
        Assert.Equal(0.4, reported.Credence);
        Assert.Equal(KnowledgeKind.Witnessed, witnessed.How);
        Assert.Null(witnessed.Teller);
        Assert.Equal(1, witnessed.Credence);
        Assert.Equal(2.0 / 3, expectation.Chance, 12);
    }
}
