using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnderGlass.Sim;
using UnderGlass.Sim.Lives;

// Actual engine output from an explicitly synthetic scenario. This harness has no model client,
// approved Stardew histories, invitations or planned activities; it exercises free-time routing.
string? output = null;
int days = 7;
long seed = 7;
try
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (int i = 0; i < args.Length; i++)
    {
        string flag = args[i];
        if (!seen.Add(flag)) throw new ArgumentException($"Repeated option: {flag}.");
        if (flag == "--help")
        {
            Console.WriteLine("LivesTrial --out <new-directory> [--days 7] [--seed 7]\n"
                + "Offline synthetic Ann/Bo routing experiment. Days and seed must be positive integers.\n"
                + "Writes three real recordings, the synthetic history input, a summary, and an HTML review. Never overwrites output.\n"
                + "Matched comparisons inside with-history.json hold the choice fixed; the separate recordings do not.");
            return 0;
        }
        if (flag is not ("--out" or "--days" or "--seed")) throw new ArgumentException($"Unknown option: {flag}.");
        if (++i >= args.Length) throw new ArgumentException($"{flag} needs a value.");
        string value = args[i];
        switch (flag)
        {
            case "--out": output = value; break;
            case "--days":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out days)
                    || days <= 0 || days > int.MaxValue / Clock.MinutesPerDay)
                    throw new ArgumentException("--days must be a positive integer within the simulation clock range.");
                break;
            case "--seed":
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seed) || seed <= 0)
                    throw new ArgumentException("--seed must be a positive 64-bit integer.");
                break;
        }
    }
    if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("--out <new-directory> is required.");
    output = Path.GetFullPath(output);
    if (Directory.Exists(output) || File.Exists(output)) throw new ArgumentException("The output path already exists. Choose a fresh directory.");

    var json = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    HistoryPack history = SyntheticHistory().CopyValidated();
    TownData town = SyntheticTown();
    Directory.CreateDirectory(output);
    WriteNew("history.json", JsonSerializer.Serialize(history, json));

    var common = new ReplayOptions { Seed = seed, Days = days, Town = town, Nights = false };
    Dictionary<string, object?> withHistory = Record("with-history.json", common with
    {
        Label = "Synthetic Ann/Bo: a warm reading about the Hall; offline Lives routing",
        LivesRouting = new() { Enabled = true, CompareHistory = new[] { "reading:ann-hall" } }, History = history,
    }, "with-history", "Matched comparisons inside this recording hold each choice's scene and unit fixed.");
    Record("without-reading.json", common with
    {
        Label = "Synthetic Ann/Bo: no warm reading; separate whole run",
        LivesRouting = new() { Enabled = true }, History = history with { Readings = Array.Empty<HistoryReading>() },
    }, "without-reading", "A separate whole run. Its later choices and movement may diverge; it is not a matched-choice comparison.");
    Record("routing-off.json", common with { Label = "Synthetic Ann/Bo: existing routing control" },
        "routing-off", "The existing routine draw with Lives routing disabled; no history is loaded.");

    JsonElement routing = (JsonElement)withHistory["livesRouting"]!;
    JsonElement[] choices = routing.GetProperty("decisions").EnumerateArray().ToArray();
    JsonElement[] comparisons = choices.Where(c => c.GetProperty("comparison").ValueKind == JsonValueKind.Object)
        .Select(c => c.GetProperty("comparison")).ToArray();
    JsonElement[] matched = comparisons.Where(c => c.GetProperty("comparable").GetBoolean()).ToArray();
    var summary = new
    {
        format = "under-glass-lives-routing-trial", version = 1,
        scenario = "synthetic-ann-bo", syntheticScenario = true, simulationOutput = true,
        days, seed, modelCalls = 0,
        evidence = "Fictional evidence classified Canon only to exercise reviewed-seed admission in this synthetic town; no Stardew canon claim or approval.",
        scope = "Existing destinations only. No complete Lives pilot, model evaluation, invitation, planned activity or counterfactual future.",
        comparisons = new
        {
            scope = "isolated-choice", source = "with-history.json",
            decisions = choices.Length,
            sampledLivesDecisions = choices.Count(c => !c.GetProperty("usedLegacy").GetBoolean()),
            legacyDecisions = choices.Count(c => c.GetProperty("usedLegacy").GetBoolean()),
            recordedComparisons = comparisons.Length, comparable = matched.Length,
            nonComparable = comparisons.Length - matched.Length,
            reasonsChanged = matched.Count(c => c.GetProperty("reasonsChanged").GetBoolean()),
            probabilitiesChanged = matched.Count(c => c.GetProperty("probabilitiesChanged").GetBoolean()),
            selectedActionChanged = matched.Count(c => c.GetProperty("selectionChanged").GetBoolean()),
        },
        recordings = new[] { "with-history.json", "without-reading.json", "routing-off.json" },
    };
    WriteNew("summary.json", JsonSerializer.Serialize(summary, json));
    WriteNew("index.html", RoutingTrialReport.Render(withHistory, JsonSerializer.SerializeToElement(summary, json)));
    Console.WriteLine($"Wrote {output}");
    Console.WriteLine($"{choices.Length} routing decisions; {matched.Length} matched comparisons; "
        + $"{matched.Count(c => c.GetProperty("selectionChanged").GetBoolean())} changed sampled choices. No model calls.");
    return 0;

    void WriteNew(string name, string content)
    {
        // Even if another process races for this directory, existing files are never overwritten.
        using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
    Dictionary<string, object?> Record(string file, ReplayOptions options, string arm, string comparisonNote)
    {
        var recording = Replay.Record(options);
        recording["trial"] = new
        {
            scenario = "synthetic-ann-bo", syntheticScenario = true, simulationOutput = true,
            arm, modelCalls = 0, comparisonNote,
            evidence = "Invented test people and history; never sourced Stardew biography.",
        };
        WriteNew(file, JsonSerializer.Serialize(recording, json));
        return recording;
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    if (output is not null && Directory.Exists(output)) Console.Error.WriteLine("Existing or partial output was preserved: " + output);
    return 1;
}

static TownData SyntheticTown()
{
    Location Room(string name) => new(name, false, Enumerable.Repeat(new string('.', 12), 8).ToArray());
    Villager Person(string name) => new(name, name, "villager", new Temperament(1, .5, .5, .5),
        new Body(100, -1), null,
        new[] { new Haunt("Yard", new Tile(4, 3), 0, Clock.MinutesPerDay, 1),
            new Haunt("Hall", new Tile(4, 3), 0, Clock.MinutesPerDay, 1) },
        new Dictionary<string, double>(), Array.Empty<string>());
    return new()
    {
        Cast = new[] { Person("Ann"), Person("Bo") }, Places = new[] { Room("Yard"), Room("Hall") },
        Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(), Acts = Array.Empty<ActKind>(),
        Feelings = new FeelingOptions(), Authority = new AuthorityOptions { ElectConstable = false },
        Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
    };
}

static HistoryPack SyntheticHistory() => new(1,
    new[] { new HistorySource("source:synthetic-hall", "synthetic:ann-enjoys-hall", HistorySourceKind.Canon) },
    new[] { new HistoryFact("fact:ann-hall", HistoryKind.Practice, new[] { "source:synthetic-hall" }) },
    new[] { new HistoryAccount("account:ann-hall", "fact:ann-hall", "Ann", "I spend time at the Hall.",
        KnowledgeKind.Lived, new[] { "source:synthetic-hall" }, new[] { new HistoryCue("place:Hall", "source:synthetic-hall") }) },
    new[] { new HistoryReading("reading:ann-hall", "account:ann-hall", new MemoryObject(MemoryObjectKind.Place, "Hall"),
        ReadingStance.Warm, "I enjoy being at the Hall.", ReadingOrigin.CanonStated, AdmissionRule.R2,
        new HistoryReview(ReviewStatus.Reviewed, "Synthetic harness; fictional evidence only", "2026-10-15"),
        new[] { "source:synthetic-hall" }, new[] { new HistoryCue("place:Hall", "source:synthetic-hall") }) });
