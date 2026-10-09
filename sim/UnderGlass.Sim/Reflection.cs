namespace UnderGlass.Sim;

/// <summary>Quiet reflection is an opt-in experiment, separate from the ordinary desire gate.</summary>
public sealed record ReflectionOptions
{
    public bool Enabled { get; init; }
    public int QuietMinutes { get; init; } = 30;
    public double DailyChance { get; init; } = 0.35;
    public int IntentionDays { get; init; } = 3;
}

/// <summary>An authored line with an executable act; an empty Kind means defer or reject.</summary>
public sealed record ReflectionChoice(string Id, string Kind, string Line);

/// <summary>A copy of one person's known context. Providers must never inspect the simulation.</summary>
public sealed record ReflectionRequest(string Id, int Tick, string Actor, string Subject, int SourceActId,
    string Memory, string Context, IReadOnlyList<ReflectionChoice> Choices);

/// <summary>Imagination proposes; the evaluator weights the actual lines. The engine samples once.
/// Backend and Note identify real calls versus authored/fallback material; they are recorded.</summary>
public sealed record ReflectionAnswer(string Thought, string SuggestedChoice,
    IReadOnlyDictionary<string, double> Weights, string Backend, string? Note = null,
    string? LayaPrompt = null, string? GenerationPrompt = null);

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
        var first = request.Choices.First(c => c.Kind.Length > 0);
        return Task.FromResult(new ReflectionAnswer($"I keep thinking about {request.Subject}. Perhaps there is something I could do.",
            first.Id, request.Choices.ToDictionary(c => c.Id, c => c.Id == first.Id ? 3.0 : 1.0), "authored"));
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
            || !record.Request.Choices.SequenceEqual(request.Choices))
            throw new InvalidOperationException($"Reflection tape does not match request {request.Id}. Use the same seed, town and options.");
        return Task.FromResult(record.Answer);
    }
}
