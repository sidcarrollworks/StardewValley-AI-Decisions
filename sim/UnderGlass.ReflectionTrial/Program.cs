using System.Globalization;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.ReflectionTrial;
using UnderGlass.Sim;

string? layaUrl = null;
string? generationUrl = null, generationModel = null, characterPackPath = null;
string? voiceCasesPath = null, dialogueCorpusPath = null, dialogueManifestPath = null;
string? htmlPath = null;
string model = "typed-decisions";
string evaluationMode = "raw", suite = "smoke";
string outputPath = "reflection-trial.json", reportPath = "reflection-trial.md";
bool authored = false;
bool evaluationSpecified = false;
int timeoutMilliseconds = 5000;
try
{
    for (int i = 0; i < args.Length; i++)
    {
        string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
        switch (args[i])
        {
            case "--authored": case "--offline": authored = true; break;
            case "--laya-url": layaUrl = Next(); break;
            case "--laya-model": case "--model": model = Next(); break;
            case "--evaluation": evaluationMode = Next(); evaluationSpecified = true; break;
            case "--suite": suite = Next(); break;
            case "--llm-url": generationUrl = Next(); break;
            case "--llm-model": generationModel = Next(); break;
            case "--character-inspiration": characterPackPath = Next(); break;
            case "--voice-cases": voiceCasesPath = Next(); break;
            case "--dialogue-corpus": dialogueCorpusPath = Next(); break;
            case "--dialogue-manifest": dialogueManifestPath = Next(); break;
            case "--timeout-ms": timeoutMilliseconds = int.Parse(Next(), CultureInfo.InvariantCulture); break;
            case "--out": outputPath = Next(); break;
            case "--report": reportPath = Next(); break;
            case "--html": htmlPath = Next(); break;
            case "--help":
                Console.WriteLine("ReflectionTrial (--laya-url <loopback base URL> [--model typed-decisions] | --authored) [--evaluation raw|canonical|balanced] [--suite smoke|social|voices] [--timeout-ms 5000] [--out reflection-trial.json] [--report reflection-trial.md]\nVoices requires --laya-url, --llm-url <loopback base URL>, --llm-model <model>, --character-inspiration <pack.json>; evaluation defaults to and requires balanced.\nWording: --suite wording --llm-url <loopback base URL> --llm-model <model> --character-inspiration <pack.json> --voice-cases <cases.json> --dialogue-corpus <corpus.json> [--dialogue-manifest <raw-assets.json>]. Holds scene/intention fixed; compares profile-only and dialogue-reference wording with two matching seeds. No evaluator or action selection.\nOptional --html comparison.html embeds the corresponding comparison viewer. Smoke/social keep generation disabled.");
                return 0;
            default: throw new ArgumentException($"Unknown option {args[i]}.");
        }
    }
    if (suite == "wording")
    {
        if (authored || layaUrl is not null || evaluationSpecified || model != "typed-decisions")
            throw new ArgumentException("Wording measures generation only: omit --authored, --laya-url, --model and --evaluation.");
        if (string.IsNullOrWhiteSpace(generationUrl) || string.IsNullOrWhiteSpace(generationModel)
            || string.IsNullOrWhiteSpace(characterPackPath) || string.IsNullOrWhiteSpace(voiceCasesPath)
            || string.IsNullOrWhiteSpace(dialogueCorpusPath))
            throw new ArgumentException("Wording requires --llm-url, --llm-model, --character-inspiration, --voice-cases and --dialogue-corpus.");
    }
    else
    {
        if (voiceCasesPath is not null || dialogueCorpusPath is not null || dialogueManifestPath is not null)
            throw new ArgumentException("--voice-cases, --dialogue-corpus and --dialogue-manifest require --suite wording.");
        if (suite == "voices" && !evaluationSpecified) evaluationMode = "balanced";
        CharacterVoiceTrial.ValidateOptions(suite, authored, layaUrl, generationUrl, generationModel, characterPackPath, evaluationMode, htmlPath);
    }
    if (suite != "wording" && !authored && layaUrl is null)
        throw new ArgumentException("Specify --laya-url for real local evaluation, or --authored for an explicitly offline plumbing run.");
    if (authored && layaUrl is not null)
        throw new ArgumentException("Choose --authored or --laya-url; mixing them would make the intended backend ambiguous.");
    if (evaluationMode is not ("raw" or "canonical" or "balanced"))
        throw new ArgumentException("--evaluation raw|canonical|balanced");
    if (suite is not ("smoke" or "social" or "voices" or "wording"))
        throw new ArgumentException("--suite smoke|social|voices|wording");
    if (timeoutMilliseconds <= 0) throw new ArgumentException("--timeout-ms must be positive.");
    outputPath = Path.GetFullPath(outputPath);
    reportPath = Path.GetFullPath(reportPath);
    if (string.Equals(outputPath, reportPath, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("JSON output and Markdown report must have different paths.");
    if (htmlPath is not null)
    {
        htmlPath = Path.GetFullPath(htmlPath);
        if (string.Equals(htmlPath, outputPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(htmlPath, reportPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("HTML, JSON and Markdown output must have different paths.");
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    if (suite == "wording")
    {
        VoiceWordingPack cases = VoiceWordingPack.Read(await File.ReadAllTextAsync(voiceCasesPath!, cancellation.Token));
        CharacterInspirationPack profiles = CharacterInspirationPack.Read(await File.ReadAllTextAsync(characterPackPath!, cancellation.Token));
        string? manifestJson = dialogueManifestPath is null ? null : await File.ReadAllTextAsync(dialogueManifestPath, cancellation.Token);
        VoiceWordingReferences references = VoiceWordingTrial.LoadReferences(await File.ReadAllTextAsync(dialogueCorpusPath!, cancellation.Token), cases, manifestJson);
        VoiceWordingResult wording = await VoiceWordingTrial.RunAsync(cases, profiles, references, new VoiceWordingOptions
        {
            BaseUrl = generationUrl!, Model = generationModel!, Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds), Replicates = 2,
        }, cancellation.Token);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(wording, jsonOptions), cancellation.Token);
        await File.WriteAllTextAsync(reportPath, VoiceWordingTrial.Markdown(wording), cancellation.Token);
        if (htmlPath is not null) await File.WriteAllTextAsync(htmlPath, VoiceWordingTrial.Html(wording), cancellation.Token);
        Console.WriteLine($"Wrote fixed-intention wording trial: {outputPath}");
        Console.WriteLine($"Report: {reportPath}");
        if (htmlPath is not null) Console.WriteLine($"Compare wording: {htmlPath}");
        Console.WriteLine($"{wording.AnswerCount} replies; {wording.FailureCount} failures; {wording.CopyFlagCount} source overlap flags. Meaning and voice need human review.");
        return wording.ExitCode;
    }
    using var mind = new ResilientReflectionMind(new ReflectionMindOptions
    {
        LayaBaseUrl = authored ? null : layaUrl,
        LayaModel = model,
        Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        EvaluationTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        EvaluationMode = Enum.Parse<ReflectionEvaluationMode>(evaluationMode, ignoreCase: true),
        GenerationBaseUrl = suite == "voices" ? generationUrl : null,
        GenerationModel = generationModel ?? "local",
    });
    if (suite == "voices")
    {
        CharacterInspirationPack pack = CharacterInspirationPack.Read(await File.ReadAllTextAsync(characterPackPath!, cancellation.Token));
        CharacterVoiceTrialResult voices = await CharacterVoiceTrial.RunAsync(mind, pack, model, generationModel!, cancellation.Token);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(voices, jsonOptions), cancellation.Token);
        await File.WriteAllTextAsync(reportPath, CharacterVoiceTrial.Markdown(voices), cancellation.Token);
        if (htmlPath is not null)
        {
            await File.WriteAllTextAsync(htmlPath, CharacterVoiceTrial.Html(voices), cancellation.Token);
            Console.WriteLine($"Wrote {htmlPath}");
        }
        Console.WriteLine($"{voices.AnswerCount} answers; {voices.HybridCount} hybrid; {voices.EvaluationCount} evaluator passes; {voices.FallbackCount} fallbacks; {voices.IncompleteReceiptCount} incomplete receipts; {voices.TruncatedEvaluationCount} server truncations.");
        Console.WriteLine($"Wrote {outputPath}");
        Console.WriteLine($"Wrote {reportPath}");
        return voices.ExitCode;
    }
    TrialResult result = await TrialRunner.RunAsync(mind, !authored, model,
        pairs: suite == "social" ? TrialScenes.CreateSocial() : null,
        cancellationToken: cancellation.Token, evaluationMode: evaluationMode);
    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(result, jsonOptions), cancellation.Token);
    await File.WriteAllTextAsync(reportPath, TrialReport.Markdown(result), cancellation.Token);
    Console.WriteLine($"{result.Observations.Count()} answers; {result.EvaluationCount} evaluator passes ({evaluationMode}); {result.FallbackCount} fallbacks; {result.NonLayaCount} non-Laya answers; {result.IncompletePacketCount} incomplete packets; {result.TruncatedResponseCount} server truncations; {result.UnverifiedResponseCount} unverified usage records.");
    Console.WriteLine($"Wrote {outputPath}");
    Console.WriteLine($"Wrote {reportPath}");
    if (result.ExitCode != 0)
        Console.Error.WriteLine("Trial incomplete: inspect backend and prompt receipts before interpreting these measurements.");
    return result.ExitCode;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Trial cancelled.");
    return 130;
}
catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or JsonException or OverflowException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
