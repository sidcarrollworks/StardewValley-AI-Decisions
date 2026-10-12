using System.Text.Json;
using UnderGlass.ReflectionTrial;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class CharacterVoiceTrialTests
{
    private static readonly string[] Names = { "Penny", "Shane", "Elliott", "George", "Maru", "Abigail", "Sam", "Leah" };
    private static CharacterInspirationPack Pack() => new(1, "Synthetic voice trial fixture; no game data.",
        new[] { new InspirationArchetype("careful", "Be useful.", "Pause before speaking.", "Plain, direct words.", "Reserved and practical.", new[] { "fixture:voice" }) },
        Names.Select(name => new CharacterInspirationProfile(name, "careful", "",
            new[] { new CharacterBackground("own", $"I used to mend things, {name} remembers.", Array.Empty<string>(), new[] { "fixture:history" }) })).ToArray());

    [Fact]
    public void EightPairsChangeOnlyCharacterAndPreserveExactIdentityAndSources()
    {
        var pairs = CharacterVoiceTrial.Create(Pack());
        Assert.Equal(Names, pairs.Select(p => p.Baseline.Actor));
        Assert.Equal(new[] { "Pam", "Marnie", "Leah", "Evelyn", "Robin", "Pierre", "Jodi", "Robin" }, pairs.Select(p => p.Baseline.Subject));
        Assert.Equal(new[] { false, false, false, true, true, false, true, false }, pairs.Select(p => p.Baseline.Source!.OwnDeed));
        Assert.All(pairs, pair =>
        {
            Assert.Null(pair.Baseline.Character);
            Assert.NotNull(pair.WithCharacter.Character);
            Assert.Equal(pair.Baseline, pair.WithCharacter with { Character = null });
            Assert.Equal(Pack().ContentHash, pair.WithCharacter.Character!.PackHash);
            Assert.Same(pair.Baseline.Choices, pair.WithCharacter.Choices);
            Assert.Same(pair.Baseline.Proposal, pair.WithCharacter.Proposal);
            Assert.Null(pair.Baseline.Opportunity);
            Assert.Null(pair.Baseline.Continuity);
        });
    }

    [Fact]
    public void FullSourceAppropriateCatalogLinesAreUsedWithNoInventedTaskOrItem()
    {
        foreach (var pair in CharacterVoiceTrial.Create(Pack()))
        {
            var request = pair.Baseline;
            var lines = ReflectionCatalog.Lines(request.Source, new Temperament(0.5, 0.5, 0.5, 0.5));
            Assert.Equal(new[] { "gift", "help", "confront", "defer", "reject" }, request.Choices.Select(c => c.Id));
            Assert.Equal(new[] { "GaveGift", "HelpedSomeone", "Argued", "", "" }, request.Choices.Select(c => c.Kind));
            Assert.All(request.Choices, choice => Assert.Equal(lines[choice.Id], choice.Line));
            Assert.Contains("(" + request.Source!.Kind + ")", request.Memory);
            if (request.Source.OwnDeed) Assert.StartsWith("Yesterday, you argued with " + request.Subject, request.Memory);
            else Assert.StartsWith("Yesterday, " + request.Subject, request.Memory);
        }
    }

    [Fact]
    public void MissingCharacterProfileFailsBeforeAnyCallCanRun()
    {
        var pack = Pack();
        Assert.Throws<ArgumentException>(() => CharacterVoiceTrial.Create(pack with { Characters = pack.Characters.Skip(1).ToArray() }));
    }

    [Fact]
    public async Task FakeMindReceivesOnlyControlledDifferenceAndAllReceiptsAreKept()
    {
        var mind = new FakeMind(CompleteAnswer);
        CharacterVoiceTrialResult result = await CharacterVoiceTrial.RunAsync(mind, Pack(), "laya-test", "generator-test");
        Assert.Equal(16, mind.Requests.Count);
        for (int i = 0; i < mind.Requests.Count; i += 2)
            Assert.Equal(mind.Requests[i], mind.Requests[i + 1] with { Character = null });
        Assert.Equal(16, result.AnswerCount);
        Assert.Equal(16, result.HybridCount);
        Assert.Equal(80, result.EvaluationCount);
        Assert.Equal(0, result.FallbackCount);
        Assert.Equal(0, result.IncompleteReceiptCount);
        Assert.Equal(0, result.UnequalSeedPairCount);
        Assert.Equal(0, result.ExitCode);
        Assert.All(result.Observations, observation =>
        {
            Assert.True(observation.Receipts.Complete);
            Assert.NotNull(observation.Answer.GenerationPrompt);
            Assert.NotNull(observation.Answer.GenerationResponse);
            Assert.Equal(5, observation.Answer.Evaluations!.Count);
            Assert.Equal(7, observation.Receipts.GenerationSeed);
            Assert.Equal(0.2, observation.Probabilities["help"]);
        });
        string markdown = CharacterVoiceTrial.Markdown(result);
        Assert.Contains("No action is sampled or executed", markdown);
        Assert.Contains("single paired sample", markdown);
        Assert.Contains("profile-specific generation instructions", markdown);
        Assert.Contains("compact evaluator framing", markdown);
        Assert.Contains("not biography alone", markdown);
        Assert.Contains("Initial own history:", markdown);
        Assert.Contains("Answers: 16; hybrid: 16", markdown);
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using JsonDocument parsed = JsonDocument.Parse(json);
        Assert.Equal("voices", parsed.RootElement.GetProperty("suite").GetString());
        Assert.False(parsed.RootElement.TryGetProperty("observations", out _));
        Assert.False(parsed.RootElement.GetProperty("comparisons")[0].GetProperty("baseline").GetProperty("request").TryGetProperty("character", out _));
        Assert.True(parsed.RootElement.GetProperty("comparisons")[0].GetProperty("withCharacter").GetProperty("request").TryGetProperty("character", out _));
    }

    [Fact]
    public async Task CallerMutationDuringModelCallCannotChangeRecordedPackHash()
    {
        var pack = Pack();
        string originalHash = pack.ContentHash;
        var profiles = (CharacterInspirationProfile[])pack.Characters;
        bool mutated = false;
        var mind = new FakeMind(request =>
        {
            if (!mutated)
            {
                profiles[0] = profiles[0] with { VoiceAccent = "Changed after the trial began." };
                mutated = true;
            }
            return CompleteAnswer(request);
        });

        var result = await CharacterVoiceTrial.RunAsync(mind, pack, "laya-test", "generator-test");

        Assert.NotEqual(originalHash, pack.ContentHash);
        Assert.Equal(originalHash, result.PackHash);
        Assert.All(result.Comparisons, pair => Assert.Equal(result.PackHash, pair.WithCharacter.Request.Character!.PackHash));
        Assert.DoesNotContain("Changed after the trial began.", result.Comparisons[0].WithCharacter.Request.Character!.Voice);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task AuthoredFallbackIsExplicitAndCannotPassAsHybrid()
    {
        var mind = new FakeMind(request => CompleteAnswer(request) with { Backend = "authored-fallback", Note = "HTTP 503", GenerationResponse = null, Evaluations = null });
        var result = await CharacterVoiceTrial.RunAsync(mind, Pack(), "laya-test", "generator-test");
        Assert.Equal(16, result.FallbackCount);
        Assert.Equal(0, result.HybridCount);
        Assert.Equal(16, result.IncompleteReceiptCount);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("HTTP 503", CharacterVoiceTrial.Markdown(result));
    }

    [Fact]
    public async Task MissingBalancedPassOrMismatchedGeneratorSeedFailsReceiptAudit()
    {
        var mind = new FakeMind(request => request.Character is null
            ? CompleteAnswer(request)
            : CompleteAnswer(request) with { GenerationPrompt = "{\"seed\":8}", GenerationResponse = GenerationResponse(8) });
        var result = await CharacterVoiceTrial.RunAsync(mind, Pack(), "laya-test", "generator-test");
        Assert.Equal(8, result.UnequalSeedPairCount);
        Assert.Equal(3, result.ExitCode);
        var request = CharacterVoiceTrial.Create(Pack())[0].Baseline;
        var complete = CompleteAnswer(request);
        Assert.False(CharacterVoiceTrial.Audit(request, complete with { Evaluations = complete.Evaluations!.Take(4).ToArray() }).Complete);
        Assert.False(CharacterVoiceTrial.Audit(request, complete with { Evaluations = Enumerable.Repeat(complete.Evaluations![0], 5).ToArray() }).Complete);
        Assert.NotNull(CharacterVoiceTrial.Audit(request, complete with { GenerationResponse = GenerationResponse(8) }).Error);
    }

    [Fact]
    public async Task TruncatedResponseCannotCountAsCompleteAndCountsEachFailedPass()
    {
        var mind = new FakeMind(request =>
        {
            var answer = CompleteAnswer(request);
            return answer with { Evaluations = answer.Evaluations!.Select(e => e with { Response = LayaResponse(true) }).ToArray() };
        });
        var result = await CharacterVoiceTrial.RunAsync(mind, Pack(), "laya-test", "generator-test");
        Assert.Equal(80, result.TruncatedEvaluationCount);
        Assert.Equal(16, result.IncompleteReceiptCount);
        Assert.Equal(3, result.ExitCode);
    }

    [Fact]
    public void TruncatedLineOrMissingPrivateHistoryFailsAuditDespiteValidJson()
    {
        var request = CharacterVoiceTrial.Create(Pack())[0].WithCharacter;
        var answer = CompleteAnswer(request);
        var missingHistory = answer with { Evaluations = answer.Evaluations!.Select(e => e with
            { Prompt = e.Prompt.Replace(request.Character!.Background!.Text, "omitted", StringComparison.Ordinal) }).ToArray() };
        Assert.False(CharacterVoiceTrial.Audit(request, missingHistory).Complete);
        var missingLine = answer with { Evaluations = answer.Evaluations!.Select(e => e with
            { Prompt = e.Prompt.Replace("I need to think about this another time.", "I need", StringComparison.Ordinal) }).ToArray() };
        Assert.False(CharacterVoiceTrial.Audit(request, missingLine).Complete);
    }

    [Fact]
    public async Task ExternalCancellationStopsBeforeTheNextArm()
    {
        using var cancel = new CancellationTokenSource();
        var mind = new FakeMind(request => { cancel.Cancel(); return CompleteAnswer(request); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CharacterVoiceTrial.RunAsync(mind, Pack(), "laya", "generator", cancel.Token));
        Assert.Single(mind.Requests);
    }

    [Fact]
    public async Task ExistingOfflineSuitesRemainGenerationFreeAndDoNotReceiveCharacterData()
    {
        var received = new FakeMind(request => new ReflectionAnswer(request.Proposal!.Thought,
            request.Proposal.SuggestedChoice, request.Choices.ToDictionary(c => c.Id, _ => 1.0), "authored"));
        var result = await TrialRunner.RunAsync(received, false, "offline");
        Assert.Equal(15, result.Observations.Count());
        Assert.Equal(0, result.ExitCode);
        Assert.All(received.Requests, request => Assert.Null(request.Character));
        Assert.All(result.Observations, observation => Assert.Null(observation.Answer.GenerationPrompt));
        CharacterVoiceTrial.ValidateOptions("smoke", true, null, null, null, null, "raw");
        CharacterVoiceTrial.ValidateOptions("social", false, "http://127.0.0.1:8000", null, null, null, "canonical");
    }

    [Theory]
    [InlineData(true, "http://127.0.0.1:8000", "http://127.0.0.1:8080", "model", "pack.json", "balanced")]
    [InlineData(false, null, "http://127.0.0.1:8080", "model", "pack.json", "balanced")]
    [InlineData(false, "http://127.0.0.1:8000", null, "model", "pack.json", "balanced")]
    [InlineData(false, "http://127.0.0.1:8000", "http://127.0.0.1:8080", "", "pack.json", "balanced")]
    [InlineData(false, "http://127.0.0.1:8000", "http://127.0.0.1:8080", "model", null, "balanced")]
    [InlineData(false, "http://127.0.0.1:8000", "http://127.0.0.1:8080", "model", "pack.json", "raw")]
    public void VoiceOptionsRequireARealHybridConfiguration(bool authored, string? laya, string? generator, string? model, string? pack, string evaluation)
        => Assert.Throws<ArgumentException>(() => CharacterVoiceTrial.ValidateOptions("voices", authored, laya, generator, model, pack, evaluation));

    [Fact]
    public void GenerationOrProfileFlagsCannotSilentlyChangeOldSuites()
    {
        Assert.Throws<ArgumentException>(() => CharacterVoiceTrial.ValidateOptions("smoke", true, null, null, null, "pack.json", "raw"));
        Assert.Throws<ArgumentException>(() => CharacterVoiceTrial.ValidateOptions("social", false, "http://127.0.0.1:8000", "http://127.0.0.1:8080", "model", null, "raw"));
        Assert.Throws<ArgumentException>(() => CharacterVoiceTrial.ValidateOptions("smoke", true, null, null, null, null, "raw", "viewer.html"));
        CharacterVoiceTrial.ValidateOptions("voices", false, "http://127.0.0.1:8000", "http://127.0.0.1:8080", "model", "pack.json", "balanced");
    }

    [Fact]
    public async Task StandaloneHtmlKeepsExactJsonInsideAnInertSlotAndEscapesModelMarkup()
    {
        const string malicious = "</script><script>alert('model text')</script> & <b>hello</b>";
        var result = await CharacterVoiceTrial.RunAsync(new FakeMind(request => CompleteAnswer(request) with { Thought = malicious }), Pack(), "laya", "generator");
        string html = CharacterVoiceTrial.Html(result);
        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<script id=\"voice-data\" type=\"application/json\">", html);
        Assert.DoesNotContain(malicious, html);
        const string marker = "<script id=\"voice-data\" type=\"application/json\">";
        int start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        using JsonDocument embedded = JsonDocument.Parse(html[start..end]);
        Assert.Equal(malicious, embedded.RootElement.GetProperty("comparisons")[0].GetProperty("baseline").GetProperty("answer").GetProperty("thought").GetString());
        Assert.Equal(16, embedded.RootElement.GetProperty("answerCount").GetInt32());
        Assert.Throws<InvalidDataException>(() => CharacterVoiceTrial.EmbedHtml("no slot", result));
        const string slot = "<script id=\"voice-data\" type=\"application/json\"></script>";
        Assert.Throws<InvalidDataException>(() => CharacterVoiceTrial.EmbedHtml(slot + slot, result));
    }

    private sealed class FakeMind(Func<ReflectionRequest, ReflectionAnswer> answer) : IReflectionMind
    {
        public List<ReflectionRequest> Requests { get; } = new();
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    private static string GenerationResponse(long seed) => JsonSerializer.Serialize(new
    {
        seed, choices = new[] { new { message = new { content = "{\"thought\":\"I could offer a hand.\",\"suggestedChoice\":\"help\"}" } } },
        usage = new { input_tokens = 100, output_tokens = 20, elapsed_ms = 50.0 },
    });
    private static string LayaResponse(bool truncated = false) => JsonSerializer.Serialize(new
    {
        usage = new { input_tokens = 200, state_tokens_dropped = 0, truncated, truncated_questions = Array.Empty<string>() },
        routing = new { model = "laya-test" },
    });
    private static ReflectionAnswer CompleteAnswer(ReflectionRequest request)
    {
        const string thought = "I could offer a hand.";
        var criteria = request.Choices.ToDictionary(c => c.Id, c => $"{(c.Kind.Length == 0 ? "no act" : c.Kind)}: {c.Line}");
        string state = thought + "\n" + request.Context + "\n" + request.Memory
            + (request.Character is null ? "" : "\n" + request.Character.EvaluatorCue + "\n" + request.Character.Background?.Text);
        string prompt = JsonSerializer.Serialize(new { state, questions = new { q = new { instructions = "Read the lines.", criteria } } });
        var map = request.Choices.ToDictionary(c => c.Id, c => (IReadOnlyList<string>)new[] { c.Id });
        var weights = request.Choices.ToDictionary(c => c.Id, _ => 1.0);
        var passes = Enumerable.Range(1, 5).Select(i => new ReflectionEvaluation(i, prompt, LayaResponse(), map, weights)).ToArray();
        return new(thought, "help", weights, "local-llm+laya", LayaPrompt: prompt,
            GenerationPrompt: "{\"seed\":7}", LayaResponse: LayaResponse(), Evaluations: passes,
            GenerationResponse: GenerationResponse(7));
    }
}
