using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class SourceResponseOutcomeTests
{
    private static Villager Person(string name, int x) => new(name, name, "villager",
        new Temperament(1, 0.9, 0.5, 0.2), new Body(100, -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
        new Dictionary<string, double>(), Array.Empty<string>());

    private static TownData Town(bool mistaken = false)
    {
        var feelings = new FeelingOptions { Desire = true, AnswerOn = false, ReturnOn = false,
            MakeUpOn = false, RetaliateOn = false, AvoidOn = false, WithdrawOn = false, FondOn = false,
            Start = new Dictionary<(string, string), double> { [("Ann", "Bob")] = 0.6, [("Bob", "Ann")] = 0.8 } };
        feelings.Acts.Returns = feelings.Acts.Repair = true;
        feelings.Acts.RemorseShare = 0;
        if (mistaken)
        {
            feelings.Start = new Dictionary<(string, string), double> { [("Ann", "Cara")] = -1 };
            feelings.GuessPerHate = 100_000;
        }
        return new TownData
        {
            Cast = mistaken ? new[] { Person("Ann", 3), Person("Bob", 5), Person("Cara", 7) }
                : new[] { Person("Ann", 3), Person("Bob", 5) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 20), 6).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[]
            {
                new ActKind("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(), Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen)),
                new ActKind("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(), Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen)),
            }.Concat(ActCatalog.Returns).Concat(ActCatalog.Repair).ToArray(),
            Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
            Perception = mistaken ? new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 } : new PerceptionOptions(),
            Familiarity = mistaken ? new[] { ("Ann", "Bob", 0.0), ("Ann", "Cara", 1.0) } : Array.Empty<(string, string, double)>(),
        };
    }

    private static async Task<SimResult> Run(string firstKind, string response, bool mistaken = false)
    {
        // Seed 2 selects the older of the two eligible memories.
        var sim = new Simulation(2, Town(mistaken));
        sim.Place(new[]
        {
            new Scenario("older", firstKind, firstKind == "Argued" ? "Ann" : "Bob", firstKind == "Argued" ? "Bob" : "Ann", Day: 0, From: 600, To: 601),
            new Scenario("newer", "GaveGift", mistaken ? "Cara" : "Bob", "Ann", Day: 0, From: 620, To: 621),
        });
        sim.ConfigureReflection(new ReflectionOptions { Enabled = true, QuietMinutes = 30, DailyChance = 1 });
        return await sim.RunAsync(1, new Mind(response));
    }

    private sealed class Mind(string response) : IReflectionMind
    {
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken token = default)
        {
            var answer = await new AuthoredReflectionMind().ReflectAsync(request, token);
            string choice = request.Actor == "Ann" && request.SourceActId == 0 ? response : "reject";
            Assert.Contains(request.Choices, c => c.Id == choice);
            return answer with { Weights = request.Choices.ToDictionary(c => c.Id, c => c.Id == choice ? 1.0 : 0.0) };
        }
    }

    [Fact]
    public async Task ThanksSettlesItsOlderGiftOnBothSidesAndLeavesTheNewerGiftOpen()
    {
        SimResult run = await Run("GaveGift", "thank");
        Act thanks = Assert.Single(run.Acts, a => a.Kind == "Thanked");
        Assert.Equal(("Ann", "Bob", 0), (thanks.Actor, thanks.Target, thanks.About));
        var older = run.LifeEvents.Where(e => e.ActId == 0 && e.Role is LifeRole.Did or LifeRole.Undergone).ToArray();
        Assert.Equal(2, older.Length);
        Assert.All(older, e => Assert.Equal(Outcome.Returned, e.Outcome));
        var newer = run.LifeEvents.Where(e => e.ActId == 1 && e.Role is LifeRole.Did or LifeRole.Undergone).ToArray();
        Assert.Equal(2, newer.Length);
        Assert.All(newer, e => Assert.Equal(Outcome.Open, e.Outcome));
    }

    [Fact]
    public async Task ApologyDoesNotReturnAnUnrelatedReverseGift()
    {
        SimResult run = await Run("Argued", "apologise");
        Act apology = Assert.Single(run.Acts, a => a.Kind == "Apologised");
        Assert.Equal(("Ann", "Bob", 0), (apology.Actor, apology.Target, apology.About));
        Assert.All(run.LifeEvents.Where(e => e.ActId == apology.Id && e.Role is LifeRole.Did or LifeRole.Undergone),
            e => Assert.Equal(Outcome.Accepted, e.Outcome));
        var gift = run.LifeEvents.Where(e => e.ActId == 1 && e.Role is LifeRole.Did or LifeRole.Undergone).ToArray();
        Assert.Equal(2, gift.Length);
        Assert.All(gift, e => Assert.Equal(Outcome.Open, e.Outcome));
    }

    [Fact]
    public async Task MistakenThanksDoesNotSettleTheTrueGiversGiftOrTheBelievedGiversDifferentGift()
    {
        SimResult run = await Run("GaveGift", "thank", mistaken: true);
        Assert.Equal("Bob", run.Acts[0].Actor);
        Assert.Equal("Cara", run.Beliefs["Ann"][0].Actor);
        Act thanks = Assert.Single(run.Acts, a => a.Kind == "Thanked");
        Assert.Equal(("Ann", "Cara", 0), (thanks.Actor, thanks.Target, thanks.About));
        Assert.All(run.LifeEvents.Where(e => e.ActId is 0 or 1 && e.Role is LifeRole.Did or LifeRole.Undergone),
            e => Assert.Equal(Outcome.Open, e.Outcome));
    }
}
