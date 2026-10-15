using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>All histories are synthetic; canonical standing here only exercises the admission gate.</summary>
public sealed class LivesMemoryFrameTests
{
    private static readonly MemoryObject Workshop = new(MemoryObjectKind.Place, "Workshop");
    private static readonly MemoryObject Friend = new(MemoryObjectKind.Person, "Bo");
    private static readonly ExpectationKey Answer = new(ExpectationKind.Answers, "Bo", "mending invitation");
    private static readonly string[] Cues = { "person:Bo", "place:Workshop" };

    private static HistoryPack Pack()
    {
        var review = new HistoryReview(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15");
        return new(1,
            new[] { new HistorySource("source", "synthetic:workshop", HistorySourceKind.Canon) },
            new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }),
                new HistoryFact("other-fact", HistoryKind.Practice, new[] { "source" }) },
            new[]
            {
                new HistoryAccount("account-ada", "fact", "Ada", "My workshop experience.", KnowledgeKind.Lived,
                    new[] { "source" }, new[] { new HistoryCue("place:Workshop") }),
                new HistoryAccount("account-bo", "fact", "Bo", "A different private account.", KnowledgeKind.Witnessed,
                    new[] { "source" }, new[] { new HistoryCue("place:Workshop") }),
                new HistoryAccount("other-account", "other-fact", "Ada", "A separate experience with Bo.", KnowledgeKind.Lived,
                    new[] { "source" }, new[] { new HistoryCue("person:Bo") }),
            },
            new[]
            {
                new HistoryReading("warm", "account-ada", Workshop, ReadingStance.Warm, "I like working there.",
                    ReadingOrigin.CanonStated, AdmissionRule.R2, review, new[] { "source" },
                    new[] { new HistoryCue("place:Workshop", "source") }),
                new HistoryReading("wary", "account-ada", Workshop, ReadingStance.Wary, "I am also wary of it.",
                    ReadingOrigin.CanonStated, AdmissionRule.R2, review, new[] { "source" },
                    new[] { new HistoryCue("place:Workshop") }),
                new HistoryReading("expects", "account-ada", Friend, ReadingStance.Expects, "Bo may turn me down.",
                    ReadingOrigin.CanonStated, AdmissionRule.R2, review, new[] { "source" },
                    new[] { new HistoryCue("person:Bo") }, Expectation: Answer, ExpectedSuccess: false),
                new HistoryReading("other-reading", "other-account", Friend, ReadingStance.Warm, "I like Bo.",
                    ReadingOrigin.CanonStated, AdmissionRule.R2, review, new[] { "source" },
                    new[] { new HistoryCue("person:Bo") }),
                new HistoryReading("private-bo", "account-bo", Friend, ReadingStance.Wary, "Bo's private worry.",
                    ReadingOrigin.CanonStated, AdmissionRule.R2, review, new[] { "source" },
                    new[] { new HistoryCue("person:Bo") }),
            });
    }

    private static PersonalMemory Memory(ILivesSink? sink = null) => new("Ada", Pack(), new(RecallCount: 20), sink);
    private static MemoryExperience Experience(long sequence, string cause = "lived", double amount = 0.4,
        MemoryObject? target = null, bool success = true) => new(sequence, (int)sequence, cause, KnowledgeKind.Lived,
        new[] { new AssociationInput(target ?? Workshop, amount, Cues) },
        new[] { new ExpectationInput(Answer, success, Cues) });
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private sealed class Sink : ILivesSink
    {
        public List<LivesMemoryChange> Changes { get; } = new();
        public List<RecallSnapshot> Recalls { get; } = new();
        public void Changed(LivesMemoryChange change) => Changes.Add(change);
        public void Recalled(RecallSnapshot recall) => Recalls.Add(recall);
    }

    [Fact]
    public void FrameUsesTheSameProductionQueriesAsTheCapturedMemory()
    {
        var memory = Memory();
        memory.Learn(Experience(0));
        MemoryFrame frame = memory.Capture();
        Assert.Equal(memory.Holder, frame.Holder);
        Assert.Equal(Json(memory.Snapshot()), Json(frame.Snapshot()));
        Assert.Equal(Json(memory.Recall(Cues, 0, true)), Json(frame.Recall(Cues, 0, true)));
        Assert.Equal(Json(memory.Sides(new[] { Friend, Workshop })), Json(frame.Sides(new[] { Friend, Workshop })));
        Assert.Equal(Json(memory.Expectation(Answer)), Json(frame.Expectation(Answer)));
    }

    [Fact]
    public void FrameRemainsFrozenAfterLearningRetractionAndDecay()
    {
        var memory = Memory();
        memory.Learn(Experience(0));
        MemoryFrame frame = memory.Capture();
        string snapshot = Json(frame.Snapshot()), recall = Json(frame.Recall(Cues, 0));
        memory.Learn(Experience(1, "later", -0.7));
        memory.Retract("lived", 2);
        memory.AdvanceToDay(1);
        Assert.Equal(snapshot, Json(frame.Snapshot()));
        Assert.Equal(recall, Json(frame.Recall(Cues, 0)));
        Assert.NotEqual(snapshot, Json(memory.Snapshot()));
        Assert.Throws<ArgumentException>(() => frame.Recall(Cues, Clock.MinutesPerDay));
    }

    [Fact]
    public void FrameQueriesRespectTheCapturedKnowledgeBoundary()
    {
        var memory = Memory();
        memory.Learn(Experience(12));
        var frame = memory.Capture();
        Assert.Throws<ArgumentException>(() => frame.Recall(Cues, 11));
        Assert.NotEmpty(frame.Recall(Cues, 12).Items);
        memory.AdvanceToDay(1);
        var tomorrow = memory.Capture();
        Assert.Throws<ArgumentException>(() => tomorrow.Recall(Cues, 12));
        Assert.NotEmpty(tomorrow.Recall(Cues, Clock.MinutesPerDay).Items);
    }

    [Fact]
    public void RemovingFactClosesThroughOnlyItsOwnAccountsReadingsAndSeeds()
    {
        var frame = Memory().Capture();
        var without = frame.WithoutHistory(new[] { "fact" });
        Assert.Equal(new[] { "account-ada", "expects", "fact", "warm", "wary" }, without.RemovedIds);
        Assert.Equal(new[] { "other-account", "other-reading" }, without.Recall(Cues, 0).Items.Select(i => i.Id).Order());
        Assert.Empty(without.Sides(new[] { Workshop }).Causes);
        Assert.Single(without.Sides(new[] { Friend }).Causes);
        Assert.Empty(without.Expectation(Answer).Entries);
        Assert.Equal(1, without.Expectation(Answer).Chance);
        Assert.NotEmpty(frame.Sides(new[] { Workshop }).Causes);
        Assert.Empty(frame.RemovedIds);
    }

    [Fact]
    public void RemovingAccountKeepsFactIdentityAndAnUnrelatedExperience()
    {
        var frame = Memory().Capture().WithoutHistory(new[] { "account-ada" });
        Assert.Equal(new[] { "account-ada", "expects", "warm", "wary" }, frame.RemovedIds);
        Assert.Contains("fact", frame.HistoryIds);
        Assert.Single(frame.Snapshot().Associations);
        Assert.Contains(frame.Recall(Cues, 0).Items, i => i.Id == "other-account");
    }

    [Fact]
    public void RemovingOneReadingDoesNotRemoveTheOtherSideOfTheSameAccount()
    {
        var memory = Memory();
        memory.Learn(Experience(0));
        var frame = memory.Capture();
        var without = frame.WithoutHistory(new[] { "warm" });
        Assert.Equal(new[] { "warm" }, without.RemovedIds);
        Assert.Equal(0.025, without.Snapshot().Associations.Single(a => a.Seed && a.Amount < 0).Amount * -1, 12);
        Assert.Equal(0.4, without.Sides(new[] { Workshop }).Warm, 12);
        Assert.Equal(0.025, without.Sides(new[] { Workshop }).Cold, 12);
        Assert.Contains(without.Recall(Cues, 0).Items, i => i.Id == "account-ada");
        Assert.Contains(without.Recall(Cues, 0).Items, i => i.Id == "wary");
        Assert.DoesNotContain(without.Recall(Cues, 0).Items, i => i.Id == "warm");
        Assert.Equal(Json(memory.Snapshot()), Json(frame.Snapshot()));
    }

    [Fact]
    public void RemovingExpectedHistoryRetainsTheFirstLivedOutcomeAndRecalculatesChance()
    {
        var memory = Memory();
        memory.Learn(Experience(0));
        var frame = memory.Capture();
        var without = frame.WithoutHistory(new[] { "expects" });
        Assert.Equal(2.0 / 3, frame.Expectation(Answer).Chance, 12);
        Assert.Equal(1, without.Expectation(Answer).Chance);
        Assert.Equal("lived", Assert.Single(without.Expectation(Answer).Entries).CauseId);
        Assert.Contains(without.Recall(Cues, 0).Items, item => item.Kind == RecallKind.Expectation
            && item.Causes.SequenceEqual(new[] { "lived" }));
    }

    [Fact]
    public void RemovingAllHistoryPreservesIndependentLivedEvidenceEvenWithACollidingCauseName()
    {
        var memory = Memory();
        memory.Learn(Experience(0, "account-ada"));
        var frame = memory.Capture();
        var without = frame.WithoutHistory(frame.HistoryIds);
        Assert.Equal(frame.HistoryIds, without.RemovedIds);
        Assert.Equal("account-ada", Assert.Single(without.Snapshot().Associations).CauseId);
        Assert.False(Assert.Single(without.Expectation(Answer).Entries).Seed);
        Assert.All(without.Recall(Cues, 0).Items, item => Assert.False(item.PreRun));
        Assert.Equal(0.4, without.Sides(new[] { Workshop }).Warm);
    }

    [Fact]
    public void CompletedPastRequiresItsSourcedPresentLinkAndCanBeAblatedWithoutErasingLiveEvidence()
    {
        HistoryPack pack = Pack();
        pack = pack with
        {
            Facts = pack.Facts.Select(f => f.Id == "fact" ? f with { Kind = HistoryKind.Past } : f).ToArray(),
            Readings = pack.Readings.Select(r => r.AccountId is "account-ada" or "account-bo"
                ? r with { Admission = AdmissionRule.R1, PresentLinkSource = "source" } : r).ToArray(),
        };
        var memory = new PersonalMemory("Ada", pack, new(RecallCount: 20));
        memory.Learn(Experience(0));
        var frame = memory.Capture();
        Assert.Contains(frame.Recall(Cues, 0).Items, item => item.Id == "warm" && item.Overlap == 2);
        var without = frame.WithoutHistory(new[] { "fact" });
        Assert.Single(without.Snapshot().Associations.Where(a => !a.Seed));
        Assert.DoesNotContain(without.Recall(Cues, 0).Items, item => item.Id == "warm");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("source")]
    [InlineData("account-bo")]
    [InlineData("private-bo")]
    public void ForeignOrUnknownHistoryIsRejectedWithoutPartialRemoval(string id)
    {
        var frame = Memory().Capture();
        string before = Json(frame.Snapshot());
        Assert.DoesNotContain(id, frame.HistoryIds);
        Assert.Throws<ArgumentException>(() => frame.WithoutHistory(new[] { "warm", id }));
        Assert.Equal(before, Json(frame.Snapshot()));
        Assert.Empty(frame.RemovedIds);
    }

    [Fact]
    public void RepeatedAndIncrementalRemovalIsIdempotentAndOrderIndependent()
    {
        var frame = Memory().Capture();
        var once = frame.WithoutHistory(new[] { "fact" });
        var repeated = once.WithoutHistory(new[] { "fact", "warm", "account-ada", "fact" });
        var incremental = frame.WithoutHistory(new[] { "warm" }).WithoutHistory(new[] { "account-ada", "fact" });
        Assert.Equal(Json(once.Snapshot()), Json(repeated.Snapshot()));
        Assert.Equal(Json(once.Recall(Cues, 0)), Json(incremental.Recall(Cues, 0)));
        Assert.Equal(once.RemovedIds, repeated.RemovedIds);
        Assert.Equal(once.RemovedIds, incremental.RemovedIds);
        Assert.Equal(frame.HistoryIds, repeated.HistoryIds);
    }

    [Fact]
    public void CaptureAblationAndQueriesCannotPublishObserverEvents()
    {
        var sink = new Sink();
        var memory = Memory(sink);
        memory.Learn(Experience(0));
        int count = sink.Changes.Count;
        var frame = memory.Capture();
        frame.Recall(Cues, 0);
        var without = frame.WithoutHistory(new[] { "fact" });
        without.Recall(Cues, 0);
        without.Snapshot();
        without.Sides(new[] { Workshop });
        without.Expectation(Answer);
        Assert.Equal(count, sink.Changes.Count);
        Assert.Empty(sink.Recalls);
    }

    [Fact]
    public void ExposedCollectionsCannotChangeTheFrame()
    {
        var frame = Memory().Capture();
        Assert.Throws<NotSupportedException>(() => ((IList<string>)frame.HistoryIds)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<AssociationEntry>)frame.Snapshot().Associations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ExpectationEntry>)frame.Expectation(Answer).Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)frame.WithoutHistory(new[] { "warm" }).RemovedIds).Clear());
    }

    [Fact]
    public void CorrectionMovesBlameAndUpdatesOneExperienceWithOneObserverReceipt()
    {
        var sink = new Sink();
        var memory = Memory(sink);
        memory.Learn(Experience(0, amount: -0.2));
        var captured = memory.Capture();
        string before = Json(captured.Snapshot());
        int count = sink.Changes.Count;
        Assert.True(memory.Correct(Experience(1, amount: -0.7, target: Friend, success: false)));
        Assert.Equal(count + 1, sink.Changes.Count);
        LivesMemoryChange change = sink.Changes[^1];
        Assert.Equal("correct", change.Reason);
        Assert.Equal("lived", change.CauseId);
        Assert.Equal(before, Json(change.Before));
        Assert.Equal(Json(memory.Snapshot()), Json(change.After));
        Assert.Equal(Friend, Assert.Single(change.AddedAssociations).Object);
        Assert.Single(change.DroppedIds);
        Assert.DoesNotContain(memory.Snapshot().Associations, a => !a.Seed && a.Object == Workshop);
        Assert.Equal(-0.7, memory.Snapshot().Associations.Single(a => !a.Seed).Amount);
        Assert.Equal(0.05, memory.Snapshot().Associations.Single(a => a.Seed && a.Object == Workshop && a.Amount > 0).Amount);
        Assert.Equal(0.025, memory.Snapshot().Associations.Single(a => a.Seed && a.Object == Friend).Amount);
        Assert.False(Assert.Single(memory.Expectation(Answer).Entries.Where(e => !e.Seed)).Success);
        Assert.Equal(1, memory.AcquisitionReceiptCount);
        Assert.Equal(before, Json(captured.Snapshot()));
    }

    [Fact]
    public void CorrectingAmountAndProvenanceDoesNotDoubleDiluteOrCountASecondOutcome()
    {
        var memory = Memory();
        memory.Learn(Experience(0));
        Assert.True(memory.Correct(Experience(1, amount: 0.8) with { How = KnowledgeKind.Told, Teller = "Cy", Credence = 0.6 }));
        Assert.Equal(0.025, memory.Snapshot().Associations.Single(a => a.Seed && a.Amount > 0 && a.Object == Workshop).Amount);
        var lived = Assert.Single(memory.Snapshot().Associations.Where(a => !a.Seed));
        Assert.Equal(0.8, lived.Amount);
        Assert.Equal(KnowledgeKind.Told, lived.How);
        Assert.Equal("Cy", lived.Teller);
        Assert.Equal(0.6, lived.Credence);
        Assert.Equal(2.0 / 3, memory.Expectation(Answer).Chance, 12);
        Assert.Equal(1, memory.AcquisitionReceiptCount);
    }

    [Fact]
    public void InvalidCorrectionIsAtomicAndOldSequenceIsAnIdempotentRetry()
    {
        var sink = new Sink();
        var memory = Memory(sink);
        memory.Learn(Experience(0));
        string before = Json(memory.Snapshot());
        int count = sink.Changes.Count;
        Assert.Throws<ArgumentException>(() => memory.Correct(Experience(1, amount: 2)));
        Assert.Throws<ArgumentException>(() => memory.Correct(Experience(1, cause: "unknown")));
        Assert.Throws<ArgumentException>(() => memory.Correct(Experience(1) with { How = KnowledgeKind.Inferred }));
        Assert.Equal(before, Json(memory.Snapshot()));
        Assert.Equal(count, sink.Changes.Count);
        Assert.False(memory.Correct(Experience(0, amount: -0.8)));
        Assert.True(memory.Correct(Experience(1, amount: 0.6)));
        Assert.False(memory.Correct(Experience(1, amount: -0.8)));
        Assert.Equal(count + 1, sink.Changes.Count);
    }

    [Fact]
    public void ExplicitCorrectionCanReestablishRetractedCauseWithoutUndoingDilutionTwice()
    {
        var memory = Memory();
        memory.Learn(Experience(0));
        memory.Retract("lived", 1);
        Assert.False(memory.Learn(Experience(2)));
        Assert.True(memory.Correct(Experience(2, amount: 0.6)));
        Assert.Equal(0.025, memory.Snapshot().Associations.Single(a => a.Seed && a.Amount > 0 && a.Object == Workshop).Amount);
        Assert.Single(memory.Snapshot().Associations.Where(a => !a.Seed));
        Assert.Single(memory.Expectation(Answer).Entries.Where(a => !a.Seed));
        Assert.Equal(1, memory.AcquisitionReceiptCount);
        memory.Retract("lived", 3);
        Assert.Equal(0.05, memory.Snapshot().Associations.Single(a => a.Seed && a.Amount > 0 && a.Object == Workshop).Amount);
    }

    [Fact]
    public void CorrectingAnEvictedProjectionUndoesItsOldDilutionAndKeepsBounds()
    {
        var memory = new PersonalMemory("Ada", Pack(), new(AssociationsPerObject: 2, LivedEntriesPerExpectation: 2));
        memory.Learn(Experience(0, "old", amount: 0.001));
        memory.Learn(Experience(1, "one", amount: 0.7));
        memory.Learn(Experience(2, "two", amount: 0.8));
        Assert.DoesNotContain(memory.Snapshot().Associations, a => a.CauseId == "old");
        Assert.DoesNotContain(memory.Expectation(Answer).Entries, e => e.CauseId == "old");
        Assert.True(memory.Correct(Experience(3, "old", amount: -0.4, target: Friend)));
        Assert.Equal(0.05 / 3, memory.Snapshot().Associations.Single(a => a.Seed && a.Object == Workshop).Amount * -1, 12);
        Assert.Equal(2, memory.Expectation(Answer).Entries.Count(e => !e.Seed));
        Assert.Equal(3, memory.AcquisitionReceiptCount);
        Assert.All(memory.Snapshot().Associations.GroupBy(a => a.Object), g => Assert.True(g.Count() <= 2));
    }
}
