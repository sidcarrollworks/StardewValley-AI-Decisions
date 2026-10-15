namespace UnderGlass.Sim.Lives;

/// <summary>First live Lives increment: existing free-time destinations, not plans or model questions.</summary>
public sealed record LivesRoutingOptions
{
    public bool Enabled { get; init; }
    public bool Record { get; init; } = true;
    public IReadOnlyList<string> CompareHistory { get; init; } = Array.Empty<string>();
    public MemoryOptions Memory { get; init; } = new();
    public ChoiceSettings Choices { get; init; } = new();
}

public sealed record RoutingChoice(ChoiceDecision Decision, ChoiceComparison? Comparison,
    string SelectedOption, string SelectedPlace, bool UsedLegacy);
public sealed record RoutingSkipped(string Holder, int At, string Reason, int OptionCount);
/// <summary>Witnessed is the event's acquisition route, not certainty about the perceived identity.</summary>
public sealed record RoutingPerception(string Holder, int At, int ActId, KnowledgeKind How,
    double IdentityConfidence, double Clarity, string Basis, string? Teller, double Credence);
