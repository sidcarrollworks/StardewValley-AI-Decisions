using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class CharacterInspirationMindTests
{
    private static CharacterInspiration Character(string? background = "I grew up here; my family still lives here.") => new(
        "reviewed-pack-hash", "guarded-helper", "Quiet, plain words. Help readily but resent being taken for granted.",
        "Gentle but guarded; kindness is not an obligation.", background is null ? null : new CharacterBackground(
            "family", background, Array.Empty<string>(), new[] { "Characters/Dialogue/Penny:family" }),
        new[] { "Characters/Dialogue/Penny:voice" }, "Reviewed before-farmer history.");

    private static ReflectionRequest Request(bool ownHarm = false) => new(
        "profile-packet", 1800, "Penny", "Robin", 7,
        ownHarm ? "At d1 12:00, you argued with Robin (Argued). You took part."
            : "At d1 12:00, you remember Robin gave a gift to you (GaveGift); confidence 1.00.",
        "Penny, age 25: mood 0.10; regard for Robin 0.30. Bold 0.20; understanding 0.80; sensitive 0.80; self-regard 0.40; expression 0.30. Quiet free time. Choose for this person, not for a well-behaved town.",
        Choices(ownHarm), new ReflectionSourceFacts(ownHarm ? "Argued" : "GaveGift", ownHarm, ownHarm ? -0.2 : 0.2, 0.3),
        Character: Character());

    private static ReflectionChoice[] Choices(bool ownHarm)
    {
        var source = new ReflectionSourceFacts(ownHarm ? "Argued" : "GaveGift", ownHarm, ownHarm ? -0.2 : 0.2, 0.3);
        var lines = ReflectionCatalog.Lines(source, new Temperament(0.8, 0.8, 0.6, 0.6, Expression: 0.8));
        string extra = ownHarm ? "apologise" : "thank";
        return new Dictionary<string, string>
        {
            ["gift"] = "GaveGift", ["help"] = "HelpedSomeone", ["confront"] = "Argued",
            [extra] = ownHarm ? "Apologised" : "Thanked", ["defer"] = "", ["reject"] = "",
        }.Select(k => new ReflectionChoice(k.Key, k.Value, lines[k.Key])).ToArray();
    }

    [Theory]
    [InlineData(false, ReflectionEvaluationMode.Raw)]
    [InlineData(false, ReflectionEvaluationMode.Balanced)]
    [InlineData(true, ReflectionEvaluationMode.Raw)]
    [InlineData(true, ReflectionEvaluationMode.Balanced)]
    public async Task GeneratorAndEvaluatorGetSameOwnHistoryWithFullSixLinePacket(bool ownHarm, ReflectionEvaluationMode mode)
    {
        using var handler = new Handler();
        using var mind = new ResilientReflectionMind(handler, Options(mode));
        ReflectionRequest request = Request(ownHarm) with { Opportunity = new ReflectionOpportunity("dream", 1200, 1800) };
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("local-llm+laya", answer.Backend);
        using var generator = JsonDocument.Parse(answer.GenerationPrompt!);
        string userJson = generator.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        using var context = JsonDocument.Parse(userJson);
        Assert.Equal(request.Character!.Voice, context.RootElement.GetProperty("privateVoice").GetString());
        Assert.Equal(180, generator.RootElement.GetProperty("max_tokens").GetInt32());
        string instructions = generator.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("not a script to repeat", instructions);
        Assert.Contains("earlier imagined idea are imagination, not evidence", instructions);
        Assert.Contains("subject's name when their pronouns are not supplied", instructions);
        Assert.Equal(request.Character.Background!.Text, context.RootElement.GetProperty("initialSelfKnowledge").GetString());
        Assert.False(context.RootElement.TryGetProperty("packHash", out _));
        Assert.False(context.RootElement.TryGetProperty("sources", out _));
        Assert.Equal(mode == ReflectionEvaluationMode.Balanced ? 6 : 1, answer.Evaluations!.Count);
        foreach (ReflectionEvaluation pass in answer.Evaluations)
        {
            using var packet = JsonDocument.Parse(pass.Prompt);
            string state = packet.RootElement.GetProperty("state").GetString()!;
            Assert.Contains(request.Character.Background.Text, state);
            Assert.Contains(request.Character.EvaluatorCue, state);
            Assert.Contains(request.Memory, state);
            Assert.Contains("Waking dream idea; not an event.", state);
            Assert.Contains("mood 0.10; regard 0.30", state);
            AssertPacket(packet, request, pass);
        }
    }

    [Fact]
    public async Task ProfileDoesNotHidePriorImaginedIdeaOrReasonToReconsider()
    {
        using var handler = new Handler();
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced));
        ReflectionRequest request = Request(true) with
        {
            Continuity = new ReflectionContinuity("root", "prior", 1, 4,
                "I could help Robin, but I wasn't ready to say anything.", "Later known Argue encounter with Robin."),
        };
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("local-llm+laya", answer.Backend);
        foreach (ReflectionEvaluation pass in answer.Evaluations!)
        {
            using var packet = JsonDocument.Parse(pass.Prompt);
            string state = packet.RootElement.GetProperty("state").GetString()!;
            Assert.Contains(request.Continuity.PriorThought, state);
            Assert.Contains(request.Continuity.Reason, state);
            AssertPacket(packet, request, pass);
        }
    }

    [Fact]
    public async Task OversizedProfilePacketFallsBackInsteadOfLosingInitialHistoryOrCandidateWords()
    {
        using var handler = new Handler();
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Raw));
        ReflectionRequest request = Request() with
        {
            Choices = new[] { new ReflectionChoice("help", "HelpedSomeone", new string('x', 1100)) },
        };
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("Required personal history", answer.Note);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GenericArchetypeDoesNotInventABackgroundField()
    {
        using var handler = new Handler();
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Raw));
        ReflectionAnswer answer = await mind.ReflectAsync(Request() with { Character = Character(null) });
        Assert.Equal("local-llm+laya", answer.Backend);
        using var generator = JsonDocument.Parse(answer.GenerationPrompt!);
        using var user = JsonDocument.Parse(generator.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.True(user.RootElement.TryGetProperty("privateVoice", out _));
        Assert.False(user.RootElement.TryGetProperty("initialSelfKnowledge", out _));
        Assert.DoesNotContain("Initial own history", answer.LayaPrompt);
    }

    private static ReflectionMindOptions Options(ReflectionEvaluationMode mode) => new()
    {
        LayaBaseUrl = "http://127.0.0.1:8000", GenerationBaseUrl = "http://127.0.0.1:8080",
        GenerationModel = "test", EvaluationMode = mode,
    };

    private static void AssertPacket(JsonDocument packet, ReflectionRequest request, ReflectionEvaluation pass)
    {
        JsonElement question = packet.RootElement.GetProperty("questions").GetProperty("q"), criteria = question.GetProperty("criteria");
        foreach (var mapping in pass.LabelsToChoiceIds)
        {
            ReflectionChoice original = request.Choices.Single(c => c.Id == mapping.Value[0]);
            Assert.Equal($"{(original.Kind.Length == 0 ? "no act" : original.Kind)}: {original.Line}", criteria.GetProperty(mapping.Key).GetString());
        }
        int chars = packet.RootElement.GetProperty("state").GetString()!.Length + question.GetProperty("instructions").GetString()!.Length
            + criteria.EnumerateObject().Sum(c => c.Name.Length + c.Value.GetString()!.Length);
        Assert.InRange(chars, 1, 1250);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            using var packet = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            string body;
            if (request.RequestUri!.AbsolutePath.EndsWith("chat/completions", StringComparison.Ordinal))
            {
                using var scene = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                string id = scene.RootElement.GetProperty("choices")[0].GetProperty("id").GetString()!;
                body = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(
                    new { thought = "I could give Robin something, though I don't want to make a fuss.", suggestedChoice = id }) } } } });
            }
            else
            {
                var weights = packet.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria")
                    .EnumerateObject().ToDictionary(c => c.Name, _ => 1.0);
                body = JsonSerializer.Serialize(new { answers = new { q = new { probabilities = weights } } });
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
