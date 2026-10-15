using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Invented people, places and evidence. These are kernel checks, not model trials.</summary>
public sealed class LivesChoiceTests
{
    private static readonly MemoryObject Place = new(MemoryObjectKind.Place, "Workshop");
    private static readonly MemoryObject Person = new(MemoryObjectKind.Person, "Bo");
    private static readonly ExpectationKey Answer = new(ExpectationKind.Answers, "Bo", "workshop visit");
    private static readonly HistoryPack Empty = new(1, Array.Empty<HistorySource>(), Array.Empty<HistoryFact>(),
        Array.Empty<HistoryAccount>(), Array.Empty<HistoryReading>());

    private static HistoryPack History(ReadingStance stance = ReadingStance.Warm) => new(1,
        new[] { new HistorySource("source", "synthetic:workshop", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "fact", "Ada", "I have spent time there.", KnowledgeKind.Lived,
            new[] { "source" }, new[] { new HistoryCue("scene") }) },
        new[] { new HistoryReading("reading", "account", Place, stance, "My own feeling about it.",
            ReadingOrigin.CanonStated, AdmissionRule.R2, new(ReviewStatus.Reviewed, "Synthetic test", "2026-10-15"),
            new[] { "source" }, new[] { new HistoryCue("scene") },
            Expectation: stance == ReadingStance.Expects ? Answer : null,
            ExpectedSuccess: stance == ReadingStance.Expects ? false : null) });
    private static ChoiceOption Visit(double weight = 1) => new("visit", "Go to the workshop.", weight,
        new[] { Place }, Array.Empty<ExpectationKey>());
    private static ChoiceOption Stay(double weight = 1) => new("stay", "Stay home.", weight,
        Array.Empty<MemoryObject>(), Array.Empty<ExpectationKey>());
    private static ChoiceOpportunity Scene(params ChoiceOption[] options) => new("dec:10:Ada:0", "Ada", 10,
        "free-time", new[] { "scene" }, options.Length == 0 ? new[] { Visit(), Stay() } : options, 0.2);
    private static MemoryFrame Frame(HistoryPack? pack = null) => new PersonalMemory("Ada", pack ?? History()).Capture();
    private static string Json(object value) => JsonSerializer.Serialize(value);

    [Fact]
    public void SameSelectionCanHaveChangedReasonsAndProbabilities()
    {
        var comparison = LivesChoices.CompareAtUnit(Frame(), Scene(), new[] { "reading" }, 0.1);
        Assert.True(comparison.Comparable);
        Assert.True(comparison.ReasonsChanged);
        Assert.True(comparison.ProbabilitiesChanged);
        Assert.False(comparison.SelectionChanged);
        Assert.Equal("visit", comparison.WithHistory.SelectedId);
        Assert.Equal("visit", comparison.WithoutHistory.SelectedId);
        Assert.Equal(Math.Exp(.25) / (Math.Exp(.25) + 1), comparison.WithHistory.Alternatives[0].Probability, 12);
        Assert.Equal(.5, comparison.WithoutHistory.Alternatives[0].Probability, 12);
        Assert.Equal(comparison.WithHistory.Unit, comparison.WithoutHistory.Unit);
    }

    [Fact]
    public void FixedUnitShowsARealSelectionFlipWithoutSearchingSeeds()
    {
        var comparison = LivesChoices.CompareAtUnit(Frame(), Scene(), new[] { "reading" }, .53);
        Assert.True(comparison.Comparable);
        Assert.True(comparison.SelectionChanged);
        Assert.Equal("visit", comparison.WithHistory.SelectedId);
        Assert.Equal("stay", comparison.WithoutHistory.SelectedId);
    }

    [Fact]
    public void WarmAndColdRemainSeparateAndCauseAliasesContributeOnlyOnce()
    {
        var memory = new PersonalMemory("Ada", Empty);
        memory.Learn(new(1, 1, "shared-event", KnowledgeKind.Lived,
            new[] { new AssociationInput(Place, .4, new[] { "scene" }), new AssociationInput(Person, .4, new[] { "scene" }) },
            Array.Empty<ExpectationInput>()));
        memory.Learn(new(2, 2, "difficult-event", KnowledgeKind.Witnessed,
            new[] { new AssociationInput(Place, -.2, new[] { "scene" }) }, Array.Empty<ExpectationInput>()));
        var visit = Visit() with { Objects = new[] { Place, Person, Place } };
        var decision = LivesChoices.Evaluate(memory.Capture(), Scene(visit, Stay()), .2);
        var result = decision.Alternatives[0];
        Assert.Equal(.4, result.Sides.Warm, 12);
        Assert.Equal(.2, result.Sides.Cold, 12);
        Assert.Equal(2, result.Sides.Causes.Count);
        Assert.Equal(5 * (.4 - .2 * .8), result.LogWeight, 12);
        Assert.Contains(result.Reasons, r => r.Code == "warm" && r.Kind == ChoiceReasonKind.Supports && r.Value == .4);
        Assert.Contains(result.Reasons, r => r.Code == "cold" && r.Kind == ChoiceReasonKind.Conflicts && r.Value == .2);
        Assert.Contains("mixed-sides:visit", decision.TornReasons);
    }

    [Fact]
    public void SpecificExpectationKeysAreMultipliedOnceEach()
    {
        var memory = new PersonalMemory("Ada", History(ReadingStance.Expects));
        var availability = new ExpectationKey(ExpectationKind.Availability, "Bo", "workshop visit");
        memory.Learn(new(1, 1, "busy", KnowledgeKind.Witnessed, Array.Empty<AssociationInput>(),
            new[] { new ExpectationInput(availability, false, new[] { "scene" }) }));
        var decision = LivesChoices.Evaluate(memory.Capture(), Scene(Visit() with
            { Expectations = new[] { Answer, Answer, availability } }, Stay()), .1);
        var alternative = decision.Alternatives[0];
        Assert.Equal(2, alternative.Expectations.Count);
        Assert.Equal(.25, alternative.Chance, 12);
        Assert.Equal(.2, alternative.Probability, 12);
        Assert.Contains(alternative.Reasons, r => r.Via == ChoiceReasonVia.Expectation && r.Cites.Contains("busy"));
    }

    [Fact]
    public void RemovingAnExpectationChangesChanceWithoutDeletingLivedEvidence()
    {
        var memory = new PersonalMemory("Ada", History(ReadingStance.Expects));
        memory.Learn(new(1, 1, "agreed", KnowledgeKind.Witnessed, Array.Empty<AssociationInput>(),
            new[] { new ExpectationInput(Answer, true, new[] { "scene" }) }));
        var comparison = LivesChoices.CompareAtUnit(memory.Capture(), Scene(Visit() with { Expectations = new[] { Answer } }, Stay()),
            new[] { "reading" }, .1);
        Assert.True(comparison.Comparable);
        Assert.Equal(2.0 / 3, comparison.WithHistory.Alternatives[0].Chance, 12);
        Assert.Equal(1, comparison.WithoutHistory.Alternatives[0].Chance);
        Assert.Equal("agreed", Assert.Single(comparison.WithoutHistory.Alternatives[0].Expectations[0].Entries).CauseId);
    }

    [Fact]
    public void IrrelevantHistoryRemovalDoesNotChangeTheDecision()
    {
        HistoryPack pack = History();
        pack = pack with
        {
            Facts = pack.Facts.Append(new HistoryFact("elsewhere", HistoryKind.Fact, new[] { "source" })).ToArray(),
            Accounts = pack.Accounts.Append(new HistoryAccount("elsewhere-account", "elsewhere", "Ada", "An unrelated fact.",
                KnowledgeKind.Told, new[] { "source" }, new[] { new HistoryCue("elsewhere") }, "Cy")).ToArray(),
        };
        var comparison = LivesChoices.CompareAtUnit(Frame(pack), Scene(), new[] { "elsewhere" }, .53);
        Assert.True(comparison.Comparable);
        Assert.False(comparison.ReasonsChanged);
        Assert.False(comparison.ProbabilitiesChanged);
        Assert.False(comparison.SelectionChanged);
    }

    [Fact]
    public void RemovingOfferSupportMakesTheComparisonNonComparable()
    {
        var scene = Scene(Visit() with { RequiredRecallIds = new[] { "reading" } }, Stay());
        var comparison = LivesChoices.CompareAtUnit(Frame(), scene, new[] { "reading" }, .2);
        Assert.False(comparison.Comparable);
        Assert.Equal("eligible-alternatives-changed", comparison.NonComparableReason);
        Assert.Null(comparison.SelectionChanged);
        Assert.Empty(comparison.Differences);
        Assert.Equal("required-recall-missing", Assert.Single(comparison.WithoutHistory.Excluded).Code);
        Assert.DoesNotContain(comparison.WithoutHistory.Alternatives, a => a.Option.Id == "visit");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void AnotherWarmMemoryCanKeepTheSameOptionAvailableAfterHistoryRemoval(int recallCount)
    {
        var frame = new PersonalMemory("Ada", TwoWarmHistories(), new(RecallCount: recallCount)).Capture();
        var scene = Scene(Visit() with { Objects = new[] { Place, Person }, WarmRecallObjects = new[] { Place, Person } }, Stay());
        var comparison = LivesChoices.CompareAtUnit(frame, scene, new[] { "reading" }, .1);
        Assert.True(comparison.Comparable);
        Assert.True(comparison.ProbabilitiesChanged);
        Assert.Equal(new[] { "visit", "stay" }, comparison.WithoutHistory.Alternatives.Select(a => a.Option.Id));
        Assert.Contains(comparison.WithoutHistory.Recall.Items, item => item.Id == "reading-2");
        Assert.Contains(comparison.WithoutHistory.Alternatives[0].Reasons, reason => reason.Code == "warm-recall"
            && reason.Cites.Contains("reading-2"));
        if (recallCount == 3)
            Assert.DoesNotContain(comparison.WithHistory.Recall.Items, item => item.Id == "reading-2");
        else Assert.Contains(comparison.WithHistory.Recall.Items, item => item.Id == "reading-2");
    }

    [Fact]
    public void RemovingEveryWarmSupportExcludesTheOptionWithoutInventingAReplacement()
    {
        var scene = Scene(Visit() with { Objects = new[] { Place, Person }, WarmRecallObjects = new[] { Place, Person } }, Stay());
        var comparison = LivesChoices.CompareAtUnit(Frame(TwoWarmHistories()), scene,
            new[] { "reading", "reading-2" }, .1);
        Assert.False(comparison.Comparable);
        Assert.Equal("eligible-alternatives-changed", comparison.NonComparableReason);
        Assert.Equal("stay", Assert.Single(comparison.WithoutHistory.Alternatives).Option.Id);
        Assert.Equal("missing-warm-recall", Assert.Single(comparison.WithoutHistory.Excluded).Code);
        Assert.Null(comparison.SelectionChanged);
    }

    [Fact]
    public void WarmRecallMustConcernTheSpecifiedObjectsAndHavePositiveWarmth()
    {
        var scene = Scene(Visit() with { WarmRecallObjects = new[] { Person } }, Stay());
        Assert.Equal("missing-warm-recall", Assert.Single(LivesChoices.Evaluate(Frame(), scene, .2).Excluded).Code);
        scene = Scene(Visit() with { WarmRecallObjects = new[] { Place } }, Stay());
        Assert.Equal("missing-warm-recall", Assert.Single(LivesChoices.Evaluate(Frame(History(ReadingStance.Wary)), scene, .2).Excluded).Code);
    }

    private static HistoryPack TwoWarmHistories()
    {
        HistoryPack pack = History();
        return pack with
        {
            Facts = pack.Facts.Append(pack.Facts[0] with { Id = "fact-2" }).ToArray(),
            Accounts = pack.Accounts.Append(pack.Accounts[0] with { Id = "account-2", FactId = "fact-2" }).ToArray(),
            Readings = pack.Readings.Append(pack.Readings[0] with { Id = "reading-2", AccountId = "account-2", Object = Person }).ToArray(),
        };
    }

    [Fact]
    public void RemovingOpportunitySupportDoesNotManufactureAnotherChoice()
    {
        var comparison = LivesChoices.CompareAtUnit(Frame(), Scene() with { RequiredRecallIds = new[] { "reading" } },
            new[] { "reading" }, .2);
        Assert.False(comparison.Comparable);
        Assert.Equal("opportunity-unavailable", comparison.NonComparableReason);
        Assert.False(comparison.WithoutHistory.Available);
        Assert.Null(comparison.WithoutHistory.SelectedId);
        Assert.Empty(comparison.WithoutHistory.Alternatives);
    }

    [Fact]
    public void EmptyRecallRequiresTheOriginalLegacyDraw()
    {
        var decision = LivesChoices.Evaluate(Frame(Empty), Scene(), .2);
        Assert.True(decision.Legacy);
        Assert.True(decision.Available);
        Assert.Null(decision.SelectedId);
        Assert.Empty(decision.Alternatives);
        var comparison = LivesChoices.CompareAtUnit(Frame(), Scene(), new[] { "fact" }, .2);
        Assert.False(comparison.Comparable);
        Assert.Equal("legacy-draw-required", comparison.NonComparableReason);
        Assert.Null(comparison.ReasonsChanged);
    }

    [Theory]
    [InlineData("fact", "account,fact,reading")]
    [InlineData("account", "account,reading")]
    [InlineData("reading", "reading")]
    public void ComparisonRecordsRequestedRootAndActualDependencyRemoval(string root, string expected)
    {
        var comparison = LivesChoices.CompareAtUnit(Frame(), Scene(), new[] { root }, .2);
        Assert.Equal(new[] { root }, comparison.RemovedHistoryIds);
        Assert.Equal(expected.Split(','), comparison.RemovedDependencyIds);
        Assert.Empty(comparison.WithoutHistory.Alternatives.SelectMany(a => a.Sides.Causes));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)comparison.RemovedDependencyIds).Clear());
    }

    [Fact]
    public void AStancelessWishCanOpenAnOptionWithoutBecomingWarmth()
    {
        var decision = LivesChoices.Evaluate(Frame(History(ReadingStance.None)),
            Scene(Visit() with { RequiredRecallIds = new[] { "reading" } }, Stay()), .2);
        Assert.False(decision.Legacy);
        Assert.Equal(0, decision.Alternatives[0].Sides.Warm);
        Assert.Equal(.5, decision.Alternatives[0].Probability);
        Assert.Contains(decision.Alternatives[0].Reasons, r => r.Via == ChoiceReasonVia.Recall
            && r.Code == "context-only" && r.Cites.Contains("reading"));
    }

    [Fact]
    public void RecallAboutAnUnrelatedObjectDoesNotAddWarmthToAnOption()
    {
        var decision = LivesChoices.Evaluate(Frame(), Scene(Visit() with { Objects = new[] { Person } }, Stay()), .2);
        Assert.Equal(0, decision.Alternatives[0].Sides.Warm);
        Assert.Equal(.5, decision.Alternatives[0].Probability);
        Assert.DoesNotContain(decision.Alternatives[0].Reasons, r => r.Code == "warm");
    }

    [Fact]
    public void SamplingUsesInputOrderAndOneNamedDraw()
    {
        var frame = Frame(History(ReadingStance.None));
        var scene = Scene(Stay(), Visit());
        var first = LivesChoices.Decide(frame, scene, 7315);
        var second = LivesChoices.Decide(frame, scene, 7315);
        Assert.Equal(Json(first), Json(second));
        Assert.Equal(Rng.Unit(7315, "lives-choice", scene.Id), first.Unit);
        Assert.Equal(new[] { "stay", "visit" }, first.Alternatives.Select(a => a.Option.Id));
        Assert.Equal("stay", LivesChoices.Evaluate(frame, scene, 0).SelectedId);
        Assert.Equal("visit", LivesChoices.Evaluate(frame, scene, .5).SelectedId);
    }

    [Fact]
    public void LargeAndTinyFiniteWeightsRemainFiniteAndNormalized()
    {
        var decision = LivesChoices.Evaluate(Frame(), Scene(Visit(1e300), Stay(1e-300)), .999);
        Assert.Equal("visit", decision.SelectedId);
        Assert.Equal(1, decision.Alternatives.Sum(a => a.Probability), 12);
        Assert.All(decision.Alternatives, a => { Assert.True(double.IsFinite(a.LogWeight)); Assert.True(double.IsFinite(a.Probability)); });
        Assert.Equal("visit", LivesChoices.Evaluate(Frame(), Scene(Visit(1e300), Stay(1e-300)), Math.BitDecrement(1.0)).SelectedId);
    }

    [Fact]
    public void ExactChoiceSettingsAreRecordedWithEachDecision()
    {
        var rules = new ChoiceSettings(3, .01, .02);
        var result = LivesChoices.Evaluate(Frame(), Scene(), .2, rules);
        Assert.Equal(rules, result.Settings);
        Assert.Equal(.15, result.Alternatives[0].LogWeight, 12);
        Assert.Throws<ArgumentException>(() => new ChoiceSettings(TornAt: double.NaN).Validate());
    }

    [Fact]
    public void ZeroBaseIsExcludedAndAllExcludedIsUnavailable()
    {
        var decision = LivesChoices.Evaluate(Frame(), Scene(Visit(0), Stay()), .2);
        Assert.Equal("stay", decision.SelectedId);
        Assert.Equal("zero-base", Assert.Single(decision.Excluded).Code);
        var unavailable = LivesChoices.Evaluate(Frame(), Scene(Visit(0)), .2);
        Assert.False(unavailable.Available);
        Assert.Null(unavailable.SelectedId);
    }

    [Fact]
    public void IntendedAndTravelCostUseTheSameScale()
    {
        var decision = LivesChoices.Evaluate(Frame(History(ReadingStance.None)),
            Scene(Visit() with { Intended = .2, GoingCost = .1 }, Stay()), .2);
        Assert.Equal(.5, decision.Alternatives[0].LogWeight, 12);
        Assert.Contains(decision.Alternatives[0].Reasons, r => r.Code == "intended" && r.Value == .2);
        Assert.Contains(decision.Alternatives[0].Reasons, r => r.Code == "going-cost" && r.Value == .1);
    }

    [Fact]
    public void TornCanMeanCloseChoicesOrWarmthWithPoorProspects()
    {
        var close = LivesChoices.Evaluate(Frame(History(ReadingStance.None)), Scene(), .1);
        Assert.Contains("close-alternatives", close.TornReasons);
        var memory = new PersonalMemory("Ada", History());
        for (int i = 1; i <= 2; i++) memory.Learn(new(i, i, "refusal-" + i, KnowledgeKind.Witnessed,
            Array.Empty<AssociationInput>(), new[] { new ExpectationInput(Answer, false, new[] { "scene" }) }));
        var unlikely = LivesChoices.Evaluate(memory.Capture(), Scene(Visit() with { Expectations = new[] { Answer } }, Stay()), .1);
        Assert.Contains("warm-but-unlikely:visit", unlikely.TornReasons);
        Assert.True(unlikely.Torn);
    }

    [Fact]
    public void ReceiptsCopyInputsAndContainOnlyReadOnlyCollections()
    {
        var objects = new List<MemoryObject> { Place };
        var warmObjects = new List<MemoryObject> { Place };
        var cues = new List<string> { "scene" };
        var cites = new List<string> { "account" };
        var options = new List<ChoiceOption> { Visit() with { Objects = objects, WarmRecallObjects = warmObjects }, Stay() };
        var scene = Scene() with { Cues = cues, Options = options,
            Excluded = new[] { new ChoiceExclusion("closed", "closed", cites) },
            Constraints = new[] { new ChoiceConstraint("travel", 5, cites) } };
        var decision = LivesChoices.Evaluate(Frame(), scene, .2);
        string before = Json(decision);
        objects.Clear(); warmObjects.Clear(); cues.Clear(); options.Clear(); cites.Clear();
        Assert.Equal(before, Json(decision));
        Assert.Throws<NotSupportedException>(() => ((IList<ChoiceAlternative>)decision.Alternatives).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MemoryObject>)decision.Alternatives[0].Option.Objects).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MemoryObject>)decision.Alternatives[0].Option.WarmRecallObjects!).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)decision.Excluded[0].Cites).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ChoiceReason>)decision.Alternatives[0].Reasons).Clear());
    }

    [Fact]
    public void ComparisonNeverMutatesLiveMemoryOrPreviouslyCapturedFrames()
    {
        var memory = new PersonalMemory("Ada", History());
        var frame = memory.Capture();
        string original = Json(memory.Snapshot());
        var comparison = LivesChoices.CompareAtUnit(frame, Scene(), new[] { "reading" }, .53);
        Assert.Equal(original, Json(memory.Snapshot()));
        memory.Learn(new(1, 1, "new", KnowledgeKind.Lived, new[] { new AssociationInput(Place, -.8, new[] { "scene" }) },
            Array.Empty<ExpectationInput>()));
        Assert.Equal(Json(comparison.WithHistory), Json(LivesChoices.Evaluate(frame, Scene(), .53)));
    }

    [Fact]
    public void AnotherHoldersFrameCannotSupplyPrivateReasons()
    {
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(Frame(), Scene() with { Holder = "Bo" }, .2));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidBaseIsRejected(double value) =>
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(Frame(), Scene(Visit(value)), .2));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-.01)]
    [InlineData(1)]
    public void InvalidDrawIsRejected(double value) =>
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(Frame(), Scene(), value));

    [Fact]
    public void InvalidAndAmbiguousInputsAreRejected()
    {
        var frame = Frame();
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene(Visit(), Visit()), .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene() with { Boldness = 2 }, .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene() with { Options = Array.Empty<ChoiceOption>() }, .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene() with { Options = Enumerable.Range(0, 6).Select(i => Visit() with { Id = "x" + i }).ToArray() }, .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene(Visit() with { Line = "bad\ud800" }), .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene(Visit() with { WarmRecallObjects = Array.Empty<MemoryObject>() }), .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene(Visit() with { WarmRecallObjects = new[] { new MemoryObject((MemoryObjectKind)999, "invalid") } }), .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene() with { Excluded = new[] { new ChoiceExclusion("visit", "closed", Array.Empty<string>()) } }, .2));
        Assert.Throws<ArgumentException>(() => LivesChoices.Evaluate(frame, Scene(), .2, new ChoiceSettings(Scale: 0)));
    }
}
