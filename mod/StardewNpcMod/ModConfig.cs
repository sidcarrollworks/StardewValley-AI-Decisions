namespace StardewNpcMod;

using NpcLive;

/// <summary>Player-editable settings (config.json, created by SMAPI on first run).</summary>
public sealed class ModConfig
{
    /// <summary>"Fake" (deterministic, no model), "Varied" (deterministic non-uniform fake) or
    /// "Laya" (local laya-serve process; see sidecar/README.md).</summary>
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

    /// <summary>How long the "valley is waking up" morning wait may hold the day while overnight
    /// work finishes (docs/spec/laya.md, "A morning wait"). 0 disables it: lines arrive whenever
    /// the plan is ready, as before.</summary>
    public int MorningWaitMs { get; set; } = 10_000;

    /// <summary>Serve the NPC Minds viewer, a read-only page showing what every NPC knows and
    /// wants, at http://127.0.0.1:{MindsViewerPort}/ (docs/spec/debug-tools.md, "Live viewer").
    /// Loopback only. On while the mod is in development.</summary>
    public bool MindsViewer { get; set; } = true;

    /// <summary>Port for the viewer. Not 8000: the Laya server uses that.</summary>
    public int MindsViewerPort { get; set; } = 8765;

    /// <summary>Write the playtest log, one JSON-lines file per save and in-game day under
    /// playtest/&lt;save&gt;/ (docs/spec/debug-tools.md, "Playtest log"). On in development;
    /// reads only, never feeds a decision.</summary>
    public bool PlaytestLog { get; set; } = true;

    /// <summary>The live switches (docs/spec/rollout.md, D30): which acts leave shadow and really
    /// show in the game. Both off by default; "Live": { "Emote": true, "Bubble": true } in
    /// config.json turns the motives runner's emotes and speech bubbles on.</summary>
    public LiveSwitches Live { get; set; } = new();
}
