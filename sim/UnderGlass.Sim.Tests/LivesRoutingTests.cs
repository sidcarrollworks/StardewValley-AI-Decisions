using System.Text.Json;
using UnderGlass.Sim;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Small synthetic towns exercise real movement and perception. No Stardew history is approved here.</summary>
public sealed class LivesRoutingTests
{
    private static Location Room(string name) => new(name, false,
        Enumerable.Repeat(new string('.', 12), 8).ToArray());

    private static Villager Person(string name, IReadOnlyList<Haunt>? haunts = null) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.5), new Body(100, -1), null,
        haunts ?? new[] { Haunt("Yard"), Haunt("Hall") },
        new Dictionary<string, double>(), Array.Empty<string>());

    private static Haunt Haunt(string place, int from = 0, int to = Clock.MinutesPerDay, int x = 4)
        => new(place, new Tile(x, 3), from, to, 1);

    private static TownData Town(params string[] names) => new()
    {
        Cast = (names.Length == 0 ? new[] { "Ann" } : names).Select(n => Person(n)).ToArray(),
        Places = new[] { Room("Yard"), Room("Hall") }, Links = Array.Empty<Link>(),
        Gatherings = Array.Empty<Gathering>(), Acts = Array.Empty<ActKind>(),
        Feelings = new FeelingOptions(), Authority = new AuthorityOptions { ElectConstable = false },
        Body = new BodyOptions { AwakeHoursAtRest = 100_000 },
        Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
    };

    private static HistoryPack History(string holder = "Ann", string place = "Hall") => new(1,
        new[] { new HistorySource("source", "synthetic:present-feeling", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact", HistoryKind.Practice, new[] { "source" }) },
        new[] { new HistoryAccount("account", "fact", holder, "I spend time there.", KnowledgeKind.Lived,
            new[] { "source" }, new[] { new HistoryCue("place:" + place, "source") }) },
        new[] { new HistoryReading("reading", "account", new MemoryObject(MemoryObjectKind.Place, place),
            ReadingStance.Warm, "I enjoy being there.", ReadingOrigin.CanonStated, AdmissionRule.R2,
            new HistoryReview(ReviewStatus.Reviewed, "Synthetic test reviewer", "2026-10-15"),
            new[] { "source" }, new[] { new HistoryCue("place:" + place, "source") }) });

    private static LivesRoutingOptions On(bool record = true, params string[] compare) => new()
        { Enabled = true, Record = record, CompareHistory = compare };

    private static (Simulation Sim, SimResult Result, string[] Places) Run(TownData town,
        LivesRoutingOptions? options = null, HistoryPack? history = null, int days = 1, long seed = 7)
    {
        var sim = new Simulation(seed, town);
        if (options is not null) sim.ConfigureLivesRouting(options, history);
        var places = new List<string>();
        var result = sim.Run(days, (_, s) => places.Add(s.Where(town.Cast[0].Name).Place));
        return (sim, result, places.ToArray());
    }

    [Fact]
    public void DisabledAndEmptyHistoryRetainTheExistingDrawAndActualMovement()
    {
        TownData town = Town();
        var baseline = Run(town, days: 2);
        var off = Run(town, new LivesRoutingOptions(), days: 2);
        var empty = Run(town, On(), days: 2);
        Assert.Equal(Metrics.LogHash(baseline.Result), Metrics.LogHash(off.Result));
        Assert.Equal(Metrics.LogHash(baseline.Result), Metrics.LogHash(empty.Result));
        Assert.Equal(baseline.Places, off.Places);
        Assert.Equal(baseline.Places, empty.Places);
        Assert.Empty(off.Result.LivesRouting);
        Assert.Null(off.Sim.LivesMemoryOf("Ann"));
        Assert.NotEmpty(empty.Result.LivesRouting);
        Assert.All(empty.Result.LivesRouting, row => Assert.True(row.UsedLegacy));
    }

    [Fact]
    public void RelevantHistoryChangesProbabilitiesAndChosenDestinationsAreActuallyReached()
    {
        var run = Run(Town(), On(), History());
        Assert.NotEmpty(run.Result.LivesRouting);
        Assert.All(run.Result.LivesRouting, row =>
        {
            Assert.False(row.UsedLegacy);
            Assert.Equal(row.Decision.SelectedId, row.SelectedOption);
            var hall = Assert.Single(row.Decision.Alternatives.Where(a =>
                a.Option.Objects.Contains(new MemoryObject(MemoryObjectKind.Place, "Hall"))));
            var yard = Assert.Single(row.Decision.Alternatives.Where(a =>
                a.Option.Objects.Contains(new MemoryObject(MemoryObjectKind.Place, "Yard"))));
            Assert.True(hall.Probability > yard.Probability);
            Assert.Contains(hall.Reasons, r => r.Code == "warm" && r.Value > 0);
            // This toy world has no roads, duties or interactions; a drawn destination is reached immediately.
            Assert.Equal(row.SelectedPlace, run.Places[row.Decision.Opportunity.At]);
        });
    }

    [Fact]
    public void SameChoiceComparisonHoldsSceneAndRandomUnitFixedWithoutRemovingLiveHistory()
    {
        var run = Run(Town(), On(compare: new[] { "reading" }), History());
        var plain = Run(Town(), On(), History());
        Assert.Equal(Metrics.LogHash(plain.Result), Metrics.LogHash(run.Result));
        Assert.Equal(plain.Places, run.Places);
        Assert.All(run.Result.LivesRouting, row =>
        {
            var pair = Assert.IsType<ChoiceComparison>(row.Comparison);
            Assert.True(pair.Comparable);
            Assert.True(pair.ProbabilitiesChanged);
            Assert.Equal(pair.WithHistory.Unit, pair.WithoutHistory.Unit);
            Assert.Equal(JsonSerializer.Serialize(pair.WithHistory.Opportunity), JsonSerializer.Serialize(pair.WithoutHistory.Opportunity));
            Assert.Contains(pair.WithHistory.Recall.Items, item => item.Id == "reading");
            Assert.DoesNotContain(pair.WithoutHistory.Recall.Items, item => item.Id == "reading");
            Assert.Contains(pair.WithoutHistory.Recall.Items, item => item.Id == "account");
        });
        Assert.Contains("reading", run.Sim.LivesMemoryOf("Ann")!.HistoryIds);
        Assert.Contains(run.Sim.LivesMemoryOf("Ann")!.Snapshot().Associations, a => a.Seed && a.Amount > 0);
    }

    [Fact]
    public void AnotherPersonsPrivateHistoryDoesNotEnterTheChoice()
    {
        var run = Run(Town("Ann", "Bob"), On(), History());
        var ann = run.Result.LivesRouting.Where(r => r.Decision.Opportunity.Holder == "Ann").ToArray();
        var bob = run.Result.LivesRouting.Where(r => r.Decision.Opportunity.Holder == "Bob").ToArray();
        Assert.NotEmpty(ann); Assert.NotEmpty(bob);
        Assert.All(ann, row => Assert.Contains(row.Decision.Recall.Items, item => item.Id == "reading"));
        Assert.All(bob, row =>
        {
            Assert.True(row.UsedLegacy);
            Assert.Empty(row.Decision.Recall.Items);
        });
        Assert.Empty(run.Sim.LivesMemoryOf("Bob")!.HistoryIds);
    }

    [Fact]
    public void AnUnrelatedHistoryDoesNotReplaceTheExistingDraw()
    {
        var baseline = Run(Town());
        var run = Run(Town(), On(), History(place: "Elsewhere"));
        Assert.Equal(Metrics.LogHash(baseline.Result), Metrics.LogHash(run.Result));
        Assert.Equal(baseline.Places, run.Places);
        Assert.All(run.Result.LivesRouting, row => Assert.True(row.UsedLegacy));
    }

    [Fact]
    public void TheRuleDoesNotDependOnACharacterName()
    {
        var ann = Run(Town("Ann"), On(), History("Ann"));
        var zoe = Run(Town("Zoe"), On(), History("Zoe"));
        var a = ann.Result.LivesRouting.First().Decision;
        var z = zoe.Result.LivesRouting.First().Decision;
        Assert.Equal(a.Alternatives.Select(x => x.Probability), z.Alternatives.Select(x => x.Probability));
        Assert.Equal(a.Alternatives.Select(x => x.Sides.Warm), z.Alternatives.Select(x => x.Sides.Warm));
        Assert.All(z.Recall.Items, item => Assert.Equal("Zoe", item.Holder));
    }

    [Fact]
    public void RecordingAndComparisonAreObserversAndRepeatedRunsAreDeterministic()
    {
        var recorded = Run(Town(), On(compare: new[] { "reading" }), History(), days: 2);
        var again = Run(Town(), On(compare: new[] { "reading" }), History(), days: 2);
        var quiet = Run(Town(), On(record: false), History(), days: 2);
        Assert.Equal(recorded.Places, quiet.Places);
        Assert.Equal(Metrics.LogHash(recorded.Result), Metrics.LogHash(quiet.Result));
        Assert.Equal(JsonSerializer.Serialize(recorded.Result.LivesRouting), JsonSerializer.Serialize(again.Result.LivesRouting));
        Assert.Equal(JsonSerializer.Serialize(recorded.Sim.LivesMemoryOf("Ann")!.Snapshot()),
            JsonSerializer.Serialize(quiet.Sim.LivesMemoryOf("Ann")!.Snapshot()));
        Assert.Empty(quiet.Result.LivesRouting);
        Assert.Empty(quiet.Result.LivesMemoryChanges);
    }

    [Fact]
    public void ConfigurationCannotBeChangedAfterTheRunStarts()
    {
        var sim = new Simulation(1, Town());
        sim.Run(1, (m, current) =>
        {
            if (m == 0) Assert.Throws<InvalidOperationException>(() => current.ConfigureLivesRouting(On(), History()));
        });
        Assert.Throws<InvalidOperationException>(() => sim.ConfigureLivesRouting(On(), History()));
    }

    [Fact]
    public void DisabledConfigurationCannotSilentlyAcceptHistoryOrComparisons()
    {
        Assert.Throws<ArgumentException>(() => new Simulation(1, Town()).ConfigureLivesRouting(new(), History()));
        Assert.Throws<ArgumentException>(() => new Simulation(1, Town()).ConfigureLivesRouting(new()
            { CompareHistory = new[] { "reading" } }));
    }

    [Theory]
    [InlineData("fact")]
    [InlineData("account")]
    [InlineData("reading")]
    public void LiveCauseNamespaceIsReservedWithoutPartiallyConfiguringTheSimulation(string kind)
    {
        HistoryPack pack = History();
        pack = kind switch
        {
            "fact" => pack with
            {
                Facts = new[] { pack.Facts[0] with { Id = "act:0" } },
                Accounts = new[] { pack.Accounts[0] with { FactId = "act:0" } },
            },
            "account" => pack with
            {
                Accounts = new[] { pack.Accounts[0] with { Id = "act:0" } },
                Readings = new[] { pack.Readings[0] with { AccountId = "act:0" } },
            },
            _ => pack with { Readings = new[] { pack.Readings[0] with { Id = "act:0" } } },
        };
        var sim = new Simulation(7, Town());
        Assert.Throws<ArgumentException>(() => sim.ConfigureLivesRouting(On(), pack));
        Assert.Null(sim.LivesMemoryOf("Ann"));
        sim.ConfigureLivesRouting(On(), History());
        var result = sim.Run(1);
        Assert.NotEmpty(result.LivesRouting);
        Assert.All(result.LivesRouting, row => Assert.False(row.UsedLegacy));
    }

    [Fact]
    public void ReplayAddsAnExplicitExperimentalBlockOnlyWhenEnabled()
    {
        var options = new ReplayOptions { Town = Town(), Seed = 7, Days = 1 };
        using var off = JsonDocument.Parse(Replay.Json(options));
        using var on = JsonDocument.Parse(Replay.Json(options with { LivesRouting = On(), History = History() }));
        Assert.False(off.RootElement.TryGetProperty("livesRouting", out _));
        var block = on.RootElement.GetProperty("livesRouting");
        Assert.Equal(1, block.GetProperty("version").GetInt32());
        Assert.False(on.RootElement.TryGetProperty("lives", out _));
        Assert.Contains("Hall", block.GetRawText());
        Assert.Contains("reading", block.GetRawText());
        Assert.Equal(Replay.Json(options with { LivesRouting = On(), History = History() }), on.RootElement.GetRawText());
    }

    [Fact]
    public void ZeroWeightOptionsRemainExcludedWhenNightRecordingIsEnabled()
    {
        var town = Town();
        town = town with { Cast = new[] { town.Cast[0] with
            { Haunts = new[] { Haunt("Yard") with { Weight = 0 }, Haunt("Hall") } } } };
        var sim = new Simulation(7, town);
        sim.ConfigureLivesRouting(On(), History());
        var nights = new NightList();
        sim.ConfigureNights(nights);
        var result = sim.Run(1);
        Assert.NotEmpty(result.LivesRouting);
        Assert.All(result.LivesRouting, row => Assert.Equal("Hall", row.SelectedPlace));
        Assert.Contains(result.LivesRouting.SelectMany(r => r.Decision.Excluded), e => e.Code == "zero-base");
        Assert.Contains(nights.Notes, n => n.Options is { Count: > 0 });
    }

    [Fact]
    public void LargeRoutineMenusStayUsableWithoutSilentlyDroppingOptions()
    {
        var town = Town();
        town = town with { Cast = new[] { town.Cast[0] with
            { Haunts = Enumerable.Range(0, 6).Select(i => Haunt(i % 2 == 0 ? "Yard" : "Hall", x: i + 1)).ToArray() } } };
        var baseline = Run(town);
        var lives = Run(town, On(), History());
        Assert.Equal(baseline.Places, lives.Places);
        Assert.Equal(Metrics.LogHash(baseline.Result), Metrics.LogHash(lives.Result));
        Assert.Empty(lives.Result.LivesRouting);
        Assert.NotEmpty(lives.Result.LivesRoutingSkipped);
        Assert.All(lives.Result.LivesRoutingSkipped, row => Assert.Equal(6, row.OptionCount));
    }

    [Fact]
    public void WitnessedFeelingsBecomeLearningButHearsayDoesNotRevealTheTruePlace()
    {
        var town = new TownData
        {
            Cast = new[] { Person("Ann", new[] { Haunt("Away", x: 3) }),
                Person("Bob", new[] { Haunt("Room", x: 3) }),
                Person("Cara", new[] { Haunt("Room", to: 660, x: 5), Haunt("Away", from: 660, x: 5) }) },
            Places = new[] { Room("Room"), Room("Away") }, Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { new ActKind("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(),
                Affect: new Affect(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper)) },
            Feelings = new FeelingOptions(),
            Authority = new AuthorityOptions { ElectConstable = false, Keepers = new Dictionary<string, string> { ["Room"] = "Ann" } },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 10 }, Wander = 0,
        };
        var sim = new Simulation(5, town, new[] { (600, "Bob", "Stole") });
        sim.ConfigureLivesRouting(On());
        var result = sim.Run(1);
        var act = Assert.Single(result.Acts.Where(a => a.Kind == "Stole"));
        Assert.Equal(Source.Told, result.Beliefs["Ann"][act.Id].Source);
        Assert.Equal(Source.Witnessed, result.Beliefs["Cara"][act.Id].Source);
        var heard = sim.LivesMemoryOf("Ann")!.Snapshot().Associations.Where(a => !a.Seed).ToArray();
        Assert.NotEmpty(heard);
        Assert.All(heard, a => { Assert.Equal(KnowledgeKind.Told, a.How); Assert.Equal("Cara", a.Teller); Assert.InRange(a.Credence, 0, 1); });
        Assert.DoesNotContain(heard, a => a.Object.Kind == MemoryObjectKind.Place);
        Assert.Contains(heard, a => a.Object == new MemoryObject(MemoryObjectKind.Person, "Bob") && a.Amount < 0);
        Assert.Contains(sim.LivesMemoryOf("Cara")!.Snapshot().Associations,
            a => a.Object == new MemoryObject(MemoryObjectKind.Place, "Room") && a.How == KnowledgeKind.Witnessed);
    }

    [Theory]
    [InlineData(false, 0.975)]
    [InlineData(true, 0.975)]
    [InlineData(true, 0)]
    public void ACorrectedAttributionReplacesThePersonWithoutAddingAnExperienceOrRevivingFadedAffect(
        bool afterNight, double retention)
    {
        var ann = Person("Ann", new[] { Haunt("Room", x: 2) }) with
            { Temperament = new Temperament(1, 0.9, 0.5, 0.2) };
        var town = new TownData
        {
            Cast = new[] { ann, Person("Bob", new[] { Haunt("Room", x: 9) }),
                Person("Cara", new[] { Haunt("Room", x: 20) }), Person("Dora", new[] { Haunt("Room", x: 10) }) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 30), 8).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { new ActKind("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(),
                Affect: new Affect(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper)) },
            Feelings = new FeelingOptions { GuessPerHate = 1_000_000,
                Start = new Dictionary<(string, string), double> { [("Ann", "Cara")] = -1 } },
            Familiarity = new[] { ("Ann", "Bob", 0.0), ("Ann", "Dora", 1.0) },
            Authority = new AuthorityOptions { ElectConstable = false, Keepers = new Dictionary<string, string> { ["Room"] = "Ann" } },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 },
            Gossip = new GossipOptions { ChatChance = afterNight ? 0 : 10, FamiliarityGrowthPerHour = 0 }, Wander = 0,
        };
        var sim = new Simulation(5, town, new[] { (600, "Bob", "Stole") });
        sim.ConfigureLivesRouting(On() with { Memory = new MemoryOptions(NightlyRetention: retention) }, History(place: "Room"));
        var result = sim.Run(afterNight ? 2 : 1, (at, _) =>
        {
            // Delay the independent eyewitness's correction until after the original feeling fades.
            if (afterNight && at == Clock.MinutesPerDay) town.Gossip.ChatChance = 10;
        });
        var act = Assert.Single(result.Acts.Where(a => a.Kind == "Stole"));
        Assert.Equal("Bob", result.Beliefs["Ann"][act.Id].Actor);
        string cause = "act:" + act.Id;
        Assert.Contains(result.LivesMemoryChanges.Where(c => c.Holder == "Ann" && c.CauseId == cause)
            .SelectMany(c => c.AddedAssociations), a => a.Object == new MemoryObject(MemoryObjectKind.Person, "Cara"));
        var final = sim.LivesMemoryOf("Ann")!.Snapshot().Associations.Where(a => a.CauseId == cause).ToArray();
        Assert.DoesNotContain(final, a => a.Object == new MemoryObject(MemoryObjectKind.Person, "Cara"));
        Assert.Equal(final.Length, final.Select(a => a.Object).Distinct().Count());
        var original = result.LivesMemoryChanges.First(c => c.Holder == "Ann" && c.CauseId == cause)
            .AddedAssociations.Single(a => a.Object.Kind == MemoryObjectKind.Place).Amount;
        if (retention == 0)
            Assert.Empty(final);
        else
        {
            Assert.Contains(final, a => a.Object == new MemoryObject(MemoryObjectKind.Person, "Bob"));
            var place = Assert.Single(final.Where(a => a.Object == new MemoryObject(MemoryObjectKind.Place, "Room")));
            Assert.Equal(original * (afterNight ? retention : 1), place.Amount, 12);
        }
        // Even a fully faded cause remains one acquired experience for seed dilution after correction.
        var seed = Assert.Single(sim.LivesMemoryOf("Ann")!.Snapshot().Associations.Where(a => a.Seed));
        Assert.Equal(new MemoryOptions().SeedStrength / 2, seed.Amount, 12);
        if (afterNight)
        {
            Assert.Contains(result.LivesMemoryChanges, c => c.Holder == "Ann" && c.CauseId == cause
                && c.Reason == "correct" && c.At >= Clock.MinutesPerDay);
            Assert.DoesNotContain(result.Feelings, f => f.Holder == "Ann" && f.ActId == act.Id
                && f.Tick >= Clock.MinutesPerDay && f.Mood != 0);
        }
    }

    [Fact]
    public void ARefusalCanStillHurtWithoutCreatingAnExtraAssociationAndChatCreatesNone()
    {
        var town = Town("Ann", "Bob") with
        {
            Cast = new[] { Person("Ann", new[] { Haunt("Room", x: 3) }), Person("Bob", new[] { Haunt("Room", x: 5) }) },
            Places = new[] { Room("Room") }, Gossip = new GossipOptions { ChatChance = 10 },
            Acts = new[] { new ActKind("Refused", 1, -1, 1, 1, 0, Array.Empty<string>(),
                Affect: new Affect(Patient.Target, -0.2, 0.3, 1, TargetIs.Chosen)) },
        };
        var sim = new Simulation(7, town);
        sim.ConfigureLivesRouting(On());
        sim.Place(new[] { new Scenario("refusal", "Refused", "Bob", "Ann", Day: 0, From: 600, To: 601) });
        var result = sim.Run(1);
        var act = Assert.Single(result.Acts);
        Assert.Equal("Refused", act.Kind);
        Assert.Contains(result.Feelings, f => f.Holder == "Ann" && f.ActId == act.Id && f.Mood < 0);
        Assert.Empty(sim.LivesMemoryOf("Ann")!.Snapshot().Associations);
        Assert.Empty(sim.LivesMemoryOf("Bob")!.Snapshot().Associations);
    }

    [Fact]
    public void AnUndergoneMishapIsLivedWithoutBlameAndDecaysOnceAtTheNextDayBoundary()
    {
        var town = Town() with
        {
            Cast = new[] { Person("Ann", new[] { Haunt("Room") }) }, Places = new[] { Room("Room") },
            Acts = new[] { new ActKind("Stumbled", 1, -1, 1, 1, 0, Array.Empty<string>(),
                Affect: new Affect(Patient.Actor, -0.3, 0.3, 0, TargetIs.None)) },
        };
        var sim = new Simulation(7, town, new[] { (600, "Ann", "Stumbled") });
        sim.ConfigureLivesRouting(On());
        var result = sim.Run(2);
        var added = result.LivesMemoryChanges.Where(c => c.Holder == "Ann" && c.CauseId is not null)
            .SelectMany(c => c.AddedAssociations).ToArray();
        var first = Assert.Single(added.Where(a => a.Object == new MemoryObject(MemoryObjectKind.Place, "Room")));
        var final = sim.LivesMemoryOf("Ann")!.Snapshot().Associations;
        Assert.NotEmpty(final);
        Assert.All(final, a => Assert.Equal(KnowledgeKind.Lived, a.How));
        Assert.DoesNotContain(final, a => a.Object.Kind == MemoryObjectKind.Person);
        var place = Assert.Single(final.Where(a => a.Object == first.Object));
        Assert.Equal(first.Amount * new MemoryOptions().NightlyRetention, place.Amount, 12);
    }
}
