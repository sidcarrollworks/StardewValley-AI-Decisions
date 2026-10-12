using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnderGlass.Sim;

namespace UnderGlass.ReflectionTrial;

public sealed record WordingIntention(string Kind, string Target, string Motive, string Stance);
public sealed record WordingAuthoredExample(string Text, string Note);
public sealed record WordingReferenceSelection(string RecordId, int NodeIndex);
public sealed record VoiceWordingCase(string Id, string Actor, string LifeStage, string LifeStageSource,
    string Subject, string Relationship, string Scene, WordingIntention Intention,
    IReadOnlyList<WordingAuthoredExample> AuthoredExamples, IReadOnlyList<WordingReferenceSelection> References);
public sealed record VoiceWordingPack(int Schema, string Provenance, IReadOnlyList<VoiceWordingCase> Cases)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
        { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static VoiceWordingPack Read(string json)
        => (JsonSerializer.Deserialize<VoiceWordingPack>(json, ReadOptions)
            ?? throw new InvalidDataException("Missing wording case pack.")).Freeze();
    [JsonIgnore] public string ContentHash => VoiceWordingTrial.Hash(JsonSerializer.Serialize(Freeze()));

    internal VoiceWordingPack Freeze()
    {
        if (Schema != 1) throw new InvalidDataException("Only wording pack schema 1 is supported.");
        VoiceWordingTrial.Text(Provenance, 2000, "case provenance");
        if (Cases is null || Cases.Count is < 1 or > 100) throw new InvalidDataException("Expected 1..100 wording cases.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cases = new List<VoiceWordingCase>();
        foreach (var c in Cases)
        {
            if (c is null) throw new InvalidDataException("Null wording case.");
            VoiceWordingTrial.Text(c.Id, 100, "case ID");
            if (!ids.Add(c.Id)) throw new InvalidDataException($"Duplicate wording case {c.Id}.");
            VoiceWordingTrial.Text(c.Actor, 100, "actor");
            VoiceWordingTrial.Text(c.Subject, 100, "subject");
            if (c.Actor == c.Subject) throw new InvalidDataException("A wording case needs a distinct subject.");
            VoiceWordingTrial.Text(c.LifeStage, 80, "life stage");
            VoiceWordingTrial.Text(c.LifeStageSource, 1000, "life-stage source");
            VoiceWordingTrial.Text(c.Relationship, 300, "relationship");
            VoiceWordingTrial.Text(c.Scene, 600, "scene");
            if (c.Intention is null || c.Intention.Target != c.Subject)
                throw new InvalidDataException("A fixed intention must target the supplied subject.");
            VoiceWordingTrial.Text(c.Intention.Kind, 100, "intention kind");
            VoiceWordingTrial.Text(c.Intention.Motive, 300, "intention motive");
            VoiceWordingTrial.Text(c.Intention.Stance, 300, "intention stance");
            if (c.AuthoredExamples is null || c.AuthoredExamples.Count is < 1 or > 5)
                throw new InvalidDataException("Expected 1..5 withheld authored examples.");
            foreach (var example in c.AuthoredExamples)
            {
                if (example is null) throw new InvalidDataException("Null authored example.");
                VoiceWordingTrial.Text(example.Text, VoiceWordingTrial.MaxThoughtCharacters, "authored wording");
                VoiceWordingTrial.Text(example.Note, 500, "authored note");
            }
            if (c.References is null || c.References.Count is < 3 or > 5)
                throw new InvalidDataException("Each wording case needs 3..5 exact dialogue reference keys.");
            var keys = new HashSet<WordingReferenceSelection>();
            foreach (var key in c.References)
            {
                if (key is null || key.NodeIndex < 0) throw new InvalidDataException("Invalid dialogue node selection.");
                VoiceWordingTrial.Text(key.RecordId, 1500, "reference key");
                if (!keys.Add(key)) throw new InvalidDataException($"Duplicate reference selection {key}.");
            }
            cases.Add(c with { AuthoredExamples = Array.AsReadOnly(c.AuthoredExamples.ToArray()),
                References = Array.AsReadOnly(c.References.ToArray()) });
        }
        return this with { Cases = Array.AsReadOnly(cases.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray()) };
    }
}

public sealed record WordingSource(string Asset, string Key, string Sha256, string GameVersion);
public sealed record VoiceWordingReference(string Id, int NodeIndex, string Text, string Speaker, string Attribution,
    string Raw, string NodeRaw, WordingSource Source, JsonElement Conditions, JsonElement NodeConditions, JsonElement Variant,
    bool RawSourceManifestVerified)
{
    public string TextDerivation => "corpus-parser";
}
public sealed class VoiceWordingReferences
{
    public string PackHash { get; }
    public string CorpusHash { get; }
    public string GameVersion { get; }
    public string? GameAssemblySha256 { get; }
    public IReadOnlyList<VoiceWordingReference> Records { get; }
    internal VoiceWordingReferences(string packHash, string corpusHash, string gameVersion, string? gameAssembly,
        IReadOnlyList<VoiceWordingReference> records)
        => (PackHash, CorpusHash, GameVersion, GameAssemblySha256, Records)
            = (packHash, corpusHash, gameVersion, gameAssembly, records);
}
public sealed record VoiceWordingOptions
{
    public required string BaseUrl { get; init; }
    public required string Model { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int Replicates { get; init; } = 2;
}
public sealed record WordingServiceReceipt(string? Model,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long? Seed,
    string? Revision, string? Device,
    string? FinishReason, JsonElement? Usage);
public sealed record WordingCopyFlag(string ReferenceId, string Match);
public sealed record VoiceWordingObservation(string Role, int Replicate,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Seed,
    string? Thought,
    string? Error, string Prompt, string? RawResponse, double ElapsedMilliseconds,
    WordingServiceReceipt? Receipt, IReadOnlyList<WordingCopyFlag> CopyFlags, string? CandidateThought = null)
{
    public bool FormatValid => Thought is not null && Error is null;
    public bool SemanticReviewRequired => true;
}
public sealed record VoiceWordingCaseResult(VoiceWordingCase Definition, string Voice,
    IReadOnlyList<string> VoiceSources, IReadOnlyList<VoiceWordingReference> References,
    IReadOnlyList<VoiceWordingObservation> Observations);
public sealed record VoiceWordingResult(string PackHash, string ProfilePackHash, string CorpusHash,
    string GameVersion, string? GameAssemblySha256, string Model, int Replicates,
    IReadOnlyList<VoiceWordingCaseResult> Cases, string PromptVersion = "json-frame-v1")
{
    public string Suite => "wording";
    public int AnswerCount => Cases.Sum(c => c.Observations.Count);
    public int FailureCount => Cases.Sum(c => c.Observations.Count(o => !o.FormatValid));
    public int CopyFlagCount => Cases.Sum(c => c.Observations.Count(o => o.CopyFlags.Count > 0));
    public int ExitCode => FailureCount == 0 ? 0 : 2;
}

/// <summary>Only wording varies. This isolated experiment never asks Laya, chooses an action,
/// reads world state, or substitutes authored prose for a failed model call.</summary>
public static class VoiceWordingTrial
{
    public const int MaxThoughtCharacters = 180;
    public const string CurrentPromptVersion = "plain-frame-v2";
    private const int ResponseByteLimit = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static VoiceWordingReferences LoadReferences(string corpusJson, VoiceWordingPack cases, string? rawAssetsJson = null)
    {
        try { return LoadReferencesCore(corpusJson, cases, rawAssetsJson); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException or ArgumentException or IndexOutOfRangeException)
        { throw new InvalidDataException("Malformed dialogue corpus or raw asset manifest: " + ex.Message, ex); }
    }

    private static VoiceWordingReferences LoadReferencesCore(string corpusJson, VoiceWordingPack cases, string? rawAssetsJson)
    {
        VoiceWordingPack pack = cases.Freeze();
        using JsonDocument document = JsonDocument.Parse(corpusJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema)
            || !schema.TryGetInt32(out int version) || version != 1)
            throw new InvalidDataException("Expected dialogue corpus schemaVersion 1.");
        string gameVersion = String(root, "gameVersion");
        Text(gameVersion, 100, "corpus game version");
        string? assembly = root.TryGetProperty("gameAssemblySha256", out var gameHash) && gameHash.ValueKind != JsonValueKind.Null
            ? gameHash.GetString() : null;
        if (assembly is not null && !HexHash(assembly)) throw new InvalidDataException("Invalid game assembly SHA256.");
        if (!root.TryGetProperty("records", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Corpus needs a records array.");
        var indexed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string id = String(row, "id");
            if (!indexed.TryAdd(id, row)) throw new InvalidDataException($"Duplicate corpus ID {id}.");
        }
        using JsonDocument? manifest = rawAssetsJson is null ? null : JsonDocument.Parse(rawAssetsJson);
        Dictionary<string, JsonElement>? assets = null;
        if (manifest is not null)
        {
            JsonElement rawRoot = manifest.RootElement;
            if (rawRoot.GetProperty("schemaVersion").GetInt32() != 1 || String(rawRoot, "gameVersion") != gameVersion
                || String(rawRoot, "language") != "en"
                || assembly is not null && String(rawRoot, "gameAssemblySha256") != assembly)
                throw new InvalidDataException("Raw asset manifest does not match the corpus version/provenance.");
            assets = rawRoot.GetProperty("assets").EnumerateArray().ToDictionary(a => String(a, "asset"), StringComparer.Ordinal);
        }
        var references = new Dictionary<WordingReferenceSelection, VoiceWordingReference>();
        foreach (VoiceWordingCase c in pack.Cases)
        foreach (WordingReferenceSelection selection in c.References)
        {
            string key = selection.RecordId;
            if (!indexed.TryGetValue(key, out JsonElement row)) throw new InvalidDataException($"Unknown dialogue reference {key}.");
            if (String(row, "speaker") != c.Actor) throw new InvalidDataException($"Dialogue reference {key} is not attributed to {c.Actor}.");
            string attribution = String(row, "attribution");
            if (attribution != "dialogue-asset" || String(row, "category") != "dialogue")
                throw new InvalidDataException($"Dialogue reference {key} must be ordinary directly attributed dialogue.");
            if (!row.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Dialogue reference {key} has no source.");
            var provenance = new WordingSource(String(source, "asset"), String(source, "key"),
                String(source, "sha256"), String(source, "gameVersion"));
            if (!HexHash(provenance.Sha256) || provenance.GameVersion != gameVersion
                || provenance.Asset != "Characters/Dialogue/" + c.Actor
                || key != provenance.Asset + ":" + provenance.Key)
                throw new InvalidDataException($"Dialogue reference {key} has inconsistent source provenance.");
            string raw = String(row, "raw");
            if (string.IsNullOrWhiteSpace(raw)) throw new InvalidDataException($"Dialogue reference {key} has no raw text.");
            if (assets is not null && (!assets.TryGetValue(provenance.Asset, out var asset)
                || String(asset, "sha256") != provenance.Sha256
                || !asset.GetProperty("entries").TryGetProperty(provenance.Key, out var entry)
                || entry.ValueKind != JsonValueKind.String || entry.GetString() != raw))
                throw new InvalidDataException($"Dialogue reference {key} differs from the supplied raw asset manifest.");
            if (!row.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Dialogue reference {key} has no recorded conditions.");
            if (!row.TryGetProperty("parsed", out var parsed) || !parsed.TryGetProperty("warnings", out var warnings)
                || warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() > 0
                || !parsed.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Dialogue reference {key} is not cleanly parsed.");
            if (selection.NodeIndex >= nodes.GetArrayLength()) throw new InvalidDataException($"Missing dialogue node {key}/{selection.NodeIndex}.");
            JsonElement node = nodes[selection.NodeIndex];
            if (String(node, "kind") != "text" || String(node, "role") != "npc")
                throw new InvalidDataException($"Selected dialogue node {key}/{selection.NodeIndex} is not NPC speech.");
            if (!node.TryGetProperty("conditions", out var nodeConditions) || nodeConditions.ValueKind != JsonValueKind.Array
                || !node.TryGetProperty("variant", out var variant) || variant.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Selected dialogue node {key}/{selection.NodeIndex} lacks condition/variant provenance.");
            string line = String(node, "text");
            string nodeRaw = String(node, "raw");
            Text(line, 600, "dialogue line");
            if (line.IndexOfAny(new[] { '$', '#', '@', '%' }) >= 0)
                throw new InvalidDataException($"Dialogue reference {key} contains unresolved markup or substitutions.");
            references.TryAdd(selection, new VoiceWordingReference(key, selection.NodeIndex, line, c.Actor, attribution, raw, nodeRaw,
                provenance, conditions.Clone(), nodeConditions.Clone(), variant.Clone(), assets is not null));
        }
        return new(pack.ContentHash, Hash(corpusJson), gameVersion, assembly,
            Array.AsReadOnly(references.Values.OrderBy(r => r.Id, StringComparer.Ordinal).ThenBy(r => r.NodeIndex).ToArray()));
    }

    public static async Task<VoiceWordingResult> RunAsync(VoiceWordingPack cases, CharacterInspirationPack profiles,
        VoiceWordingReferences references, VoiceWordingOptions options, CancellationToken cancellationToken = default,
        HttpMessageHandler? handler = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VoiceWordingPack pack = cases.Freeze();
        CharacterInspirationPack frozenProfiles = CharacterInspirationPack.Read(JsonSerializer.Serialize(profiles));
        string packHash = pack.ContentHash, profileHash = frozenProfiles.ContentHash;
        if (references.PackHash != packHash) throw new InvalidDataException("Loaded references belong to a different wording case pack.");
        Uri endpoint = Endpoint(options.BaseUrl);
        Text(options.Model, 200, "generator model");
        if (options.Timeout <= TimeSpan.Zero || options.Timeout.TotalSeconds > 60 || options.Replicates is < 2 or > 3)
            throw new ArgumentOutOfRangeException(nameof(options), "Use a positive timeout at most 60 seconds and 2..3 replicates.");
        using var http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false }, disposeHandler: true)
            { Timeout = Timeout.InfiniteTimeSpan };
        // Resolve every immutable input before the first await; no caller mutation may change
        // later arms, profile receipts or result hashes partway through the experiment.
        var prepared = pack.Cases.Select(c =>
        {
            CharacterInspiration character = frozenProfiles.ForCharacter(c.Actor, c.Subject, 0, c.Id)
                ?? throw new InvalidDataException($"Missing voice profile for {c.Actor}.");
            var selected = c.References.Select(key => references.Records.Single(r => r.Id == key.RecordId && r.NodeIndex == key.NodeIndex)).ToArray();
            var prompts = new List<(string Role, int Replicate, long Seed, string Prompt)>();
            for (int replicate = 1; replicate <= options.Replicates; replicate++)
            foreach (string role in new[] { "profile-only", "dialogue-reference" })
            {
                long seed = Seed(c.Id, replicate);
                prompts.Add((role, replicate, seed, Prompt(c, character.Voice,
                    role == "dialogue-reference" ? selected : null, options.Model, seed)));
            }
            return (Case: c, Character: character, References: Array.AsReadOnly(selected), Prompts: prompts);
        }).ToArray();
        var result = new List<VoiceWordingCaseResult>();
        foreach (var preparedCase in prepared)
        {
            var observations = new List<VoiceWordingObservation>();
            foreach (var preparedPrompt in preparedCase.Prompts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                observations.Add(await Observe(http, endpoint, preparedPrompt.Prompt, preparedPrompt.Role,
                    preparedPrompt.Replicate, preparedPrompt.Seed, preparedCase.References, options, cancellationToken));
            }
            result.Add(new(preparedCase.Case, preparedCase.Character.Voice, preparedCase.Character.Sources,
                preparedCase.References, observations.AsReadOnly()));
        }
        return new(packHash, profileHash, references.CorpusHash, references.GameVersion, references.GameAssemblySha256,
            options.Model, options.Replicates, result.AsReadOnly(), CurrentPromptVersion);
    }

    private static string Prompt(VoiceWordingCase c, string voice, IReadOnlyList<VoiceWordingReference>? references,
        string model, long seed)
    {
        const string instructions = "Write private self-talk, not a description of the person or spoken dialogue. Keep the supplied plan, target, motive and stance; add no facts. "
            + "Dialogue references show style only, not this scene's history; do not copy their sentences. "
            + "Return only JSON with exactly one string field, thought, of 1..180 characters. No explanation.";
        string plan = c.Intention.Kind == "Thanked" ? "thank " + c.Intention.Target
            : "carry out " + c.Intention.Kind + " toward " + c.Intention.Target;
        string user = $"You are {c.Actor} ({c.LifeStage}).\nWrite your next thought using I or me, in one or two short sentences.\n"
            + $"Plan (not yet done): {plan}. Express this plan, not a finished interaction.\n"
            + $"Relationship: {c.Relationship}\nScene: {c.Scene}\nVoice: {voice}\n"
            + $"Intention kind: {c.Intention.Kind}\nTarget: {c.Intention.Target}\nMotive: {c.Intention.Motive}\nStance: {c.Intention.Stance}";
        if (references is not null) user += "\n\nDialogue style references (not scene facts):\n"
            + string.Join("\n", references.Select(r => "- " + r.Text));
        if (instructions.Length + user.Length > 6000) throw new InvalidDataException("Wording prompt exceeds the generator's 6000-character limit; no reference was truncated.");
        return JsonSerializer.Serialize(new
        {
            model, messages = new[] { new { role = "system", content = instructions }, new { role = "user", content = user } },
            temperature = 0.7, max_tokens = 120, seed, response_format = new { type = "json_object" },
        }, JsonOptions);
    }

    private static async Task<VoiceWordingObservation> Observe(HttpClient http, Uri endpoint, string prompt,
        string role, int replicate, long seed, IReadOnlyList<VoiceWordingReference> references,
        VoiceWordingOptions options, CancellationToken cancellationToken)
    {
        string? raw = null, thought = null, candidate = null, error = null;
        WordingServiceReceipt? receipt = null;
        var timer = Stopwatch.StartNew();
        using var deadline = new CancellationTokenSource(options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                { Content = new StringContent(prompt, Encoding.UTF8, "application/json") };
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (response.RequestMessage?.RequestUri is { } actual && actual != endpoint)
                throw new InvalidDataException("Generator response came from a different endpoint.");
            if (response.Content.Headers.ContentLength > ResponseByteLimit) throw new InvalidDataException("Generator response exceeds 65536 bytes.");
            using var stream = await response.Content.ReadAsStreamAsync(linked.Token);
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), linked.Token);
                if (read == 0) break;
                if (body.Length + read > ResponseByteLimit) throw new InvalidDataException("Generator response exceeds 65536 bytes.");
                body.Write(buffer, 0, read);
            }
            raw = Encoding.UTF8.GetString(body.ToArray());
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Generator HTTP {(int)response.StatusCode}; no redirect or fallback was used.");
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;
            string? routedModel = OptionalString(root, "model"), revision = OptionalString(root, "revision"), device = OptionalString(root, "device");
            long? routedSeed = root.TryGetProperty("seed", out var s) && s.TryGetInt64(out long value) ? value : null;
            JsonElement? usage = root.TryGetProperty("usage", out var u) ? u.Clone() : null;
            JsonElement choices = root.GetProperty("choices");
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() != 1) throw new InvalidDataException("Expected exactly one generation choice.");
            JsonElement choice = choices[0];
            string? finish = OptionalString(choice, "finish_reason");
            receipt = new(routedModel, routedSeed, revision, device, finish, usage);
            string content = String(choice.GetProperty("message"), "content");
            using JsonDocument generated = JsonDocument.Parse(content);
            if (generated.RootElement.ValueKind == JsonValueKind.Object
                && generated.RootElement.TryGetProperty("thought", out var candidateText) && candidateText.ValueKind == JsonValueKind.String)
                candidate = candidateText.GetString();
            if (routedModel != options.Model || routedSeed != seed || string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(device))
                throw new InvalidDataException("Generator routing/seed receipt is missing or differs from the request.");
            if (finish != "stop") throw new InvalidDataException($"Generator did not finish normally ({finish ?? "missing finish reason"}).");
            if (generated.RootElement.ValueKind != JsonValueKind.Object || generated.RootElement.EnumerateObject().Count() != 1
                || !generated.RootElement.TryGetProperty("thought", out var text) || text.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Expected exactly {thought:string}; no action choice or extra fields.");
            Text(candidate, MaxThoughtCharacters, "generated thought");
            linked.Token.ThrowIfCancellationRequested();
            thought = candidate;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { error = $"Generator request timed out after {options.Timeout.TotalMilliseconds:0} ms."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidDataException
            or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        { error = ex.Message; }
        timer.Stop();
        cancellationToken.ThrowIfCancellationRequested();
        return new(role, replicate, seed, thought, error, prompt, raw, timer.Elapsed.TotalMilliseconds, receipt,
            candidate is null ? Array.Empty<WordingCopyFlag>() : CopyFlags(candidate, references), candidate);
    }

    private static IReadOnlyList<WordingCopyFlag> CopyFlags(string thought, IReadOnlyList<VoiceWordingReference> references)
    {
        string normalized = Normalize(thought);
        return Array.AsReadOnly(references.Select(r => (Reference: r, Text: Normalize(r.Text)))
            .Where(x => x.Text == normalized || x.Text.Length >= 20 && (normalized.Contains(x.Text, StringComparison.Ordinal)
                || normalized.Length >= 20 && x.Text.Contains(normalized, StringComparison.Ordinal)))
            .Select(x => new WordingCopyFlag(x.Reference.Id, x.Text == normalized ? "exact normalized line" : "full normalized substring"))
            .Distinct().ToArray());
    }
    private static string Normalize(string text) => Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    private static long Seed(string id, int replicate) => BinaryPrimitives.ReadInt64LittleEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes($"wording:{id}:{replicate.ToString(CultureInfo.InvariantCulture)}"))) & long.MaxValue;
    private static Uri Endpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https")
            || !uri.IsLoopback || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            throw new ArgumentException("Use an explicit loopback HTTP(S) generator base URL without credentials, query or fragment.");
        string prefix = uri.AbsoluteUri.TrimEnd('/');
        return new Uri(prefix + (prefix.EndsWith("/v1", StringComparison.Ordinal) ? "/" : "/v1/") + "chat/completions");
    }
    private static string String(JsonElement value, string name) => value.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.String
        ? child.GetString()! : throw new InvalidDataException($"Missing string {name}.");
    private static string? OptionalString(JsonElement value, string name) => value.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.String ? child.GetString() : null;
    private static bool HexHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    internal static void Text(string? text, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl))
            throw new InvalidDataException($"Invalid {name}: expected 1..{maximum} characters without control characters.");
    }

    public static string Markdown(VoiceWordingResult result)
    {
        var text = new StringBuilder("# Fixed-intention wording trial\n\n");
        text.AppendLine($"Model: `{result.Model}`; prompt `{result.PromptVersion}`. Cases: {result.Cases.Count}; replicates: {result.Replicates}; answers: {result.AnswerCount}; failures: {result.FailureCount}; copy flags: {result.CopyFlagCount}.");
        text.AppendLine($"Case pack `{result.PackHash}`; profile pack `{result.ProfilePackHash}`; corpus `{result.CorpusHash}`; game {result.GameVersion}.");
        text.AppendLine("\nBoth arms are asked to express the same fixed intention. No evaluator, action selection, world change or authored fallback is involved. Valid JSON does not prove voice, grounding or intention fidelity. Source dialogue demonstrates style, not present-scene history. Copy flags are reading aids, not semantic judgments. Raw-source manifest verification compares the exported original text and source hash; it does not independently verify parser-derived text or installed game files.\n");
        foreach (var c in result.Cases)
        {
            text.AppendLine($"## {c.Definition.Actor}: {c.Definition.Id}\n");
            text.AppendLine($"Scene: {c.Definition.Scene}\n\nFixed intention: {c.Definition.Intention.Kind} toward {c.Definition.Intention.Target}; {c.Definition.Intention.Motive}; {c.Definition.Intention.Stance}.");
            text.AppendLine("\nWithheld authored examples:");
            foreach (var e in c.Definition.AuthoredExamples) text.AppendLine($"- {e.Text} ({e.Note})");
            text.AppendLine("\nSource references (local review material):");
            foreach (var r in c.References) text.AppendLine($"- `{r.Id}` node {r.NodeIndex}: {r.Text}");
            foreach (var o in c.Observations) text.AppendLine($"\n**{o.Role}, replicate {o.Replicate}, seed {o.Seed}:** {o.Thought ?? o.CandidateThought ?? "(no parsed thought)"}"
                + (o.Error is null ? "" : " [FAILED: " + o.Error + "]") + (o.CopyFlags.Count == 0 ? "" : " [source-copy flag]"));
            text.AppendLine();
        }
        return text.ToString();
    }
    public static string Html(VoiceWordingResult result)
    {
        using Stream stream = typeof(VoiceWordingTrial).Assembly.GetManifestResourceStream("wording.html")
            ?? throw new InvalidOperationException("The wording viewer is not embedded.");
        using var reader = new StreamReader(stream);
        return EmbedHtml(reader.ReadToEnd(), result);
    }
    public static string EmbedHtml(string template, VoiceWordingResult result)
    {
        const string slot = "<script id=\"wording-data\" type=\"application/json\"></script>";
        int index = template.IndexOf(slot, StringComparison.Ordinal);
        if (index < 0 || template.IndexOf(slot, index + slot.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException("Wording viewer needs exactly one empty wording-data JSON slot.");
        return template.Replace(slot, "<script id=\"wording-data\" type=\"application/json\">"
            + JsonSerializer.Serialize(result, JsonOptions) + "</script>", StringComparison.Ordinal);
    }
}
