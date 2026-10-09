using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class ReflectionCatalogTests
{
    private static readonly Temperament Character = new(0.6, 0.6, 0.5, 0.5);

    private static ReflectionRequest Request(bool own = false, double valence = 0.2, double regard = 0.8, string id = "7:631:Ann:0")
    {
        string kind = valence > 0 ? "GaveGift" : valence < 0 ? "Argued" : "SharedAnObservation";
        var source = new ReflectionSourceFacts(kind, own, valence, regard);
        var lines = ReflectionCatalog.Lines(source, Character);
        var kinds = new Dictionary<string, string> { ["gift"] = "GaveGift", ["help"] = "HelpedSomeone", ["confront"] = "Argued", ["defer"] = "", ["reject"] = "" };
        string memory = own ? $"At d0 10:00, you interacted with Bob ({kind}). You took part."
            : $"At d0 10:00, you remember Bob interacting with you ({kind}); confidence 1.00.";
        return new ReflectionRequest(id, 631, "Ann", "Bob", 0, memory,
            $"Ann: quiet free time; regard for Bob {regard:0.00}; boldness 0.60.",
            kinds.Select(k => new ReflectionChoice(k.Key, k.Value, lines[k.Key])).ToArray(), source);
    }

    [Theory]
    [InlineData(false, 0.2, 0.8, "received-kindness-reciprocity", "gift")]
    [InlineData(false, 0.2, -0.8, "received-kindness-suspicion", "confront")]
    [InlineData(true, 0.2, 0.8, "own-kindness-connection", "help")]
    [InlineData(true, 0.2, -0.8, "own-kindness-boundary", "confront")]
    [InlineData(false, -0.3, 0.8, "received-hostility-repair", "gift")]
    [InlineData(false, -0.3, -0.8, "received-hostility-pride", "confront")]
    [InlineData(true, -0.3, 0.8, "own-hostility-regret", "help")]
    [InlineData(true, -0.3, -0.8, "own-hostility-double-down", "confront")]
    public void SourcePerspectiveAndRelationshipChangeTheImaginedMotive(bool own, double valence, double regard,
        string expectedId, string expectedChoice)
    {
        ReflectionProposal p = ReflectionCatalog.Propose(Request(own, valence, regard));
        Assert.Equal(expectedId, p.Id);
        Assert.Equal(expectedChoice, p.SuggestedChoice);
        Assert.Contains("Bob", p.Thought);
        Assert.NotEmpty(p.Tags);
        Assert.DoesNotContain('{', p.Thought);
        Assert.DoesNotContain("Perhaps there is something I could do", p.Thought);
    }

    [Fact]
    public void AmbivalenceCanProduceOpposingPossibilitiesAcrossSeedsWithoutChangingKnownFacts()
    {
        ReflectionRequest q = Request(regard: 0);
        var proposals = Enumerable.Range(0, 32).Select(seed => ReflectionCatalog.Propose(q with { Id = $"{seed}:631:Ann:0" })).ToArray();
        Assert.Contains(proposals, p => p.Id == "received-kindness-reciprocity");
        Assert.Contains(proposals, p => p.Id == "received-kindness-suspicion");
        Assert.All(proposals, p => Assert.Contains("Bob", p.Thought));
    }

    [Fact]
    public void SeedAndChoiceOrderDoNotChangeAnOtherwiseIdenticalProposal()
    {
        ReflectionRequest q = Request(regard: 0);
        ReflectionProposal a = ReflectionCatalog.Propose(q);
        ReflectionProposal b = ReflectionCatalog.Propose(q with { Choices = q.Choices.Reverse().ToArray() });
        Assert.Equal((a.Id, a.Thought, a.SuggestedChoice), (b.Id, b.Thought, b.SuggestedChoice));
        Assert.Equal(a.Tags, b.Tags);
    }

    [Fact]
    public void UnknownAndUnavailableKindsUseTheSuppliedMemoryWithoutInventingAnAct()
    {
        ReflectionRequest q = Request(valence: 0) with
        {
            Memory = "Bob showed me the repaired fence.",
            Context = "Unrelated confidential world fact: Cara stole an apple.",
            Choices = new[] { new ReflectionChoice("only-available-act", "HelpedSomeone", "Could I give you a hand?") },
        };
        foreach (ReflectionRequest request in new[] { q, q with { Source = new ReflectionSourceFacts("GaveGift", false, 0.2, 0.9) } })
        {
            ReflectionProposal p = ReflectionCatalog.Propose(request);
            Assert.Equal("neutral-known-encounter", p.Id);
            Assert.Equal("only-available-act", p.SuggestedChoice);
            Assert.Contains(q.Memory, p.Thought);
            Assert.DoesNotContain("Cara", p.Thought);
            Assert.DoesNotContain("apple", p.Thought);
        }
    }

    [Fact]
    public void KindAndHostileMemoriesOfferDifferentCompleteResponses()
    {
        var kind = ReflectionCatalog.Lines(new ReflectionSourceFacts("GaveGift", false, 0.2, 0), Character);
        var hostile = ReflectionCatalog.Lines(new ReflectionSourceFacts("Argued", false, -0.3, 0), Character);
        var own = ReflectionCatalog.Lines(new ReflectionSourceFacts("Argued", true, -0.3, 0), Character);

        Assert.Contains("what you did", kind["gift"]);
        Assert.DoesNotContain("hurt", kind["gift"]);
        Assert.DoesNotContain("still upset", kind["confront"]);
        Assert.Contains("treated me", hostile["confront"]);
        Assert.Contains("treated you", own["help"]);
        Assert.NotEqual(kind["confront"], hostile["confront"]);
        Assert.All(kind.Values.Concat(hostile.Values).Concat(own.Values), line =>
        {
            Assert.InRange(line.Length, 1, 120);
            Assert.DoesNotContain('{', line);
        });
    }

    [Fact]
    public void ReservedVoicesKeepTheSameActionsWithDifferentAuthoredLines()
    {
        var source = new ReflectionSourceFacts("GaveGift", false, 0.2, 0);
        var reserved = ReflectionCatalog.Lines(source, Character with { Boldness = 0.2, Chattiness = 0.2, Expression = 0.2 });
        var expressive = ReflectionCatalog.Lines(source, Character with { Boldness = 0.8, Chattiness = 0.8, Expression = 0.8 });
        Assert.Equal(reserved.Keys, expressive.Keys);
        foreach (string act in new[] { "gift", "help", "confront" }) Assert.NotEqual(reserved[act], expressive[act]);
        Assert.Equal(reserved["defer"], expressive["defer"]);
        Assert.Equal(reserved["reject"], expressive["reject"]);
    }

    [Fact]
    public async Task AuthoredWeightsFavorTheProposalRatherThanTheFirstChoice()
    {
        ReflectionRequest q = Request(regard: -0.8);
        ReflectionAnswer a = await new AuthoredReflectionMind().ReflectAsync(q);
        Assert.Equal("gift", q.Choices[0].Id);
        Assert.Equal("confront", a.SuggestedChoice);
        Assert.Equal(3, a.Weights["confront"]);
        Assert.Equal(1, a.Weights["gift"]);
        Assert.Contains("received-kindness-suspicion", a.Note);
    }

    [Fact]
    public async Task APreparedProposalCanBeOverriddenWithoutChangingTheSceneOrCandidateLines()
    {
        ReflectionRequest q = Request();
        q = q with { Proposal = ReflectionCatalog.Propose(q) };
        var alternate = new ReflectionProposal("probe-autonomy", "I could assert my independence instead of returning the gesture.",
            "confront", new[] { "probe", "autonomy" });
        ReflectionRequest changed = q with { Proposal = alternate };
        ReflectionAnswer first = await new AuthoredReflectionMind().ReflectAsync(q);
        ReflectionAnswer second = await new AuthoredReflectionMind().ReflectAsync(changed);

        Assert.Same(q.Source, changed.Source);
        Assert.Same(q.Choices, changed.Choices);
        Assert.Equal(q.Memory, changed.Memory);
        Assert.Equal("gift", first.SuggestedChoice);
        Assert.Equal(alternate.Thought, second.Thought);
        Assert.Equal("confront", second.SuggestedChoice);
        Assert.True(second.Weights["confront"] > second.Weights["gift"]);
    }

    [Fact]
    public async Task TapeChecksStructuredFactsAndProposalButAcceptsSerializedCopiesOfTags()
    {
        ReflectionRequest q = Request();
        q = q with { Proposal = ReflectionCatalog.Propose(q) };
        ReflectionAnswer answer = await new AuthoredReflectionMind().ReflectAsync(q);
        var tape = new RecordedReflectionMind(new[] { new ReflectionRecord(q, answer, "gift", Array.Empty<ReflectionEvent>()) });
        ReflectionRequest copied = JsonSerializer.Deserialize<ReflectionRequest>(JsonSerializer.Serialize(q))!;
        Assert.Equal(answer, await tape.ReflectAsync(copied));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Source = q.Source! with { OwnDeed = true } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Source = q.Source! with { Kind = "Argued" } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Proposal = q.Proposal! with { Thought = "A different motive." } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Proposal = q.Proposal! with { Tags = new[] { "edited" } } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.ReflectAsync(q with { Source = null, Proposal = null }));
    }

    [Fact]
    public async Task EveryContextualPacketFitsTheRealAdapterBudgetWithCompleteLines()
    {
        var requests = (from own in new[] { false, true }
                        from valence in new[] { -0.3, 0.0, 0.2 }
                        from regard in new[] { -0.8, 0.8 }
                        select Request(own, valence, regard)).ToArray();
        using var handler = new EchoWeights();
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions { LayaBaseUrl = "http://localhost:8000" });
        foreach (ReflectionRequest raw in requests)
        {
            ReflectionRequest q = raw with { Proposal = ReflectionCatalog.Propose(raw) };
            ReflectionAnswer answer = await mind.ReflectAsync(q);
            Assert.Equal("authored+laya", answer.Backend);
            Assert.Equal(q.Proposal.Thought, answer.Thought);
            using JsonDocument doc = JsonDocument.Parse(answer.LayaPrompt!);
            JsonElement root = doc.RootElement, question = root.GetProperty("questions").GetProperty("q");
            var criteria = question.GetProperty("criteria").EnumerateObject().ToArray();
            int length = root.GetProperty("state").GetString()!.Length + question.GetProperty("instructions").GetString()!.Length
                + criteria.Sum(p => p.Name.Length + p.Value.GetString()!.Length);
            Assert.InRange(length, 1, 1250);
            foreach (ReflectionChoice c in q.Choices)
                Assert.EndsWith(c.Line, criteria.Single(p => p.Name == c.Id).Value.GetString());
        }
        Assert.Equal(requests.Length, handler.Calls);
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
}
