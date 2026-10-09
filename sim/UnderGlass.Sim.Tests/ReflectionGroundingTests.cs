using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionGroundingTests
{
    [Theory]
    [InlineData("HelpedSomeone")]
    [InlineData("Complimented")]
    [InlineData("Thanked")]
    public void AQuietGiftFollowingNonGiftKindnessDoesNotInventAnEarlierGift(string kind)
    {
        var source = new ReflectionSourceFacts(kind, true, 0.2, 0.5);
        var quiet = new Temperament(0.5, 0.5, 0.5, 0.5) with { Expression = 0.2 };
        string line = ReflectionCatalog.Lines(source, quiet)["gift"];

        Assert.Equal("I brought you something. No need to make a fuss.", line);
        Assert.DoesNotContain("something else", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnAutonomyResponseToKindnessMakesNoClaimAboutEarlierRequests()
    {
        var source = new ReflectionSourceFacts("HelpedSomeone", false, 0.2, -0.5);
        string line = ReflectionCatalog.Lines(source, new Temperament(0.8, 0.5, 0.5, 0.5))["confront"];

        Assert.Equal("I want to handle this myself. I want to make my own choices.", line);
        Assert.DoesNotContain("didn't ask", line, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(Authority.Warned)]
    [InlineData(Authority.TakenIn)]
    [InlineData(Authority.Questioned)]
    [InlineData(Simulation.Service)]
    [InlineData(Simulation.FamilyRow)]
    public async Task PassiveAuthorityAndFamilyEventsDoNotReverseWhoDidWhatInReflection(string kindName)
    {
        ActKind passive = DefaultTown.Acts().Single(k => k.Name == kindName);
        Assert.Equal(Patient.Actor, passive.Affect!.Patient);
        Assert.Equal(TargetIs.Given, passive.Affect.Target);
        string[] supported = { "GaveGift", "HelpedSomeone", "Argued" };
        FeelingOptions feelings = FeelingOptions.WithDesire();
        feelings.AnswerOn = feelings.ReturnOn = feelings.MakeUpOn = feelings.RetaliateOn = false;
        feelings.AvoidOn = feelings.WithdrawOn = false;
        var town = new TownData
        {
            Cast = new[] { Person("Ann", 3), Person("Bob", 5) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 12), 6).ToArray()) },
            Links = Array.Empty<Link>(),
            Gatherings = Array.Empty<Gathering>(),
            Acts = DefaultTown.Acts().Where(k => supported.Contains(k.Name) || k.Name == kindName).ToArray(),
            Feelings = feelings,
            Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 },
            Gossip = new GossipOptions { ChatChance = 0 },
            Wander = 0,
        };
        var sim = new Simulation(1, town);
        sim.ConfigureReflection(new ReflectionOptions { Enabled = true, DailyChance = 1, QuietMinutes = 30 });
        sim.Place(new[]
        {
            new Scenario("passive-role", kindName, "Ann", "Bob", Day: 0, From: Clock.At(10), To: Clock.At(11)),
            new Scenario("supported-gift", "GaveGift", "Ann", "Bob", Day: 0, From: Clock.At(12), To: Clock.At(13)),
        });
        var mind = new RejectingMind();
        SimResult result = await sim.RunAsync(2, mind);

        Act received = Assert.Single(result.Acts, a => a.Kind == kindName);
        Assert.Equal("Ann", received.Actor); // Ann is the patient; Bob is the official/parent.
        Assert.Equal("Bob", received.Target);
        Belief witnessed = result.Beliefs["Bob"][received.Id];
        Assert.Equal(Source.Witnessed, witnessed.Source);
        Assert.Equal("Ann", witnessed.Actor);
        Assert.Equal("Bob", witnessed.Target);
        Assert.NotEmpty(mind.Requests); // Supported memories still reach the reflection mind.
        Assert.Equal(new[] { "Ann", "Bob" }, mind.Requests.Select(q => q.Actor).Distinct().Order());
        Assert.All(mind.Requests, q =>
        {
            Assert.NotEqual(received.Id, q.SourceActId);
            Assert.Equal("GaveGift", q.Source!.Kind);
            Assert.DoesNotContain(kindName, q.Memory);
        });
    }

    private static Villager Person(string name, int x) => new(name, name, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
        new Dictionary<string, double>(), Array.Empty<string>());

    private sealed class RejectingMind : IReflectionMind
    {
        public List<ReflectionRequest> Requests { get; } = new();
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new ReflectionAnswer(request.Proposal!.Thought, request.Proposal.SuggestedChoice,
                request.Choices.ToDictionary(c => c.Id, c => c.Id == "reject" ? 1.0 : 0.0), "test"));
        }
    }
}
