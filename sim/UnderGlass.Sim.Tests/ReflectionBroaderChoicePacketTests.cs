using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionBroaderChoicePacketTests
{
    [Theory]
    [InlineData(false, ReflectionEvaluationMode.Raw)]
    [InlineData(false, ReflectionEvaluationMode.Balanced)]
    [InlineData(true, ReflectionEvaluationMode.Raw)]
    [InlineData(true, ReflectionEvaluationMode.Balanced)]
    public async Task ContextualThanksAndApologyFitWithAllSixCompleteLines(bool ownHarm, ReflectionEvaluationMode mode)
    {
        string extra = ownHarm ? "apologise" : "thank";
        var source = new ReflectionSourceFacts(ownHarm ? "Argued" : "GaveGift", ownHarm, ownHarm ? -0.2 : 0.2, 0.3);
        var lines = ReflectionCatalog.Lines(source, new Temperament(0.8, 0.8, 0.6, 0.6, Expression: 0.8));
        Assert.Contains(extra, lines.Keys);
        Assert.DoesNotContain(ownHarm ? "thank" : "apologise", lines.Keys);
        var kinds = new Dictionary<string, string>
        {
            ["gift"] = "GaveGift", ["help"] = "HelpedSomeone", ["confront"] = "Argued",
            [extra] = ownHarm ? "Apologised" : "Thanked", ["defer"] = "", ["reject"] = "",
        };
        var entry = ReflectionCatalog.Entries.Single(e => e.Choice == extra);
        string thought = (ownHarm ? entry.Thought : entry.DreamThought!).Replace("{subject}", "Robin");
        var request = new ReflectionRequest("broader-packet", 1800, "Penny", "Robin", 7,
            ownHarm ? "I argued with Robin. This is my own remembered act, not access to Robin's private feelings. I have seen Robin again since thinking about it."
                : "I remember Robin gave me a gift. This is a firsthand memory from before sleep. I do not know what Robin privately intended by that kindness.",
            "Penny: mood 0.10; regard for Robin 0.30; quiet, sensitive and reluctant to argue in public. I can consider a response without assuming forgiveness.",
            kinds.Select(k => new ReflectionChoice(k.Key, k.Value, lines[k.Key])).ToArray(), source,
            new ReflectionProposal(entry.Id, thought, extra, entry.Tags),
            ownHarm ? new ReflectionContinuity("root", "prior", 1, 4,
                "I could try to make things easier between us, but I was not ready to decide what to say.", "A later known encounter with Robin.") : null,
            ownHarm ? null : new ReflectionOpportunity("dream", 1320, 1800));
        using var handler = new WeightsHandler();
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions
        {
            LayaBaseUrl = "http://127.0.0.1:8000", EvaluationMode = mode,
        });
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored+laya", answer.Backend);
        Assert.Equal(mode == ReflectionEvaluationMode.Balanced ? 6 : 1, handler.Calls);
        Assert.Equal(request.Choices.Select(c => c.Id).Order(), answer.Weights.Keys.Order());
        Assert.All(answer.Evaluations!, evaluation =>
        {
            using var packet = JsonDocument.Parse(evaluation.Prompt);
            var root = packet.RootElement;
            string state = root.GetProperty("state").GetString()!;
            var question = root.GetProperty("questions").GetProperty("q");
            var criteria = question.GetProperty("criteria");
            Assert.Contains(thought, state);
            Assert.Contains(request.Memory[..99], state);
            Assert.Contains(request.Context[..99], state);
            Assert.Contains(ownHarm ? "Earlier imagined idea (not evidence)" : "dream idea is imagination, not evidence", state);
            Assert.Equal(6, criteria.EnumerateObject().Count());
            foreach (var mapping in evaluation.LabelsToChoiceIds)
            {
                var choice = request.Choices.Single(c => c.Id == Assert.Single(mapping.Value));
                Assert.Equal($"{(choice.Kind.Length == 0 ? "no act" : choice.Kind)}: {choice.Line}", criteria.GetProperty(mapping.Key).GetString());
            }
            int chars = state.Length + question.GetProperty("instructions").GetString()!.Length
                + criteria.EnumerateObject().Sum(p => p.Name.Length + p.Value.GetString()!.Length);
            Assert.InRange(chars, 1, ResilientReflectionMind.LayaTextLimit);
        });
    }

    private sealed class WeightsHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            using var packet = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var weights = packet.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria")
                .EnumerateObject().ToDictionary(p => p.Name, _ => 1.0);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { answers = new { q = new { probabilities = weights } } }), Encoding.UTF8, "application/json"),
            };
        }
    }
}
