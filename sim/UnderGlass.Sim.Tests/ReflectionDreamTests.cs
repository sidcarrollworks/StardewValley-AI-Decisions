using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class ReflectionDreamTests
{
    private const int Day = Clock.MinutesPerDay;
    private static Villager Person(string name, int x, bool sleeps = true) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, sleeps ? 0.2 : -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Day, 1) },
        new Dictionary<string, double>(), Array.Empty<string>());

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Help = new("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Argument = new("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen));

    private static TownData Town(bool sleeps = true) => new()
    {
        Cast = new[] { Person("Ann", 3, sleeps), Person("Bob", 5, sleeps), Person("Cara", 30, sleeps) },
        Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToArray()) },
        Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(), Acts = new[] { Gift, Help, Argument },
        Feelings = new FeelingOptions { Desire = true, AnswerOn = false, ReturnOn = false, MakeUpOn = false,
            RetaliateOn = false, AvoidOn = false, WithdrawOn = false },
        Authority = new AuthorityOptions { ElectConstable = false },
        Body = new BodyOptions { AwakeHoursAtRest = sleeps ? 20 : 100_000, SleepHoursToFill = 8,
            CircadianWeight = 0, HeadHomeMargin = 0 },
        Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
    };

    private static ReflectionOptions Options(double dreams = 1, double daytime = 0) => new()
        { Enabled = true, DailyChance = daytime, DreamChance = dreams };

    private static Simulation Scene(int seed = 1, TownData? town = null, ReflectionOptions? options = null, int at = 600)
    {
        var sim = new Simulation(seed, town ?? Town());
        sim.Place(new[] { new Scenario("known-kindness", "GaveGift", "Bob", "Ann", Day: 0, From: at, To: at + 1) });
        sim.ConfigureReflection(options ?? Options());
        return sim;
    }

    private sealed class Mind(string pick = "reject", int delay = 0, CancellationTokenSource? cancel = null,
        Func<ReflectionRequest, string>? choose = null) : IReflectionMind
    {
        public readonly List<ReflectionRequest> Requests = new();
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest q, CancellationToken token = default)
        {
            Requests.Add(q);
            if (cancel is not null) cancel.Cancel();
            token.ThrowIfCancellationRequested();
            if (delay > 0) await Task.Delay(delay, token);
            ReflectionAnswer authored = await new AuthoredReflectionMind().ReflectAsync(q, token);
            string selected = choose?.Invoke(q) ?? pick;
            return authored with { Weights = q.Choices.ToDictionary(c => c.Id, c => c.Id == selected ? 1.0 : 0.0), Backend = "test" };
        }
    }

    [Fact]
    public async Task DreamRequestsRequireARealWakeAndKnowledgeFromBeforeThatSleep()
    {
        var mind = new Mind();
        var awakeAtCall = new Dictionary<int, HashSet<string>>();
        SimResult r = await Scene().RunAsync(3, mind, (m, sim) =>
            awakeAtCall[m] = new[] { "Ann", "Bob", "Cara" }.Where(n => !sim.Where(n).Asleep).ToHashSet());

        Assert.Equal(new[] { "Ann", "Bob" }, mind.Requests.Select(q => q.Actor).Order());
        Assert.All(r.Reflections, t =>
        {
            ReflectionOpportunity o = Assert.IsType<ReflectionOpportunity>(t.Request.Opportunity);
            Assert.Equal("dream", o.Kind);
            Assert.Equal(t.Request.Tick, o.WokeAt);
            Assert.Contains(r.Sleeps, s => s.Name == t.Request.Actor && s.SleptAt == o.SleptAt && s.WokeAt == o.WokeAt);
            int knownAt = t.Request.Source!.OwnDeed ? r.Acts[t.Request.SourceActId].Tick
                : r.Beliefs[t.Request.Actor][t.Request.SourceActId].GotTick;
            Assert.True(knownAt < o.SleptAt);
            Assert.Contains(t.Request.Actor, awakeAtCall[t.Request.Tick]);
            Assert.Equal(new[] { "dreamed", "considered", "rejected" }, t.Events.Select(e => e.Status));
            Assert.All(t.Events, e => Assert.Equal(o.WokeAt + 1, e.Tick));
            Assert.StartsWith("Waking,", t.Answer.Thought);
            Assert.Contains("dream", t.Request.Proposal!.Tags);
            Assert.Contains("not a witnessed dream event", t.Request.Context);
        });
    }

    [Fact]
    public async Task AwakeTimeDoesNotBecomeADreamAndInitialSleepHasNoInventedPast()
    {
        var mind = new Mind();
        SimResult r = await Scene(town: Town(sleeps: false)).RunAsync(4, mind);

        Assert.Single(r.Acts);
        Assert.All(r.Sleeps, s => Assert.Equal(0, s.SleptAt));
        Assert.Empty(mind.Requests);
        Assert.Empty(r.Reflections);
    }

    [Fact]
    public async Task ImaginingAndRejectingDoesNotWriteDreamEventsIntoWorldEvidence()
    {
        SimResult dreamed = await Scene().RunAsync(3, new Mind());
        SimResult baseline = Scene(options: new ReflectionOptions()).Run(3);

        Assert.NotEmpty(dreamed.Reflections);
        Assert.Equal(JsonSerializer.Serialize(baseline.Acts), JsonSerializer.Serialize(dreamed.Acts));
        Assert.Equal(JsonSerializer.Serialize(baseline.Beliefs), JsonSerializer.Serialize(dreamed.Beliefs));
        Assert.Equal(JsonSerializer.Serialize(baseline.Sleeps), JsonSerializer.Serialize(dreamed.Sleeps));
        Assert.Empty(dreamed.Beliefs["Cara"]);
        Assert.DoesNotContain(dreamed.Reflections, t => t.Request.Actor == "Cara");
    }

    [Fact]
    public async Task DreamChanceUsesItsOwnSeededDrawAndIsNotRetriedWhileAwake()
    {
        int dreams = 0, misses = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            var mind = new Mind();
            SimResult r = await Scene(seed, options: Options(dreams: 0.37)).RunAsync(2, mind);
            foreach (string actor in new[] { "Ann", "Bob" })
            {
                Sleep sleep = Assert.Single(r.Sleeps, s => s.Name == actor && s.SleptAt > 600 && s.WokeAt < 2 * Day);
                bool expected = Rng.Unit(seed, "dream", actor, sleep.SleptAt.ToString(CultureInfo.InvariantCulture)) < 0.37;
                Assert.Equal(expected, mind.Requests.Any(q => q.Actor == actor));
                if (expected) dreams++; else misses++;
            }
        }
        Assert.True(dreams > 0 && misses > 0);
    }

    [Fact]
    public async Task FailedDreamRollLeavesTheOrdinaryDaytimeOpportunityAvailable()
    {
        var mind = new Mind();
        // The long quiet requirement makes the first source wait until after the first sleep.
        ReflectionOptions options = Options(dreams: double.Epsilon, daytime: 1) with { QuietMinutes = 500 };
        SimResult r = await Scene(options: options, at: 1_000).RunAsync(4, mind);

        Assert.NotEmpty(mind.Requests);
        Assert.All(mind.Requests, q => Assert.Null(q.Opportunity));
        Assert.All(mind.Requests, q => Assert.Contains(r.Sleeps,
            s => s.Name == q.Actor && s.SleptAt > 1_000 && s.WokeAt < q.Tick));
        Assert.All(mind.Requests.GroupBy(q => (q.Actor, Clock.Day(q.Tick))), group => Assert.Single(group));
        Assert.All(r.Reflections, t => Assert.DoesNotContain(t.Events, e => e.Status == "dreamed"));
    }

    [Fact]
    public async Task DreamsAndQuietReflectionShareOneSubmittedThoughtPerDayAndUseEachSourceOnce()
    {
        var mind = new Mind();
        Simulation sim = Scene(options: Options(daytime: 1));
        sim.Place(new[] { new Scenario("later-kindness", "HelpedSomeone", "Bob", "Ann", Day: 0, From: 1_000, To: 1_001) });
        SimResult r = await sim.RunAsync(4, mind);

        Assert.Contains(mind.Requests, q => q.Opportunity?.Kind == "dream");
        Assert.Contains(mind.Requests, q => q.Opportunity is null);
        Assert.All(mind.Requests.GroupBy(q => (q.Actor, Clock.Day(q.Tick))), group => Assert.Single(group));
        Assert.All(mind.Requests.GroupBy(q => (q.Actor, q.SourceActId)), group => Assert.Single(group));
        Assert.Equal(2, r.Acts.Count);
    }

    [Fact]
    public async Task UnknownIdentityCannotBeCompletedByDreamImagination()
    {
        TownData town = Town() with { Perception = new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 } };
        var mind = new Mind();
        SimResult r = await Scene(town: town).RunAsync(3, mind);

        Assert.Null(r.Beliefs["Ann"][0].Actor);
        Assert.DoesNotContain(mind.Requests, q => q.Actor == "Ann");
        Assert.Equal("Bob", Assert.Single(mind.Requests).Actor); // only the actual participant knows their own deed
    }

    [Fact]
    public async Task HearingAStoryBeforeBedDoesNotMakeItFirsthandInADream()
    {
        var discussed = new ActKind("DiscussedSomeone", 4, -1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, -0.1, 0.1, 1, TargetIs.Given));
        TownData town = Town();
        town = town with
        {
            Cast = new[]
            {
                Person("Ann", 3) with { Haunts = new[] { new Haunt("Room", new Tile(3, 2), 0, 660, 1), new Haunt("Room", new Tile(30, 2), 660, Day, 1) } },
                Person("Bob", 5),
                Person("Cara", 6) with { Haunts = new[] { new Haunt("Room", new Tile(6, 2), 0, 720, 1), new Haunt("Room", new Tile(28, 2), 720, Day, 1) } },
            },
            Acts = new[] { Gift, Help, Argument, discussed },
            Gossip = new GossipOptions { ChatChance = 1, VolunteerLevel = 0, TellsPerDay = 10, ConfrontMin = 99 },
        };
        var mind = new Mind();
        Simulation sim = Scene(town: town);
        sim.Place(new[] { new Scenario("hearsay", discussed.Name, "Bob", "Ann", Day: 0, From: 690, To: 691) });
        SimResult r = await sim.RunAsync(3, mind);

        Assert.Equal(Source.Told, r.Beliefs["Ann"][1].Source);
        Assert.Contains(mind.Requests, q => q.Actor == "Ann" && q.SourceActId == 0);
        Assert.DoesNotContain(mind.Requests, q => q.Actor == "Ann" && q.SourceActId == 1);
    }

    [Fact]
    public async Task MistakenIdentitySurvivesTheNightAndDoesNotExposeTheActualActor()
    {
        TownData town = Town();
        town = town with
        {
            Cast = town.Cast.Select(v => v.Name == "Ann" ? v with { Temperament = new Temperament(1, 0.9, 0.5, 0.2) } : v).ToArray(),
            Perception = new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 },
            Familiarity = new[] { ("Ann", "Bob", 0.0), ("Ann", "Cara", 1.0) },
        };
        town.Feelings.Start = new Dictionary<(string, string), double> { [("Ann", "Cara")] = -1 };
        town.Feelings.GuessPerHate = 100_000;
        var mind = new Mind();
        SimResult r = await Scene(town: town).RunAsync(3, mind);
        ReflectionRequest q = Assert.Single(mind.Requests, q => q.Actor == "Ann");

        Assert.Equal("Bob", r.Acts[q.SourceActId].Actor);
        Assert.Equal("Cara", q.Subject);
        Assert.DoesNotContain("Bob", q.Memory + q.Context + q.Proposal!.Thought);
        Assert.Equal("dream", q.Opportunity!.Kind);
    }

    [Fact]
    public async Task AcceptedDreamWaitsUntilAfterWakingAndKeepsTheKnownCause()
    {
        SimResult r = await Scene().RunAsync(3, new Mind("gift"));
        ReflectionRecord t = r.Reflections.First();
        ReflectionEvent acted = Assert.Single(t.Events, e => e.Status == "acted");
        Act act = r.Acts.Single(a => a.Id == acted.ActId);

        Assert.True(acted.Tick > t.Request.Opportunity!.WokeAt);
        Assert.Equal(t.Request.SourceActId, act.About);
        Assert.DoesNotContain(r.Sleeps, s => s.Name == t.Request.Actor && s.SleptAt <= acted.Tick && (s.WokeAt is null || s.WokeAt > acted.Tick));
    }

    [Fact]
    public async Task AWaitingIntentionSuppressesDreamsDespiteAnotherKnownMemory()
    {
        TownData town = Town() with { Acts = new[] { Gift, Help with { Allowed = new[] { "Elsewhere" } }, Argument } };
        var mind = new Mind(choose: q => q.Actor == "Ann" ? "help" : "reject");
        Simulation sim = Scene(town: town, options: Options(daytime: 1));
        sim.Place(new[] { new Scenario("second-known-gift", "GaveGift", "Bob", "Ann", Day: 0, From: 1_000, To: 1_001) });
        SimResult r = await sim.RunAsync(2, mind);
        ReflectionRecord first = Assert.Single(r.Reflections, t => t.Request.Actor == "Ann");

        Assert.Contains(first.Events, e => e.Status == "waiting");
        Assert.DoesNotContain(first.Events, e => e.Status is "acted" or "expired");
        Assert.Null(first.Request.Opportunity);
        Assert.Equal(2, r.Beliefs["Ann"].Count);
        Assert.Contains(r.Sleeps, s => s.Name == "Ann" && s.SleptAt > first.Request.Tick && s.WokeAt < 2 * Day);
    }

    [Fact]
    public async Task DreamLatencyAndRecordedAnswersPreserveSimulationTimeAndTapeChecksTheSleepInterval()
    {
        SimResult fast = await Scene().RunAsync(3, new Mind());
        SimResult slow = await Scene().RunAsync(3, new Mind(delay: 4));
        SimResult replayed = await Scene().RunAsync(3, new RecordedReflectionMind(fast.Reflections));
        Assert.Equal(Metrics.LogHash(fast), Metrics.LogHash(slow));
        Assert.Equal(Metrics.LogHash(fast), Metrics.LogHash(replayed));
        Assert.Equal(JsonSerializer.Serialize(fast.Reflections), JsonSerializer.Serialize(slow.Reflections));
        Assert.Equal(JsonSerializer.Serialize(fast.Reflections), JsonSerializer.Serialize(replayed.Reflections));
        ReflectionRequest q = fast.Reflections[0].Request;
        var tape = new RecordedReflectionMind(fast.Reflections);
        await tape.ReflectAsync(JsonSerializer.Deserialize<ReflectionRequest>(JsonSerializer.Serialize(q))!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Opportunity = null }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with
            { Opportunity = q.Opportunity! with { SleptAt = q.Opportunity.SleptAt - 1 } }));
    }

    [Fact]
    public async Task CallerCancellationDuringADreamRequestPropagates()
    {
        using var cancel = new CancellationTokenSource();
        var mind = new Mind(cancel: cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Scene().RunAsync(3, mind, cancellationToken: cancel.Token));
        Assert.Equal("dream", Assert.Single(mind.Requests).Opportunity!.Kind);
    }

    [Fact]
    public async Task DreamsDefaultOffAndExplicitZeroLeaveQuietRunsIdentical()
    {
        ReflectionOptions implicitOff = new() { Enabled = true, DailyChance = 1 };
        Assert.Equal(0, implicitOff.DreamChance);
        SimResult a = await Scene(options: implicitOff).RunAsync(3, new Mind());
        SimResult b = await Scene(options: implicitOff with { DreamChance = 0 }).RunAsync(3, new Mind());
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(b));
        Assert.Equal(JsonSerializer.Serialize(a.Reflections), JsonSerializer.Serialize(b.Reflections));
        Assert.All(a.Reflections, t => Assert.Null(t.Request.Opportunity));
    }

    [Fact]
    public async Task EveryAuthoredDreamHasDistinctWakingContentImmutableTagsAndFitsTheEvaluatorPacket()
    {
        using var handler = new EchoWeights();
        using var evaluator = new ResilientReflectionMind(handler, new ReflectionMindOptions { LayaBaseUrl = "http://localhost:8000" });
        foreach (ReflectionCatalogEntry entry in ReflectionCatalog.Entries)
        {
            double valence = entry.Tone == "kindness" ? 0.2 : entry.Tone == "hostility" ? -0.3 : 0;
            var source = new ReflectionSourceFacts(entry.Tone == "hostility" ? "Argued" : entry.Tone == "kindness" ? "GaveGift" : "Met",
                entry.Perspective == "own", valence, 0);
            var lines = ReflectionCatalog.Lines(source, new Temperament(0.5, 0.5, 0.5, 0.5));
            ReflectionChoice[] choices = new[] { ("gift", "GaveGift"), ("help", "HelpedSomeone"), ("confront", "Argued"), ("defer", ""), ("reject", "") }
                .Select(c => new ReflectionChoice(c.Item1, c.Item2, lines[c.Item1])).ToArray();
            ReflectionRequest quiet = Enumerable.Range(0, 100).Select(i => new ReflectionRequest($"catalog-{i}", 1_700, "Ann", "Bob", 0,
                "You remember your encounter with Bob.", "Ann is newly awake, guarded but open to reconsidering what the remembered encounter means.", choices, source))
                .First(q => ReflectionCatalog.Propose(q).Id == entry.Id);
            ReflectionProposal ordinary = ReflectionCatalog.Propose(quiet);
            ReflectionRequest dream = quiet with { Opportunity = new ReflectionOpportunity("dream", 1_000, 1_700) };
            dream = dream with { Proposal = ReflectionCatalog.Propose(dream) };

            Assert.Equal(ordinary.Id, dream.Proposal.Id);
            Assert.NotEqual(ordinary.Thought, dream.Proposal.Thought);
            Assert.StartsWith("Waking,", dream.Proposal.Thought);
            Assert.Contains("dream", dream.Proposal.Tags);
            Assert.DoesNotContain("dream", entry.Tags);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)dream.Proposal.Tags).Add("mutable"));
            ReflectionAnswer answer = await evaluator.ReflectAsync(dream);
            Assert.Equal("authored+laya", answer.Backend);
            using JsonDocument doc = JsonDocument.Parse(answer.LayaPrompt!);
            JsonElement root = doc.RootElement, question = root.GetProperty("questions").GetProperty("q");
            int size = root.GetProperty("state").GetString()!.Length + question.GetProperty("instructions").GetString()!.Length
                + question.GetProperty("criteria").EnumerateObject().Sum(p => p.Name.Length + p.Value.GetString()!.Length);
            Assert.InRange(size, 1, 1_250);
        }
        Assert.Equal(ReflectionCatalog.Entries.Count, handler.Calls);
    }

    private sealed class EchoWeights : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            using JsonDocument doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var weights = doc.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria")
                .EnumerateObject().ToDictionary(c => c.Name, _ => 1.0);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { answers = new { q = new { probabilities = weights } } }), Encoding.UTF8, "application/json"),
            };
        }
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DreamChanceMustBeAProbability(double chance)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Scene(options: Options(dreams: chance)));
}
