using System.Collections.ObjectModel;
using System.Text.Json;

namespace UnderGlass.Sim.Lives;

/// <summary>A detached, holder-known scene and the exact deterministic decision Laya may reweigh.
/// Context must describe only what the holder knows now. No provider receives a simulation.</summary>
public sealed record LivesDecisionRequest(ChoiceDecision Fallback, string Context,
    string Model = "laya", int Version = 1);
public sealed record LivesDecisionPacket(string Json, int Characters,
    IReadOnlyDictionary<string, string> Labels, IReadOnlyList<string> Omitted);
public sealed record LivesDecisionAnswer(IReadOnlyDictionary<string, double> Weights, string Backend,
    string Model, string? Note = null, LivesDecisionPacket? Packet = null, string? Response = null,
    bool Called = false, string? ReportedModel = null);
public sealed record LivesDecisionRecord(LivesDecisionRequest Request, LivesDecisionAnswer Answer,
    string SelectedId);
public interface ILivesDecisionMind
{
    Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Providers return weights only. The engine retains the one already keyed draw, the
/// eligible option order and the untouched fallback beside the model's answer.</summary>
public static class LivesDecisionEngine
{
    public static LivesDecisionAnswer Fallback(LivesDecisionRequest request, string? note = null,
        LivesDecisionPacket? packet = null, string? response = null, bool called = false)
    {
        Validate(request);
        return new(Weights(request.Fallback.Alternatives.ToDictionary(a => a.Option.Id, a => a.Probability)),
            called || packet is not null ? "authored-fallback" : "authored", request.Model, note, packet, response, called);
    }

    public static string Apply(LivesDecisionRequest request, LivesDecisionAnswer answer)
    {
        Validate(request);
        if (answer.Model != request.Model) throw new InvalidDataException("Decision model identity does not match its request.");
        var weights = Normalize(request, answer.Weights);
        string selected = request.Fallback.Alternatives.Last(a => weights[a.Option.Id] > 0).Option.Id;
        double cumulative = 0;
        foreach (var alternative in request.Fallback.Alternatives)
        {
            cumulative += weights[alternative.Option.Id];
            if (request.Fallback.Unit < cumulative) return alternative.Option.Id;
        }
        return selected;
    }

    public static IReadOnlyDictionary<string, double> Normalize(LivesDecisionRequest request,
        IReadOnlyDictionary<string, double> weights)
    {
        Validate(request);
        var ids = request.Fallback.Alternatives.Select(a => a.Option.Id).ToArray();
        if (weights is null || weights.Count != ids.Length || !ids.ToHashSet(StringComparer.Ordinal).SetEquals(weights.Keys)
            || weights.Values.Any(v => !double.IsFinite(v) || v < 0) || weights.Values.Max() <= 0)
            throw new InvalidDataException("Decision weights must cover every eligible alternative once with finite nonnegative mass.");
        double maximum = weights.Values.Max(), total = weights.Values.Sum(v => v / maximum);
        return Weights(ids.ToDictionary(id => id, id => weights[id] / maximum / total, StringComparer.Ordinal));
    }

    public static void Validate(LivesDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var d = request.Fallback;
        if (request.Version != 1 || string.IsNullOrWhiteSpace(request.Model) || request.Model.Length > 128
            || request.Context is null || d is null || !d.Available || d.Legacy
            || d.Alternatives.Count is < 1 or > 5 || !double.IsFinite(d.Unit) || d.Unit is < 0 or >= 1
            || d.Alternatives.Select(a => a.Option.Id).Distinct(StringComparer.Ordinal).Count() != d.Alternatives.Count
            || d.Alternatives.Any(a => string.IsNullOrWhiteSpace(a.Option.Id) || string.IsNullOrWhiteSpace(a.Option.Line)
                || !double.IsFinite(a.Probability) || a.Probability < 0)
            || d.Recall.Holder != d.Opportunity.Holder || d.Recall.At > d.Opportunity.At
            || d.Recall.Items.Any(m => m.Holder != d.Opportunity.Holder || m.KnownAt > d.Opportunity.At)
            || d.Alternatives.Sum(a => a.Probability) <= 0)
            throw new ArgumentException("A Lives decision needs a current, available nonlegacy choice and valid model identity.");
    }

    public static string ExactRequest(LivesDecisionRequest request)
    { Validate(request); return JsonSerializer.Serialize(request); }

    private static IReadOnlyDictionary<string, double> Weights(Dictionary<string, double> weights) =>
        new ReadOnlyDictionary<string, double>(weights);
}

public sealed class AuthoredLivesDecisionMind : ILivesDecisionMind
{
    public Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(LivesDecisionEngine.Fallback(request)); }
}
