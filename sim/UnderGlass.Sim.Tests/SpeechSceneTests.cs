using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Speech is recorded beside acts and changes nothing: the same seed gives the same world
/// with it on or off, and the same lines twice over.</summary>
public class SpeechSceneTests
{
    private const int Day = Clock.MinutesPerDay;

    private static Villager Person(string name, int x, string household = "") => new(name, household.Length == 0 ? name : household, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Day, 1) }, new Dictionary<string, double>(), Array.Empty<string>());

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Argument = new("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen));

    private static TownData Town()
    {
        FeelingOptions f = FeelingOptions.WithDesire();
        f.AnswerOn = f.ReturnOn = f.MakeUpOn = f.RetaliateOn = f.AvoidOn = f.WithdrawOn = false;
        return new TownData
        {
            Cast = new[] { Person("Ann", 3), Person("Bob", 5, household: "Ann") },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { Gift, Argument }, Feelings = f,
            Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 },
            Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static Simulation Scene(bool speech, CharacterInspirationPack? pack = null)
    {
        var sim = new Simulation(3, Town());
        sim.Place(new[]
        {
            new Scenario("gift", "GaveGift", "Ann", "Bob", Day: 0, From: Clock.At(10), To: Clock.At(10) + 1),
            new Scenario("row", "Argued", "Bob", "Ann", Day: 0, From: Clock.At(12), To: Clock.At(12) + 1),
        });
        sim.ConfigureSpeech(new SpeechOptions { Enabled = speech });
        if (pack is not null)
        {
            sim.ConfigureReflection(new ReflectionOptions { Enabled = true, DailyChance = 0 });
            sim.ConfigureCharacterInspiration(pack);
        }
        return sim;
    }

    private static CharacterInspirationPack Pack() => CharacterInspirationPack.Read(JsonSerializer.Serialize(new
    {
        schema = 1, provenance = "test pack",
        archetypes = new[] { new { id = "weary-defender", drives = "d", coping = "c", voice = "v", evaluatorCue = "e", sources = new[] { "s" } } },
        characters = new[] { new { name = "Bob", archetypeId = "weary-defender", voiceAccent = "", background = Array.Empty<object>(), sources = new[] { "s" } } },
    }));

    [Fact]
    public async Task CustomCatalogAndNewcomerTestVoiceChangeSpeechOnly()
    {
        var pack = new SpeechPack(1, "test", new[] {
            new SpeechLine("plain", "GaveGift", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "A little something for you.", Array.Empty<string>()),
            new SpeechLine("arrival", "GaveGift", new[]{"newcomer"}, Array.Empty<string>(), Array.Empty<string>(), "I am new here. This is for you.", Array.Empty<string>()) });
        async Task<SimResult> Run(bool voice)
        {
            var town = Town() with { Cast = new[] { Person("Newcomer", 3), Person("Bob", 5) } };
            var sim = new Simulation(3, town);
            sim.Place(new[] { new Scenario("gift", "GaveGift", "Newcomer", "Bob", Day:0, From:Clock.At(10), To:Clock.At(10)+1) });
            sim.ConfigureSpeech(new SpeechOptions { Enabled=true, Catalog=pack, NewcomerVoice=voice });
            return await sim.RunAsync(1, new AuthoredReflectionMind());
        }
        SimResult enabled = await Run(true), disabled = await Run(false);
        Assert.Equal(Metrics.LogHash(disabled), Metrics.LogHash(enabled));
        Assert.All(enabled.Speech.Where(s=>s.Speaker=="Newcomer"), s=>Assert.Equal("newcomer",s.Archetype));
        Assert.All(enabled.Speech.Where(s=>s.Speaker!="Newcomer"), s=>Assert.Null(s.Archetype));
        Assert.All(disabled.Speech, s=>Assert.Equal("plain",s.LineId));
        Assert.NotEmpty(enabled.Speech);
        Assert.Equal(enabled.Speech, (await Run(true)).Speech);
    }

    [Fact]
    public void CustomCatalogIsExcludedFromOptionsSerialization()
    {
        var options = new SpeechOptions { Enabled=true, NewcomerVoice=true, Catalog=SpeechCatalog.Default };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options));
        Assert.False(json.RootElement.TryGetProperty("Catalog",out _));
        Assert.True(json.RootElement.GetProperty("NewcomerVoice").GetBoolean());
    }

    [Fact]
    public async Task EveryActToAPersonGetsALineFromTheSpeakersSideAndNothingElseChanges()
    {
        SimResult on = await Scene(true).RunAsync(1, new AuthoredReflectionMind());
        SimResult off = await Scene(false).RunAsync(1, new AuthoredReflectionMind());
        Assert.Empty(off.Speech);
        Assert.Equal(off.Acts.Count, on.Acts.Count);
        Assert.Equal(Metrics.LogHash(off), Metrics.LogHash(on)); // speech never enters the log or a decision
        foreach (Act act in on.Acts.Where(a => a.Target is not null))
        {
            Speech said = Assert.Single(on.Speech, s => s.ActId == act.Id);
            Assert.Equal(act.Actor, said.Speaker);
            Assert.Equal(act.Tick, said.Tick);
            SpeechLine line = SpeechCatalog.Default.Lines.Single(l => l.Id == said.LineId);
            Assert.Equal(act.Kind, line.Kind);
            Assert.Equal(SpeechCatalog.Render(line, act.Target!), said.Line);
            Assert.DoesNotContain("{subject}", said.Line);
            Assert.Null(said.Archetype); // no character pack configured
            Assert.Equal("household", said.Relationship); // Ann and Bob share a household in this town
            Assert.Contains(said.Mood, SpeechCatalog.Moods);
        }
        Assert.Equal(on.Acts.Count(a => a.Target is not null), on.Speech.Count);
        // Deterministic: the same seed says the same things.
        SimResult again = await Scene(true).RunAsync(1, new AuthoredReflectionMind());
        Assert.Equal(on.Speech.Select(s => (s.ActId, s.Line)), again.Speech.Select(s => (s.ActId, s.Line)));
    }

    [Fact]
    public async Task ArchetypeComesFromTheConfiguredPackForTheSpeakerOnly()
    {
        SimResult run = await Scene(true, Pack()).RunAsync(1, new AuthoredReflectionMind());
        Assert.Equal("weary-defender", Assert.Single(run.Speech, s => s.Speaker == "Bob").Archetype);
        Assert.Null(Assert.Single(run.Speech, s => s.Speaker == "Ann").Archetype);
    }

    [Fact]
    public async Task RecordingCarriesTheTableOnlyWhenSpeechIsOn()
    {
        string with = await Replay.JsonAsync(new ReplayOptions { Seed = 3, Days = 7, Feelings = FeelingOptions.WithDesire(), Speech = new SpeechOptions { Enabled = true } }, new AuthoredReflectionMind());
        string without = await Replay.JsonAsync(new ReplayOptions { Seed = 3, Days = 7, Feelings = FeelingOptions.WithDesire() }, new AuthoredReflectionMind());
        using JsonDocument a = JsonDocument.Parse(with), b = JsonDocument.Parse(without);
        Assert.True(a.RootElement.GetProperty("speechOptions").GetProperty("Enabled").GetBoolean());
        Assert.Equal(1, a.RootElement.GetProperty("speechCatalog").GetProperty("schema").GetInt32());
        JsonElement speech = a.RootElement.GetProperty("speech");
        // One line per act that had a person as its target (acts rows carry the target index at [7]).
        int targeted = a.RootElement.GetProperty("acts").EnumerateArray().Count(row => row[7].GetInt32() >= 0);
        Assert.True(targeted > 0, "the week had no act toward a person");
        Assert.Equal(targeted, speech.GetArrayLength());
        Assert.True(speech[0].TryGetProperty("line", out _));
        Assert.False(b.RootElement.TryGetProperty("speech", out _));
        Assert.Equal(a.RootElement.GetProperty("hash").GetString(), b.RootElement.GetProperty("hash").GetString());
    }

    [Fact]
    public void SpeechMustBeConfiguredBeforeRunning()
    {
        var sim = Scene(true);
        sim.RunAsync(1, new AuthoredReflectionMind()).GetAwaiter().GetResult();
        Assert.Throws<InvalidOperationException>(() => sim.ConfigureSpeech(new SpeechOptions()));
    }
}
