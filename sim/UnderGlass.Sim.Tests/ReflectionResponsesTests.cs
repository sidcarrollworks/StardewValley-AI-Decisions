using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class ReflectionResponsesTests
{
    private static Villager Person(string name, int x) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
        new Dictionary<string, double>(), Array.Empty<string>());

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen));
    private static readonly ActKind Help = new("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen));
    private static readonly ActKind Argument = new("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen));

    private static TownData Town(bool enabled = true, bool watch = false, double targetRegard = 0.8)
    {
        var feelings = new FeelingOptions { Desire = true, AnswerOn = false, ReturnOn = false, MakeUpOn = false,
            RetaliateOn = false, AvoidOn = false, WithdrawOn = false, FondOn = false,
            Start = new Dictionary<(string, string), double> { [("Ann", "Bob")] = 0.6, [("Bob", "Ann")] = targetRegard } };
        feelings.Acts.Returns = feelings.Acts.Repair = enabled;
        feelings.Acts.Watch = watch;
        feelings.Acts.RemorseShare = 0; // isolate the model's chosen response from ordinary motive draws
        return new TownData
        {
            Cast = new[] { Person("Ann", 3), Person("Bob", 5) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 20), 6).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { Gift, Help, Argument }.Concat(ActCatalog.Returns).Concat(ActCatalog.Repair).ToArray(),
            Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static Simulation Scene(string source = "GaveGift", TownData? town = null)
    {
        var sim = new Simulation(1, town ?? Town());
        bool own = source == "Argued";
        sim.Place(new[] { new Scenario("source", source, own ? "Ann" : "Bob", own ? "Bob" : "Ann", Day: 0, From: 600, To: 601) });
        sim.ConfigureReflection(new ReflectionOptions { Enabled = true, QuietMinutes = 30, DailyChance = 1 });
        return sim;
    }

    private sealed class Mind(string response = "reject") : IReflectionMind
    {
        public List<ReflectionRequest> Requests { get; } = new();
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken token = default)
        {
            Requests.Add(request);
            var answer = await new AuthoredReflectionMind().ReflectAsync(request, token);
            string selected = request.Actor == "Ann" && request.Choices.Any(c => c.Id == response) ? response : "reject";
            return answer with { Weights = request.Choices.ToDictionary(c => c.Id, c => c.Id == selected ? 1.0 : 0.0) };
        }
    }

    [Fact]
    public async Task ThanksAnswersReceivedKindnessAndKeepsTheSourceAndCompletedOutcome()
    {
        var mind = new Mind("thank");
        SimResult run = await Scene().RunAsync(1, mind);
        ReflectionRecord reflection = Assert.Single(run.Reflections, r => r.Request.Actor == "Ann" && r.Request.SourceActId == 0);
        Assert.Contains(reflection.Request.Choices, c => c.Id == "thank" && c.Kind == "Thanked");
        Assert.DoesNotContain(reflection.Request.Choices, c => c.Id == "apologise");
        Assert.All(mind.Requests.Where(q => q.Actor == "Bob" && q.SourceActId == 0),
            q => Assert.DoesNotContain(q.Choices, c => c.Id == "thank"));
        var acted = Assert.Single(reflection.Events, e => e.Status == "acted");
        Act act = run.Acts.Single(a => a.Id == acted.ActId);
        Assert.Equal(("Thanked", "Ann", "Bob", 0), (act.Kind, act.Actor, act.Target, act.About));
        Assert.True(Assert.Single(reflection.Events, e => e.Status == "outcome").Tick >= act.Tick + 1);
        Assert.Contains(run.LifeEvents, e => e.Person == "Bob" && e.ActId == 0 && e.Role == LifeRole.Did && e.Outcome == Outcome.Returned);
    }

    [Theory]
    [InlineData(0.8, Outcome.Accepted)]
    [InlineData(-0.9, Outcome.Refused)]
    public async Task ApologyWaitsForItsRealAnswerAndRetainsTheHurt(double regard, Outcome expected)
    {
        var mind = new Mind("apologise");
        SimResult run = await Scene("Argued", Town(targetRegard: regard)).RunAsync(1, mind);
        ReflectionRecord reflection = Assert.Single(run.Reflections, r => r.Request.Actor == "Ann" && r.Request.SourceActId == 0);
        Assert.Contains(reflection.Request.Choices, c => c.Id == "apologise" && c.Kind == "Apologised");
        Assert.All(mind.Requests.Where(q => q.Actor == "Bob" && q.SourceActId == 0),
            q => Assert.DoesNotContain(q.Choices, c => c.Id == "apologise"));
        Act act = run.Acts.Single(a => a.Id == Assert.Single(reflection.Events, e => e.Status == "acted").ActId);
        Assert.Equal(("Apologised", "Ann", "Bob", 0), (act.Kind, act.Actor, act.Target, act.About));
        ReflectionEvent outcome = Assert.Single(reflection.Events, e => e.Status == "outcome");
        Assert.True(outcome.Tick >= act.Tick + 5);
        Assert.Contains(expected.ToString(), outcome.Text);
        Assert.Contains(run.LifeEvents, e => e.Person == "Ann" && e.ActId == act.Id && e.Outcome == expected);
        Assert.Single(run.Acts, a => a.Kind == "Apologised" && a.About == 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DisabledOrWatchedCatalogNeverOffersItsNewResponses(bool enabled, bool watch)
    {
        foreach (string source in new[] { "GaveGift", "Argued" })
        {
            var mind = new Mind();
            await Scene(source, Town(enabled, watch)).RunAsync(1, mind);
            Assert.NotEmpty(mind.Requests);
            Assert.All(mind.Requests, q => Assert.DoesNotContain(q.Choices, c => c.Id is "thank" or "apologise"));
        }
    }

    [Fact]
    public async Task LightKindnessCannotStartAChainOfThanks()
    {
        var mind = new Mind("thank");
        SimResult run = await Scene("Thanked").RunAsync(1, mind);
        Assert.NotEmpty(mind.Requests);
        Assert.All(mind.Requests, q => Assert.DoesNotContain(q.Choices, c => c.Id == "thank"));
        Assert.Single(run.Acts);
    }

    [Fact]
    public async Task WarmDailyCapAndReturnFormCapStillRestrictThanks()
    {
        TownData capped = Town();
        capped.Feelings.Acts.WarmPerDay = 0;
        TownData cheaperSource = Town();
        cheaperSource = cheaperSource with { Acts = cheaperSource.Acts.Select(k => k.Name == "GaveGift"
            ? k with { Gate = new ActGate(0.1, 0, new[] { DesireKind.Return }) } : k).ToArray() };
        foreach (TownData town in new[] { capped, cheaperSource })
        {
            var mind = new Mind("thank");
            SimResult run = await Scene(town: town).RunAsync(1, mind);
            Assert.All(mind.Requests, q => Assert.DoesNotContain(q.Choices, c => c.Id == "thank"));
            Assert.DoesNotContain(run.Acts, a => a.Kind == "Thanked");
        }
    }

    [Theory]
    [InlineData("age")]
    [InlineData("location")]
    [InlineData("regard")]
    public async Task ApologyCannotBypassAgeLocationOrRemorseRegard(string gate)
    {
        TownData town = Town();
        if (gate == "age") town = town with { Cast = town.Cast.Select(v => v.Name == "Ann" ? v with { Age = 4 } : v).ToArray() };
        if (gate == "location") town = town with { Acts = town.Acts.Select(k => k.Name == "Apologised" ? k with { Allowed = new[] { "Elsewhere" } } : k).ToArray() };
        if (gate == "regard") town.Feelings.Acts.RemorseAt = 1;
        var mind = new Mind("apologise");
        SimResult run = await Scene("Argued", town).RunAsync(1, mind);
        Assert.NotEmpty(mind.Requests);
        Assert.All(mind.Requests, q => Assert.DoesNotContain(q.Choices, c => c.Id == "apologise"));
        Assert.DoesNotContain(run.Acts, a => a.Kind == "Apologised");
    }
}
