using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class CharacterInspirationTests
{
    private static InspirationArchetype Archetype(string id = "guarded-helper") => new(id,
        "Want useful work and independence.", "Offer practical help, but withdraw when pressed.",
        "Plain, reserved; occasional dry humor.", "Practical and guarded; values independence.", new[] { "review:archetype/guarded-helper" });
    private static CharacterBackground Background(string id, string text, params string[] subjects)
        => new(id, text, subjects, new[] { "fixture:reviewed/" + id });
    private static CharacterInspirationPack Pack() => new(1, "Synthetic test material; not Stardew canon.",
        new[] { Archetype() }, new[]
        {
            new CharacterInspirationProfile("Ann", "guarded-helper", "Avoid grand speeches.", new[]
            {
                Background("general", "I used to mend household tools."),
                Background("with-bob", "Bob and I learned to mend tools together.", "Bob"),
                Background("with-cara", "Cara and I once kept a garden.", "Cara"),
            }),
            new CharacterInspirationProfile("Bob", "guarded-helper", "", new[]
                { Background("bob-private", "I stopped playing the violin years ago.") }),
        });

    private static Villager Person(string name, int x) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
        new Dictionary<string, double>(), Array.Empty<string>());

    private static Simulation Scene(CharacterInspirationPack? pack = null, bool encounter = true,
        string other = "Bob", bool mistaken = false)
    {
        var feelings = new FeelingOptions { Desire = true, AnswerOn = false, ReturnOn = false,
            MakeUpOn = false, RetaliateOn = false, AvoidOn = false, WithdrawOn = false };
        if (mistaken)
        {
            feelings.Start = new Dictionary<(string, string), double> { [("Ann", "Cara")] = -1 };
            feelings.GuessPerHate = 100_000;
        }
        var ann = Person("Ann", 3);
        if (mistaken) ann = ann with { Temperament = new Temperament(1, 0.9, 0.5, 0.2) };
        var town = new TownData
        {
            Cast = new[] { ann, Person(other, 5), Person("Cara", 30) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { new ActKind("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
                Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen)) },
            Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
            Perception = mistaken ? new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 } : new PerceptionOptions(),
            Familiarity = mistaken ? new[] { ("Ann", other, 0.0), ("Ann", "Cara", 1.0) } : Array.Empty<(string, string, double)>(),
        };
        var sim = new Simulation(1, town);
        if (pack is not null) sim.ConfigureCharacterInspiration(pack);
        if (encounter) sim.Place(new[] { new Scenario("known", "GaveGift", other, "Ann", Day: 0, From: 600, To: 601) });
        sim.ConfigureReflection(new ReflectionOptions { Enabled = true, QuietMinutes = 30, DailyChance = 1 });
        return sim;
    }

    private sealed class Reject : IReflectionMind
    {
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken token = default)
        {
            var answer = await new AuthoredReflectionMind().ReflectAsync(request, token);
            return answer with { Weights = request.Choices.ToDictionary(c => c.Id, c => c.Id == "reject" ? 1.0 : 0.0) };
        }
    }

    [Fact]
    public async Task EachRequestContainsOnlyItsOwnersRelevantBackgroundAndNoInventedEncounter()
    {
        SimResult run = await Scene(Pack()).RunAsync(1, new Reject());
        CharacterInspiration ann = Assert.Single(run.Reflections, r => r.Request.Actor == "Ann").Request.Character!;
        CharacterInspiration bob = Assert.Single(run.Reflections, r => r.Request.Actor == "Bob").Request.Character!;
        Assert.Equal("with-bob", ann.Background!.Id);
        Assert.Equal("bob-private", bob.Background!.Id);
        Assert.DoesNotContain("violin", JsonSerializer.Serialize(ann));
        Assert.DoesNotContain("garden", JsonSerializer.Serialize(ann));
        Assert.DoesNotContain("mend", JsonSerializer.Serialize(bob));
        Assert.Equal(Pack().ContentHash, ann.PackHash);
        Assert.All(run.Reflections, r => Assert.Equal(0, r.Request.SourceActId));
        Assert.Single(run.Acts);
        Assert.DoesNotContain(run.Beliefs.SelectMany(p => p.Value.Values), b => b.ActId != 0);
    }

    [Fact]
    public async Task MissingProfileDoesNotInheritAnotherCharactersHistoryAndGeneralFallbackIsOwnerOnly()
    {
        SimResult run = await Scene(Pack(), other: "Newcomer").RunAsync(1, new Reject());
        Assert.Null(Assert.Single(run.Reflections, r => r.Request.Actor == "Newcomer").Request.Character);
        Assert.Equal("general", Assert.Single(run.Reflections, r => r.Request.Actor == "Ann").Request.Character!.Background!.Id);
        CharacterInspiration generic = Pack().ForArchetype("guarded-helper");
        Assert.Null(generic.Background);
        Assert.DoesNotContain("Avoid grand speeches", generic.Voice);
        Assert.Equal(Archetype().EvaluatorCue, generic.EvaluatorCue);
    }

    [Fact]
    public async Task RelevanceUsesTheBelievedSubjectRatherThanCorrectingAMistakenIdentity()
    {
        SimResult run = await Scene(Pack(), mistaken: true).RunAsync(1, new Reject());
        ReflectionRequest request = Assert.Single(run.Reflections, r => r.Request.Actor == "Ann").Request;
        Assert.Equal("Bob", run.Acts[0].Actor);
        Assert.Equal("Cara", request.Subject);
        Assert.Equal("with-cara", request.Character!.Background!.Id);
        Assert.DoesNotContain("with-bob", JsonSerializer.Serialize(request.Character));
    }

    [Fact]
    public async Task ConfiguredBackgroundAloneCreatesNoActsBeliefsFeelingsOrReflectionSources()
    {
        SimResult baseline = await Scene(encounter: false).RunAsync(1, new Reject());
        SimResult configured = await Scene(Pack(), encounter: false).RunAsync(1, new Reject());
        Assert.Empty(configured.Reflections);
        Assert.Empty(configured.Acts);
        Assert.Equal(Metrics.LogHash(baseline), Metrics.LogHash(configured));
        Assert.Equal(JsonSerializer.Serialize(baseline.Beliefs), JsonSerializer.Serialize(configured.Beliefs));
        Assert.Equal(JsonSerializer.Serialize(baseline.Feelings), JsonSerializer.Serialize(configured.Feelings));
    }

    [Fact]
    public async Task FixedAnswersKeepWorldStateAndAuthoredProposalsIdenticalWithProfilesEnabled()
    {
        SimResult baseline = await Scene().RunAsync(1, new Reject());
        SimResult configured = await Scene(Pack()).RunAsync(1, new Reject());
        Assert.Equal(Metrics.LogHash(baseline), Metrics.LogHash(configured));
        Assert.Equal(JsonSerializer.Serialize(baseline.Beliefs), JsonSerializer.Serialize(configured.Beliefs));
        Assert.Equal(JsonSerializer.Serialize(baseline.Feelings), JsonSerializer.Serialize(configured.Feelings));
        Assert.Equal(JsonSerializer.Serialize(baseline.Reflections), JsonSerializer.Serialize(configured.Reflections
            .Select(r => r with { Request = r.Request with { Character = null } })));
        Assert.DoesNotContain("Character", JsonSerializer.Serialize(baseline.Reflections));
    }

    [Fact]
    public void PackConfiguredWhileReflectionIsOffPreservesTheExistingSeedHash()
    {
        var sim = new Simulation(42, feelings: FeelingOptions.Off);
        sim.ConfigureCharacterInspiration(Pack());
        Assert.Equal("d1a5fd6ac8e82bcd", Metrics.LogHash(sim.Run(7)));
    }

    [Fact]
    public async Task ConfigurationCopiesMutableInputAndCannotBeReplacedMidRun()
    {
        var sources = new List<string> { "fixture:original" };
        var anchors = new List<CharacterBackground> { Background("only", "My original reviewed memory.") with { Sources = sources } };
        var profiles = new List<CharacterInspirationProfile> { new("Ann", "guarded-helper", "", anchors) };
        var pack = Pack() with { Characters = profiles };
        string hash = pack.ContentHash;
        var sim = Scene(pack);
        sources[0] = "fixture:mutated";
        anchors.Clear();
        profiles.Clear();
        SimResult run = await sim.RunAsync(1, new Reject());
        CharacterInspiration snapshot = Assert.Single(run.Reflections, r => r.Request.Actor == "Ann").Request.Character!;
        Assert.Equal(hash, snapshot.PackHash);
        Assert.Equal("fixture:original", Assert.Single(snapshot.Background!.Sources));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.Background.Sources)[0] = "mutate");
        Assert.Throws<InvalidOperationException>(() => sim.ConfigureCharacterInspiration(Pack()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedCharacterMatchesTheSameObservedSceneRequest(bool mistaken)
    {
        var pack = Pack();
        var ann = pack.Characters[0];
        pack = pack with { Characters = new[] { ann with { Background = ann.Background.Concat(new[]
        {
            Background("another-bob", "Bob and I used to trade spare parts.", "Bob"),
            Background("another-cara", "Cara and I shared a workbench.", "Cara"),
        }).ToArray() }, pack.Characters[1] } };
        SimResult run = await Scene(pack, mistaken: mistaken).RunAsync(1, new Reject());
        Assert.NotEmpty(run.Reflections);
        foreach (ReflectionRecord record in run.Reflections)
        {
            ReflectionRequest request = record.Request;
            CharacterInspiration? prepared = pack.ForCharacter(request.Actor, request.Subject, 1, request.Id);
            Assert.Equal(JsonSerializer.Serialize(request.Character), JsonSerializer.Serialize(prepared));
            Assert.Equal(pack.ContentHash, prepared!.PackHash);
        }
        Assert.Null(pack.ForCharacter("Newcomer", "Ann", 1, "prepared-newcomer"));
    }

    [Fact]
    public void PreparedCharacterFreezesNestedMutableInputAndRetainsItsWholePackHash()
    {
        var archetypeSources = new List<string> { "fixture:original-voice" };
        var personalSources = new List<string> { "fixture:personal-voice", "fixture:original-voice" };
        var historySources = new List<string> { "fixture:original-history" };
        var subjects = new List<string> { "Bob" };
        var anchors = new List<CharacterBackground> { new("only", "Bob and I once shared a workshop.", subjects, historySources) };
        var profiles = new List<CharacterInspirationProfile> { new("Ann", "guarded-helper", "", anchors, personalSources) };
        var archetypes = new List<InspirationArchetype> { Archetype() with { Sources = archetypeSources } };
        var pack = Pack() with { Characters = profiles, Archetypes = archetypes };
        string originalHash = pack.ContentHash;
        CharacterInspiration prepared = pack.ForCharacter("Ann", "Bob", 1, "prepared-scene")!;

        archetypeSources[0] = "fixture:edited-voice";
        personalSources.Clear();
        historySources[0] = "fixture:edited-history";
        subjects[0] = "Cara";
        anchors.Clear();
        profiles.Clear();
        archetypes.Clear();

        Assert.Equal(originalHash, prepared.PackHash);
        Assert.Equal(new[] { "fixture:original-voice", "fixture:personal-voice" }, prepared.Sources);
        Assert.Equal("fixture:original-history", Assert.Single(prepared.Background!.Sources));
        Assert.Equal("Bob", Assert.Single(prepared.Background.Subjects));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)prepared.Sources)[0] = "mutate");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)prepared.Background.Sources)[0] = "mutate");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)prepared.Background.Subjects)[0] = "mutate");
    }

    [Fact]
    public async Task CanonicalPackOrderAndDeserializedTapePreserveExactRequests()
    {
        var pack = Pack();
        var reversed = pack with { Characters = pack.Characters.Reverse().Select(p => p with { Background = p.Background.Reverse().ToArray() }).ToArray() };
        Assert.Equal(pack.ContentHash, reversed.ContentHash);
        var read = CharacterInspirationPack.Read(JsonSerializer.Serialize(reversed));
        SimResult original = await Scene(pack).RunAsync(1, new Reject());
        var tape = JsonSerializer.Deserialize<ReflectionRecord[]>(JsonSerializer.Serialize(original.Reflections))!;
        SimResult replay = await Scene(read).RunAsync(1, new RecordedReflectionMind(tape));
        Assert.Equal(Metrics.LogHash(original), Metrics.LogHash(replay));
        Assert.Equal(JsonSerializer.Serialize(original.Reflections), JsonSerializer.Serialize(replay.Reflections));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditingEvenAnUnselectedBackgroundOrItsSourceInvalidatesThePackTape(bool sourceOnly)
    {
        var pack = Pack();
        SimResult original = await Scene(pack).RunAsync(1, new Reject());
        var ann = pack.Characters[0];
        var general = ann.Background.Single(b => b.Id == "general");
        var edited = sourceOnly ? general with { Sources = new[] { "fixture:revised-review" } }
            : general with { Text = "I used to repair clocks." };
        var changed = pack with { Characters = new[] { ann with
            { Background = ann.Background.Select(b => b.Id == "general" ? edited : b).ToArray() }, pack.Characters[1] } };
        Assert.NotEqual(pack.ContentHash, changed.ContentHash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Scene(changed).RunAsync(1, new RecordedReflectionMind(original.Reflections)));
    }

    [Fact]
    public async Task NamedVoiceSourceChangesInvalidateTheTapeWithoutLeakingIntoGenericArchetypeSources()
    {
        var pack = Pack();
        var ann = pack.Characters[0];
        pack = pack with { Characters = new[] { ann with { Sources = new[] { "fixture:ann-voice" } }, pack.Characters[1] } };
        SimResult original = await Scene(pack).RunAsync(1, new Reject());
        CharacterInspiration named = Assert.Single(original.Reflections, r => r.Request.Actor == "Ann").Request.Character!;
        Assert.Contains("fixture:ann-voice", named.Sources);
        Assert.DoesNotContain("fixture:ann-voice", pack.ForArchetype(ann.ArchetypeId).Sources);
        Assert.DoesNotContain("fixture:ann-voice", Assert.Single(original.Reflections, r => r.Request.Actor == "Bob").Request.Character!.Sources);
        var changed = pack with { Characters = new[] { pack.Characters[0] with { Sources = new[] { "fixture:ann-voice-revised" } }, pack.Characters[1] } };
        Assert.NotEqual(pack.ContentHash, changed.ContentHash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Scene(changed).RunAsync(1, new RecordedReflectionMind(original.Reflections)));
    }

    [Fact]
    public async Task NoRelevantOrGeneralAnchorLeavesTheBackgroundEmptyInsteadOfBorrowingAnotherPerson()
    {
        var pack = Pack();
        var ann = pack.Characters[0];
        pack = pack with { Characters = new[] { ann with
            { Background = ann.Background.Where(b => b.Id == "with-cara").ToArray() }, pack.Characters[1] } };
        SimResult run = await Scene(pack).RunAsync(1, new Reject());
        Assert.Null(Assert.Single(run.Reflections, r => r.Request.Actor == "Ann").Request.Character!.Background);
    }

    [Theory]
    [InlineData("history")]
    [InlineData("history-source")]
    [InlineData("voice")]
    [InlineData("archetype-source")]
    [InlineData("subjects")]
    public async Task TapeRejectsChangedPersonalContextEvenIfItsPackHashIsCopied(string change)
    {
        SimResult run = await Scene(Pack()).RunAsync(1, new Reject());
        ReflectionRecord record = Assert.Single(run.Reflections, r => r.Request.Actor == "Ann");
        CharacterInspiration original = record.Request.Character!;
        var changed = change switch
        {
            "history" => original with { Background = original.Background! with { Text = "A different initial memory." } },
            "history-source" => original with { Background = original.Background! with { Sources = new[] { "fixture:different" } } },
            "subjects" => original with { Background = original.Background! with { Subjects = new[] { "Cara" } } },
            "voice" => original with { Voice = "An edited voice." },
            _ => original with { Sources = new[] { "fixture:different" } },
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RecordedReflectionMind(run.Reflections)
            .ReflectAsync(record.Request with { Character = changed }));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("archetype-duplicate")]
    [InlineData("character-duplicate")]
    [InlineData("background-duplicate")]
    [InlineData("unknown-archetype")]
    [InlineData("no-source")]
    [InlineData("no-profile-source")]
    [InlineData("conditional")]
    [InlineData("long-background")]
    [InlineData("long-cue")]
    [InlineData("long-voice")]
    public void InvalidOrConditionalRuntimeContentIsRejected(string invalid)
    {
        var pack = Pack();
        var profile = pack.Characters[0];
        var anchor = profile.Background[0];
        pack = invalid switch
        {
            "schema" => pack with { Schema = 2 },
            "archetype-duplicate" => pack with { Archetypes = new[] { Archetype(), Archetype() } },
            "character-duplicate" => pack with { Characters = new[] { profile, profile } },
            "background-duplicate" => pack with { Characters = new[] { profile with { Background = new[] { anchor, anchor } } } },
            "unknown-archetype" => pack with { Characters = new[] { profile with { ArchetypeId = "missing" } } },
            "no-source" => pack with { Characters = new[] { profile with { Background = new[] { anchor with { Sources = Array.Empty<string>() } } } } },
            "no-profile-source" => pack with { Characters = new[] { profile with { Sources = Array.Empty<string>() } } },
            "conditional" => pack with { Characters = new[] { profile with { Background = new[] { anchor with { Condition = "heart-event-seen" } } } } },
            "long-background" => pack with { Characters = new[] { profile with { Background = new[] { anchor with { Text = new string('x', 121) } } } } },
            "long-cue" => pack with { Archetypes = new[] { Archetype() with { EvaluatorCue = new string('x', 91) } } },
            _ => pack with { Characters = new[] { profile with { VoiceAccent = new string('x', 300) } } },
        };
        Assert.Throws<InvalidDataException>(() => Scene(pack));
        Assert.Throws<InvalidDataException>(() => pack.ForCharacter("Ann", "Bob", 1, "prepared-scene"));
    }
}
