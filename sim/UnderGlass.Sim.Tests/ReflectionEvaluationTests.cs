using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class ReflectionEvaluationTests
{
    private static ReflectionRequest Request() => new("trial", 600, "Ann", "Bob", 4,
        "Bob mocked Ann's work yesterday.", "Ann is reserved but wants Bob to understand why she was hurt.",
        new[]
        {
            new ReflectionChoice("challenge", "Argued", "What happened hurt. Could we speak privately?"),
            new ReflectionChoice("help", "HelpedSomeone", "Let me give you a hand."),
            new ReflectionChoice("later", "", "I need more time to think."),
        }, Proposal: new ReflectionProposal("prepared", "I could help Bob, or first tell him what is still bothering me.", "help", new[] { "test" }));

    private static ReflectionMindOptions Options(ReflectionEvaluationMode mode) => new()
    {
        LayaBaseUrl = "http://127.0.0.1:8000",
        EvaluationMode = mode,
    };

    [Theory]
    [InlineData(ReflectionEvaluationMode.Canonical)]
    [InlineData(ReflectionEvaluationMode.Balanced)]
    public async Task SameMeaningProducesIdenticalPacketsDespiteInputOrderAndOriginalLabels(ReflectionEvaluationMode mode)
    {
        var packets = new List<string>();
        using var handler = new Handler(async (request, _) =>
        {
            string packet = await request.Content!.ReadAsStringAsync();
            packets.Add(packet);
            return Weighted(packet);
        });
        using var mind = new ResilientReflectionMind(handler, Options(mode));
        ReflectionRequest original = Request();
        ReflectionAnswer first = await mind.ReflectAsync(original);
        string[] originalPackets = packets.ToArray();
        packets.Clear();
        var renamed = original.Choices.Reverse().Select(c => c with { Id = "other-" + c.Id }).ToArray();
        ReflectionRequest changed = original with
        {
            Choices = renamed,
            Proposal = original.Proposal! with { SuggestedChoice = "other-help" },
        };
        ReflectionAnswer second = await mind.ReflectAsync(changed);

        Assert.Equal(originalPackets, packets);
        Assert.Equal(mode == ReflectionEvaluationMode.Balanced ? 3 : 1, packets.Count);
        foreach (ReflectionChoice choice in original.Choices)
            Assert.Equal(first.Weights[choice.Id], second.Weights["other-" + choice.Id]);
        Assert.All(second.Evaluations!, e =>
        {
            Assert.Null(e.Error);
            Assert.NotNull(e.Response);
            Assert.Equal(new[] { "o0", "o1", "o2" }, e.LabelsToChoiceIds.Keys);
            Assert.All(e.LabelsToChoiceIds.Values.SelectMany(ids => ids), id => Assert.StartsWith("other-", id));
        });
    }

    [Theory]
    [InlineData(ReflectionEvaluationMode.Canonical)]
    [InlineData(ReflectionEvaluationMode.Balanced)]
    public async Task MovingTheSameLineBetweenCallerIdsDoesNotChangeItsProbability(ReflectionEvaluationMode mode)
    {
        using var handler = new Handler(async (request, _) => Weighted(await request.Content!.ReadAsStringAsync()));
        using var mind = new ResilientReflectionMind(handler, Options(mode));
        ReflectionRequest original = Request();
        original = original with
        {
            Choices = original.Choices.Concat(new[] { new ReflectionChoice("public", "Argued", "Everyone should hear what happened.") }).ToArray(),
        };
        ReflectionRequest swapped = original with
        {
            Choices = original.Choices.Select(c => c with
            {
                Line = c.Id == "challenge" ? original.Choices[3].Line : c.Id == "public" ? original.Choices[0].Line : c.Line,
            }).ToArray(),
        };
        ReflectionAnswer first = await mind.ReflectAsync(original);
        ReflectionAnswer second = await mind.ReflectAsync(swapped);

        Assert.Equal(first.Evaluations!.Select(e => e.Prompt), second.Evaluations!.Select(e => e.Prompt));
        Assert.Equal(first.Weights["challenge"], second.Weights["public"]);
        Assert.Equal(first.Weights["public"], second.Weights["challenge"]);
        Assert.Equal(first.Weights["help"], second.Weights["help"]);
    }

    [Fact]
    public async Task BalancedGivesEverySemanticAlternativeEverySlotAndAveragesNormalizedPasses()
    {
        int call = 0;
        using var handler = new Handler(async (request, _) =>
        {
            string prompt = await request.Content!.ReadAsStringAsync();
            call++;
            // Vary score scale heavily between rotations. It must not weight a pass more.
            double scale = call == 1 ? 1e200 : call == 2 ? 1e-100 : 1;
            return Weighted(prompt, slot => (slot + 1) * scale);
        });
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());

        Assert.Equal("authored+laya", answer.Backend);
        Assert.Equal(3, call);
        Assert.All(answer.Weights.Values, value => Assert.Equal(1.0 / 3, value, 12));
        Assert.Equal(1, answer.Weights.Values.Sum(), 12);
        Assert.Equal(new[] { 1, 2, 3 }, answer.Evaluations!.Select(e => e.Pass));
        foreach (string originalId in Request().Choices.Select(c => c.Id))
        {
            string[] slots = answer.Evaluations!.Select(e => e.LabelsToChoiceIds.Single(p => p.Value.Contains(originalId)).Key).Order().ToArray();
            Assert.Equal(new[] { "o0", "o1", "o2" }, slots);
        }
        Assert.All(answer.Evaluations!, e => Assert.Equal(1, e.Weights!.Values.Sum(), 12));
        Assert.Equal(answer.Evaluations![^1].Prompt, answer.LayaPrompt);
        Assert.Equal(answer.Evaluations![^1].Response, answer.LayaResponse);
    }

    [Theory]
    [InlineData(ReflectionEvaluationMode.Canonical)]
    [InlineData(ReflectionEvaluationMode.Balanced)]
    public async Task DuplicateAlternativesDoNotAcquireExtraProbabilityMass(ReflectionEvaluationMode mode)
    {
        using var handler = new Handler(async (request, _) => Weighted(await request.Content!.ReadAsStringAsync()));
        using var mind = new ResilientReflectionMind(handler, Options(mode));
        ReflectionRequest original = Request();
        ReflectionAnswer before = await mind.ReflectAsync(original);
        ReflectionAnswer after = await mind.ReflectAsync(original with
        {
            Choices = original.Choices.Concat(new[] { original.Choices[0] with { Id = "duplicate" } }).Reverse().ToArray(),
        });

        Assert.Equal(before.Evaluations!.Select(e => e.Prompt), after.Evaluations!.Select(e => e.Prompt));
        Assert.Equal(before.Weights["challenge"], after.Weights["challenge"] + after.Weights["duplicate"], 12);
        Assert.Equal(after.Weights["challenge"], after.Weights["duplicate"]);
        Assert.Equal(before.Weights["help"], after.Weights["help"]);
        Assert.Equal(before.Weights["later"], after.Weights["later"]);
        Assert.All(after.Evaluations!, e => Assert.Contains(e.LabelsToChoiceIds.Values, ids => ids.Count == 2));
    }

    [Fact]
    public async Task RawModeRetainsCallerLabelsAndOrderingForCompatibility()
    {
        using var handler = new Handler(async (request, _) => Weighted(await request.Content!.ReadAsStringAsync()));
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Raw));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        ReflectionEvaluation receipt = Assert.Single(answer.Evaluations!);
        using JsonDocument prompt = JsonDocument.Parse(receipt.Prompt);
        Assert.Equal(Request().Choices.Select(c => c.Id), prompt.RootElement.GetProperty("questions").GetProperty("q")
            .GetProperty("criteria").EnumerateObject().Select(p => p.Name));
        Assert.All(receipt.LabelsToChoiceIds, p => Assert.Equal(p.Key, Assert.Single(p.Value)));
        Assert.Equal(ReflectionEvaluationMode.Raw, new ReflectionMindOptions().EvaluationMode);
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("malformed")]
    [InlineData("http")]
    public async Task FailureAfterAPartialEvaluationDiscardsAllModelWeightsAndKeepsEveryAttempt(string failure)
    {
        int call = 0;
        using var handler = new Handler(async (request, _) =>
        {
            string prompt = await request.Content!.ReadAsStringAsync();
            call++;
            if (call == 1) return Weighted(prompt);
            if (failure == "malformed") return Text("not JSON");
            if (failure == "http") return Text("local server failed", HttpStatusCode.ServiceUnavailable);
            return Weighted(prompt, truncated: true);
        });
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced));
        ReflectionAnswer answer = await mind.ReflectAsync(Request());

        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Equal(2, call);
        Assert.Equal(3, answer.Weights["help"]); // Authored proposal's unnormalized fallback weights.
        Assert.Equal(2, answer.Evaluations!.Count);
        Assert.NotNull(answer.Evaluations[0].Weights);
        Assert.Null(answer.Evaluations[0].Error);
        Assert.Null(answer.Evaluations[1].Weights);
        Assert.NotNull(answer.Evaluations[1].Error);
        Assert.NotNull(answer.Evaluations[1].Response);
        if (failure == "malformed") Assert.Equal("not JSON", answer.Evaluations[1].Response);
        if (failure == "http") Assert.Equal("local server failed", answer.Evaluations[1].Response);
    }

    [Fact]
    public async Task OneOverallDeadlineCancelsALaterPassWithoutWaitingForItsPerRequestTimeout()
    {
        int call = 0;
        bool transportCancelled = false;
        using var handler = new Handler(async (request, token) =>
        {
            call++;
            if (call == 1) return Weighted(await request.Content!.ReadAsStringAsync(token));
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { transportCancelled = true; throw; }
            return Text("{}");
        });
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced) with
        {
            Timeout = TimeSpan.FromSeconds(5),
            EvaluationTimeout = TimeSpan.FromMilliseconds(150),
        });
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.True(transportCancelled);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("evaluation timed out", answer.Note);
        Assert.Contains("all 3 passes", answer.Note);
        Assert.Equal(2, call);
        Assert.Equal(2, answer.Evaluations!.Count);
    }

    [Fact]
    public async Task CallerCancellationDuringALaterPassStillPropagates()
    {
        int call = 0;
        using var source = new CancellationTokenSource();
        using var handler = new Handler(async (request, token) =>
        {
            call++;
            if (call == 1) return Weighted(await request.Content!.ReadAsStringAsync(token));
            source.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Text("{}");
        });
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mind.ReflectAsync(Request(), source.Token));
        Assert.Equal(2, call);
    }

    [Fact]
    public async Task HybridGeneratesOnlyOnceAndEveryRotationEvaluatesTheSameGeneratedThought()
    {
        int generationCalls = 0, evaluationCalls = 0;
        const string thought = "Perhaps I could offer help and leave the argument for later.";
        using var handler = new Handler(async (request, token) =>
        {
            string body = await request.Content!.ReadAsStringAsync(token);
            if (request.RequestUri!.Port == 1234)
            {
                generationCalls++;
                using JsonDocument prompt = JsonDocument.Parse(body);
                using JsonDocument context = JsonDocument.Parse(prompt.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                string label = context.RootElement.GetProperty("choices").EnumerateArray().Single(c => c.GetProperty("kind").GetString() == "HelpedSomeone")
                    .GetProperty("id").GetString()!;
                return Text(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(new { thought, suggestedChoice = label }) } } } }));
            }
            evaluationCalls++;
            return Weighted(body);
        });
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced) with
        {
            GenerationBaseUrl = "http://127.0.0.1:1234",
            GenerationModel = "local-test",
        });
        ReflectionAnswer answer = await mind.ReflectAsync(Request());
        Assert.Equal("local-llm+laya", answer.Backend);
        Assert.Equal(1, generationCalls);
        Assert.Equal(3, evaluationCalls);
        Assert.Equal(thought, answer.Thought);
        Assert.Equal("help", answer.SuggestedChoice);
        Assert.All(answer.Evaluations!, evaluation =>
        {
            using JsonDocument prompt = JsonDocument.Parse(evaluation.Prompt);
            Assert.Contains(thought, prompt.RootElement.GetProperty("state").GetString());
        });
    }

    [Fact]
    public void InvalidEvaluationOptionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResilientReflectionMind(Options((ReflectionEvaluationMode)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResilientReflectionMind(Options(ReflectionEvaluationMode.Balanced) with { EvaluationTimeout = TimeSpan.Zero }));
    }

    [Fact]
    public async Task ReconsiderationIncludesEarlierImaginationSeparatelyFromTheNewKnownMemory()
    {
        ReflectionRequest request = Request() with
        {
            Continuity = new ReflectionContinuity("root", "earlier", 1, 2,
                "I could give Bob a gift to leave a way back.", "Later known Argue encounter with Bob."),
        };
        using var handler = new Handler(async (http, _) => Weighted(await http.Content!.ReadAsStringAsync()));
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced));
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored+laya", answer.Backend);
        Assert.All(answer.Evaluations!, e =>
        {
            using JsonDocument prompt = JsonDocument.Parse(e.Prompt);
            string state = prompt.RootElement.GetProperty("state").GetString()!;
            Assert.Contains("Earlier imagined idea (not evidence): " + request.Continuity.PriorThought, state);
            Assert.Contains("Reason to reconsider: " + request.Continuity.Reason, state);
            Assert.Contains("Known memory: " + request.Memory, state);
            Assert.DoesNotContain("earlier", state); // No private bookkeeping IDs enter the prompt.
        });
    }

    [Fact]
    public async Task EarlierImaginationCannotSilentlyDisplaceRequiredGrounding()
    {
        ReflectionRequest request = Request() with
        {
            Continuity = new ReflectionContinuity("root", "earlier", 1, 2, new string('x', 1250), "A new encounter."),
        };
        using var handler = new Handler((_, _) => throw new InvalidOperationException("No HTTP packet should fit."));
        using var mind = new ResilientReflectionMind(handler, Options(ReflectionEvaluationMode.Balanced));
        ReflectionAnswer answer = await mind.ReflectAsync(request);
        Assert.Equal("authored-fallback", answer.Backend);
        Assert.Contains("earlier imagined idea", answer.Note);
        Assert.Null(answer.Evaluations);
    }

    private static HttpResponseMessage Weighted(string prompt, Func<int, double>? score = null, bool truncated = false)
    {
        using JsonDocument document = JsonDocument.Parse(prompt);
        var weights = document.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria")
            .EnumerateObject().Select((p, i) => (p.Name, Weight: score?.Invoke(i) ?? i + 1))
            .ToDictionary(p => p.Name, p => p.Weight);
        return Text(JsonSerializer.Serialize(new
        {
            answers = new { q = new { probabilities = weights } },
            usage = new { input_tokens = 220, state_tokens_dropped = 0, truncated, truncated_questions = Array.Empty<string>() },
            routing = new { model = "typed-decisions" },
        }));
    }

    private static HttpResponseMessage Text(string content, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
