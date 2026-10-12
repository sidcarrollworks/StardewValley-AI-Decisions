using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnderGlass.Minds;
using UnderGlass.Sim;

namespace UnderGlass.ReflectionTrial;

public sealed record CharacterVoicePair(string Id, ReflectionRequest Baseline, ReflectionRequest WithCharacter);
public sealed record VoicePassReceipt(int Pass, int? TextCharacters, bool CompleteScene,
    ResponseAudit Response, string? Error);
public sealed record VoiceReceiptAudit(bool GenerationReceiptPresent, long? GenerationSeed,
    int? GenerationInputTokens, int? GenerationOutputTokens, double? GenerationMilliseconds,
    IReadOnlyList<VoicePassReceipt> Evaluations, string? Error)
{
    public bool Complete => GenerationReceiptPresent && GenerationSeed is not null && Error is null
        && Evaluations.Count == 5 && Evaluations.Select(p => p.Pass).Order().SequenceEqual(Enumerable.Range(1, 5))
        && Evaluations.All(p => p.CompleteScene && p.Error is null
            && p.Response.Verified && !p.Response.HasTruncation);
}
public sealed record CharacterVoiceObservation(string Role, ReflectionRequest Request, ReflectionAnswer Answer,
    IReadOnlyDictionary<string, double> Probabilities, string TopChoice, double ElapsedMilliseconds,
    VoiceReceiptAudit Receipts);
public sealed record CharacterVoiceComparison(string Id, CharacterVoiceObservation Baseline,
    CharacterVoiceObservation WithCharacter);
public sealed record CharacterVoiceTrialResult(string PackHash, long Seed, string LayaModel,
    string GenerationModel, IReadOnlyList<CharacterVoiceComparison> Comparisons, string EvaluationMode = "balanced")
{
    public string Suite => "voices";
    [JsonIgnore]
    public IEnumerable<CharacterVoiceObservation> Observations => Comparisons.SelectMany(p => new[] { p.Baseline, p.WithCharacter });
    public int AnswerCount => Observations.Count();
    public int HybridCount => Observations.Count(o => o.Answer.Backend == "local-llm+laya");
    public int FallbackCount => Observations.Count(o => o.Answer.Backend.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    public int EvaluationCount => Observations.Sum(o => o.Answer.Evaluations?.Count ?? 0);
    public int IncompleteReceiptCount => Observations.Count(o => !o.Receipts.Complete);
    public int TruncatedEvaluationCount => Observations.Sum(o => o.Receipts.Evaluations.Count(e => e.Response.HasTruncation));
    public int UnequalSeedPairCount => Comparisons.Count(p => p.Baseline.Receipts.GenerationSeed is not null
        && p.WithCharacter.Receipts.GenerationSeed is not null
        && p.Baseline.Receipts.GenerationSeed != p.WithCharacter.Receipts.GenerationSeed);
    public int ExitCode => HybridCount != AnswerCount ? 2
        : IncompleteReceiptCount > 0 || TruncatedEvaluationCount > 0 || UnequalSeedPairCount > 0 ? 3 : 0;
}

/// <summary>Paired hypothetical scenes. Only the copied private character pack changes;
/// no simulation action, social outcome or model-quality score is produced.</summary>
public static class CharacterVoiceTrial
{
    public const long Seed = 7;
    public static void ValidateOptions(string suite, bool authored, string? layaUrl,
        string? generationUrl, string? generationModel, string? packPath, string evaluationMode, string? htmlPath = null)
    {
        if (suite != "voices")
        {
            if (generationUrl is not null || generationModel is not null || packPath is not null || htmlPath is not null)
                throw new ArgumentException("--llm-url, --llm-model, --character-inspiration and --html are only used by --suite voices; smoke/social keep generation disabled.");
            return;
        }
        if (authored || string.IsNullOrWhiteSpace(layaUrl) || string.IsNullOrWhiteSpace(generationUrl)
            || string.IsNullOrWhiteSpace(generationModel) || string.IsNullOrWhiteSpace(packPath))
            throw new ArgumentException("--suite voices requires --laya-url, --llm-url, --llm-model and --character-inspiration, with no --authored/--offline option.");
        if (evaluationMode != "balanced")
            throw new ArgumentException("--suite voices requires --evaluation balanced for both arms.");
    }

    public static IReadOnlyList<CharacterVoicePair> Create(CharacterInspirationPack pack)
    {
        var scenes = new[]
        {
            ("Penny", "Pam", "HelpedSomeone", false),
            ("Shane", "Marnie", "GaveGift", false),
            ("Elliott", "Leah", "HelpedSomeone", false),
            ("George", "Evelyn", "Argued", true),
            ("Maru", "Robin", "Argued", true),
            ("Abigail", "Pierre", "Argued", false),
            ("Sam", "Jodi", "Argued", true),
            ("Leah", "Robin", "GaveGift", false),
        };
        return scenes.Select((scene, index) =>
        {
            var (actor, subject, kind, own) = scene;
            var source = new ReflectionSourceFacts(kind, own, kind == "Argued" ? -0.3 : 0.2, 0.15);
            string memory = kind == "Argued"
                ? own ? $"Yesterday, you argued with {subject} (Argued). You took part."
                    : $"Yesterday, {subject} argued with you (Argued). You took part."
                : kind == "GaveGift" ? $"Yesterday, {subject} gave a gift to you (GaveGift). You took part."
                    : $"Yesterday, {subject} helped you (HelpedSomeone). You took part.";
            var lines = ReflectionCatalog.Lines(source, new Temperament(0.5, 0.5, 0.5, 0.5));
            ReflectionChoice[] choices =
            {
                new("gift", "GaveGift", lines["gift"]), new("help", "HelpedSomeone", lines["help"]),
                new("confront", "Argued", lines["confront"]), new("defer", "", lines["defer"]),
                new("reject", "", lines["reject"]),
            };
            string id = $"voice-trial:{Seed}:{index}:{actor}";
            var baseline = new ReflectionRequest(id, 2160, actor, subject, 100 + index, memory,
                "Mood 0.10; regard 0.15. Calm, with time to think.", choices, source);
            baseline = baseline with { Proposal = ReflectionCatalog.Propose(baseline) };
            CharacterInspiration character = pack.ForCharacter(actor, subject, Seed, id)
                ?? throw new ArgumentException($"The voice trial pack needs a profile for {actor}.", nameof(pack));
            return new CharacterVoicePair(actor.ToLowerInvariant(), baseline, baseline with { Character = character });
        }).ToArray();
    }

    public static async Task<CharacterVoiceTrialResult> RunAsync(IReflectionMind mind, CharacterInspirationPack pack,
        string layaModel, string generationModel, CancellationToken cancellationToken = default)
    {
        // The public pack may wrap caller-owned lists. Freeze it before preparing any
        // requests or awaiting a model so the result hash describes the same content.
        CharacterInspirationPack snapshot = CharacterInspirationPack.Read(JsonSerializer.Serialize(pack));
        string packHash = snapshot.ContentHash;
        IReadOnlyList<CharacterVoicePair> pairs = Create(snapshot);
        var results = new List<CharacterVoiceComparison>();
        foreach (CharacterVoicePair pair in pairs)
            results.Add(new(pair.Id, await Observe("baseline", pair.Baseline),
                await Observe("with-character", pair.WithCharacter)));
        return new(packHash, Seed, layaModel, generationModel, results);

        async Task<CharacterVoiceObservation> Observe(string role, ReflectionRequest request)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            ReflectionAnswer answer = await mind.ReflectAsync(request, cancellationToken);
            timer.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            var weights = TrialRunner.Normalize(answer.Weights, request.Choices.Select(c => c.Id));
            string top = weights.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
            return new(role, request, answer, weights, top, timer.Elapsed.TotalMilliseconds, Audit(request, answer));
        }
    }

    public static VoiceReceiptAudit Audit(ReflectionRequest request, ReflectionAnswer answer)
    {
        long? seed = null;
        int? input = null, output = null;
        double? elapsed = null;
        string? error = null;
        bool generation = answer.GenerationPrompt is not null && answer.GenerationResponse is not null;
        if (generation)
        {
            try
            {
                using JsonDocument packet = JsonDocument.Parse(answer.GenerationPrompt!);
                seed = packet.RootElement.GetProperty("seed").GetInt64();
                using JsonDocument response = JsonDocument.Parse(answer.GenerationResponse!);
                JsonElement root = response.RootElement;
                _ = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                    ?? throw new InvalidDataException("Generation receipt has no raw content.");
                if (root.TryGetProperty("seed", out JsonElement returnedSeed) && returnedSeed.GetInt64() != seed)
                    throw new InvalidDataException("Generator response seed differs from its request.");
                if (root.TryGetProperty("usage", out JsonElement usage))
                {
                    if (usage.TryGetProperty("input_tokens", out JsonElement n)) input = n.GetInt32();
                    if (usage.TryGetProperty("output_tokens", out n)) output = n.GetInt32();
                    if (usage.TryGetProperty("elapsed_ms", out n)) elapsed = n.GetDouble();
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException
                or FormatException or OverflowException or InvalidDataException or IndexOutOfRangeException)
            { error = "Could not verify generation receipt: " + ex.Message; }
        }
        var passes = new List<VoicePassReceipt>();
        foreach (ReflectionEvaluation pass in answer.Evaluations ?? Array.Empty<ReflectionEvaluation>())
        {
            int? characters = null;
            bool complete = false;
            string? passError = pass.Error;
            try
            {
                using JsonDocument packet = JsonDocument.Parse(pass.Prompt);
                string state = packet.RootElement.GetProperty("state").GetString()!;
                JsonElement question = packet.RootElement.GetProperty("questions").GetProperty("q");
                JsonElement criteria = question.GetProperty("criteria");
                characters = state.Length + question.GetProperty("instructions").GetString()!.Length
                    + criteria.EnumerateObject().Sum(p => p.Name.Length + p.Value.GetString()!.Length);
                string[] mapped = pass.LabelsToChoiceIds.Values.SelectMany(ids => ids).ToArray();
                bool lines = mapped.Length == request.Choices.Count && mapped.Distinct(StringComparer.Ordinal).Count() == request.Choices.Count
                    && request.Choices.All(c => mapped.Contains(c.Id, StringComparer.Ordinal))
                    && criteria.EnumerateObject().Count() == pass.LabelsToChoiceIds.Count
                    && pass.LabelsToChoiceIds.All(label => criteria.TryGetProperty(label.Key, out JsonElement text)
                        && label.Value.All(id => request.Choices.Any(c => c.Id == id && text.GetString() == $"{(c.Kind.Length == 0 ? "no act" : c.Kind)}: {c.Line}")));
                complete = characters <= ResilientReflectionMind.LayaTextLimit && lines
                    && state.Contains(request.Memory, StringComparison.Ordinal) && state.Contains(request.Context, StringComparison.Ordinal)
                    && state.Contains(answer.Thought, StringComparison.Ordinal)
                    && (request.Character is null || state.Contains(request.Character.EvaluatorCue, StringComparison.Ordinal)
                        && (request.Character.Background is null || state.Contains(request.Character.Background.Text, StringComparison.Ordinal)));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
            { passError = "Could not audit evaluator packet: " + ex.Message; }
            passes.Add(new(pass.Pass, characters, complete, TrialRunner.AuditResponse(answer with { LayaResponse = pass.Response }), passError));
        }
        return new(generation, seed, input, output, elapsed, passes, error);
    }

    public static string Markdown(CharacterVoiceTrialResult result)
    {
        var text = new StringBuilder("# Paired character voice trial\n\n");
        text.AppendLine($"Eight hypothetical scenes; seed {result.Seed}; generator `{Cell(result.GenerationModel)}`; evaluator `{Cell(result.LayaModel)}` ({result.EvaluationMode}). Pack: `{result.PackHash}`.").AppendLine();
        text.AppendLine("Each baseline runs immediately before its character-pack variant. Within each pair, actor, subject, source, mood, memory, choices, authored fallback proposal and request ID are identical. Only Character changes. The same request ID supplies the generator seed. Both arms use fixed neutral current context and five complete catalog lines; these are not recorded town events.").AppendLine();
        text.AppendLine("The adapter's profile branch also adds profile-specific generation instructions and uses compact evaluator framing. This compares the complete previous/new prompting paths, not biography alone. Name hiding is a reading aid, not a blind evaluation.").AppendLine();
        text.AppendLine($"Answers: {result.AnswerCount}; hybrid: {result.HybridCount}; explicit fallbacks: {result.FallbackCount}; evaluator passes: {result.EvaluationCount}; incomplete receipts: {result.IncompleteReceiptCount}; truncated evaluations: {result.TruncatedEvaluationCount}; unequal-seed pairs: {result.UnequalSeedPairCount}.");
        if (result.ExitCode != 0) text.AppendLine("**Some requested hybrid calls or receipts were incomplete. Inspect the exact JSON receipts before interpreting the affected pairs.**");
        text.AppendLine().AppendLine("No action is sampled or executed. Proposed act and highest-weight response are distinct. Their labels do not establish that the prose matches either act. This single paired sample does not measure inference noise or assign a voice-quality score; read for cadence, motives, grounding and semantic coherence.");
        foreach (CharacterVoiceComparison pair in result.Comparisons)
        {
            ReflectionRequest request = pair.Baseline.Request;
            CharacterInspiration character = pair.WithCharacter.Request.Character!;
            text.AppendLine().AppendLine($"## {request.Actor} → {request.Subject}").AppendLine();
            text.AppendLine($"Known encounter: {request.Memory}");
            text.AppendLine($"Voice: {character.Voice}");
            text.AppendLine($"Initial own history: {character.Background?.Text ?? "(none)"}");
            text.AppendLine($"Sources: {string.Join("; ", character.Sources.Concat(character.Background?.Sources ?? Array.Empty<string>()))}").AppendLine();
            text.AppendLine("| Arm | Private thought | Proposed act | Highest-weight response | Backend | ms | Receipt audit |");
            text.AppendLine("|---|---|---|---|---|---:|---|");
            foreach (CharacterVoiceObservation arm in new[] { pair.Baseline, pair.WithCharacter })
            {
                string suggested = arm.Request.Choices.FirstOrDefault(c => c.Id == arm.Answer.SuggestedChoice)?.Kind ?? "(unknown)";
                text.AppendLine($"| {arm.Role} | {Cell(arm.Answer.Thought)} | {Cell(suggested)} | {arm.TopChoice} | {Cell(arm.Answer.Backend)} | {arm.ElapsedMilliseconds.ToString("F1", CultureInfo.InvariantCulture)} | {(arm.Receipts.Complete ? "complete" : "incomplete")} |");
                if (arm.Answer.Backend.Contains("fallback", StringComparison.OrdinalIgnoreCase))
                    text.AppendLine($"\n{arm.Role} fallback: {Cell(arm.Answer.Note ?? "Reason unavailable.")}\n");
            }
            text.AppendLine().AppendLine("Complete request, private profile, generation prompt/raw reply, evaluator packets/mappings/raw replies, probabilities and timings are retained in the paired JSON artifact.");
        }
        return text.ToString();
    }

    public static string Html(CharacterVoiceTrialResult result)
    {
        using Stream stream = typeof(CharacterVoiceTrial).Assembly.GetManifestResourceStream("voices.html")
            ?? throw new InvalidOperationException("The character voice viewer template is not embedded.");
        using var reader = new StreamReader(stream);
        return EmbedHtml(reader.ReadToEnd(), result);
    }

    public static string EmbedHtml(string template, CharacterVoiceTrialResult result)
    {
        const string slot = "<script id=\"voice-data\" type=\"application/json\"></script>";
        int position = template.IndexOf(slot, StringComparison.Ordinal);
        if (position < 0 || template.IndexOf(slot, position + slot.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException("Voice viewer needs exactly one empty voice-data JSON slot.");
        // The default encoder escapes HTML-significant characters, including '<', so
        // model text cannot terminate this inert script element or introduce markup.
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return template.Replace(slot, "<script id=\"voice-data\" type=\"application/json\">" + json + "</script>", StringComparison.Ordinal);
    }

    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", "", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
}
