using System.Globalization;
using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Private reflection in small, observable scenes. Model doubles choose weights, never
/// write world state; the ordinary act engine must supply every consequence.</summary>
public class ReflectionSceneTests
{
    private const int Day = Clock.MinutesPerDay;
    private static readonly int Ten = Clock.At(10);

    private static Haunt At(int x, int from = 0, int to = Day) => new("Room", new Tile(x, 2), from, to, 1);
    private static Villager Person(string name, params Haunt[] haunts) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        haunts.Length == 0 ? new[] { At(name == "Ann" ? 3 : 5) } : haunts,
        new Dictionary<string, double>(), Array.Empty<string>());

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Help = new("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Argument = new("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen));

    private static FeelingOptions Feelings()
    {
        FeelingOptions f = FeelingOptions.WithDesire();
        // Keep life recording and physical limits, isolate the reflection decision from the
        // ordinary gate's independent return/retaliation decisions.
        f.AnswerOn = f.ReturnOn = f.MakeUpOn = f.RetaliateOn = false;
        f.AvoidOn = f.WithdrawOn = false;
        f.OutcomeDays = 1;
        return f;
    }

    private static TownData Town(params Villager[] people) => new()
    {
        Cast = people.Length == 0 ? new[] { Person("Ann"), Person("Bob") } : people,
        Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList()) },
        Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
        Acts = new[] { Gift, Help, Argument }, Feelings = Feelings(),
        Authority = new AuthorityOptions { ElectConstable = false },
        Body = new BodyOptions { AwakeHoursAtRest = 100_000 },
        Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
    };

    private static ReflectionOptions Enabled(int quiet = 30, int expiry = 3, double chance = 1)
        => new() { Enabled = true, QuietMinutes = quiet, IntentionDays = expiry, DailyChance = chance };

    private static Simulation Scene(TownData? town = null, ReflectionOptions? reflection = null)
    {
        var sim = new Simulation(1, town ?? Town(), new[] { (Ten, "Ann", "GaveGift") });
        sim.ConfigureReflection(reflection ?? Enabled());
        return sim;
    }

    private sealed class Mind(Func<ReflectionRequest, string>? choose = null, int delay = 0,
        Func<ReflectionRequest, ReflectionAnswer>? answer = null) : IReflectionMind
    {
        public List<ReflectionRequest> Requests { get; } = new();
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (delay > 0) await Task.Delay(delay, cancellationToken);
            if (answer is not null) return answer(request);
            string selection = choose?.Invoke(request) ?? "reject";
            return new ReflectionAnswer("Perhaps I could do something about that encounter.", "gift",
                request.Choices.ToDictionary(c => c.Id, c => c.Id == selection ? 1.0 : 0.0), "test");
        }
    }

    private static string Canon(SimResult r) => JsonSerializer.Serialize(r.Reflections);

    [Theory]
    [InlineData("reject", "rejected")]
    [InlineData("defer", "deferred")]
    public async Task RefusingOrDeferringIsOneDecisionWithNoActAndNoRetryOfThatMemory(string choice, string status)
    {
        var mind = new Mind(_ => choice);
        SimResult r = await Scene().RunAsync(3, mind);

        Assert.Equal(2, mind.Requests.Count); // each participant considers the one encounter once
        Assert.Single(r.Acts);
        Assert.Empty(r.Pursued);
        Assert.All(r.Reflections, t =>
        {
            Assert.Equal(0, t.Request.SourceActId);
            Assert.Equal(choice, t.Choice);
            Assert.Equal(new[] { "considered", status }, t.Events.Select(e => e.Status));
            Assert.All(t.Events, e => Assert.Equal(t.Request.Tick + 1, e.Tick));
            Assert.All(t.Events, e => Assert.Equal(-1, e.ActId));
        });
    }

    [Fact]
    public async Task ContextContainsOnlyTheirOwnDeedOrTheirFirsthandBelief()
    {
        var mind = new Mind();
        SimResult r = await Scene(Town(Person("Ann"), Person("Bob"), Person("Cara", At(30)))).RunAsync(1, mind);

        Assert.Equal(new[] { "Ann", "Bob" }, mind.Requests.Select(q => q.Actor).Order());
        ReflectionRequest own = Assert.Single(mind.Requests, q => q.Actor == "Ann");
        Assert.Equal("Bob", own.Subject);
        Assert.Contains("You took part", own.Memory);
        ReflectionRequest received = Assert.Single(mind.Requests, q => q.Actor == "Bob");
        Belief belief = r.Beliefs["Bob"][received.SourceActId];
        Assert.Equal(Source.Witnessed, belief.Source);
        Assert.Equal(belief.Actor, received.Subject);
        Assert.Contains("you remember Ann", received.Memory);
        Assert.True(received.Tick >= belief.GotTick);
        Assert.All(mind.Requests, q => Assert.DoesNotContain("Cara", q.Memory + q.Context));
        Assert.Empty(r.Beliefs["Cara"]);
    }

    [Fact]
    public async Task UnknownIdentityIsNeverFilledInFromTheTrueAct()
    {
        TownData town = Town() with { Perception = new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 } };
        var mind = new Mind();
        SimResult r = await Scene(town).RunAsync(1, mind);

        Assert.Null(r.Beliefs["Bob"][0].Actor);
        Assert.Equal("Ann", r.Acts[0].Actor); // the engine knows, but Bob does not
        Assert.Single(mind.Requests);
        Assert.Equal("Ann", mind.Requests[0].Actor);
        Assert.DoesNotContain(r.Reflections, t => t.Request.Actor == "Bob");
    }

    [Fact]
    public async Task AConfidentMistakeKeepsTheBelievedSubjectInsteadOfTheTrueActor()
    {
        Villager bob = Person("Bob") with { Temperament = new Temperament(1, 0.9, 0.5, 0.2) };
        TownData town = Town(Person("Ann"), bob, Person("Cara", At(30))) with
        {
            Perception = new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 },
            Familiarity = new[] { ("Bob", "Ann", 0.0), ("Bob", "Cara", 1.0) },
        };
        town.Feelings.Start = new Dictionary<(string, string), double> { [("Bob", "Cara")] = -1 };
        town.Feelings.GuessPerHate = 100_000;
        var sim = new Simulation(1, town);
        sim.Place(new[] { new Scenario("misidentified", "GaveGift", "Ann", "Bob", Day: 0, From: Ten, To: Ten + 1) });
        sim.ConfigureReflection(Enabled());
        var mind = new Mind();
        SimResult r = await sim.RunAsync(1, mind);

        Assert.Equal("Ann", r.Acts[0].Actor);
        Assert.Equal("Cara", r.Beliefs["Bob"][0].Actor);
        ReflectionRequest q = Assert.Single(mind.Requests, q => q.Actor == "Bob");
        Assert.Equal("Cara", q.Subject);
        Assert.Contains("you remember Cara", q.Memory);
        Assert.DoesNotContain("Ann", q.Memory + q.Context);
    }

    [Fact]
    public async Task HearsayAboutSomeoneDoesNotBecomeTheirFirsthandMemory()
    {
        var discussed = new ActKind("DiscussedSomeone", 4, -1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, -0.1, 0.1, 1, TargetIs.Given));
        TownData town = Town(Person("Ann"), Person("Bob", At(30)),
            Person("Cara", At(5, 0, Clock.At(11)), At(28, Clock.At(11)))) with
        {
            Acts = new[] { Gift, Help, Argument, discussed },
            Gossip = new GossipOptions { ChatChance = 1, VolunteerLevel = 0, TellsPerDay = 10, ConfrontMin = 99 },
        };
        var sim = new Simulation(1, town);
        // A named subject can be discussed while absent; only Cara is present to witness it.
        sim.Place(new[] { new Scenario("behind-their-back", discussed.Name, "Ann", "Bob", Day: 0, From: Ten, To: Ten + 1) });
        sim.ConfigureReflection(Enabled());
        var mind = new Mind();
        SimResult r = await sim.RunAsync(1, mind);

        Assert.Equal(Source.Told, r.Beliefs["Bob"][0].Source);
        Assert.Equal("Bob", r.Beliefs["Bob"][0].Target);
        Assert.DoesNotContain(mind.Requests, q => q.Actor == "Bob" || q.Actor == "Cara");
        Assert.Equal("Ann", Assert.Single(mind.Requests).Actor);
    }

    [Fact]
    public async Task AcceptedIntentionWaitsForARealEncounterAndRetainsItsCauseAndLine()
    {
        Villager bob = Person("Bob", At(5, 0, Ten + 5), At(30, Ten + 5, Clock.At(12)), At(5, Clock.At(12)));
        var mind = new Mind(q => q.Actor == "Ann" && q.SourceActId == 0 ? "gift" : "reject");
        var sim = new Simulation(1, Town(Person("Ann"), bob), new[] { (Ten, "Bob", "GaveGift") });
        sim.ConfigureReflection(Enabled());
        SimResult r = await sim.RunAsync(3, mind);

        ReflectionRecord t = Assert.Single(r.Reflections, t => t.Request.Actor == "Ann" && t.Request.SourceActId == 0);
        Assert.Equal(new[] { "considered", "accepted", "waiting", "acted", "outcome" }, t.Events.Select(e => e.Status));
        ReflectionEvent acted = Assert.Single(t.Events, e => e.Status == "acted");
        Act act = Assert.Single(r.Acts, a => a.Id == acted.ActId);
        Assert.True(acted.Tick >= Clock.At(12));
        Assert.True(acted.Tick > t.Events.Single(e => e.Status == "accepted").Tick);
        Assert.Equal(("Ann", "Bob", "GaveGift", 0, false), (act.Actor, act.Target, act.Kind, act.About, act.Injected));
        Assert.Contains(act.Id, r.Pursued);
        Assert.Contains(t.Request.Choices.Single(c => c.Id == t.Choice).Line, acted.Text);
        Assert.Contains(r.Sentiments, s => s.Holder == "Ann" && s.Toward == "Bob");
        // The recorded thought explains this choice; do not add the ordinary gate's strongest
        // sentiment as a second, potentially contradictory explanation after the choice.
        Assert.DoesNotContain(r.Log, l => l.StartsWith($"{acted.Tick} why Ann GaveGift Bob ", StringComparison.Ordinal));
        LifeEvent life = Assert.Single(r.LifeEvents, l => l.ActId == act.Id && l.Role == LifeRole.Did);
        Assert.Equal(Outcome.Ignored, life.Outcome);
        ReflectionEvent outcome = Assert.Single(t.Events, e => e.Status == "outcome");
        Assert.Equal(act.Id, outcome.ActId);
        Assert.True(outcome.Tick >= life.ResolvedTick);
        Assert.Contains(life.Outcome.ToString(), outcome.Text);
    }

    [Fact]
    public async Task AnUnavailableTargetLetsTheIntentionExpireWithoutAnAct()
    {
        Villager bob = Person("Bob", At(5, 0, Ten + 5), At(30, Ten + 5)) with
        {
            Job = new Job("Room", new Tile(30, 2), 0, Day, new[] { 0 }, 1),
        };
        var mind = new Mind(q => q.Actor == "Ann" ? "gift" : "reject");
        SimResult r = await Scene(Town(Person("Ann"), bob), Enabled(expiry: 1)).RunAsync(3, mind);

        ReflectionRecord t = Assert.Single(r.Reflections, t => t.Request.Actor == "Ann");
        Assert.Equal(new[] { "considered", "accepted", "waiting", "expired" }, t.Events.Select(e => e.Status));
        Assert.Equal(Day, t.Events[^1].Tick - t.Events[0].Tick);
        Assert.Single(r.Acts);
    }

    [Fact]
    public async Task EvaluatorMayReshapeTheSuggestedAction()
    {
        var mind = new Mind(q => q.Actor == "Ann" && q.SourceActId == 0 ? "help" : "reject");
        SimResult r = await Scene().RunAsync(1, mind);
        ReflectionRecord t = Assert.Single(r.Reflections, t => t.Request.Actor == "Ann" && t.Request.SourceActId == 0);

        Assert.Equal("gift", t.Answer.SuggestedChoice);
        Assert.Equal("help", t.Choice);
        Assert.Contains(t.Events, e => e.Status == "reshaped");
        Act act = r.Acts.Single(a => a.Id == t.Events.Single(e => e.Status == "acted").ActId);
        Assert.Equal("HelpedSomeone", act.Kind);
    }

    [Fact]
    public async Task DelayedAnswersKeepTheSameSimulatedClockAndConsequences()
    {
        string Pick(ReflectionRequest q) => q.Actor == "Ann" && q.SourceActId == 0 ? "help" : "reject";
        var fast = new Mind(Pick);
        var slow = new Mind(Pick, delay: 5);
        var fastTicks = new List<int>();
        var slowTicks = new List<int>();
        SimResult a = await Scene().RunAsync(2, fast, (m, _) => fastTicks.Add(m));
        SimResult b = await Scene().RunAsync(2, slow, (m, _) => slowTicks.Add(m));

        Assert.NotEmpty(a.Reflections);
        Assert.Equal(Enumerable.Range(0, 2 * Day), fastTicks);
        Assert.Equal(fastTicks, slowTicks);
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(b));
        Assert.Equal(Canon(a), Canon(b));
    }

    [Fact]
    public async Task SyncAndAsyncAuthoredRunsAgreeAndRecordedAnswersReproduceTheRun()
    {
        SimResult sync = Scene().Run(3);
        SimResult asyncRun = await Scene().RunAsync(3, new AuthoredReflectionMind());
        SimResult recorded = await Scene().RunAsync(3, new RecordedReflectionMind(sync.Reflections));

        Assert.NotEmpty(sync.Reflections);
        Assert.Equal(Metrics.LogHash(sync), Metrics.LogHash(asyncRun));
        Assert.Equal(Canon(sync), Canon(asyncRun));
        Assert.Equal(Metrics.LogHash(sync), Metrics.LogHash(recorded));
        Assert.Equal(Canon(sync), Canon(recorded));
    }

    [Fact]
    public async Task RecordedAnswerRefusesAnEditedContextOrLineEvenWithTheSameId()
    {
        SimResult r = await Scene().RunAsync(1, new Mind());
        ReflectionRequest q = r.Reflections[0].Request;
        var tape = new RecordedReflectionMind(r.Reflections);

        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Context = q.Context + " Changed." }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Memory = "An unrelated memory." }));
        var changed = q.Choices.Select((c, i) => i == 0 ? c with { Line = "An entirely different line." } : c).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Choices = changed }));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("negative")]
    [InlineData("infinite")]
    [InlineData("zero")]
    [InlineData("missing")]
    [InlineData("long-thought")]
    [InlineData("null-answer")]
    [InlineData("null-weights")]
    public async Task InvalidModelAnswersUseAnExplicitRecordedFallback(string invalid)
    {
        var mind = new Mind(answer: q =>
        {
            var weights = q.Choices.ToDictionary(c => c.Id, _ => 1.0);
            if (invalid == "negative") weights["gift"] = -1;
            if (invalid == "infinite") weights["gift"] = double.PositiveInfinity;
            if (invalid == "zero") foreach (string key in weights.Keys.ToArray()) weights[key] = 0;
            if (invalid == "missing") weights.Remove("gift");
            if (invalid == "null-answer") return null!;
            return new ReflectionAnswer(invalid == "long-thought" ? new string('x', 301) : "A thought.",
                invalid == "unknown" ? "teleport" : "gift", invalid == "null-weights" ? null! : weights, "broken-test");
        });
        SimResult r = await Scene().RunAsync(1, mind);

        Assert.NotEmpty(r.Reflections);
        Assert.All(r.Reflections, t =>
        {
            Assert.Equal("authored-fallback", t.Answer.Backend);
            Assert.False(string.IsNullOrWhiteSpace(t.Answer.Note));
            Assert.Contains(t.Request.Choices, c => c.Id == t.Choice);
            Assert.All(t.Answer.Weights.Values, w => Assert.InRange(w, 0, 1));
            Assert.Equal(1, t.Answer.Weights.Values.Sum(), 12);
        });
    }

    [Fact]
    public async Task ZeroOpportunityChanceMakesNoRequestsAndChangesNoOrdinaryBehavior()
    {
        var mind = new Mind();
        SimResult r = await Scene(reflection: Enabled(chance: 0)).RunAsync(2, mind);
        SimResult off = Scene(reflection: new ReflectionOptions()).Run(2);

        Assert.Empty(mind.Requests);
        Assert.Empty(r.Reflections);
        Assert.Equal(Metrics.LogHash(off), Metrics.LogHash(r));
    }

    [Fact]
    public async Task ALongEncounterMustFinishBeforeTheQuietStretchStarts()
    {
        TownData town = Town() with { Acts = new[] { Gift with { DurationMinutes = 90 }, Help, Argument } };
        var mind = new Mind();
        SimResult r = await Scene(town).RunAsync(1, mind);

        Assert.Equal(2, mind.Requests.Count);
        Assert.All(mind.Requests, q => Assert.True(q.Tick >= Ten + 90 + 30));
        Assert.Single(r.Acts);
    }

    [Fact]
    public async Task WorkingTimeDoesNotCountAsQuietReflection()
    {
        Villager ann = Person("Ann") with { Job = new Job("Room", new Tile(3, 2), 0, Clock.At(12), Array.Empty<int>(), 1) };
        var mind = new Mind();
        await Scene(Town(ann, Person("Bob"))).RunAsync(1, mind);

        ReflectionRequest q = Assert.Single(mind.Requests, q => q.Actor == "Ann");
        Assert.True(q.Tick >= Clock.At(12) + 30);
    }

    [Fact]
    public async Task OrdinaryIdleWanderingStillAllowsAQuietOpportunity()
    {
        var mind = new Mind();
        SimResult r = await Scene(Town() with { Wander = 2 }).RunAsync(1, mind);

        Assert.NotEmpty(mind.Requests);
        Assert.NotEmpty(r.Reflections);
        Assert.All(r.Reflections, t => Assert.Equal(0, t.Request.SourceActId));
    }

    [Fact]
    public async Task DisabledReflectionNeverCallsTheMindAndKeepsThePreFeelingsHash()
    {
        var mind = new Mind(answer: _ => throw new InvalidOperationException("Disabled reflection called the model."));
        SimResult r = await new Simulation(42, feelings: FeelingOptions.Off).RunAsync(7, mind);

        Assert.Equal("e7f6653087ff7e18", Metrics.LogHash(r));
        Assert.Empty(r.Reflections);
        Assert.Empty(mind.Requests);
        string json = await Replay.JsonAsync(new ReplayOptions { Seed = 42, Days = 1 }, mind);
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("reflections", out _));
        Assert.False(doc.RootElement.TryGetProperty("reflectionOptions", out _));
        Assert.Empty(mind.Requests);
    }

    [Fact]
    public async Task ReplayCarriesTheContextChosenLineAndChronologicalOutcomes()
    {
        TownData town = Town() with { Acts = new[] { Gift with { PerDay = 2 }, Help, Argument } };
        town = town with { Cast = town.Cast.Select(p => p with { Acts = new Dictionary<string, double> { ["GaveGift"] = 1 } }).ToArray() };
        var options = new ReplayOptions { Seed = 1, Days = 3, Town = town, Reflection = Enabled() };
        var mind = new Mind(q => q.Actor == "Ann" ? "gift" : "reject");
        using JsonDocument doc = JsonDocument.Parse(await Replay.JsonAsync(options, mind));
        JsonElement run = doc.RootElement;
        JsonElement[] thoughts = run.GetProperty("reflections").EnumerateArray().ToArray();

        Assert.NotEmpty(thoughts);
        Assert.Contains(thoughts, t => t.GetProperty("events").EnumerateArray().Any(e => e.GetProperty("status").GetString() == "outcome"));
        foreach (JsonElement t in thoughts)
        {
            JsonElement request = t.GetProperty("request");
            int tick = request.GetProperty("tick").GetInt32();
            Assert.False(string.IsNullOrWhiteSpace(request.GetProperty("memory").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("answer").GetProperty("thought").GetString()));
            string choice = t.GetProperty("choice").GetString()!;
            string line = request.GetProperty("choices").EnumerateArray().Single(c => c.GetProperty("id").GetString() == choice).GetProperty("line").GetString()!;
            JsonElement[] events = t.GetProperty("events").EnumerateArray().ToArray();
            int[] ticks = events.Select(e => e.GetProperty("tick").GetInt32()).ToArray();
            Assert.Equal(ticks.Order(), ticks);
            Assert.All(ticks, m => Assert.True(m > tick));
            foreach (JsonElement e in events.Where(e => e.GetProperty("status").GetString() == "acted"))
            {
                Assert.Contains(line, e.GetProperty("text").GetString());
                int id = e.GetProperty("actId").GetInt32();
                JsonElement act = run.GetProperty("acts").EnumerateArray().Single(a => a[0].GetInt32() == id);
                Assert.Equal(e.GetProperty("tick").GetInt32(), act[1].GetInt32());
                Assert.Equal(request.GetProperty("sourceActId").GetInt32(), act[8].GetInt32());
            }
        }
    }

    [Fact]
    public void ReflectionRequiresTheActingModeThatCanRecordConsequences()
    {
        foreach (FeelingOptions f in new[] { FeelingOptions.Off, FeelingOptions.Observe, new FeelingOptions(),
            new FeelingOptions { Desire = true, DesireActs = false } })
        {
            var sim = new Simulation(1, Town() with { Feelings = f });
            Assert.Throws<ArgumentException>(() => sim.ConfigureReflection(Enabled()));
            sim.ConfigureReflection(new ReflectionOptions());
        }
    }

    [Fact]
    public async Task CancellationStopsAsyncRunAndRestoresCallerCulture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        using var cancellation = new CancellationTokenSource();
        var sim = Scene();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sim.RunAsync(1, new Mind(),
            (m, _) => { if (m == 10) cancellation.Cancel(); }, cancellation.Token));
        Assert.Equal(before, CultureInfo.CurrentCulture);
        var offMind = new Mind();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Replay.RecordAsync(
            new ReplayOptions { Days = 1 }, offMind, cancellation.Token));
        Assert.Empty(offMind.Requests);
        Assert.Equal(before, CultureInfo.CurrentCulture);
    }
}
