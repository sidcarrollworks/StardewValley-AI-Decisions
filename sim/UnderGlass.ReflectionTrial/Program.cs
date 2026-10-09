using System.Globalization;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.ReflectionTrial;

string? layaUrl = null;
string model = "typed-decisions";
string evaluationMode = "raw", suite = "smoke";
string outputPath = "reflection-trial.json", reportPath = "reflection-trial.md";
bool authored = false;
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
            case "--evaluation": evaluationMode = Next(); break;
            case "--suite": suite = Next(); break;
            case "--timeout-ms": timeoutMilliseconds = int.Parse(Next(), CultureInfo.InvariantCulture); break;
            case "--out": outputPath = Next(); break;
            case "--report": reportPath = Next(); break;
            case "--help":
                Console.WriteLine("ReflectionTrial (--laya-url <loopback base URL> [--model typed-decisions] | --authored) [--evaluation raw|canonical|balanced] [--suite smoke|social] [--timeout-ms 5000] [--out reflection-trial.json] [--report reflection-trial.md]");
                return 0;
            default: throw new ArgumentException($"Unknown option {args[i]}.");
        }
    }
    if (!authored && layaUrl is null)
        throw new ArgumentException("Specify --laya-url for real local evaluation, or --authored for an explicitly offline plumbing run.");
    if (authored && layaUrl is not null)
        throw new ArgumentException("Choose --authored or --laya-url; mixing them would make the intended backend ambiguous.");
    if (evaluationMode is not ("raw" or "canonical" or "balanced"))
        throw new ArgumentException("--evaluation raw|canonical|balanced");
    if (suite is not ("smoke" or "social"))
        throw new ArgumentException("--suite smoke|social");
    outputPath = Path.GetFullPath(outputPath);
    reportPath = Path.GetFullPath(reportPath);
    if (string.Equals(outputPath, reportPath, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("JSON output and Markdown report must have different paths.");

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    using var mind = new ResilientReflectionMind(new ReflectionMindOptions
    {
        LayaBaseUrl = authored ? null : layaUrl,
        LayaModel = model,
        Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        EvaluationTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        EvaluationMode = Enum.Parse<ReflectionEvaluationMode>(evaluationMode, ignoreCase: true),
        // GenerationBaseUrl remains null: every counterfactual shares its authored thought.
    });
    TrialResult result = await TrialRunner.RunAsync(mind, !authored, model,
        pairs: suite == "social" ? TrialScenes.CreateSocial() : null,
        cancellationToken: cancellation.Token, evaluationMode: evaluationMode);
    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
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
