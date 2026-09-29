using NpcDecision;
using NpcInitiation;
using NpcIntents;
using NpcMemory;
using NpcSchedules;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StardewNpcMod;

/// <summary>
/// The live mod, in shadow mode (records and logs, changes no game state). Hooks SaveLoaded /
/// DayStarted / TimeChanged / DayEnding / Saving / ReturnedToTitle, plus MenuChanged to notice
/// conversations.
/// <list type="bullet">
/// <item>Each ten-minute tick, every NPC observes the player and the other NPCs that share its
/// location within <see cref="MemoryStore.CoLocationRadius"/> tiles (memory from the NPC side).</item>
/// <item>At day end, overnight intents are planned on a background task with a time budget; the plan
/// is collected (never waited on) and shadow-logged the next morning.</item>
/// <item>The initiation ladder runs on its own background worker; its would-be actions are logged.</item>
/// </list>
/// Model calls (Laya or the fake) never run on the game thread, including during the save.
/// </summary>
public class ModEntry : Mod
{
    private const string SaveKey = "squid.StardewNpcMod.memory";

    private ModConfig _config = new();
    private RegionMap _regions = null!;
    private IDecisionClient _model = new FakeDecisionClient();
    private MemoryStore _memory = new();
    private BackgroundLadder _ladder = null!;
    private IntentPlanJob? _planJob;

    // NPCs with a planned line for today (feeds the ladder's intent boost).
    private readonly HashSet<string> _intentsToday = new(StringComparer.OrdinalIgnoreCase);

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _regions = RegionMap.Load(Path.Combine(Helper.DirectoryPath, "regions.json"));
        _model = BuildModel();
        _ladder = NewLadder(null);

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Display.MenuChanged += OnMenuChanged;

        Monitor.Log($"Shadow mode ready: co-location radius {_memory.CoLocationRadius} tiles, decision backend {_model.GetType().Name}.", LogLevel.Info);
    }

    /// <summary>The raw model client. Every use goes through a <see cref="ResilientDecisionClient"/>
    /// with a timeout, on a background thread.</summary>
    private IDecisionClient BuildModel()
    {
        if (!string.Equals(_config.DecisionBackend, "Laya", StringComparison.OrdinalIgnoreCase))
            return new FakeDecisionClient();

        var laya = new LayaDecisionClient(new LayaOptions
        {
            BaseUrl = _config.LayaUrl,
            Model = _config.LayaModel,
            ApiKey = _config.LayaApiKey,
            Timeout = TimeSpan.FromMilliseconds(_config.DecisionTimeoutMs),
        });
        // Health check off the game thread; failures only mean every decision will fall back.
        Task.Run(() => Monitor.Log(laya.IsHealthy()
            ? $"Laya is up at {_config.LayaUrl}."
            : $"Laya is not answering at {_config.LayaUrl}; decisions will use the fallback until it is. See sidecar/README.md.",
            LogLevel.Info));
        return laya;
    }

    private IDecisionClient Guarded(CancellationToken budget = default)
        => new ResilientDecisionClient(_model, TimeSpan.FromMilliseconds(_config.DecisionTimeoutMs), budget);

    private BackgroundLadder NewLadder(string? json)
    {
        int seed = Fnv1a.Seed("ladder", Game1.uniqueIDForThisGame.ToString()); // VERIFY: per-save id
        InitiationLadder ladder = json is null
            ? new InitiationLadder(Guarded(), seed)
            : InitiationLadder.FromJson(json, Guarded(), seed);
        return new BackgroundLadder(ladder, _config.LadderMaxBacklog);
    }

    // ---- events --------------------------------------------------------------------------------

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        try
        {
            LoadMemory();
            Monitor.Log($"Memory loaded: {_memory.Diaries.Count} NPC diaries, {_memory.Beliefs.Count} routine beliefs.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load memory: {ex}", LogLevel.Error);
        }
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // Don't clear _intentsToday here: the 6:00 TimeChanged fires during the new-day transition,
        // before the save and before DayStarted (seen in the SMAPI log), and usually collects the plan.
        try
        {
            CollectPlan(morning: true);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Intent delivery failed: {ex}", LogLevel.Error);
        }
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        // VERIFY: TimeChanged fires on the ten-minute tick (and may also fire at other clock jumps).
        int tick = TimeUtils.TickIndex(e.NewTime);
        if (tick < 0)
            return; // outside the 600..2600 live day
        int now = Now(tick);

        try
        {
            _memory.Observe(now, CollectPresences(), _regions, HeartsFor);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Observation failed: {ex}", LogLevel.Error);
        }

        try
        {
            RunLadder(now);
            CollectPlan(morning: false);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow ladder failed: {ex}", LogLevel.Error);
        }
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        try
        {
            StartPlanning();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Could not start overnight planning: {ex}", LogLevel.Error);
        }
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        // Only fast local serialization here; planning is already running in the background.
        try
        {
            SaveMemory();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to save memory: {ex}", LogLevel.Error);
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        // Fresh memory per save; a different save must start clean.
        _planJob?.Dispose();
        _planJob = null;
        _memory = new MemoryStore();
        _ladder = NewLadder(null);
        _intentsToday.Clear();
        Monitor.Log("Memory reset for the title screen.", LogLevel.Info);
    }

    // ---- perception ------------------------------------------------------------------------------

    /// <summary>Absolute tick for the current day, including the year.</summary>
    private static int Now(int tickOfDay)
        => GameClock.AbsoluteTick(new GameTime(GameClock.SeasonIndex(Game1.currentSeason), Game1.dayOfMonth, tickOfDay, Game1.year));

    /// <summary>Where every villager and the player stand this tick. This is the perception layer:
    /// it only feeds <see cref="MemoryStore.Observe"/>, which ages and coarsens it into the ledger.
    /// Nothing here reaches a decision directly.</summary>
    private static List<Presence> CollectPresences()
    {
        var presences = new List<Presence>();
        var seen = new HashSet<GameLocation>();

        // VERIFY: Game1.locations holds the static maps (town buildings included); building interiors
        // on the farm are not listed, so the player's own location is added explicitly. Also unverified:
        // whether off-screen NPCs' positions update in real time (brief, open question).
        IEnumerable<GameLocation> locations = Game1.locations;
        if (Game1.player.currentLocation is { } playerLocation)
            locations = locations.Append(playerLocation);

        foreach (GameLocation location in locations)
        {
            if (location is null || !seen.Add(location))
                continue;
            foreach (NPC npc in location.characters)
            {
                if (!npc.IsVillager) // confirmed: property, not method; drops monsters/animals
                    continue;
                presences.Add(new Presence(npc.Name, location.Name, npc.TilePoint.X, npc.TilePoint.Y));
            }
        }

        Farmer player = Game1.player;
        if (player.currentLocation is { } here)
            presences.Add(new Presence(MemoryStore.PlayerName, here.Name, player.TilePoint.X, player.TilePoint.Y, IsPlayer: true));
        return presences;
    }

    private static int HeartsFor(string npc)
        => Game1.player.getFriendshipHeartLevelForNPC(npc); // VERIFY: 1.6 name

    /// <summary>
    /// Every conversation counts as the player responding to that NPC (talking, or the NPC's reaction
    /// to a gift), not just the first of the day. VERIFY: a character's dialogue opens a
    /// <see cref="DialogueBox"/> whose <c>characterDialogue.speaker</c> is that NPC; question boxes
    /// and letters have no speaker and are ignored.
    /// </summary>
    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!Context.IsWorldReady || e.NewMenu is not DialogueBox { characterDialogue.speaker: { } speaker })
            return;
        int tick = TimeUtils.TickIndex(Game1.timeOfDay);
        if (tick >= 0)
            _ladder.EnqueueResponse(speaker.Name, Now(tick));
    }

    // ---- initiation ladder (shadow) -------------------------------------------------------------

    /// <summary>Hand the ladder this tick's inputs (each NPC's own ledger view of the player, never a
    /// live position) and log whatever the worker finished.</summary>
    private void RunLadder(int now)
    {
        var inputs = new List<InitiationInput>();
        foreach (string npc in _memory.Diaries.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            LedgerView? view = _memory.Ledger.View(npc, MemoryStore.PlayerName, now);
            inputs.Add(new InitiationInput(npc, view, _intentsToday.Contains(npc), HeartsFor(npc)));
        }
        if (inputs.Count > 0 && !_ladder.EnqueueTick(now, inputs))
            Monitor.Log($"[shadow] ladder is behind the model; skipped a tick ({_ladder.Dropped} so far).", LogLevel.Trace);

        foreach (BackgroundLadder.Result result in _ladder.Drain())
        {
            foreach ((string npc, DiaryEntry entry) in result.DiaryLines)
                _memory.DiaryOf(npc).Append(entry);
            foreach (InitiationEvent ev in result.Events)
                Monitor.Log(ev.Kind == "Attempt"
                    ? $"[shadow] {ev.Npc} would try {ev.Step} (urge {ev.UrgeBefore:0.00}; {ev.Reason})"
                    : $"[shadow] {ev.Npc}: {ev.Step} {ev.Kind.ToLowerInvariant()} (urge {ev.UrgeBefore:0.00} -> {ev.UrgeAfter:0.00})",
                    LogLevel.Info);
        }
    }

    // ---- overnight intents (shadow) -------------------------------------------------------------

    /// <summary>
    /// At day end, snapshot every NPC diary and plan tomorrow's intents on a background task. Only
    /// the day just ended is considered (so "yesterday" is true). The budget cuts off a slow model;
    /// remaining decisions fall back. The plan is kept in memory only (survives sleep, not quit).
    /// </summary>
    private void StartPlanning()
    {
        _planJob?.Dispose();
        _planJob = null;
        _intentsToday.Clear(); // today's lines are over; tomorrow's arrive when the new plan is collected

        var snapshots = _memory.Diaries
            .Where(kv => kv.Value.Entries.Count > 0)
            .Select(kv => new NpcMemorySnapshot(kv.Key, VoiceSheets.Voice(kv.Key), kv.Value.Entries.ToList(), Array.Empty<string>()))
            .ToList();
        if (snapshots.Count == 0)
            return;

        int today = GameClock.DayIndex(Now(0));
        int seed = today; // deterministic per day
        _planJob = IntentPlanJob.Start(
            budget => new IntentPlanner(Guarded(budget), new LineRenderer()).Plan(snapshots, seed, sourceDay: today),
            TimeSpan.FromMilliseconds(_config.PlanningBudgetMs));
        Monitor.Log($"[shadow] planning tomorrow's intents in the background ({snapshots.Count} NPC diaries).", LogLevel.Trace);
    }

    /// <summary>Non-blocking: if the overnight plan is ready, log the would-be lines and drop it.</summary>
    private void CollectPlan(bool morning)
    {
        if (_planJob is null)
            return;
        if (!_planJob.TryTake(out IntentPlan plan, out Exception? error))
        {
            if (morning)
                Monitor.Log("[shadow] overnight planning is still running; its lines will be logged when ready.", LogLevel.Info);
            return;
        }

        if (error is not null)
            Monitor.Log($"Overnight planning failed: {error}", LogLevel.Error);
        if (_planJob.BudgetExhausted)
            Monitor.Log("[shadow] planning hit its time budget; some decisions used the fallback.", LogLevel.Info);
        foreach (IntentCandidate candidate in plan.Candidates)
        {
            _intentsToday.Add(candidate.Npc);
            Monitor.Log($"[shadow] {candidate.Npc} would say: \"{candidate.Line}\" ({candidate.Reason})", LogLevel.Info);
        }

        _planJob.Dispose();
        _planJob = null;
    }

    // ---- persistence ----------------------------------------------------------------------------

    private void SaveMemory()
    {
        Helper.Data.WriteSaveData(SaveKey, new Dictionary<string, string>
        {
            ["version"] = MemoryStore.CurrentVersion.ToString(),
            ["memory"] = _memory.ToJson(),
            ["ladder"] = _ladder.LatestJson, // last finished state; saving never waits on the model
        });
        Monitor.Log($"Memory saved ({_memory.Diaries.Count} NPC diaries, {_memory.Beliefs.Count} beliefs).", LogLevel.Info);
    }

    private void LoadMemory()
    {
        var model = Helper.Data.ReadSaveData<Dictionary<string, string>>(SaveKey);
        _memory = new MemoryStore();
        _ladder = NewLadder(null);
        if (model is null)
            return;

        // Only step-4/5 saves have no version; anything versioned is read as the current format.
        if (model.ContainsKey("version"))
        {
            if (model.TryGetValue("memory", out string? memoryJson))
                _memory = MemoryStore.FromJson(memoryJson);
            if (model.TryGetValue("ladder", out string? ladderJson))
                _ladder = NewLadder(ladderJson);
            return;
        }

        // Version 1 (steps 4-5): ticks had no year. Migrate relative to today.
        _memory = MemoryStore.FromVersion1(model, new GameTime(GameClock.SeasonIndex(Game1.currentSeason), Game1.dayOfMonth, 0, Game1.year));
        Monitor.Log("Migrated memory from the previous save format (added the year to old timestamps).", LogLevel.Info);
    }
}
