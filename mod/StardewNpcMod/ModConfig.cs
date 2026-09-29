namespace StardewNpcMod;

/// <summary>Player-editable settings (config.json, created by SMAPI on first run).</summary>
public sealed class ModConfig
{
    /// <summary>"Fake" (deterministic, no model) or "Laya" (local laya-serve process; see sidecar/README.md).</summary>
    public string DecisionBackend { get; set; } = "Fake";

    /// <summary>Base URL of the local Laya server. Keep it on loopback.</summary>
    public string LayaUrl { get; set; } = "http://127.0.0.1:8000";

    /// <summary>Laya checkpoint: "typed-decisions", "english" or "multilingual".</summary>
    public string LayaModel { get; set; } = "typed-decisions";

    /// <summary>Only needed when the server was started with LAYA_API_KEY.</summary>
    public string? LayaApiKey { get; set; }

    /// <summary>Per-call timeout; a slower answer falls back to the deterministic default.</summary>
    public int DecisionTimeoutMs { get; set; } = 1500;

    /// <summary>Total time overnight planning may spend on model calls before the rest fall back.</summary>
    public int PlanningBudgetMs { get; set; } = 20_000;

    /// <summary>Ticks the ladder worker may fall behind before new ticks are dropped.</summary>
    public int LadderMaxBacklog { get; set; } = 6;
}
