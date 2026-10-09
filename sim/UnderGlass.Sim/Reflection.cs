namespace UnderGlass.Sim;

/// <summary>Quiet reflection is an opt-in experiment, separate from the ordinary desire gate.</summary>
public sealed record ReflectionOptions
{
    public bool Enabled { get; init; }
    public int QuietMinutes { get; init; } = 30;
    public double DailyChance { get; init; } = 0.35;
    public int IntentionDays { get; init; } = 3;
    /// <summary>At most two continuations of one deferred idea, each requiring new known events.
    /// Zero keeps deferral final; it does not disable independent thoughts about new sources.</summary>
    public int MaxReconsiderations { get; init; } = 2;
    public int ReconsiderAfterDays { get; init; } = 1;
    /// <summary>Optional waking inspiration from a recorded sleep; zero preserves quiet-only runs.
    /// A missed dream leaves the daytime opportunity available, with one submitted thought per day.</summary>
    public double DreamChance { get; init; }
}

/// <summary>An authored line with an executable act; an empty Kind means defer or reject.</summary>
public sealed record ReflectionChoice(string Id, string Kind, string Line);

/// <summary>The kind as the character remembers it, and their part in it. Valence is the known
/// kind's signed affect, not access to anyone else's feelings or to an act's hidden true actor.</summary>
public sealed record ReflectionSourceFacts(string Kind, bool OwnDeed, double Valence, double Regard);

/// <summary>An authored possibility, distinct from the character's eventual response. Callers
/// can replace Proposal on a copied request to compare evaluations of the same scene.</summary>
public sealed record ReflectionProposal(string Id, string Thought, string SuggestedChoice, IReadOnlyList<string> Tags);

/// <summary>A link to an earlier deferred idea, not an assertion that the idea was true.
/// PriorThought is a bounded excerpt; the full original remains in the record named by PriorId.</summary>
public sealed record ReflectionContinuity(string RootId, string PriorId, int Revision, int PriorSourceActId,
    string PriorThought, string Reason);

/// <summary>The person's own sleep interval that prompted an imagined possibility on waking.
/// No dream is an observed world event; a null opportunity on a request means ordinary quiet time.</summary>
public sealed record ReflectionOpportunity(string Kind, int SleptAt, int WokeAt);

/// <summary>A copy of one person's known context. Providers must never inspect the simulation.</summary>
public sealed record ReflectionRequest(string Id, int Tick, string Actor, string Subject, int SourceActId,
    string Memory, string Context, IReadOnlyList<ReflectionChoice> Choices,
    ReflectionSourceFacts? Source = null, ReflectionProposal? Proposal = null, ReflectionContinuity? Continuity = null,
    ReflectionOpportunity? Opportunity = null);

/// <summary>One independently inspectable evaluator pass, including its temporary-label mapping
/// back to the engine's offered choices. Multiple passes do not mean multiple acceptance draws.</summary>
public sealed record ReflectionEvaluation(int Pass, string Prompt, string? Response,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LabelsToChoiceIds,
    IReadOnlyDictionary<string, double>? Weights = null, string? Error = null);

/// <summary>Imagination proposes; the evaluator weights the actual lines. The engine samples once.
/// Backend and Note identify real calls versus authored/fallback material; they are recorded.</summary>
public sealed record ReflectionAnswer(string Thought, string SuggestedChoice,
    IReadOnlyDictionary<string, double> Weights, string Backend, string? Note = null,
    string? LayaPrompt = null, string? GenerationPrompt = null, string? LayaResponse = null,
    IReadOnlyList<ReflectionEvaluation>? Evaluations = null, string? GenerationResponse = null);

public interface IReflectionMind
{
    Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A timestamped change, never a fabricated memory or witnessed act.</summary>
public sealed record ReflectionEvent(int Tick, string Status, string Text, int ActId = -1);

/// <summary>A thought and its inspectable provenance. Events are filtered by the replay clock.</summary>
public sealed record ReflectionRecord(ReflectionRequest Request, ReflectionAnswer Answer, string Choice,
    IReadOnlyList<ReflectionEvent> Events);

/// <summary>Explicit authored baseline for offline comparisons, not an imitation of model output.</summary>
public sealed class AuthoredReflectionMind : IReflectionMind
{
    public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReflectionProposal proposal = request.Proposal ?? ReflectionCatalog.Propose(request);
        if (string.IsNullOrWhiteSpace(proposal.Thought) || proposal.Thought.Length > 300
            || !request.Choices.Any(c => c.Id == proposal.SuggestedChoice && c.Kind.Length > 0))
            throw new ArgumentException("The prepared reflection proposal must contain a short thought and an offered executable choice.", nameof(request));
        return Task.FromResult(new ReflectionAnswer(proposal.Thought, proposal.SuggestedChoice,
            request.Choices.ToDictionary(c => c.Id, c => c.Id == proposal.SuggestedChoice ? 3.0 : 1.0), "authored",
            $"Authored proposal {proposal.Id}; weights favor its proposed response."));
    }
}

/// <summary>Re-use recorded decisions only when the complete request matches. A model change or
/// an edited town must not silently apply answers to a different memory or set of lines.</summary>
public sealed class RecordedReflectionMind : IReflectionMind
{
    private readonly Dictionary<string, ReflectionRecord> _records;
    public RecordedReflectionMind(IEnumerable<ReflectionRecord> records)
        => _records = records.ToDictionary(r => r.Request.Id, StringComparer.Ordinal);

    public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_records.TryGetValue(request.Id, out ReflectionRecord? record)
            || record.Request.Tick != request.Tick || record.Request.Actor != request.Actor
            || record.Request.Subject != request.Subject || record.Request.SourceActId != request.SourceActId
            || record.Request.Memory != request.Memory || record.Request.Context != request.Context
            || record.Request.Source != request.Source || !SameProposal(record.Request.Proposal, request.Proposal)
            || record.Request.Continuity != request.Continuity
            || record.Request.Opportunity != request.Opportunity
            || !record.Request.Choices.SequenceEqual(request.Choices))
            throw new InvalidOperationException($"Reflection tape does not match request {request.Id}. Use the same seed, town and options.");
        return Task.FromResult(record.Answer);
    }

    private static bool SameProposal(ReflectionProposal? a, ReflectionProposal? b) => a is null || b is null
        ? a is null && b is null
        : a.Id == b.Id && a.Thought == b.Thought && a.SuggestedChoice == b.SuggestedChoice && a.Tags.SequenceEqual(b.Tags);
}
