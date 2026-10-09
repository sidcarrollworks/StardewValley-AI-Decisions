namespace UnderGlass.Minds;

/// <summary>Raw preserves the original labels/order. Canonical removes those arbitrary inputs;
/// Balanced additionally gives every distinct alternative every position once.</summary>
public enum ReflectionEvaluationMode { Raw, Canonical, Balanced }

/// <summary>Optional, explicitly configured local services. Neither adapter installs a model.</summary>
public sealed record ReflectionMindOptions
{
    public string? LayaBaseUrl { get; init; }
    public string LayaModel { get; init; } = "typed-decisions";
    public string? GenerationBaseUrl { get; init; }
    public string GenerationModel { get; init; } = "local";
    /// <summary>Each HTTP call has this deadline, including reading its response body.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>One deadline shared by all Laya passes, starting after optional generation.</summary>
    public TimeSpan EvaluationTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public ReflectionEvaluationMode EvaluationMode { get; init; } = ReflectionEvaluationMode.Raw;
    public int MaxThoughtChars { get; init; } = 220;
}
