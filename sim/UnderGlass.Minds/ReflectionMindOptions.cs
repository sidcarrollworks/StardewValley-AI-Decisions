namespace UnderGlass.Minds;

/// <summary>Optional, explicitly configured local services. Neither adapter installs a model.</summary>
public sealed record ReflectionMindOptions
{
    public string? LayaBaseUrl { get; init; }
    public string LayaModel { get; init; } = "typed-decisions";
    public string? GenerationBaseUrl { get; init; }
    public string GenerationModel { get; init; } = "local";
    /// <summary>Each HTTP call has this deadline, including reading its response body.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public int MaxThoughtChars { get; init; } = 220;
}
