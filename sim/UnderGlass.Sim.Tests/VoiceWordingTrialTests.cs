using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnderGlass.ReflectionTrial;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class VoiceWordingTrialTests
{
    private const string Model = "test-model", GameVersion = "synthetic-1";
    private static readonly string AssetHash = new('a', 64), AssemblyHash = new('b', 64);
    private static VoiceWordingCase Case() => new("thanks", "Ann", "Adult", "Synthetic reviewed fixture.", "Bob",
        "Ordinary acquaintance.", "Bob helped Ann carry a box, and the work is finished.",
        new("Thanked", "Bob", "Appreciation for the help.", "Reserved but sincere."),
        new[] { new WordingAuthoredExample("WITHHELD CONTROL PROSE", "Human comparison only.") },
        Enumerable.Range(0, 3).Select(i => new WordingReferenceSelection("Characters/Dialogue/Ann:line" + i, 0)).ToArray());
    private static VoiceWordingPack Pack() => new(1, "Synthetic test cases.", new[] { Case() });
    private static CharacterInspirationPack Profiles() => new(1, "Synthetic profile fixtures.",
        new[] { new InspirationArchetype("reserved", "Care and autonomy.", "Withdraw under pressure.", "Plain, brief wording.", "Respect autonomy.", new[] { "fixture:voice" }) },
        new[] { new CharacterInspirationProfile("Ann", "reserved", "Dry humor.",
            new[] { new CharacterBackground("secret", "PRIVATE BIOGRAPHY MUST NOT ENTER WORDING PROMPTS", Array.Empty<string>(), new[] { "fixture:private" }) }) });
    private static JsonObject CorpusObject()
    {
        return JsonSerializer.SerializeToNode(new
        {
            schemaVersion = 1, gameVersion = GameVersion, gameAssemblySha256 = AssemblyHash,
            records = Enumerable.Range(0, 3).Select(i => new
            {
                id = "Characters/Dialogue/Ann:line" + i, speaker = "Ann", attribution = "dialogue-asset", category = "dialogue",
                raw = "Reference wording number " + i + ".", source = new { asset = "Characters/Dialogue/Ann", key = "line" + i, sha256 = AssetHash, gameVersion = GameVersion },
                conditions = new { rawKey = "line" + i }, parsed = new { warnings = Array.Empty<string>(), nodes = new[]
                {
                    new { kind = "text", role = "npc", raw = "Reference wording number " + i + ".", text = "Reference wording number " + i + ".", conditions = Array.Empty<string>(),
                        variant = new { random = 0, branch = 0, gender = 0 } },
                } },
            }),
        })!.AsObject();
    }
    private static string Corpus() => CorpusObject().ToJsonString();
    private static string Manifest() => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, gameVersion = GameVersion, gameAssemblySha256 = AssemblyHash, language = "en",
        assets = new[] { new { asset = "Characters/Dialogue/Ann", sha256 = AssetHash,
            entries = Enumerable.Range(0, 3).ToDictionary(i => "line" + i, i => "Reference wording number " + i + ".") } },
    });
    private static VoiceWordingOptions Options(TimeSpan? timeout = null) => new()
        { BaseUrl = "http://127.0.0.1:8080", Model = Model, Timeout = timeout ?? TimeSpan.FromSeconds(2), Replicates = 2 };
    private static Task<VoiceWordingResult> Run(Handler handler, VoiceWordingOptions? options = null, CancellationToken token = default)
        => VoiceWordingTrial.RunAsync(Pack(), Profiles(), VoiceWordingTrial.LoadReferences(Corpus(), Pack()), options ?? Options(), token, handler);

    [Fact]
    public void SelectedNodeIsExplicitAndItsConditionsVariantAndRawSourceSurvive()
    {
        var corpus = CorpusObject();
        var row = corpus["records"]![0]!;
        row["raw"] = "FIRST ALTERNATIVE|Selected ordinary words.";
        var nodes = row["parsed"]!["nodes"]!.AsArray();
        var selected = nodes[0]!.DeepClone();
        selected["text"] = "Selected ordinary words.";
        selected["conditions"] = new JsonArray("$d reviewedFlag");
        selected["variant"]!["branch"] = 1;
        nodes.Add(selected);
        var c = Case();
        var pack = Pack() with { Cases = new[] { c with { References = c.References.Select((r, i) => i == 0 ? r with { NodeIndex = 1 } : r).ToArray() } } };
        var loaded = VoiceWordingTrial.LoadReferences(corpus.ToJsonString(), pack);
        VoiceWordingReference actual = loaded.Records[0];
        Assert.Equal("Selected ordinary words.", actual.Text);
        Assert.Equal(1, actual.NodeIndex);
        Assert.Equal(1, actual.Variant.GetProperty("branch").GetInt32());
        Assert.Equal("$d reviewedFlag", actual.NodeConditions[0].GetString());
        Assert.Equal("line0", actual.Conditions.GetProperty("rawKey").GetString());
        Assert.Contains("FIRST ALTERNATIVE", actual.Raw);
        Assert.False(actual.RawSourceManifestVerified);
        Assert.Equal("corpus-parser", actual.TextDerivation);
        Assert.Throws<NotSupportedException>(() => ((IList<VoiceWordingReference>)loaded.Records).Clear());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-owner")]
    [InlineData("ambiguous")]
    [InlineData("wrong-category")]
    [InlineData("warning")]
    [InlineData("player")]
    [InlineData("control")]
    [InlineData("missing-node")]
    [InlineData("bad-hash")]
    [InlineData("wrong-version")]
    [InlineData("wrong-source")]
    [InlineData("substitution")]
    [InlineData("duplicate-id")]
    public void ReferencesCannotBorrowAnotherSpeakerOrPromotePlayerControlOrUnverifiedRows(string defect)
    {
        JsonObject corpus = CorpusObject();
        JsonNode row = corpus["records"]![0]!;
        var pack = Pack();
        switch (defect)
        {
            case "unknown": row["id"] = "Characters/Dialogue/Ann:missing"; break;
            case "wrong-owner": row["speaker"] = "Bob"; break;
            case "ambiguous": row["attribution"] = "key-hint"; break;
            case "wrong-category": row["category"] = "gift-reaction"; break;
            case "warning": row["parsed"]!["warnings"] = new JsonArray("Unresolved control."); break;
            case "player": row["parsed"]!["nodes"]![0]!["role"] = "player"; break;
            case "control": row["parsed"]!["nodes"]![0]!["kind"] = "conditional-command"; break;
            case "missing-node": row["parsed"]!["nodes"] = new JsonArray(); break;
            case "bad-hash": row["source"]!["sha256"] = "not-a-hash"; break;
            case "wrong-version": row["source"]!["gameVersion"] = "different"; break;
            case "wrong-source": row["source"]!["key"] = "line"; break;
            case "substitution": row["parsed"]!["nodes"]![0]!["text"] = "Hello @."; break;
            case "duplicate-id": corpus["records"]!.AsArray().Add(row.DeepClone()); break;
        }
        Assert.Throws<InvalidDataException>(() => VoiceWordingTrial.LoadReferences(corpus.ToJsonString(), pack));
    }

    [Fact]
    public void OptionalRawManifestChecksOriginalTextAndAssetHashInsteadOfTrustingCopiedMetadata()
    {
        var loaded = VoiceWordingTrial.LoadReferences(Corpus(), Pack(), Manifest());
        Assert.All(loaded.Records, r => Assert.True(r.RawSourceManifestVerified));
        JsonObject changed = CorpusObject();
        changed["records"]![0]!["raw"] = "Changed original text.";
        Assert.Throws<InvalidDataException>(() => VoiceWordingTrial.LoadReferences(changed.ToJsonString(), Pack(), Manifest()));
        changed = CorpusObject();
        changed["records"]![0]!["source"]!["sha256"] = new string('c', 64);
        Assert.Throws<InvalidDataException>(() => VoiceWordingTrial.LoadReferences(changed.ToJsonString(), Pack(), Manifest()));
    }

    [Fact]
    public async Task BothArmsShareFixedIntentionFrameAndSeedAndOnlySourceArmGetsSelectedSpeech()
    {
        var handler = new Handler();
        VoiceWordingResult result = await Run(handler);
        Assert.Equal(4, result.AnswerCount);
        Assert.Equal(0, result.FailureCount);
        var observations = Assert.Single(result.Cases).Observations;
        Assert.Equal(observations[0].Seed, observations[1].Seed);
        Assert.Equal(observations[2].Seed, observations[3].Seed);
        Assert.NotEqual(observations[0].Seed, observations[2].Seed);
        for (int i = 0; i < 4; i += 2)
        {
            JsonNode baseline = JsonNode.Parse(observations[i].Prompt)!;
            JsonNode sourced = JsonNode.Parse(observations[i + 1].Prompt)!;
            Assert.Equal(baseline["messages"]![0]!.ToJsonString(), sourced["messages"]![0]!.ToJsonString());
            string baseFrame = baseline["messages"]![1]!["content"]!.GetValue<string>();
            string sourceFrame = sourced["messages"]![1]!["content"]!.GetValue<string>();
            const string referenceMarker = "\n\nDialogue style references (not scene facts):\n";
            Assert.DoesNotContain(referenceMarker, baseFrame);
            Assert.Equal(baseFrame, sourceFrame[..sourceFrame.IndexOf(referenceMarker, StringComparison.Ordinal)]);
            Assert.Equal(3, sourceFrame[(sourceFrame.IndexOf(referenceMarker, StringComparison.Ordinal) + referenceMarker.Length)..].Split('\n').Length);
            Assert.Contains("Intention kind: Thanked", baseFrame);
            Assert.Contains("Plan (not yet done): thank Bob.", baseFrame);
            Assert.Contains("Target: Bob", baseFrame);
            Assert.Equal(120, baseline["max_tokens"]!.GetValue<int>());
            Assert.Contains("You are Ann (Adult).", baseFrame);
        }
        Assert.Equal("plain-frame-v2", result.PromptVersion);
        Assert.All(handler.Packets, p =>
        {
            Assert.DoesNotContain("PRIVATE BIOGRAPHY", p);
            Assert.DoesNotContain("WITHHELD CONTROL", p);
            Assert.DoesNotContain("suggestedChoice", p);
            Assert.DoesNotContain("choices", p);
        });
        Assert.All(handler.Uris, uri => Assert.Equal("/v1/chat/completions", uri.AbsolutePath));
        Assert.All(observations, o => { Assert.True(o.FormatValid); Assert.True(o.SemanticReviewRequired); Assert.NotNull(o.RawResponse); });
    }

    [Fact]
    public async Task MutatingCallerListsDuringCallsCannotChangeFramesHashesOrWithheldControls()
    {
        var cases = new List<VoiceWordingCase> { Case() };
        var pack = Pack() with { Cases = cases };
        var profiles = new List<CharacterInspirationProfile>(Profiles().Characters);
        var profilePack = Profiles() with { Characters = profiles };
        string hash = pack.ContentHash, profileHash = profilePack.ContentHash;
        var handler = new Handler((request, packet, token) =>
        {
            cases.Clear(); profiles.Clear();
            return Task.FromResult(Reply(packet));
        });
        var result = await VoiceWordingTrial.RunAsync(pack, profilePack, VoiceWordingTrial.LoadReferences(Corpus(), pack), Options(), handler: handler);
        Assert.Equal(hash, result.PackHash);
        Assert.Equal(profileHash, result.ProfilePackHash);
        Assert.Equal(4, result.AnswerCount);
        Assert.Equal("WITHHELD CONTROL PROSE", Assert.Single(result.Cases[0].Definition.AuthoredExamples).Text);
    }

    [Theory]
    [InlineData("extra-field")]
    [InlineData("empty")]
    [InlineData("too-long")]
    [InlineData("not-json")]
    [InlineData("wrong-model")]
    [InlineData("wrong-seed")]
    [InlineData("missing-device")]
    [InlineData("length")]
    public async Task InvalidRepliesAndRoutingAreExplicitFailuresWithoutAuthoredSubstitution(string defect)
    {
        var handler = new Handler((request, packet, token) =>
        {
            JsonObject body = ReplyObject(packet);
            switch (defect)
            {
                case "extra-field": body["choices"]![0]!["message"]!["content"] = "{\"thought\":\"Thanks.\",\"suggestedChoice\":\"help\"}"; break;
                case "empty": body["choices"]![0]!["message"]!["content"] = "{\"thought\":\" \"}"; break;
                case "too-long": body["choices"]![0]!["message"]!["content"] = JsonSerializer.Serialize(new { thought = new string('x', 181) }); break;
                case "not-json": body["choices"]![0]!["message"]!["content"] = "I should thank Bob."; break;
                case "wrong-model": body["model"] = "other-model"; break;
                case "wrong-seed": body["seed"] = 0L; break;
                case "missing-device": body.Remove("device"); break;
                case "length": body["choices"]![0]!["finish_reason"] = "length"; break;
            }
            return Task.FromResult(Response(body.ToJsonString()));
        });
        VoiceWordingResult result = await Run(handler);
        Assert.Equal(4, result.FailureCount);
        Assert.Equal(2, result.ExitCode);
        Assert.All(result.Cases[0].Observations, o => { Assert.Null(o.Thought); Assert.NotNull(o.Error); Assert.NotNull(o.RawResponse); });
        if (defect == "too-long") Assert.All(result.Cases[0].Observations, o => Assert.Equal(new string('x', 181), o.CandidateThought));
        if (defect == "extra-field") Assert.All(result.Cases[0].Observations, o => Assert.Equal("Thanks.", o.CandidateThought));
    }

    [Fact]
    public async Task TimeoutIsRecordedButExternalCancellationStopsTheExperiment()
    {
        var slow = new Handler(async (_, _, token) => { await Task.Delay(5000, token); throw new Exception("Unreachable."); });
        VoiceWordingResult result = await Run(slow, Options(TimeSpan.FromMilliseconds(10)));
        Assert.Equal(4, result.FailureCount);
        Assert.All(result.Cases[0].Observations, o => Assert.Contains("timed out", o.Error));
        using var cancel = new CancellationTokenSource();
        var cancelled = new Handler((_, packet, _) => { cancel.Cancel(); return Task.FromResult(Reply(packet)); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(cancelled, token: cancel.Token));
        Assert.Single(cancelled.Packets);
        var preCancelled = new Handler();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(preCancelled, token: cancel.Token));
        Assert.Empty(preCancelled.Packets);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("changed-endpoint")]
    [InlineData("oversized")]
    [InlineData("lying-size")]
    [InlineData("http-error")]
    public async Task TransportFailuresAreBoundedAndNeverFollowedOrRetried(string defect)
    {
        var handler = new Handler((_, packet, _) =>
        {
            var response = Reply(packet);
            if (defect == "redirect") { response.StatusCode = HttpStatusCode.Redirect; response.Headers.Location = new Uri("http://example.com/"); }
            if (defect == "changed-endpoint") response.RequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://example.com/");
            if (defect == "oversized") response.Content = new StringContent(new string('x', 65537));
            if (defect == "lying-size") { response.Content = new StringContent(new string('x', 65537)); response.Content.Headers.ContentLength = 1; }
            if (defect == "http-error") response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return Task.FromResult(response);
        });
        var result = await Run(handler);
        Assert.Equal(4, handler.Packets.Count);
        Assert.Equal(4, result.FailureCount);
        Assert.All(result.Cases[0].Observations, o => Assert.Null(o.Thought));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    public async Task CopiedSourceWordingIsFlaggedEvenWhenItsLengthFailsFormatValidation(int padding)
    {
        var handler = new Handler((_, packet, _) => Task.FromResult(Reply(packet, "Reference wording number 0." + new string('x', padding))));
        var result = await Run(handler);
        Assert.Equal(padding == 0 ? 0 : 4, result.FailureCount);
        Assert.Equal(4, result.CopyFlagCount);
        Assert.All(result.Cases[0].Observations, o => Assert.Equal(padding == 0 ? "exact normalized line" : "full normalized substring", Assert.Single(o.CopyFlags).Match));
    }

    [Fact]
    public async Task InputValidationPreventsCallsAndEmbeddedJsonEscapesMarkup()
    {
        var handler = new Handler();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(handler, Options() with { BaseUrl = "http://example.com" }));
        Assert.Empty(handler.Packets);
        var pack = Pack();
        Assert.Throws<InvalidDataException>(() => VoiceWordingPack.Read(JsonSerializer.Serialize(pack with
            { Cases = new[] { Case() with { Intention = Case().Intention with { Target = "Cara" } } } })));
        Assert.Throws<InvalidDataException>(() => VoiceWordingPack.Read(JsonSerializer.Serialize(pack with { Cases = new[] { Case(), Case() } })));
        var changed = pack with { Provenance = "Edited case pack." };
        await Assert.ThrowsAsync<InvalidDataException>(() => VoiceWordingTrial.RunAsync(changed, Profiles(), VoiceWordingTrial.LoadReferences(Corpus(), pack), Options(), handler: new Handler()));
        var result = await Run(new Handler((_, packet, _) => Task.FromResult(Reply(packet, "</script><script>bad()</script>"))));
        const string slot = "<script id=\"wording-data\" type=\"application/json\"></script>";
        string embedded = VoiceWordingTrial.EmbedHtml(slot, result);
        Assert.DoesNotContain("</script><script>bad", embedded);
        Assert.Contains("\\u003C", embedded);
        Assert.Throws<InvalidDataException>(() => VoiceWordingTrial.EmbedHtml(slot + slot, result));
    }

    [Fact]
    public async Task ReportSeedsRemainExactStringsInJsonAndEmbeddedHtmlAndReadOldNumericReceipts()
    {
        const long seed = 4434080816128553118;
        var result = await Run(new Handler());
        var c = result.Cases[0];
        var observation = c.Observations[0] with { Seed = seed,
            Receipt = c.Observations[0].Receipt! with { Seed = seed } };
        result = result with { Cases = new[] { c with { Observations = new[] { observation } } } };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        string json = JsonSerializer.Serialize(result, options);
        using var report = JsonDocument.Parse(json);
        var stored = report.RootElement.GetProperty("cases")[0].GetProperty("observations")[0];
        Assert.Equal("4434080816128553118", stored.GetProperty("seed").GetString());
        Assert.Equal("4434080816128553118", stored.GetProperty("receipt").GetProperty("seed").GetString());
        const string slot = "<script id=\"wording-data\" type=\"application/json\"></script>";
        string embedded = VoiceWordingTrial.EmbedHtml(slot, result);
        using var embeddedJson = JsonDocument.Parse(embedded[(embedded.IndexOf('>') + 1)..embedded.LastIndexOf("</script>", StringComparison.Ordinal)]);
        var embeddedObservation = embeddedJson.RootElement.GetProperty("cases")[0].GetProperty("observations")[0];
        Assert.Equal(stored.GetProperty("seed").GetString(), embeddedObservation.GetProperty("seed").GetString());
        Assert.Equal(stored.GetProperty("receipt").GetProperty("seed").GetString(), embeddedObservation.GetProperty("receipt").GetProperty("seed").GetString());
        Assert.Equal(seed, JsonSerializer.Deserialize<VoiceWordingResult>(json, options)!.Cases[0].Observations[0].Seed);
        string oldJson = json.Replace("\"4434080816128553118\"", "4434080816128553118", StringComparison.Ordinal);
        var oldObservation = JsonSerializer.Deserialize<VoiceWordingResult>(oldJson, options)!.Cases[0].Observations[0];
        Assert.Equal(seed, oldObservation.Seed);
        Assert.Equal(seed, oldObservation.Receipt!.Seed);
    }

    [Fact]
    public async Task OversizedLaterFrameIsRejectedBeforeAnyHttpCall()
    {
        string actor = new('A', 100), subject = new('B', 100);
        var corpus = CorpusObject();
        var keys = new List<WordingReferenceSelection>();
        for (int i = 0; i < 5; i++)
        {
            var row = corpus["records"]![0]!.DeepClone();
            string key = "long" + i, id = "Characters/Dialogue/" + actor + ":" + key;
            row["id"] = id; row["speaker"] = actor; row["raw"] = new string('r', 600);
            row["source"]!["asset"] = "Characters/Dialogue/" + actor; row["source"]!["key"] = key;
            row["parsed"]!["nodes"]![0]!["text"] = new string('r', 600);
            row["parsed"]!["nodes"]![0]!["raw"] = new string('r', 600);
            corpus["records"]!.AsArray().Add(row);
            keys.Add(new(id, 0));
        }
        var late = Case() with { Id = "zz-late", Actor = actor, Subject = subject, LifeStage = new string('l', 80),
            Relationship = new string('r', 300), Scene = new string('s', 600), References = keys,
            Intention = new(new string('k', 100), subject, new string('m', 300), new string('s', 300)) };
        var pack = Pack() with { Cases = new[] { Case(), late } };
        var profilePack = Profiles();
        profilePack = profilePack with
        {
            Archetypes = profilePack.Archetypes.Concat(new[] { new InspirationArchetype("maximum", new string('d', 150),
                new string('c', 80), new string('v', 105), "A brief cue.", new[] { "fixture:voice" }) }).ToArray(),
            Characters = profilePack.Characters.Concat(new[] { new CharacterInspirationProfile(actor, "maximum", "", Array.Empty<CharacterBackground>()) }).ToArray(),
        };
        var handler = new Handler();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => VoiceWordingTrial.RunAsync(pack, profilePack,
            VoiceWordingTrial.LoadReferences(corpus.ToJsonString(), pack), Options(), handler: handler));
        Assert.Contains("6000-character", error.Message);
        Assert.Empty(handler.Packets);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"one\"}")]
    [InlineData("{\"schemaVersion\":1,\"gameVersion\":\"test\",\"records\":[42]}")]
    public void MalformedCorpusShapeIsReportedAsDataError(string json)
        => Assert.Throws<InvalidDataException>(() => VoiceWordingTrial.LoadReferences(json, Pack()));

    [Fact]
    public void ShippedCasesUseValidProfilesAndHoldOnlyPreparedGratitudePlans()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "sim", "voice-wording-cases.json"))) root = root.Parent;
        Assert.NotNull(root);
        var pack = VoiceWordingPack.Read(File.ReadAllText(Path.Combine(root!.FullName, "sim", "voice-wording-cases.json")));
        var profiles = CharacterInspirationPack.Read(File.ReadAllText(Path.Combine(root.FullName, "sim", "character-inspiration.json")));
        Assert.Equal(6, pack.Cases.Count);
        Assert.Equal(new[] { "George", "Maru", "Vincent" }, pack.Cases.Select(c => c.Actor).Distinct().Order(StringComparer.Ordinal));
        Assert.All(pack.Cases, c =>
        {
            Assert.Equal("Thanked", c.Intention.Kind);
            Assert.Equal(c.Subject, c.Intention.Target);
            Assert.NotNull(profiles.ForCharacter(c.Actor, c.Subject, 0, c.Id));
            Assert.All(c.References, r => Assert.StartsWith("Characters/Dialogue/" + c.Actor + ":", r.RecordId));
        });
    }

    private static JsonObject ReplyObject(string packet, string thought = "I should thank Bob for lending a hand.")
    {
        using var prompt = JsonDocument.Parse(packet);
        return JsonSerializer.SerializeToNode(new
        {
            model = Model, seed = prompt.RootElement.GetProperty("seed").GetInt64(), revision = new string('c', 40), device = "cpu",
            usage = new { input_tokens = 100, output_tokens = 20, elapsed_ms = 20.0 },
            choices = new[] { new { message = new { role = "assistant", content = JsonSerializer.Serialize(new { thought }) }, finish_reason = "stop" } },
        })!.AsObject();
    }
    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Reply(string packet, string thought = "I should thank Bob for lending a hand.") => Response(ReplyObject(packet, thought).ToJsonString());
    private sealed class Handler(Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>>? answer = null) : HttpMessageHandler
    {
        public List<string> Packets { get; } = new();
        public List<Uri> Uris { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string packet = await request.Content!.ReadAsStringAsync(token);
            Packets.Add(packet); Uris.Add(request.RequestUri!);
            return answer is null ? Reply(packet) : await answer(request, packet, token);
        }
    }
}
