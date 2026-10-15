using System.Text.Json;
using UnderGlass.Sim.Lives;

namespace UnderGlass.Minds;

/// <summary>Replay validates the whole frozen request, not just its ID or candidate labels.
/// Model answers and failed-call receipts are replayed without network access.</summary>
public sealed class RecordedLivesDecisionMind : ILivesDecisionMind
{
    private readonly Dictionary<string, (string Request, string Answer)> _records;
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    public RecordedLivesDecisionMind(IEnumerable<LivesDecisionRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _records = new(StringComparer.Ordinal);
        foreach (var record in records)
        {
            string key = record.Request.Fallback.Opportunity.Id;
            if (LivesDecisionEngine.Apply(record.Request, record.Answer) != record.SelectedId)
                throw new InvalidDataException($"Lives tape selection does not match its weights and draw: {key}.");
            if (!_records.TryAdd(key, (LivesDecisionEngine.ExactRequest(record.Request), JsonSerializer.Serialize(record.Answer))))
                throw new InvalidDataException($"Duplicate Lives tape request: {key}.");
        }
    }
    public int Remaining => _records.Count - _consumed.Count;
    public void RequireConsumed()
    { if (Remaining != 0) throw new InvalidOperationException($"Lives tape has {Remaining} unconsumed decision(s)."); }
    public Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = request.Fallback.Opportunity.Id;
        if (!_records.TryGetValue(key, out var record) || record.Request != LivesDecisionEngine.ExactRequest(request))
            throw new InvalidOperationException($"Lives tape does not match the complete request {key}; use the same history, options, model and source version.");
        if (!_consumed.Add(key)) throw new InvalidOperationException($"Lives tape request was consumed twice: {key}.");
        return Task.FromResult(JsonSerializer.Deserialize<LivesDecisionAnswer>(record.Answer)!);
    }
}
