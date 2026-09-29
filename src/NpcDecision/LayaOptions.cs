namespace NpcDecision;

/// <summary>
/// Settings for <see cref="LayaDecisionClient"/>, the client for a local <c>laya-serve</c> process
/// (Laya v0.3.22, github.com/NandhaKishorM/laya). Defaults match the server's own defaults where
/// one exists (port 8000) but assume a loopback bind rather than the server's 0.0.0.0 default.
/// </summary>
public sealed class LayaOptions
{
    /// <summary>
    /// Server base URL. Verified: <c>laya-serve</c> listens on <c>LAYA_HOST</c>:<c>LAYA_PORT</c>
    /// (default port 8000). A path prefix is allowed (for a reverse proxy that strips it, per
    /// <c>LAYA_ROOT_PATH</c>); routes are appended as <c>/health</c> and <c>/v1/systemone</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";

    /// <summary>
    /// Checkpoint to request via the body's <c>model</c> field. Verified values: <c>english</c>,
    /// <c>multilingual</c>, <c>typed-decisions</c> (or the Hugging Face ids); any other value lets
    /// the server's router choose. Null or empty omits the field.
    /// </summary>
    public string? Model { get; set; } = "typed-decisions";

    /// <summary>
    /// Optional bearer token. Verified: when the server has <c>LAYA_API_KEY</c> set it requires
    /// <c>Authorization: Bearer &lt;key&gt;</c> on <c>/v1/systemone</c> (401 otherwise);
    /// <c>/health</c> is always open. Sent only when non-empty.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Per-call timeout covering the whole request and response body. On timeout the in-flight
    /// request is cancelled and <see cref="LayaException"/> is thrown. Verified latency: tens of
    /// ms on GPU, hundreds of ms on CPU; the first calls after start-up can be much slower.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum characters of state sent. Longer state is cut, keeping the start (Laya itself
    /// truncates an over-long state to its first window). The server's hard cap is 50,000
    /// characters (413 above it), but the checkpoints only see 512 tokens (english) or 1024
    /// (typed-decisions, multilingual), so anything much past a few thousand characters is
    /// dropped by the model anyway. Zero or negative disables truncation.
    /// </summary>
    public int MaxStateChars { get; set; } = 4000;
}
