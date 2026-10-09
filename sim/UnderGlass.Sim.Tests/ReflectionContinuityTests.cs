using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Reconsideration needs new personal knowledge, not a fresh draw over the same idea.
/// All model answers below are explicit controls so the tests exercise continuity mechanics.</summary>
public class ReflectionContinuityTests
{
    private const int Day = Clock.MinutesPerDay;
    private const int Ten = 600;
    private static Haunt At(int x, int from = 0, int to = Day) => new("Room", new Tile(x, 2), from, to, 1);
    private static Villager Person(string name, params Haunt[] haunts) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        haunts.Length > 0 ? haunts : new[] { At(name == "Ann" ? 3 : name == "Bob" ? 5 : 6) },
        new Dictionary<string, double>(), Array.Empty<string>());

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Help = new("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Argued = new("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen));

    private static TownData Town(params Villager[] people) => new()
    {
        Cast = people.Length > 0 ? people : new[] { Person("Ann"), Person("Bob") },
        Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToArray()) },
        Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(), Acts = new[] { Gift, Help, Argued },
        Feelings = new FeelingOptions { Desire = true, AnswerOn = false, ReturnOn = false, MakeUpOn = false,
            RetaliateOn = false, AvoidOn = false, WithdrawOn = false },
        Authority = new AuthorityOptions { ElectConstable = false },
        Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
    };

    private static ReflectionOptions Options(int revisions = 2, int delay = 1) => new()
        { Enabled = true, QuietMinutes = 30, DailyChance = 1, MaxReconsiderations = revisions, ReconsiderAfterDays = delay };

    private static Scenario Encounter(string id, string actor, int day, int at = Ten, string kind = "GaveGift")
        => new(id, kind, actor, "Ann", Day: day, From: at, To: at + 1);

    private static Simulation Scene(IEnumerable<Scenario> encounters, TownData? town = null, ReflectionOptions? options = null)
    {
        var sim = new Simulation(1, town ?? Town());
        sim.Place(encounters);
        sim.ConfigureReflection(options ?? Options());
        return sim;
    }

    private sealed class Mind(Func<ReflectionRequest, string>? choose = null, string? thought = null) : IReflectionMind
    {
        public readonly List<ReflectionRequest> Requests = new();
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest q, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            Requests.Add(q);
            string pick = q.Actor == "Ann" ? choose?.Invoke(q) ?? "defer" : "reject";
            return Task.FromResult(new ReflectionAnswer(thought ?? "I could offer a gift, but I need time to think about it.", "gift",
                q.Choices.ToDictionary(c => c.Id, c => c.Id == pick ? 1.0 : 0.0), "test"));
        }
    }

    [Fact]
    public async Task ADeferredIdeaRevisitsNewFirsthandKnowledgeAndRetainsBothCauses()
    {
        var encounters = new[] { Encounter("first-kindness", "Bob", 0), Encounter("later-help", "Bob", 1, Ten + 60, "HelpedSomeone") };
        var mind = new Mind(q => q.SourceActId == 0 ? "defer" : q.Continuity is not null ? "gift" : "reject");
        SimResult r = await Scene(encounters).RunAsync(4, mind);
        ReflectionRecord prior = Assert.Single(r.Reflections, t => t.Request.Actor == "Ann" && t.Request.SourceActId == 0);
        ReflectionRecord next = Assert.Single(r.Reflections, t => t.Request.Continuity is not null);
        ReflectionContinuity c = next.Request.Continuity!;

        Assert.Equal("defer", prior.Choice);
        Assert.Equal((prior.Request.Id, prior.Request.Id, 1, prior.Request.SourceActId),
            (c.RootId, c.PriorId, c.Revision, c.PriorSourceActId));
        Assert.Equal(prior.Answer.Thought, c.PriorThought);
        Assert.Contains("Later known HelpedSomeone encounter with Bob", c.Reason);
        Belief newKnowledge = r.Beliefs["Ann"][next.Request.SourceActId];
        Assert.Equal(Source.Witnessed, newKnowledge.Source);
        Assert.True(newKnowledge.GotTick > prior.Events.First(e => e.Status == "considered").Tick);
        Assert.True(next.Request.Tick - prior.Events.First(e => e.Status == "considered").Tick >= Day);
        Assert.Equal("reconsidered", next.Events[0].Status);
        Assert.Equal(next.Request.SourceActId, next.Events[0].ActId);
        Act performed = r.Acts.Single(a => a.Id == next.Events.Single(e => e.Status == "acted").ActId);
        Assert.Equal(next.Request.SourceActId, performed.About);
        Assert.NotEqual(c.PriorSourceActId, performed.About);

        SimResult replayed = await Scene(encounters).RunAsync(4, new RecordedReflectionMind(r.Reflections));
        Assert.Equal(Metrics.LogHash(r), Metrics.LogHash(replayed));
        Assert.Equal(JsonSerializer.Serialize(r.Reflections), JsonSerializer.Serialize(replayed.Reflections));
    }

    [Fact]
    public async Task ChangingRegardWithoutANewEncounterNeverRetriesADeferredIdea()
    {
        TownData town = Town();
        town.Feelings.DriftPerDay = 0.1;
        var mind = new Mind();
        Simulation sim = Scene(new[] { Encounter("first", "Bob", 0) }, town);
        SimResult r = await sim.RunAsync(4, mind);

        ReflectionRequest first = Assert.Single(mind.Requests, q => q.Actor == "Ann");
        Assert.NotEqual(first.Source!.Regard, sim.PersonalRegard("Ann", "Bob"));
        Assert.Null(first.Continuity);
        Assert.All(r.Reflections, t => Assert.Null(t.Request.Continuity));
        Assert.Single(r.Acts);
    }

    [Fact]
    public async Task AChangedEncounterWaitsTheFullCooldownInsteadOfConsumingItsOneOpportunityEarly()
    {
        var mind = new Mind();
        SimResult r = await Scene(new[] { Encounter("first", "Bob", 0), Encounter("same-day-news", "Bob", 0, Ten + 90) })
            .RunAsync(2, mind);
        ReflectionRecord first = r.Reflections.Single(t => t.Request.Actor == "Ann" && t.Request.SourceActId == 0);
        ReflectionRequest next = Assert.Single(mind.Requests, q => q.Actor == "Ann" && q.Continuity is not null);
        int applied = first.Events.Single(e => e.Status == "considered").Tick;

        Assert.Equal(applied + Day, next.Tick);
        Assert.True(r.Beliefs["Ann"][next.SourceActId].GotTick < Day);
        Assert.NotEqual(first.Request.SourceActId, next.SourceActId);
    }

    [Fact]
    public async Task RejectedIdeasStayTerminalWhileDistinctSourcesMayPromptIndependentThoughts()
    {
        var mind = new Mind(_ => "reject");
        SimResult r = await Scene(new[] { Encounter("first", "Bob", 0), Encounter("second", "Bob", 1, Ten + 60) }).RunAsync(3, mind);

        ReflectionRequest[] requests = mind.Requests.Where(q => q.Actor == "Ann").ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, q => Assert.Null(q.Continuity));
        Assert.NotEqual(requests[0].SourceActId, requests[1].SourceActId);
        Assert.All(r.Reflections, t => Assert.DoesNotContain(t.Events, e => e.Status == "reconsidered"));
    }

    [Fact]
    public async Task ADeferredIdeaDoesNotCaptureAnEncounterWithAnotherSubject()
    {
        var mind = new Mind();
        SimResult r = await Scene(new[] { Encounter("bob", "Bob", 0), Encounter("cara", "Cara", 1, Ten + 60) },
            Town(Person("Ann"), Person("Bob"), Person("Cara"))).RunAsync(3, mind);
        ReflectionRequest[] thoughts = mind.Requests.Where(q => q.Actor == "Ann").ToArray();

        Assert.Equal(new[] { "Bob", "Cara" }, thoughts.Select(q => q.Subject));
        Assert.All(thoughts, q => Assert.Null(q.Continuity));
        Assert.Equal(2, r.Acts.Count);
    }

    [Fact]
    public async Task ALaterOwnDeedCanReopenAnIdeaWithoutBorrowingSomeoneElsesPrivateKnowledge()
    {
        var encounters = new[]
        {
            new Scenario("own-first", "GaveGift", "Ann", "Bob", Day: 0, From: Ten, To: Ten + 1),
            new Scenario("own-later", "HelpedSomeone", "Ann", "Bob", Day: 1, From: Ten + 60, To: Ten + 61),
        };
        var mind = new Mind();
        SimResult r = await Scene(encounters).RunAsync(3, mind);
        ReflectionRequest next = Assert.Single(mind.Requests, q => q.Actor == "Ann" && q.Continuity is not null);

        Assert.True(next.Source!.OwnDeed);
        Assert.Contains("You took part", next.Memory);
        Assert.Equal("Ann", r.Acts[next.SourceActId].Actor);
        Assert.Equal("Bob", next.Subject);
        Assert.Equal("HelpedSomeone", next.Source.Kind);
        Assert.NotEqual(next.SourceActId, next.Continuity!.PriorSourceActId);
    }

    [Fact]
    public async Task ReconsiderationLinksTheBelievedPersonEvenWhenBothEncountersAreMisidentified()
    {
        TownData town = Town(Person("Ann") with { Temperament = new Temperament(1, 0.9, 0.5, 0.2) },
            Person("Bob"), Person("Cara", At(30))) with
        {
            Perception = new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 },
            Familiarity = new[] { ("Ann", "Bob", 0.0), ("Ann", "Cara", 1.0) },
        };
        town.Feelings.Start = new Dictionary<(string, string), double> { [("Ann", "Cara")] = -1 };
        town.Feelings.GuessPerHate = 100_000;
        var mind = new Mind();
        SimResult r = await Scene(new[] { Encounter("first", "Bob", 0), Encounter("later", "Bob", 1, Ten + 60) }, town)
            .RunAsync(3, mind);
        ReflectionRequest next = Assert.Single(mind.Requests, q => q.Actor == "Ann" && q.Continuity is not null);
        ReflectionRequest prior = mind.Requests.Single(q => q.Id == next.Continuity!.PriorId);

        Assert.Equal("Bob", r.Acts[next.SourceActId].Actor);
        Assert.Equal("Cara", r.Beliefs["Ann"][next.SourceActId].Actor);
        Assert.Equal("Cara", prior.Subject);
        Assert.Equal("Cara", next.Subject);
        Assert.Contains("with Cara", next.Continuity!.Reason);
        Assert.DoesNotContain("Bob", next.Memory + next.Context + next.Continuity.Reason + next.Proposal!.Thought);
    }

    [Fact]
    public async Task NewHearsayCannotReopenADeferredIdea()
    {
        var discussed = new ActKind("DiscussedSomeone", 4, -1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, -0.1, 0.1, 1, TargetIs.Given));
        TownData town = Town(Person("Ann", At(3, 0, 660), At(30, 660)), Person("Bob"),
            Person("Cara", At(6, 0, 720), At(28, 720))) with
        {
            Acts = new[] { Gift, Help, Argued, discussed },
            Gossip = new GossipOptions { ChatChance = 1, VolunteerLevel = 0, TellsPerDay = 10, ConfrontMin = 99 },
        };
        var mind = new Mind();
        SimResult r = await Scene(new[] { Encounter("first", "Bob", 0), Encounter("hearsay", "Bob", 0, 690, discussed.Name) }, town)
            .RunAsync(3, mind);

        Assert.Equal(Source.Told, r.Beliefs["Ann"][1].Source);
        Assert.Equal("Bob", r.Beliefs["Ann"][1].Actor);
        Assert.Equal("Ann", r.Beliefs["Ann"][1].Target);
        Assert.Single(mind.Requests, q => q.Actor == "Ann");
        Assert.All(r.Reflections.Where(t => t.Request.Actor == "Ann"), t => Assert.Null(t.Request.Continuity));
    }

    [Fact]
    public async Task EachDeferredRootHasAtMostTwoRevisionsAndEveryDecisionUsesADistinctSource()
    {
        var mind = new Mind();
        SimResult r = await Scene(Enumerable.Range(0, 5).Select(day => Encounter($"day-{day}", "Bob", day, Ten + day * 5)))
            .RunAsync(6, mind);
        ReflectionRequest[] requests = mind.Requests.Where(q => q.Actor == "Ann").ToArray();
        ReflectionRequest first = requests[0];
        var firstChain = requests.Where(q => q.Continuity?.RootId == first.Id).ToArray();

        Assert.Equal(new[] { 1, 2 }, firstChain.Select(q => q.Continuity!.Revision));
        Assert.Equal(firstChain[0].Id, firstChain[1].Continuity!.PriorId);
        Assert.Equal(requests.Length, requests.Select(q => q.SourceActId).Distinct().Count());
        Assert.All(requests.Where(q => q.Continuity is not null), q => Assert.InRange(q.Continuity!.Revision, 1, 2));
        Assert.Equal(5, r.Acts.Count); // deferral itself never manufactures another encounter
    }

    [Fact]
    public async Task ReconsiderationCanBeDisabledWithoutSuppressingIndependentNewThoughts()
    {
        var mind = new Mind();
        await Scene(new[] { Encounter("first", "Bob", 0), Encounter("next", "Bob", 1, Ten + 60) }, options: Options(revisions: 0))
            .RunAsync(3, mind);
        Assert.Equal(2, mind.Requests.Count(q => q.Actor == "Ann"));
        Assert.All(mind.Requests, q => Assert.Null(q.Continuity));
    }

    [Fact]
    public async Task AnActedIntentionKeepsItsOutcomeWhileAllowingLaterThoughts()
    {
        var mind = new Mind(q => q.SourceActId == 0 ? "gift" : "reject");
        SimResult r = await Scene(new[] { Encounter("bob", "Bob", 0), Encounter("cara", "Cara", 1, Ten + 60) },
            Town(Person("Ann"), Person("Bob"), Person("Cara"))).RunAsync(9, mind);
        ReflectionRecord first = r.Reflections.Single(t => t.Request.Actor == "Ann" && t.Request.SourceActId == 0);
        ReflectionEvent acted = first.Events.Single(e => e.Status == "acted");
        ReflectionEvent outcome = first.Events.Single(e => e.Status == "outcome");

        Assert.Contains(mind.Requests, q => q.Actor == "Ann" && q.Tick > acted.Tick && q.Tick < outcome.Tick);
        Assert.Equal(acted.ActId, outcome.ActId);
        Assert.Contains("Ignored", outcome.Text);
        Assert.True(outcome.Tick - acted.Tick >= 7 * Day);
        Assert.All(mind.Requests.GroupBy(q => (q.Actor, Day: Clock.Day(q.Tick))), group => Assert.Single(group));
    }

    [Fact]
    public async Task PriorIdeaIsBoundedAndTapeRejectsEditedContinuity()
    {
        var mind = new Mind(thought: new string('x', 280));
        SimResult r = await Scene(new[] { Encounter("first", "Bob", 0), Encounter("next", "Bob", 1, Ten + 60) }).RunAsync(3, mind);
        ReflectionRequest next = mind.Requests.Single(q => q.Actor == "Ann" && q.Continuity is not null);
        Assert.InRange(next.Continuity!.PriorThought.Length, 1, 120);
        Assert.EndsWith("…", next.Continuity.PriorThought);
        Assert.Equal(280, r.Reflections.Single(t => t.Request.Id == next.Continuity.PriorId).Answer.Thought.Length);

        var tape = new RecordedReflectionMind(r.Reflections);
        await tape.ReflectAsync(JsonSerializer.Deserialize<ReflectionRequest>(JsonSerializer.Serialize(next))!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(next with
            { Continuity = next.Continuity with { PriorThought = "An invented different idea." } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(next with
            { Continuity = next.Continuity with { PriorSourceActId = 999 } }));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 0)]
    public void ContinuityOptionsRequireABoundedRevisionCountAndRealDelay(int revisions, int delay)
    {
        var sim = new Simulation(1, Town());
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.ConfigureReflection(Options(revisions, delay)));
    }
}
