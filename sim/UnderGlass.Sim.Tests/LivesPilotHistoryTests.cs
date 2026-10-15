using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The reviewed pilot content, not invented histories. Source interpretation is
/// audited in pilot-history-review-2026-10-15.md; these tests protect its runtime boundaries.</summary>
public sealed class LivesPilotHistoryTests
{
    private static readonly MemoryObject Pam = new(MemoryObjectKind.Person, "Pam");
    private static readonly MemoryObject Saloon = new(MemoryObjectKind.Place, "Saloon");
    private static readonly MemoryObject Game = new(MemoryObjectKind.Activity, "PlayedGame");

    private static HistoryPack Pack()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        const string relative = "sim/history/penny-pam-gus.json";
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relative)))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return HistoryPack.Read(File.ReadAllText(Path.Combine(directory!.FullName, relative)));
    }

    [Fact]
    public void PackIsExplicitlyReviewedAndOnlyTheThreeNamedPeopleHaveAccounts()
    {
        HistoryPack pack = Pack();
        Assert.Equal(new[] { "Gus", "Pam", "Penny" }, pack.Accounts.Select(a => a.Holder).Distinct().Order());
        Assert.Equal(9, pack.Facts.Count);
        Assert.Equal(12, pack.Accounts.Count);
        Assert.Equal(21, pack.Readings.Count);
        Assert.All(pack.Readings, reading =>
        {
            Assert.Equal(ReviewStatus.Reviewed, reading.Review.Status);
            Assert.Equal("2026-10-15", reading.Review.Date);
            Assert.Contains("Codex", reading.Review.By);
            Assert.Equal(ReadingOrigin.CanonStated, reading.Origin);
        });
        Assert.All(pack.Sources, source => Assert.StartsWith("Characters/Dialogue/", source.Key));
        Assert.Empty(new PersonalMemory("Newcomer", pack).Snapshot().Associations);
        Assert.Empty(new PersonalMemory("Newcomer", pack).Recall(new[] { "person:Pam", "place:Saloon" }, 0).Items);
    }

    [Fact]
    public void SaloonPracticeHasThreePersonalRoutesAndDoesNotDoublePennysBurden()
    {
        HistoryPack pack = Pack();
        HistoryAccount[] accounts = pack.Accounts.Where(a => a.FactId == "ppga-saloon-practice").ToArray();
        Assert.Equal(3, accounts.Length);
        Assert.Equal(KnowledgeKind.Inferred, accounts.Single(a => a.Holder == "Penny").How);
        Assert.Equal(KnowledgeKind.Lived, accounts.Single(a => a.Holder == "Pam").How);
        Assert.Equal(KnowledgeKind.Witnessed, accounts.Single(a => a.Holder == "Gus").How);
        var penny = new PersonalMemory("Penny", pack);
        MemorySides combined = penny.Sides(new[] { Pam, Saloon });
        Assert.Equal(.05, combined.Warm, 12);
        Assert.Equal(.05, combined.Cold, 12);
        CauseContribution burden = Assert.Single(combined.Causes.Where(c => c.Cold > 0));
        Assert.Equal("ppga-penny-saloon", burden.CauseId);
        Assert.Equal(2, burden.Routes.Count);
        Assert.Contains(combined.Causes, cause => cause.CauseId == "ppga-penny-care" && cause.Warm == .05);
    }

    [Fact]
    public void PamKeepsWarmAndWarySaloonReadingsButHerWishCreatesNoGameEnjoyment()
    {
        var pam = new PersonalMemory("Pam", Pack());
        MemorySides sides = pam.Sides(new[] { Saloon });
        Assert.Equal(.05, sides.Warm, 12);
        Assert.Equal(.05, sides.Cold, 12);
        Assert.Single(sides.Causes);
        Assert.Empty(pam.Snapshot().Associations.Where(a => a.Object == Game));
        RecalledMemory wish = Assert.Single(pam.Recall(new[] { "wish:activity:PlayedGame" }, 0).Items
            .Where(item => item.Id == "ppg-pam-hobby-wish"));
        Assert.Equal(RecallKind.Reading, wish.Kind);
        Assert.Equal(0, wish.Warm);
        Assert.Equal(0, wish.Cold);
        var option = new ChoiceOption("offer", "Ask about a game.", .1, new[] { Game },
            Array.Empty<ExpectationKey>(), RequiredRecallIds: new[] { wish.Id });
        var scene = new ChoiceOpportunity("wish-probe", "Pam", 0, "encounter",
            new[] { "wish:activity:PlayedGame" }, new[] { option }, .5);
        Assert.Equal("offer", LivesChoices.Evaluate(pam.Capture(), scene, .5).SelectedId);
        ChoiceDecision removed = LivesChoices.Evaluate(pam.Capture().WithoutHistory(new[] { wish.Id }), scene, .5);
        Assert.DoesNotContain(removed.Alternatives, a => a.Option.Id == "offer");
    }

    [Fact]
    public void OnlyTruckNostalgiaSeedsFromACompletedPastAndTheSharedCauseCountsOnce()
    {
        HistoryPack pack = Pack();
        var pastIds = pack.Facts.Where(f => f.Kind == HistoryKind.Past).Select(f => f.Id).ToHashSet();
        var pastAccounts = pack.Accounts.Where(a => pastIds.Contains(a.FactId)).ToDictionary(a => a.Id);
        HistoryReading[] numeric = pack.Readings.Where(r => pastAccounts.ContainsKey(r.AccountId)
            && r.Stance != ReadingStance.None).ToArray();
        Assert.Equal(3, numeric.Length);
        Assert.All(numeric, reading =>
        {
            Assert.Equal("ppga-pam-truck-job", reading.AccountId);
            Assert.Equal(AdmissionRule.R1, reading.Admission);
            Assert.Equal("ppg-source-pam-fall-sun4", reading.PresentLinkSource);
        });
        var pam = new PersonalMemory("Pam", pack);
        var activities = new[] { "Travel", "Radio", "MeetingPeople" }
            .Select(key => new MemoryObject(MemoryObjectKind.Activity, key));
        Assert.Equal(.05, pam.Sides(activities).Warm, 12);
        Assert.DoesNotContain(new PersonalMemory("Penny", pack).Snapshot().Associations,
            entry => entry.CauseId == "ppga-penny-father-left");
        Assert.All(pack.Accounts.Where(a => a.FactId == "ppgf-pam-bus-work-ended"),
            account => Assert.DoesNotContain(pack.Readings, r => r.AccountId == account.Id && r.Stance != ReadingStance.None));
    }

    [Fact]
    public void FatherMemoryHasASourcedAutumnCueButNoInventedLinkToPam()
    {
        HistoryPack pack = Pack();
        HistoryAccount father = pack.Accounts.Single(a => a.Id == "ppga-penny-father-left");
        Assert.Equal(new HistoryCue("season:fall", "ppg-source-penny-fall-fri6"), Assert.Single(father.Cues));
        var penny = new PersonalMemory("Penny", pack);
        Assert.Contains(penny.Recall(new[] { "season:fall" }, 0).Items, item => item.Id == father.Id);
        Assert.DoesNotContain(penny.Recall(new[] { "person:Pam" }, 0).Items,
            item => item.Id == father.Id || item.Id == "ppg-penny-father-autumn");
        Assert.DoesNotContain(new PersonalMemory("Pam", pack).Capture().HistoryIds, id => id == father.Id);
    }

    [Fact]
    public void WhereaboutsStartAsPersonalExpectationsNotInventedAnswersOrAgreements()
    {
        HistoryPack pack = Pack();
        var penny = new PersonalMemory("Penny", pack);
        var gus = new PersonalMemory("Gus", pack);
        Assert.Equal(2, penny.Snapshot().Expectations.Count);
        Assert.Equal(4, gus.Snapshot().Expectations.Count);
        foreach (int day in new[] { 0, 5 })
        {
            int evening = day * Clock.MinutesPerDay + 18 * 60;
            ExpectationSnapshot atHome = penny.Expectation(CompanyPolicy.WhereaboutsKey("Pam", "Home:Trailer", evening));
            ExpectationSnapshot atSaloon = gus.Expectation(CompanyPolicy.WhereaboutsKey("Pam", "Saloon", evening));
            Assert.Equal(.5, atHome.Chance);
            Assert.False(Assert.Single(atHome.Entries).Success);
            Assert.Equal(1, atSaloon.Chance);
            Assert.True(Assert.Single(atSaloon.Entries).Success);
        }
        Assert.Empty(new PersonalMemory("Pam", pack).Snapshot().Expectations);
        Assert.All(penny.Snapshot().Expectations.Concat(gus.Snapshot().Expectations),
            entry => Assert.Equal(ExpectationKind.Whereabouts, entry.Key.Kind));
        Assert.Equal(.05, gus.Sides(new[] { Pam }).Cold, 12);
        Assert.Equal(0, gus.Sides(new[] { Pam }).Warm);
    }

    [Fact]
    public void FirstLivedOutcomeCanOutweighTheSmallSeedAndUpdatesTheSpecificExpectation()
    {
        var penny = new PersonalMemory("Penny", Pack());
        ExpectationKey whereabouts = CompanyPolicy.WhereaboutsKey("Pam", "Home:Trailer", 18 * 60);
        penny.Learn(new MemoryExperience(0, 18 * 60, "test-observed-home", KnowledgeKind.Witnessed,
            new[] { new AssociationInput(Pam, .08, new[] { "person:Pam" }) },
            new[] { new ExpectationInput(whereabouts, true, new[] { "person:Pam" }) }));
        Assert.Equal(2.0 / 3, penny.Expectation(whereabouts).Chance, 12);
        MemorySides sides = penny.Sides(new[] { Pam });
        Assert.Equal(.105, sides.Warm, 12);
        Assert.Equal(.025, sides.Cold, 12);
        Assert.Equal(.08, sides.Causes.Single(c => c.CauseId == "test-observed-home").Warm);
        Assert.Empty(penny.Expectation(new(ExpectationKind.Answers, "Pam", "evening:PlayedGame")).Entries);
        Assert.Empty(penny.Expectation(new(ExpectationKind.FollowThrough, "Pam", "PlayedGame:Home:Trailer:evening")).Entries);
    }

    [Fact]
    public void SamePennyChoiceChangesWithBurdenRemovedWithoutChangingCareOrTheScene()
    {
        var penny = new PersonalMemory("Penny", Pack());
        var scene = new ChoiceOpportunity("pilot-history-probe", "Penny", 10, "encounter",
            new[] { "person:Pam" }, new[]
            {
                new ChoiceOption("offer", "Ask Pam to spend the evening together.", .1, new[] { Pam },
                    new[] { CompanyPolicy.WhereaboutsKey("Pam", "Home:Trailer", 18 * 60) }),
                new ChoiceOption("own-evening", "Keep my evening for reading.", 1,
                    new[] { new MemoryObject(MemoryObjectKind.Activity, "Reading") }, Array.Empty<ExpectationKey>()),
            }, .2);
        ChoiceComparison comparison = LivesChoices.CompareAtUnit(penny.Capture(), scene,
            new[] { "ppg-penny-burden" }, .2);
        Assert.True(comparison.Comparable);
        Assert.True(comparison.ProbabilitiesChanged);
        ChoiceAlternative before = comparison.WithHistory.Alternatives.Single(a => a.Option.Id == "offer");
        ChoiceAlternative after = comparison.WithoutHistory.Alternatives.Single(a => a.Option.Id == "offer");
        Assert.Equal(before.Sides.Warm, after.Sides.Warm);
        Assert.Equal(.05, before.Sides.Cold, 12);
        Assert.Equal(0, after.Sides.Cold);
        Assert.True(after.Probability > before.Probability);
        Assert.Equal(comparison.WithHistory.Unit, comparison.WithoutHistory.Unit);
        Assert.Equal(.05, penny.Sides(new[] { Pam }).Cold, 12);
    }
}
