using System.Text.Json.Serialization;

namespace UnderGlass.Sim.Lives;

/// <summary>Existing free-time destinations, with optional typed evaluation; separate from shared plans.</summary>
public sealed record LivesRoutingOptions
{
    public bool Enabled { get; init; }
    public bool Record { get; init; } = true;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Evaluate { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }
    public IReadOnlyList<string> CompareHistory { get; init; } = Array.Empty<string>();
    public MemoryOptions Memory { get; init; } = new();
    public ChoiceSettings Choices { get; init; } = new();
}

public sealed record RoutingChoice(ChoiceDecision Decision, ChoiceComparison? Comparison,
    string SelectedOption, string SelectedPlace, bool UsedLegacy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LivesDecisionRecord? Evaluation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Resolution = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ResolvedAt = null);
public sealed record RoutingSkipped(string Holder, int At, string Reason, int OptionCount);
/// <summary>Witnessed is the event's acquisition route, not certainty about the perceived identity.</summary>
public sealed record RoutingPerception(string Holder, int At, int ActId, KnowledgeKind How,
    double IdentityConfidence, double Clarity, string Basis, string? Teller, double Credence);
