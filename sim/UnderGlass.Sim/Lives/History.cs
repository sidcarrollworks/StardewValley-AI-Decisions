using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnderGlass.Sim.Lives;

public enum HistoryKind { Fact, Past, Practice }
public enum HistorySourceKind { Canon, Authored, ProductDecision }
public enum KnowledgeKind { Lived, Witnessed, Told, Inferred, Reading }
public enum ReadingOrigin { CanonStated, Authored, ExperimentalStated }
public enum AdmissionRule { R1, R2 }
public enum ReviewStatus { Proposed, Reviewed, Rejected }
public enum ReadingStance { None, Warm, Wary, Expects }
public enum MemoryObjectKind { Person, Kind, Place, Activity, Situation }
public enum ExpectationKind { Whereabouts, Availability, Answers, FollowThrough, Activity }

public sealed record HistorySource(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Key,
    [property: JsonRequired] HistorySourceKind Kind);
public sealed record HistoryCue([property: JsonRequired] string Key, string? SourceId = null);
public sealed record MemoryObject(
    [property: JsonRequired] MemoryObjectKind Kind,
    [property: JsonRequired] string Key);
public sealed record ExpectationKey(
    [property: JsonRequired] ExpectationKind Kind,
    [property: JsonRequired] string Subject,
    [property: JsonRequired] string Context);
public sealed record HistoryFact(
    [property: JsonRequired] string Id,
    [property: JsonRequired] HistoryKind Kind,
    [property: JsonRequired] IReadOnlyList<string> Sources);

/// <summary>One holder's account. Authoring sources are provenance, not in-world acquisition.</summary>
public sealed record HistoryAccount(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string FactId,
    [property: JsonRequired] string Holder,
    [property: JsonRequired] string Text,
    [property: JsonRequired] KnowledgeKind How,
    [property: JsonRequired] IReadOnlyList<string> Sources,
    [property: JsonRequired] IReadOnlyList<HistoryCue> Cues,
    string? Teller = null);
public sealed record HistoryReview(
    [property: JsonRequired] ReviewStatus Status,
    string? By = null, string? Date = null);

/// <summary>A private reading of exactly one account, never a fact about another holder's mind.
/// Authored interpretations cannot seed numbers. ExperimentalStated explicitly authors the
/// character’s own present starting attitude for an experiment; it is never a canon claim.</summary>
public sealed record HistoryReading(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string AccountId,
    [property: JsonRequired] MemoryObject Object,
    [property: JsonRequired] ReadingStance Stance,
    [property: JsonRequired] string Text,
    [property: JsonRequired] ReadingOrigin Origin,
    [property: JsonRequired] AdmissionRule? Admission,
    [property: JsonRequired] HistoryReview Review,
    [property: JsonRequired] IReadOnlyList<string> Sources,
    [property: JsonRequired] IReadOnlyList<HistoryCue> Cues,
    string? PresentLinkSource = null, ExpectationKey? Expectation = null, bool? ExpectedSuccess = null);

/// <summary>Versioned, explicitly reviewed input. Validation does not itself admit a record:
/// proposed and rejected readings remain here for audit, outside runtime recall and seeding.</summary>
public sealed record HistoryPack(
    [property: JsonRequired] int Version,
    [property: JsonRequired] IReadOnlyList<HistorySource> Sources,
    [property: JsonRequired] IReadOnlyList<HistoryFact> Facts,
    [property: JsonRequired] IReadOnlyList<HistoryAccount> Accounts,
    [property: JsonRequired] IReadOnlyList<HistoryReading> Readings)
{
    private const int MaxJsonCharacters = 8 * 1024 * 1024;
    private const int MaxRecords = 20_000;
    private const int MaxReferences = 128;
    private static readonly JsonSerializerOptions ReadOptions = Options();

    public static HistoryPack Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonCharacters)
            throw Invalid("Expected a history pack of at most 8 MiB of JSON characters.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            CheckUniqueProperties(document.RootElement);
            return (document.RootElement.Deserialize<HistoryPack>(ReadOptions)
                ?? throw Invalid("The history pack is null.")).CopyValidated();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid history pack JSON: " + ex.Message, ex);
        }
    }

    /// <summary>Validate and detach all caller-owned collections. Ordering never depends on
    /// current culture or JSON property/array order. Input records themselves are not modified.</summary>
    public HistoryPack CopyValidated()
    {
        if (Version != 1) throw Invalid("Only history pack version 1 is supported.");
        List(Sources, "sources");
        List(Facts, "facts");
        List(Accounts, "accounts");
        List(Readings, "readings");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var sources = new Dictionary<string, HistorySource>(StringComparer.Ordinal);
        foreach (HistorySource source in Sources)
        {
            AddId(source.Id, ids);
            Text(source.Key, 200, "source key");
            Defined(source.Kind);
            sources.Add(source.Id, source with { });
        }

        var facts = new Dictionary<string, HistoryFact>(StringComparer.Ordinal);
        foreach (HistoryFact fact in Facts)
        {
            AddId(fact.Id, ids);
            Defined(fact.Kind);
            facts.Add(fact.Id, fact with { Sources = References(fact.Sources, sources, fact.Id) });
        }

        var accounts = new Dictionary<string, HistoryAccount>(StringComparer.Ordinal);
        var accountKeys = new HashSet<(string Holder, string Fact)>();
        foreach (HistoryAccount account in Accounts)
        {
            AddId(account.Id, ids);
            RecallId(account.Id);
            Text(account.Holder, 200, "account holder");
            Text(account.Text, 2000, "account text");
            Defined(account.How);
            if (account.How == KnowledgeKind.Reading)
                throw Invalid("An account needs lived, witnessed, told or inferred acquisition; a reading belongs in its separate record.");
            HistoryFact fact = Reference(account.FactId, facts, "account fact");
            if (!accountKeys.Add((account.Holder, account.FactId)))
                throw Invalid($"Duplicate account for {account.Holder} and {account.FactId}; combine its citations.");
            if (account.How == KnowledgeKind.Told) Text(account.Teller, 200, "account teller");
            else if (account.Teller is not null) throw Invalid("Only a Told account can name a teller.");
            IReadOnlyList<string> references = References(account.Sources, sources, account.Id);
            // A fact's citation can substantiate a shared cue, but never gives another holder an account.
            var cueSources = references.Concat(fact.Sources).ToHashSet(StringComparer.Ordinal);
            accounts.Add(account.Id, account with
            {
                Sources = references,
                Cues = CuesCopy(account.Cues, sources, cueSources, account.Id),
            });
        }

        var readings = new List<HistoryReading>();
        var associationKeys = new HashSet<(string Holder, MemoryObject Object, ReadingStance Stance)>();
        var expectationKeys = new HashSet<(string Holder, ExpectationKey Key)>();
        foreach (HistoryReading reading in Readings)
        {
            AddId(reading.Id, ids);
            RecallId(reading.Id);
            HistoryAccount account = Reference(reading.AccountId, accounts, "reading account");
            HistoryFact fact = facts[account.FactId];
            if (reading.Object is null) throw Invalid("Missing reading object.");
            Defined(reading.Object.Kind);
            Text(reading.Object.Key, 200, "reading object key");
            Defined(reading.Stance);
            Defined(reading.Origin);
            if (reading.Admission is { } admission) Defined(admission);
            if (reading.Origin == ReadingOrigin.CanonStated && reading.Admission is null)
                throw Invalid("A canon-stated reading requires R1 or R2 admission.");
            if (reading.Origin != ReadingOrigin.CanonStated && reading.Admission is not null)
                throw Invalid("An authored interpretation has no canon admission rule; its admission must be null.");
            Text(reading.Text, 2000, "reading text");
            CheckReview(reading.Review);
            IReadOnlyList<string> references = References(reading.Sources, sources, reading.Id);
            if (reading.Origin == ReadingOrigin.CanonStated && !references.Any(s => sources[s].Kind == HistorySourceKind.Canon))
                throw Invalid("A CanonStated reading needs a canon source.");
            if (reading.Origin == ReadingOrigin.ExperimentalStated)
            {
                if (!references.Any(s => sources[s].Kind == HistorySourceKind.Authored))
                    throw Invalid("An experimental character statement requires an Authored source.");
                if (fact.Kind == HistoryKind.Past && reading.Stance != ReadingStance.None)
                    throw Invalid("An experimental past is recall-only; author a separate present attitude to seed feelings.");
                if (reading.PresentLinkSource is not null)
                    throw Invalid("An experimental statement cannot claim a canon present link.");
            }
            if (reading.PresentLinkSource is not null)
            {
                HistorySource source = Reference(reading.PresentLinkSource, sources, "present link source");
                if (source.Kind != HistorySourceKind.Canon || !references.Contains(source.Id, StringComparer.Ordinal))
                    throw Invalid("A present link must cite a canon source in this reading's sources.");
            }
            if (fact.Kind == HistoryKind.Past && reading.Stance != ReadingStance.None &&
                reading.Origin == ReadingOrigin.CanonStated &&
                (reading.Admission != AdmissionRule.R1 || reading.PresentLinkSource is null))
                throw Invalid("A past's numeric reading needs R1 and its holder's canon-stated present link.");
            if (reading.Stance == ReadingStance.Expects)
            {
                if (reading.Expectation is null || reading.ExpectedSuccess is null)
                    throw Invalid("An Expects reading needs an expectation key and an expected outcome.");
                Defined(reading.Expectation.Kind);
                Text(reading.Expectation.Subject, 200, "expectation subject");
                Text(reading.Expectation.Context, 200, "expectation context");
            }
            else if (reading.Expectation is not null || reading.ExpectedSuccess is not null)
                throw Invalid("Only an Expects reading can carry an expectation or expected outcome.");

            // Warm and wary remain distinct; repeat citations of the same seed do not add strength.
            // Audit-only interpretations/revisions are not seeds and can coexist with an admitted reading.
            if (reading.Review.Status == ReviewStatus.Reviewed && reading.Origin != ReadingOrigin.Authored &&
                reading.Stance != ReadingStance.None &&
                !(reading.Stance == ReadingStance.Expects
                    ? expectationKeys.Add((account.Holder, reading.Expectation!))
                    : associationKeys.Add((account.Holder, reading.Object, reading.Stance))))
                throw Invalid($"Duplicate seed for {account.Holder}, {reading.Object.Key} and {reading.Stance}; combine its citations.");
            readings.Add(reading with
            {
                Object = reading.Object with { },
                Review = reading.Review with { },
                Sources = references,
                Cues = CuesCopy(reading.Cues, sources, references.ToHashSet(StringComparer.Ordinal), reading.Id),
                Expectation = reading.Expectation is null ? null : reading.Expectation with { },
            });
        }
        return new HistoryPack(Version,
            Frozen(sources.Values.OrderBy(s => s.Id, StringComparer.Ordinal)),
            Frozen(facts.Values.OrderBy(f => f.Id, StringComparer.Ordinal)),
            Frozen(accounts.Values.OrderBy(a => a.Id, StringComparer.Ordinal)),
            Frozen(readings.OrderBy(r => r.Id, StringComparer.Ordinal)));
    }

    private static void CheckReview(HistoryReview? review)
    {
        if (review is null) throw Invalid("Every reading requires an explicit review status.");
        Defined(review.Status);
        if (review.By is not null) Text(review.By, 200, "reviewer");
        if (review.Date is not null && !DateOnly.TryParseExact(review.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _)) throw Invalid("Review date must be a calendar date in yyyy-MM-dd form.");
        if (review.Status == ReviewStatus.Reviewed && (review.By is null || review.Date is null))
            throw Invalid("A reviewed reading requires a reviewer and review date.");
    }

    private static IReadOnlyList<string> References(IReadOnlyList<string>? references,
        IReadOnlyDictionary<string, HistorySource> sources, string owner)
    {
        List(references, owner + " sources", MaxReferences);
        if (references!.Count == 0) throw Invalid($"Missing provenance for {owner}.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string reference in references)
        {
            Reference(reference, sources, "source");
            if (!seen.Add(reference)) throw Invalid($"Repeated source {reference} on {owner}.");
        }
        return Frozen(seen.OrderBy(s => s, StringComparer.Ordinal));
    }

    private static IReadOnlyList<HistoryCue> CuesCopy(IReadOnlyList<HistoryCue>? cues,
        IReadOnlyDictionary<string, HistorySource> sources, ISet<string> allowedSources, string owner)
    {
        List(cues, owner + " cues", MaxReferences);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var copied = new List<HistoryCue>();
        foreach (HistoryCue cue in cues!)
        {
            Text(cue.Key, 200, "cue key");
            if (!keys.Add(cue.Key)) throw Invalid($"Repeated cue {cue.Key} on {owner}.");
            if (cue.SourceId is not null)
            {
                HistorySource source = Reference(cue.SourceId, sources, "cue source");
                if (source.Kind != HistorySourceKind.Canon || !allowedSources.Contains(cue.SourceId))
                    throw Invalid($"Sourced cue {cue.Key} needs a canon citation belonging to {owner}.");
            }
            copied.Add(cue with { });
        }
        return Frozen(copied.OrderBy(c => c.Key, StringComparer.Ordinal));
    }

    private static T Reference<T>(string? key, IReadOnlyDictionary<string, T> records, string name)
    {
        Text(key, 200, name);
        return records.TryGetValue(key!, out T? record) ? record : throw Invalid($"Unknown {name}: {key}.");
    }

    private static void List<T>(IReadOnlyList<T>? items, string name, int limit = MaxRecords)
    {
        if (items is null || items.Count > limit || items.Any(i => i is null))
            throw Invalid($"Invalid {name}: expected a non-null list with no null entries and at most {limit} entries.");
    }

    private static IReadOnlyList<T> Frozen<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());
    private static void AddId(string id, ISet<string> ids)
    {
        Text(id, 200, "ID");
        if (!ids.Add(id)) throw Invalid($"Duplicate history ID: {id}.");
    }
    private static void RecallId(string id)
    {
        if (id.StartsWith("association:", StringComparison.Ordinal) || id.StartsWith("expectation:", StringComparison.Ordinal))
            throw Invalid("Account and reading IDs cannot use the reserved association: or expectation: prefix.");
    }
    private static void Defined<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw Invalid($"Unknown {typeof(T).Name}: {value}.");
    }
    private static void Text(string? value, int limit, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || value.Any(char.IsControl) || !LivesText.IsWellFormed(value))
            throw Invalid($"Invalid {name}: expected nonempty Unicode text of at most {limit} characters without control characters.");
    }
    private static InvalidDataException Invalid(string message) => new(message);

    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16,
        };
        options.Converters.Add(new EnumNames<HistoryKind>());
        options.Converters.Add(new EnumNames<HistorySourceKind>());
        options.Converters.Add(new EnumNames<KnowledgeKind>());
        options.Converters.Add(new EnumNames<ReadingOrigin>());
        options.Converters.Add(new EnumNames<AdmissionRule>());
        options.Converters.Add(new EnumNames<ReviewStatus>());
        options.Converters.Add(new EnumNames<ReadingStance>());
        options.Converters.Add(new EnumNames<MemoryObjectKind>());
        options.Converters.Add(new EnumNames<ExpectationKind>());
        return options;
    }

    private static void CheckUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw Invalid($"Duplicate JSON property {property.Name}.");
                CheckUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) CheckUniqueProperties(item);
    }

    // Accept explicit enum names only, including rejecting numeric strings and unnamed values.
    private sealed class EnumNames<T> : JsonConverter<T> where T : struct, Enum
    {
        private static readonly HashSet<string> Names = Enum.GetNames<T>().ToHashSet(StringComparer.Ordinal);
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { } name || !Names.Contains(name))
                throw new JsonException($"Expected a named {typeof(T).Name} value.");
            return Enum.Parse<T>(name);
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (!Enum.IsDefined(value)) throw new JsonException($"Unknown {typeof(T).Name}.");
            writer.WriteStringValue(value.ToString());
        }
    }
}

internal static class LivesText
{
    // URI/JSON encoding replaces unpaired surrogates, which would collapse distinct causal IDs.
    internal static bool IsWellFormed(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (++i == value.Length || !char.IsLowSurrogate(value[i])) return false;
            }
            else if (char.IsLowSurrogate(value[i])) return false;
        }
        return true;
    }
}
