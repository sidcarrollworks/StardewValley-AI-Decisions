using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnderGlass.Sim;
using UnderGlass.Sim.Lives;

/// <summary>Fixed, unfiltered mechanism matrix. Every recording comes from Replay; the report
/// displays receipts without recalculating decisions or claiming these fictional seeds are canon.</summary>
internal static class CompanyTrial
{
    private const string Home = "Home:House";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int Run(string output)
    {
        string root = FindRoot();
        string viewer = Path.GetRelativePath(output, Path.Combine(root, "sim", "viewer")).Replace('\\', '/');
        Directory.CreateDirectory(output);
        var rows = new List<object>();
        var counts = new List<RunCount>();
        HistoryPack history = SyntheticHistory().CopyValidated();
        Write("synthetic-history.json", JsonSerializer.Serialize(history, Json));

        foreach (long seed in new long[] { 7, 29, 101 })
        {
            TownData town = TownData.Default();
            town.Feelings.Acts.With("company,invitations");
            Record($"town-seed{seed}", "Town · no starting history", "town", new ReplayOptions
            {
                Seed = seed, Days = 7, Town = town, Nights = true,
                Label = $"Immediate company · normal town · seed {seed} · no starting history or models",
                LivesRouting = new() { Enabled = true }, LivesCompany = new() { Enabled = true },
            }, "Shipped town and ordinary settings; only routing, company and invitations enabled. No canon history pack.");
        }
        foreach (long seed in Enumerable.Range(1, 16).Select(i => (long)i))
            Record($"synthetic-household-seed{seed}", "Synthetic household", "synthetic", new ReplayOptions
            {
                Seed = seed, Days = 2, Town = SyntheticTown(), Nights = false,
                Label = $"Synthetic Ann/Bo household · seed {seed} · mechanism fixture, not a town recommendation",
                LivesRouting = new() { Enabled = true, CompareHistory = new[] { "reading:wary" } }, History = history,
                LivesCompany = new() { Enabled = true, OfferBase = 1, SeekBase = 100, AnswerScale = 0 },
            }, "Fictional histories; equal yes/no weights with state-weighted not-now expose different answers. Compare the same choice with only the wary reading removed; the warm opening remains.");
        foreach (long seed in new long[] { 7, 29, 101 })
            Record($"synthetic-miss-seed{seed}", "Synthetic missed search", "synthetic-miss", new ReplayOptions
            {
                Seed = seed, Days = 2, Town = SyntheticTown(away: true), Nights = false,
                Label = $"Synthetic deliberate miss · seed {seed} · observer knows Bo is away; Ann does not",
                LivesRouting = new() { Enabled = true, CompareHistory = new[] { "reading:wary" } }, History = history,
                LivesCompany = new() { Enabled = true, OfferBase = 1, SeekBase = 100, AnswerScale = 0 },
            }, "Bo works away all day. Ann knows their shared home, never the unseen workshop; a miss is whereabouts evidence only.");
        Record("synthetic-opening-seed7", "Synthetic opening removed", "synthetic-opening", new ReplayOptions
        {
            Seed = 7, Days = 2, Town = SyntheticTown(), Nights = false,
            Label = "Synthetic sole opening removed · seed 7 · changed eligibility is not a matched probability claim",
            LivesRouting = new() { Enabled = true, CompareHistory = new[] { "reading:warm" } },
            History = history with { Readings = history.Readings.Where(reading => reading.Id == "reading:warm").ToArray() },
            LivesCompany = new() { Enabled = true, OfferBase = 100, SeekBase = 100, AnswerScale = 0 },
        }, "Removing the only warm opening can make asking unavailable. Such receipts are explicitly non-comparable; they are never shown as matched probability deltas.");

        var summary = new
        {
            format = "under-glass-lives-company-trial", version = 1, generated = "2026-10-15",
            simulationOutput = true, modelCalls = 0, runs = counts,
            method = "All fixed seeds are retained. Town: 7/29/101 × 7 days; fictional household: 1..16 × 2 days; fictional missed searches: 7/29/101 × 2 days; sole opening removed: 7 × 2 days.",
            scope = "Immediate shared games, not future evening plans, a completed canon pilot, model quality, or full Lives observer 0.2.",
        };
        Write("summary.json", JsonSerializer.Serialize(summary, Json));
        Write("index.html", CompanyTrialReport.Render(JsonSerializer.SerializeToElement(new { summary, runs = rows }, Json), viewer));
        Console.WriteLine($"Wrote {output}");
        Console.WriteLine($"{counts.Count} fixed recordings; {counts.Sum(r => r.Episodes)} episodes; "
            + $"{counts.Sum(r => r.Completed)} completed; {counts.Sum(r => r.Comparable)} matched choices; "
            + $"{counts.Sum(r => r.ChangedSelection)} changed selections. No model calls.");
        return 0;

        void Write(string file, string value)
        {
            using var stream = new FileStream(Path.Combine(output, file), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(value);
        }

        void Record(string id, string label, string group, ReplayOptions options, string method)
        {
            Console.WriteLine($"Recording {id}: {options.Days} days...");
            var clock = Stopwatch.StartNew();
            Dictionary<string, object?> recording = Replay.Record(options);
            clock.Stop();
            recording["trial"] = new { suite = "company", id, group, syntheticScenario = group != "town", method, modelCalls = 0 };
            Write(id + ".json", JsonSerializer.Serialize(recording, Json));
            JsonElement company = (JsonElement)recording["livesCompany"]!;
            JsonElement routing = (JsonElement)recording["livesRouting"]!;
            CompanyUpdate[] updates = company.GetProperty("updates").Deserialize<CompanyUpdate[]>(Json)!;
            CompanyChoice[] choices = company.GetProperty("choices").Deserialize<CompanyChoice[]>(Json)!;
            CompanyAnswer[] answers = company.GetProperty("answers").Deserialize<CompanyAnswer[]>(Json)!;
            CompanyOutcome[] outcomes = company.GetProperty("outcomes").Deserialize<CompanyOutcome[]>(Json)!;
            LivesMemoryChange[] changes = routing.GetProperty("memoryChanges").Deserialize<LivesMemoryChange[]>(Json)!;
            var learning = changes.Where(change => change.CauseId?.StartsWith("company:", StringComparison.Ordinal) == true)
                .Select(change => new
                {
                    change.Holder, change.At, change.CauseId,
                    associations = change.AddedAssociations.Select(a => new { a.Object, a.Amount }),
                    expectations = change.After.Expectations.Where(e => e.Entries.Any(entry => entry.CauseId == change.CauseId))
                        .Select(e => new { e.Key, before = change.Before.Expectations.FirstOrDefault(b => b.Key == e.Key)?.Chance ?? 1,
                            after = e.Chance, success = e.Entries.Last(entry => entry.CauseId == change.CauseId).Success }),
                }).ToArray();
            var episodes = updates.GroupBy(update => update.Episode.Id).Select(g => g.Last().Episode).ToArray();
            ChoiceComparison[] comparisons = choices.Where(c => c.Comparison is { Comparable: true }).Select(c => c.Comparison!).ToArray();
            var count = new RunCount(id, group, options.Seed, options.Days, clock.Elapsed.TotalSeconds,
                episodes.Length, episodes.Count(e => e.Stage == CompanyStage.Completed),
                episodes.GroupBy(e => e.Stage.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                answers.GroupBy(a => a.SelectedId).ToDictionary(g => g.Key, g => g.Count()),
                comparisons.Length, comparisons.Count(c => c.ProbabilitiesChanged == true), comparisons.Count(c => c.SelectionChanged == true),
                choices.Count(c => c.Comparison is { Comparable: false }));
            counts.Add(count);
            rows.Add(new { id, label, group, method, file = id + ".json", count, updates, choices, answers, outcomes, learning });
            Console.WriteLine($"  {episodes.Length} episodes, {count.Completed} completed, {clock.Elapsed.TotalSeconds:0.00}s.");
        }
    }

    private sealed record RunCount(string Id, string Group, long Seed, int Days, double Seconds, int Episodes,
        int Completed, IReadOnlyDictionary<string, int> Endings, IReadOnlyDictionary<string, int> Answers,
        int Comparable, int ChangedProbabilities, int ChangedSelection, int NonComparable);

    private static string FindRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "sim", "viewer", "ui.css"))) return directory.FullName;
        throw new InvalidOperationException("Run from the repository so the review can link its shared viewer styles.");
    }

    private static TownData SyntheticTown(bool away = false)
    {
        Location Room(string name) => new(name, false, Enumerable.Repeat(new string('.', 20), 12).ToArray());
        Villager Person(string name, int age, Job? job = null) => new(name, "House", "villager",
            new Temperament(.5, .5, .5, .5, Sensitivity: name == "Ann" ? .8 : .2), new Body(100, -1), job,
            Array.Empty<Haunt>(), new Dictionary<string, double>(), Array.Empty<string>(), Age: age);
        var feelings = new FeelingOptions();
        feelings.Acts.With("company,invitations");
        return new()
        {
            Cast = new[] { Person("Ann", 22), Person("Bo", 68, away ? new Job("Workshop", new Tile(4, 4), 0,
                Clock.MinutesPerDay, Array.Empty<int>(), 0, Commute: 0) : null) },
            Places = away ? new[] { Room(Home), Room("Workshop") } : new[] { Room(Home) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = ActCatalog.Kinds(feelings.Acts, Array.Empty<ActKind>()), Feelings = feelings,
            Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000, CircadianWeight = 0 },
            Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private static HistoryPack SyntheticHistory() => new(1,
        new[] { new HistorySource("source:synthetic", "synthetic:ann-and-bo", HistorySourceKind.Canon) },
        new[] { new HistoryFact("fact:company", HistoryKind.Practice, new[] { "source:synthetic" }) },
        new[] { new HistoryAccount("account:company", "fact:company", "Ann", "Bo and I spend time together.",
            KnowledgeKind.Lived, new[] { "source:synthetic" }, new[] { new HistoryCue("person:Bo", "source:synthetic") }) },
        new[] { Reading("warm", ReadingStance.Warm, "I enjoy Bo's company."),
            Reading("wary", ReadingStance.Wary, "Sometimes I worry Bo will become impatient.") });

    private static HistoryReading Reading(string id, ReadingStance stance, string text) => new("reading:" + id,
        "account:company", new(MemoryObjectKind.Person, "Bo"), stance, text, ReadingOrigin.CanonStated, AdmissionRule.R2,
        new(ReviewStatus.Reviewed, "Fictional mechanism fixture, not Stardew evidence", "2026-10-15"),
        new[] { "source:synthetic" }, new[] { new HistoryCue("person:Bo", "source:synthetic") });
}
