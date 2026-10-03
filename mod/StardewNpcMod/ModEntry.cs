using HarmonyLib;
using NpcDecision;
using NpcDiaryEvents;
using NpcInitiation;
using NpcIntents;
using NpcMemory;
using NpcMinds;
using NpcMinds.Playtest;
using NpcMotives;
using NpcLive;
using NpcSchedules;
using NpcTemperament;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.SpecialOrders;
using StardewValley.Menus;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewNpcMod.Patches;
using System.Diagnostics;
using System.Reflection;

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
/// <item>NPCs who miss the player ask the NPCs around them where the player is (gossip), and the
/// ladder can send an NPC to look for the player where it believes they are.</item>
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
    private readonly MotiveOptions _motiveOptions = new();
    private RegardKeeper _regard = null!;
    private BackgroundMotives _motives = null!;
    private readonly Dictionary<string, string> _lastMotiveLines = new(StringComparer.OrdinalIgnoreCase);
    private readonly LiveBreaker _live = new();
    private readonly LiveLedger _liveLedger = new();
    private readonly LiveOptions _liveOptions = new();
    private bool _layaWarned; // the viewer's "questions failing" Warn fires once per failure streak
    private bool _historySeeded; // set once the save's regard was seeded from the game's history
    private PlayerSearch _search = new();
    private IntentPlanJob? _planJob;

    // Playtest log (docs/spec/debug-tools.md, "Playtest log"): the per-save, per-day JSON-lines
    // record. Appended on the game thread only; model calls arrive through its worker queue.
    private PlaytestLog _playtest = null!;
    private readonly Dictionary<string, (string Location, int X, int Y)> _lastPresencePositions = new();
    private DateTime _planJobStartedUtc;

    // NPCs with a planned line for today (feeds the ladder's intent boost).
    private readonly HashSet<string> _intentsToday = new(StringComparer.OrdinalIgnoreCase);

    // First-conversation tracking for the Talked diary kind; cleared at the 6:00 tick, on load,
    // and at the title screen.
    private readonly HashSet<string> _talkedToday = new(StringComparer.OrdinalIgnoreCase);

    // Step 2 (diary enrichment part 2): Harmony postfixes queue here; the next tick drains into
    // memory (docs/spec/diary.md, "A postfix records, the tick applies").
    private readonly DiaryEventQueue _events = new();
    private readonly DiaryOptions _diaryOptions = new();
    private readonly HashSet<string> _seenSpecialOrders = new(StringComparer.OrdinalIgnoreCase);

    // Step 3: the last background health check's answer. While false, every decision falls back
    // immediately without an HTTP attempt (docs/spec/laya.md, "Short-circuit when down").
    private volatile bool _layaUp = true;

    // Heartbeat cosmetics (week review, findings 9-10): the backlog read BEFORE this tick's work
    // was enqueued, and how many lines today's plan collected (for the "3 lines" wording).
    private int _backlogAtTickStart;
    private int _planCollectedLinesToday = -1;

    // Festival capture, reset after NoteDayEnd uses it (docs/spec/diary.md, "Festival").
    private bool _festivalAttended;
    private readonly HashSet<string> _festivalActors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _festivalTalked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Delivered lines per NPC (novelty + cite cooldowns; persistence.md key
    /// `recentLines`). Empty in shadow mode — nothing is delivered until step 6.</summary>
    private readonly Dictionary<string, List<PlanPersistence.RecentLine>> _recentLines = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Planner tuning shared between the mod's news context and the plan job.</summary>
    private readonly IntentPlannerOptions _plannerOptions = new();

    /// <summary>Ambient chat tuning (docs/spec/ledger-gossip.md); never saved.</summary>
    private readonly ChatOptions _chatOptions = new();

    /// <summary>The daily belief decay factor (docs/spec/routines.md, `DailyDecay`); never saved.</summary>
    private const double DailyDecay = 0.97;

    /// <summary>Per-save deterministic seed for simulated chats and priors.</summary>
    private int MemorySeed() => unchecked((int)Fnv1a.Seed("memory", Game1.uniqueIDForThisGame.ToString()));

    // NPC Minds viewer (docs/spec/debug-tools.md, "Live viewer"): read-only. The game thread
    // builds a snapshot after each tick and swaps it in; the server thread only serializes it.
    // The call log is written by the model workers through RecordingDecisionClient.
    private readonly RingLog<DecisionCall> _calls = new(200);
    private readonly RingLog<FeedItem> _feed = new(300);
    private readonly SpreadTable _spread = new(); // the spread panel's per-day answer table
    private LayaCalibration? _calibration;       // data/laya-calibration.json, read once in Entry
    private readonly MindsSnapshotBuilder _mindsBuilder = new();
    private MindsServer? _minds;
    private long _mindsSeq;
    private IReadOnlyList<InitiationInput> _lastLadderInputs = Array.Empty<InitiationInput>();
    private IReadOnlyList<IntentCandidate> _planToday = Array.Empty<IntentCandidate>();
    private TemperamentTable? _temperaments; // the seed table; only the viewer reads it so far
    private readonly Dictionary<string, TemperamentView> _temperamentViews = new(StringComparer.OrdinalIgnoreCase);

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _regions = RegionMap.Load(Path.Combine(Helper.DirectoryPath, "regions.json"));
        _model = BuildModel();
        _motives = NewMotives(null);
        NewRegard(null);
        _search = NewSearch();

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
        helper.ConsoleCommands.Add("npcmod_live", "Turn the live emotes and bubbles off for this session.\n\nUsage: npcmod_live off", (_, _) =>
        {
            _live.TripAll();
            Monitor.Log("[live] all live acts turned off for this session.", LogLevel.Info);
        });
        helper.Events.Display.MenuChanged += OnMenuChanged;

        ApplyPatches();
        _temperaments = LoadTemperaments();
        _calibration = LoadCalibration();
        StartMindsViewer();

        Monitor.Log($"Shadow mode ready: co-location radius {_memory.CoLocationRadius} tiles, decision backend {_model.GetType().Name}.", LogLevel.Info);
    }

    /// <summary>
    /// Read-only Harmony postfixes for the diary kinds (docs/spec/diary.md, "Harmony"): the code
    /// API, one class per patched method, all applied from this one place. A missing method (game
    /// update) logs a warning and skips that patch; the mod still loads.
    /// </summary>
    private void ApplyPatches()
    {
        var harmony = new HarmonyLib.Harmony(ModManifest.UniqueID);

        MethodInfo? receiveGift = AccessTools.Method(typeof(NPC), nameof(NPC.receiveGift));
        if (receiveGift is null)
            Monitor.Log("GiftReceived patch skipped: NPC.receiveGift not found (game update?). Gift notes are off until this is fixed.", LogLevel.Warn);
        else
        {
            harmony.Patch(receiveGift, postfix: new HarmonyMethod(typeof(GiftPatch), nameof(GiftPatch.Postfix)));
            GiftPatch.Queue = _events;
            GiftPatch.Log = Monitor;
        }

        MethodInfo? questComplete = AccessTools.Method(typeof(Quest), nameof(Quest.questComplete));
        if (questComplete is null)
            Monitor.Log("QuestHelped patch skipped: Quest.questComplete not found (game update?). Quest notes are off until this is fixed.", LogLevel.Warn);
        else
        {
            harmony.Patch(questComplete, postfix: new HarmonyMethod(typeof(QuestPatch), nameof(QuestPatch.Postfix)));
            QuestPatch.Queue = _events;
            QuestPatch.Log = Monitor;
        }
    }

    /// <summary>The raw model client. Every use goes through a <see cref="ResilientDecisionClient"/>
    /// with a timeout, on a background thread.</summary>
    private IDecisionClient BuildModel()
    {
        if (string.Equals(_config.DecisionBackend, "Varied", StringComparison.OrdinalIgnoreCase))
            return new VariedFakeDecisionClient();
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
        // A healthy check also fires one throwaway question so the first real call is not the
        // slow one (docs/spec/laya.md, "Warm-up").
        Task.Run(() =>
        {
            bool up = laya.IsHealthy();
            _layaUp = up;
            Monitor.Log(up
                ? $"Laya is up at {_config.LayaUrl}."
                : $"Laya is not answering at {_config.LayaUrl}; decisions will use the fallback until it is. See sidecar/README.md.",
                LogLevel.Info);
            if (up)
                laya.TryWarmUp(); // first real call would be the slow one (docs/spec/laya.md)
        });
        return laya;
    }

    /// <param name="caller">"ladder" or "plan": labels the call in the NPC Minds viewer.</param>
    private int MotivesSeed() => unchecked((int)Fnv1a.Seed("motives", Game1.uniqueIDForThisGame.ToString()));

    private BackgroundMotives NewMotives(string? json)
        => new(MotivesRunner.FromJson(json, Guarded("motives"), _motiveOptions), _config.LadderMaxBacklog);

    /// <summary>A fresh regard keeper on the current store. The Noting hook is not saved: call this
    /// wherever _memory is replaced (load, migration, title screen).</summary>
    private void NewRegard(string? json)
    {
        _regard = new RegardKeeper(RegardBook.FromJson(json),
            npc => _temperaments?.Of(npc) ?? Temperament.Neutral, _motiveOptions);
        _memory.Noting = OnNoting;
    }

    /// <summary>Runs inside MemoryStore.Note: append the stress/regard playtest records and log a
    /// regard change. Must not write to _memory (Note is mid-append).</summary>
    private void OnNoting(string npc, DiaryEntry entry, IReadOnlyList<DiaryEntry> before)
    {
        if (_regard.OnNoted(npc, entry, before) is not { } note)
            return;
        _playtest?.Append(MotiveRecords.Stress(note, entry.AbsoluteTick));
        if (note.Before != note.After)
        {
            _playtest?.Append(MotiveRecords.Regard(note, entry.AbsoluteTick));
            if (MotiveText.RegardLine(note) is { } line) // a change too small to show isn't logged
                Monitor.Log("[shadow] " + line, LogLevel.Trace);
        }
    }

    private IDecisionClient Guarded(string caller, CancellationToken budget = default)
    {
        // Budget pass-through (docs/spec/laya.md): session-scoped calls run through a view of the
        // shared client with this session's token, so a cancelled budget aborts the in-flight HTTP
        // call — and no session can leak its token into another (the ladder keeps its own).
        IDecisionClient inner = budget != default && _model is LayaDecisionClient laya
            ? laya.WithBudget(budget)
            : _model;
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromMilliseconds(_config.DecisionTimeoutMs), budget,
            isDown: () => _model is LayaDecisionClient && !_layaUp);
        // The recorder returns the resilient client's answers unchanged; it only copies them to
        // the viewer's call log and spread table, and queues the playtest log's model records.
        // Built with the playtest log on even when the viewer is off, so its model records do not
        // depend on an unrelated setting.
        return _config.MindsViewer || _playtest is { Enabled: true }
            ? new RecordingDecisionClient(resilient, _calls, caller, _spread, _playtest) : resilient;
    }

    /// <summary>Re-check health every 6 ticks (one in-game hour) off the game thread and log only
    /// when the state changes (docs/spec/laya.md, "Sidecar lifecycle").</summary>
    private void RecheckLayaHealth()
    {
        if (_model is not LayaDecisionClient laya)
            return;
        bool up = laya.IsHealthy();
        if (up == _layaUp)
            return;
        _layaUp = up;
        Monitor.Log(up
            ? "Laya is back up; decisions use the model again."
            : "Laya went down; decisions fall back until it recovers.", LogLevel.Info);
        if (up)
            laya.TryWarmUp(); // a checkpoint may have lazy-loaded while down; warm it
    }

    /// <summary>Asking around for the player, triggered by the motives (the urge ladder is retired,
    /// D31): a villager who misses the player, worries or has news asks at AskAroundStrength.</summary>
    private PlayerSearch NewSearch() => new(new SearchOptions { AskUrge = _motiveOptions.AskAroundStrength });

    /// <summary>
    /// Seed family routine priors from the game's own schedule data, once per save (step 7,
    /// docs/spec/routines.md). Skips the player's spouse (their schedule is the marriage one) and
    /// every pair that already knows anything. Deterministic per save seed.
    /// </summary>
    private void SeedPriors()
    {
        try
        {
            string spouse = Game1.player.getSpouse()?.Name ?? "";
            var extractor = new RoutineExtractor(_regions);
            var missing = new List<string>();
            Dictionary<string, NpcRoutine> routines = extractor.ExtractAll(
                // VERIFY: a spouse's schedule is the marriage one; priors about it are wrong.
                Game1.characterData.Keys
                    .Where(npc => !string.Equals(npc, spouse, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                // Schedules live at Characters/schedules/<Name> (ScheduleAssets.NameFor), NOT
                // Data/Schedules — verified in stardew-source-notes.md, "Motives verify pass",
                // "Schedules" (NPC.cs:5993). Helper.GameContent.Load also picks up other mods'
                // schedule edits. A villager with no schedule asset skips only that villager
                // (logged below); the misses seed on the next load, since a failed run never
                // sets the belief's Seeded flag.
                name => Helper.GameContent.Load<Dictionary<string, string>>(name),
                missing);
            foreach (string npc in missing)
                Monitor.Log($"No schedule asset for {npc}; its family priors are skipped.", LogLevel.Trace);

            int seeded = _memory.SeedPriors(routines, ResolveHomes(),
                Game1.uniqueIDForThisGame.ToString(),
                new PriorOptions { Kind = RelationshipKind.Family });
            Monitor.Log($"[shadow] Seeded {seeded} family routine priors from {routines.Count} schedules.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Routine seeding failed (the game is unaffected): {ex}", LogLevel.Warn);
        }
    }

    // ---- events --------------------------------------------------------------------------------

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        try
        {
            // The playtest log is per save (the folder is the save's), built here before any tick
            // can append (docs/spec/debug-tools.md, "Playtest log").
            _playtest?.Dispose();
            _playtest = new PlaytestLog(
                // VERIFY: SaveFolderName is set before SaveLoaded fires (SMAPI sets CurrentSavePath
                // while loading a save; it is null only at the title screen).
                Path.Combine(Helper.DirectoryPath, "playtest", Constants.SaveFolderName!),
                _config.PlaytestLog,
                message => Monitor.Log(message, LogLevel.Warn));
            _lastPresencePositions.Clear(); // a previous save's tiles must not suppress this save's
            LoadMemory();
            _talkedToday.Clear(); // a load starts a fresh day; never carry a previous session's set
            _lastMotiveLines.Clear();
            _seenSpecialOrders.Clear();
            foreach (string key in Game1.player.team.completedSpecialOrders)
                _seenSpecialOrders.Add(key); // old completions must not re-fire QuestHelped
            SeedPriors(); // family routine priors, once per save (routines.md, step 7)
            HistoryAtInstall(); // seed regard from the game's own history, once per save
            Monitor.Log($"Memory loaded: {_memory.Diaries.Count} NPC diaries, {_memory.Beliefs.Count} routine beliefs.", LogLevel.Info);
            _feed.Clear();
            _calls.Clear();
            _lastLadderInputs = Array.Empty<InitiationInput>();
            _planToday = Array.Empty<IntentCandidate>();
            _temperamentViews.Clear(); // content packs may differ between saves
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load memory: {ex}", LogLevel.Error);
        }
        PublishPortraits();
        PublishMinds();
    }

    /// <summary>
    /// The viewer's card portraits: each villager's neutral portrait, cut from the player's own
    /// installed game content and handed to the viewer server as PNG bytes in memory. Nothing is
    /// written to disk or the repo (the art is the game's). Runs on the game thread at save load,
    /// so content packs that change portraits show up; any failure skips that villager only and
    /// the page falls back to initials. Display only: nothing reads these back.
    /// </summary>
    private void PublishPortraits()
    {
        MindsServer? minds = _minds;
        if (minds is null)
            return;
        var pngs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in Game1.characterData.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // Verified: NPC.Portrait (NPC.cs:506) is the loaded portrait sheet for the NPC's
                // current appearance (ChooseAppearance loads the appearance's own asset,
                // NPC.cs:1020), laid out in 64x64 frames (NPC.cs:46-48) with frame 0 the neutral
                // face (DialogueBox.cs:697 draws portrait index 0 at 64x64).
                Microsoft.Xna.Framework.Graphics.Texture2D? sheet = Game1.getCharacterFromName(name)?.Portrait;
                if (sheet is null || sheet.Width < 64 || sheet.Height < 64)
                    continue;
                var pixels = new Microsoft.Xna.Framework.Color[64 * 64];
                sheet.GetData(0, new Microsoft.Xna.Framework.Rectangle(0, 0, 64, 64), pixels, 0, pixels.Length);
                using var frame = new Microsoft.Xna.Framework.Graphics.Texture2D(Game1.graphics.GraphicsDevice, 64, 64);
                frame.SetData(pixels);
                using var png = new MemoryStream();
                frame.SaveAsPng(png, 64, 64);
                pngs[name] = png.ToArray();
            }
            catch (Exception ex)
            {
                Monitor.Log($"NPC Minds viewer: no portrait for {name} ({ex.GetType().Name}: {ex.Message}).", LogLevel.Trace);
            }
        }
        minds.PublishPortraits(pngs);
        Monitor.Log($"NPC Minds viewer: {pngs.Count} portraits ready.", LogLevel.Trace);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // Don't clear _intentsToday here: the 6:00 TimeChanged fires during the new-day transition,
        // before the save and before DayStarted (seen in the SMAPI log), and usually collects the plan.
        try
        {
            // After a mid-day load the 6:00 tick never fired this session; same-day calls are no-ops.
            _playtest.OpenDay(Game1.year, Game1.currentSeason, Game1.dayOfMonth);
            CollectPlan(morning: true);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Intent delivery failed: {ex}", LogLevel.Error);
        }

        OpenMorningWaitIfNeeded();
        PublishMinds();
    }

    /// <summary>The morning wait (docs/spec/laya.md, "A morning wait"): if the overnight plan is
    /// still running at DayStarted, hold the day behind a small menu until it finishes or the
    /// deadline passes (config MorningWaitMs; 0 disables). Single player only: in multiplayer the
    /// clock does not stop for menus, so the wait would serve nothing.</summary>
    private void OpenMorningWaitIfNeeded()
    {
        if (_config.MorningWaitMs <= 0 || !Context.IsMainPlayer || Context.IsMultiplayer || Game1.activeClickableMenu is not null)
            return; // single player only: in multiplayer the clock does not stop for menus
        if (_planJob is not { } job || job.IsCompleted)
            return; // nothing to wait for (the 6:00 tick usually collects the plan already)

        Game1.activeClickableMenu = new MorningWaitMenu(
            _config.MorningWaitMs,
            isDone: () => job.IsCompleted,
            onFinished: () => CollectPlan(morning: true));
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        // TimeChanged is SMAPI's watcher on Game1.timeOfDay: one event per value change, however
        // big the jump; it does not fire on the tick a save loads or while saving (verified).
        int tick = TimeUtils.TickIndex(e.NewTime);
        if (tick < 0)
            return; // outside the 600..2600 live day
        var tickWatch = Stopwatch.StartNew(); // the playtest log's whole-tick perf record
        int now = Now(tick);
        if (tick == 0)
        {
            _talkedToday.Clear(); // the new day: the day-end notes already used yesterday's set
            // The playtest log's new day file (the 6:00 tick is already the new date): switched
            // before this tick's records are buffered, so they land in the right day.
            _playtest.OpenDay(Game1.year, Game1.currentSeason, Game1.dayOfMonth);

            // Regard drifts toward neutral overnight, then the day's snapshot goes to the log.
            _regard.Drift();
            foreach (RegardRecord record in MotiveRecords.Snapshot(_regard.Book, now))
                _playtest.Append(record);
        }
        // (the spread panel's table was reset at DayEnding, before the overnight plan; the 6:00
        // tick must NOT clear it, or the plan's answers would vanish from the day they belong to)

        if (_model is LayaDecisionClient && tick % 6 == 0)
            Task.Run(RecheckLayaHealth);

        try
        {
            NoteSpecialOrders(now);
            _events.Drain(_memory, now, Monitor); // before Observe: the span tracker still holds the
                                                  // last tick's pairs, which SawGift witnesses need
        }
        catch (Exception ex)
        {
            Monitor.Log($"Diary events failed: {ex}", LogLevel.Error);
        }

        try
        {
            var observeWatch = Stopwatch.StartNew();
            List<Presence> presences = CollectPresences();
            _memory.Observe(now, presences, _regions, HeartsFor);
            observeWatch.Stop();
            _playtest.Append(new PerfRecord("observe", observeWatch.Elapsed.TotalMilliseconds) { Tick = now });
            // Presence records only, built from this tick's CollectPresences list (AGENTS.md rule 2).
            foreach (PresenceRecord delta in PlaytestRecords.PresenceDeltas(presences, _lastPresencePositions, now))
                _playtest.Append(delta);
            AskAround(now);
            foreach ((string listener, DiaryEntry heard) in _memory.ChatHeard(now, NewsScore, _chatOptions))
            {
                Monitor.Log($"[shadow] diary {heard}: {heard.Kind} {heard.Subject} ({heard.Detail})", LogLevel.Trace);
                _playtest.Append(HeardRecord(listener, heard));
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"Observation failed: {ex}", LogLevel.Error);
        }

        try
        {
            BuildViews(now); // each NPC's view of the player and its lead, for the motives
            LogHeartbeat(tick); // before CollectPlan, so "ready" can appear for the finishing tick
            CollectPlan(morning: false);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow views or plan failed: {ex}", LogLevel.Error);
        }

        try
        {
            RunMotives(now); // appends its own motives perf record
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow motives failed: {ex}", LogLevel.Error);
        }

        if (tick == 0)
        {
            try
            {
                PlaytestMorning(now); // weather, census, yesterday's game events; then flushes
            }
            catch (Exception ex)
            {
                Monitor.Log($"Playtest morning block failed (the game is unaffected): {ex.Message}", LogLevel.Trace);
            }
        }

        _playtest.DrainWorkerQueue(); // hand off the model-call records the workers queued this tick

        PublishMinds(); // last: the viewer sees this tick's memory, ladder results and plan

        tickWatch.Stop();
        _playtest.Append(new PerfRecord("tick", tickWatch.Elapsed.TotalMilliseconds) { Tick = now });
    }

    /// <summary>Festival capture runs here, not on TimeChanged: the clock is stopped for the whole
    /// festival (Game1.shouldTimePass is false while isFestival()), and when it ends the event
    /// clears isFestival before the one 22:00 TimeChanged fires (docs/spec/diary.md).</summary>
    private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;
        try
        {
            CaptureFestival();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Festival capture failed: {ex}", LogLevel.Error);
        }
        try
        {
            // Drain the motives queue about once a second too, so a live wave shows while the
            // player is still there (docs/architecture.md, "Live emotes and bubbles").
            DrainMotives();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Live motives drain failed: {ex}", LogLevel.Error);
        }
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        try
        {
            _spread.Reset(); // first, unconditionally: the spread panel's day starts here and the
                             // overnight plan's answers count toward the day they plan for — even
                             // when a later step below throws, the table must not keep
                             // yesterday's answers (SpreadPanelTests pins the order: only an
                             // explicit Reset clears, nothing at the 6:00 tick)
            _events.Drain(_memory, Now(119), Monitor); // gifts/quests after the last tick still
                                                       // make tonight's plan
            _memory.DecayBeliefs(Now(119), DailyDecay); // beliefs untouched today age (routines.md)
            NoteDayEnd();
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
            _playtest.Flush(); // the playtest buffer goes to disk here too (never during a tick)
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
        _memory = new MemoryStore(MemorySeed());
        _motives = NewMotives(null);
        NewRegard(null);
        _search = NewSearch();
        _intentsToday.Clear();
        _talkedToday.Clear();
        _lastMotiveLines.Clear();
        _historySeeded = false;
        _events.Clear();
        _seenSpecialOrders.Clear();
        _festivalAttended = false;
        _festivalActors.Clear();
        _festivalTalked.Clear();
        _layaUp = true;
        _backlogAtTickStart = 0;
        _planCollectedLinesToday = -1;
        _planToday = Array.Empty<IntentCandidate>();
        _recentLines.Clear();
        QuestPatch.Reset();
        _feed.Clear();
        _calls.Clear();
        _lastLadderInputs = Array.Empty<InitiationInput>();
        _planToday = Array.Empty<IntentCandidate>();
        _playtest.Flush(); // keep this session's buffered records before the writer closes
        _playtest.Dispose();
        _lastPresencePositions.Clear(); // per save state, like everything else here
        _minds?.Publish(MindsSnapshot.Idle(++_mindsSeq, BackendName()));
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

        // Game1.locations holds the static maps only (verified); building interiors are reached
        // through Utility.ForEachLocation(includeInteriors). Off-screen NPCs do move every tick
        // on the host (Game1.UpdateLocations -> updateEvenIfFarmerIsntHere), so positions here
        // are current. The player's own location is added explicitly for the interiors case.
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
        => Game1.player.getFriendshipHeartLevelForNPC(npc); // Farmer.cs:2785 (1.6 name, verified)

    /// <summary>
    /// Every conversation counts as the player responding to that NPC (talking, or the NPC's reaction
    /// to a gift), not just the first of the day. Verified: a character's dialogue opens a
    /// <see cref="DialogueBox"/> whose <c>characterDialogue</c> (DialogueBox.cs:14, set by the
    /// Dialogue ctor at :140) carries <c>speaker</c> as that NPC (Dialogue.cs:211/375); question
    /// boxes use the string+responses ctor (DialogueBox.cs:115), which never sets
    /// <c>characterDialogue</c>, and letters have no speaker — both are ignored.
    /// </summary>
    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!Context.IsWorldReady || e.NewMenu is not DialogueBox { characterDialogue.speaker: { } speaker })
            return;
        int tick = TimeUtils.TickIndex(Game1.timeOfDay);
        if (tick < 0)
            return;
        _motives.EnqueueTalked(speaker.Name, Now(tick));

        // First conversation of the day: a Talked diary entry (docs/spec/diary.md).
        if (_talkedToday.Add(speaker.Name))
        {
            DiaryEntry entry = new(Now(tick), MemoryStore.PlayerName, "Talked",
                DiaryDetail.Format(("hearts", HeartsFor(speaker.Name).ToString())));
            _memory.Note(speaker.Name, entry);
        }

        if (Game1.isFestival())
            _festivalTalked.Add(speaker.Name); // "talked there" for the Festival note
    }

    // ---- views and asking around (the urge ladder is retired, D31) -----------------------------

    /// <summary>NPCs who miss the player ask the NPCs around them (memory only, game thread). How
    /// much each wants to find the player comes from the motives worker's last finished tick.</summary>
    private void AskAround(int now)
    {
        foreach (SearchEvent ev in _search.Tick(_memory, now, MotiveDrive.Seeking(_motives.LatestDecisions)))
        {
            LedgerView learned = ev.Learned;
            string heard = learned.HopCount > 1 ? "heard you were" : "saw you";
            string where = learned.Place is null ? "" : $" at {PlaceNames.Display(learned.Place)}";
            string text = $"{ev.Seeker} asked {string.Join(", ", ev.Asked)} about you: {learned.ToldBy} {heard}{where} {Ago(learned.AgeTicks)}.";
            Monitor.Log("[shadow] " + text, LogLevel.Info);
            AddFeed(now, "Asked", ev.Seeker, text);

            // The playtest log's ask-around records: one per neighbour asked, answered or not
            // (ev.Told names the neighbours whose answer was kept).
            foreach (string neighbour in ev.Asked)
                _playtest.Append(new GossipRecord(ev.Seeker, neighbour, "where", MemoryStore.PlayerName,
                    Hops: 0, Kind: "ask",
                    Answered: ev.Told?.Contains(neighbour, StringComparer.OrdinalIgnoreCase) == true)
                {
                    Tick = ev.AbsoluteTick,
                });
        }
    }

    /// <summary>This tick's view of the player for every NPC with a diary: its own ledger view and its
    /// best lead on where the player is, both from memory, never a live position. The motives read
    /// them (RunMotives) and so does the viewer. The urge ladder that used to run on them is retired
    /// (D31); the records keep their old name, InitiationInput.</summary>
    private void BuildViews(int now)
    {
        var inputs = new List<InitiationInput>();
        foreach (string npc in _memory.Diaries.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            LedgerView? view = _memory.Ledger.View(npc, MemoryStore.PlayerName, now);
            Whereabouts lead = _memory.LookFor(npc, MemoryStore.PlayerName, now, _regions.BlockMinutes, regions: _regions);
            inputs.Add(new InitiationInput(npc, view, _intentsToday.Contains(npc), HeartsFor(npc), lead));
        }
        _lastLadderInputs = inputs;
        _backlogAtTickStart = _motives.Backlog; // read BEFORE this tick's motives are enqueued
    }

    // ---- motives (shadow, step 14) -------------------------------------------------------------

    /// <summary>Seeds regard toward the player from the game's own history, once per save
    /// (docs/spec/vanilla-sources.md, "History at install"): every gift ever given (counted by
    /// the villager's taste for it), the heart events seen (the <c>f &lt;Npc&gt; &lt;points&gt;</c>
    /// preconditions on the Data/Events keys), and the relationship status, minus what the mod
    /// already saw in the diary. Verified: Farmer.giftedItems is NPC name -> item id -> count
    /// (Farmer.cs:766); eventsSeen holds the event id strings, and an event's id is the first
    /// /-segment of its Data/Events key (GameLocation.cs:15711-15715, Event.cs:4751); the
    /// FriendshipStatus values map one-to-one onto HistoryStatus (FriendshipStatus.cs); Stardrop
    /// Tea tastes 7, which TasteLabel reads as Love. Marked with historySeeded so it never runs
    /// twice.</summary>
    private void HistoryAtInstall()
    {
        Dictionary<string, string>? model = Helper.Data.ReadSaveData<Dictionary<string, string>>(SaveKey);
        _historySeeded = model is not null && model.ContainsKey("historySeeded");
        if (_historySeeded)
            return;

        var histories = new List<NpcHistory>();
        foreach (string npc in Game1.characterData.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            NPC? character = Game1.getCharacterFromName(npc);
            if (character is null)
                continue;
            NpcHistory history = new(npc);

            // Every gift ever given to this villager, counted by their taste for it.
            if (Game1.player.giftedItems.TryGetValue(npc, out var byItem))
            {
                foreach ((string id, int count) in byItem)
                {
                    if (ItemRegistry.Create(id, 1) is not { } item)
                        continue;
                    switch (GiftNotes.TasteLabel(character.getGiftTasteForThisItem(item)))
                    {
                        case "Love": history = history with { Loved = history.Loved + count }; break;
                        case "Like": history = history with { Liked = history.Liked + count }; break;
                        case "Dislike": history = history with { Disliked = history.Disliked + count }; break;
                        case "Hate": history = history with { Hated = history.Hated + count }; break;
                        default: history = history with { Neutral = history.Neutral + count }; break;
                    }
                }
            }

            // Heart events seen: every seen event whose Data/Events key carries a
            // "f <Npc> <points>" precondition counts as a heart event for that NPC.
            int heartEvents = 0;
            Utility.ForEachLocation(location =>
            {
                if (!location.TryGetLocationEvents(out _, out Dictionary<string, string>? events))
                    return true;
                foreach ((string key, _) in events)
                {
                    string[] segments = Event.SplitPreconditions(key);
                    if (segments.Length < 2 || !Game1.player.eventsSeen.Contains(segments[0]))
                        continue;
                    foreach (string precondition in segments.Skip(1))
                    {
                        string[] parts = precondition.Split(' ');
                        if (parts.Length >= 3 && parts[0] == "f"
                            && string.Equals(parts[1], npc, StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(parts[2], out _))
                            heartEvents++;
                    }
                }
                return true;
            });
            history = history with { HeartEventsSeen = heartEvents };

            // The relationship status; the enums' values coincide (Friendly -> None, the rest as-is).
            if (Game1.player.friendshipData.TryGetValue(npc, out Friendship? friendship))
                history = history with { Status = friendship.Status == FriendshipStatus.Friendly
                    ? HistoryStatus.None : (HistoryStatus)(int)friendship.Status };

            // Leave out what the mod already saw itself, so nothing counts twice.
            history = history.Except(RegardHistory.SeenInDiary(npc, _memory.DiaryOf(npc).Entries));

            histories.Add(history);
        }

        int tick = Now(TimeUtils.TickIndex(Game1.timeOfDay));
        foreach (HistorySeed seed in RegardHistory.Seed(_regard.Book, histories,
                     npc => _temperaments?.Of(npc) ?? Temperament.Neutral, _motiveOptions))
        {
            Monitor.Log("[shadow] " + seed.Line, LogLevel.Info);
            _playtest.Append(MotiveRecords.History(seed, tick));
        }

        // Remembered here and written by SaveMemory with the rest of the data (writing now would
        // be lost anyway: the next save rebuilds the data dict from scratch).
        _historySeeded = true;
    }

    /// <summary>Feeds this tick's ladder views and leads (memory only, never live positions) to
    /// the motives worker, and drains its finished decisions. The worker is the only place that
    /// calls the model (AGENTS.md rule 5); this method never does.</summary>
    private void RunMotives(int now)
    {
        var watch = Stopwatch.StartNew();
        int seed = MotivesSeed();
        var inputs = new List<MotiveInputs>();
        // A talk, a menu, a scene or a warp's fade: nothing in person is decided now; the NPC tries
        // again next tick (live test 2026-10-03). Game state, not a position (AGENTS.md rule 2).
        bool busy = !Context.IsPlayerFree;
        foreach (InitiationInput ladder in _lastLadderInputs) // this tick's views and leads, built by BuildViews
        {
            string npc = ladder.Npc;
            double news = _planToday.Where(c => string.Equals(c.Npc, npc, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.News).DefaultIfEmpty(0).Max();
            string card = NpcCard.Render(npc, TemperamentOf(npc), VoiceSheets.Voice(npc), ladder.Hearts, NowLine());
            bool met = Game1.player.friendshipData.ContainsKey(npc); // VERIFY: the game adds the entry at the first meeting
            inputs.Add(MotiveInputBuilder.Build(npc, now, _temperaments?.Of(npc) ?? Temperament.Neutral, ladder.Hearts,
                _memory.DiaryOf(npc).Entries, _regard.Book.Of(npc, MemoryStore.PlayerName), ladder.PlayerView, ladder.Lead,
                news, seed, met, card) with { PlayerBusy = busy });
        }
        if (inputs.Count > 0 && !_motives.EnqueueTick(now, inputs))
            Monitor.Log($"[shadow] motives are behind the model; skipped a tick ({_motives.Dropped} so far).", LogLevel.Trace);

        DrainMotives();
        _playtest.Append(new PerfRecord("motives", watch.Elapsed.TotalMilliseconds) { Tick = now });
    }

    /// <summary>Processes every finished motives decision exactly once: the [shadow] line, the
    /// viewer feed and the playtest records stay exactly as before, and when a live switch is on,
    /// the act is shown through LiveGate (or held back with a [live] reason). Called from
    /// RunMotives at each TimeChanged tick and about once a second from OnOneSecondUpdateTicked,
    /// so a wave shows while the player is still there. Never calls the model (AGENTS.md rule 5).</summary>
    private void DrainMotives()
    {
        foreach (MotiveEvent ev in _motives.Drain())
        {
            string line = MotiveText.Line(ev);
            Monitor.Log("[shadow] " + line, ev.Kind is "Act" or "Grudge" or "Responded" ? LogLevel.Info : LogLevel.Trace);
            _lastMotiveLines[ev.Npc] = line;
            if (ev.Kind is "Act" or "Grudge")
                AddFeed(ev.AbsoluteTick, "Motive", ev.Npc, line);
            _playtest.Append(MotiveRecords.Decision(ev));
            if (ev.Kind == "Grudge" && ev.RegardRelief > 0)
                _playtest.Append(MotiveRecords.Regard(_regard.Relieve(ev.Npc, MemoryStore.PlayerName, ev.RegardRelief), ev.AbsoluteTick));

            ShowLive(ev);
            if (_liveLedger.OnResolved(ev) is { } entry)
                _memory.Note(ev.Npc, entry);
        }
    }

    /// <summary>The first behavior to leave shadow (docs/spec/rollout.md, D30): the motives
    /// runner's Emote and Bubble acts, each behind its own switch, both off by default. LiveFacts
    /// is the one place besides CollectPresences/Observe that reads live state (AGENTS.md rule 2),
    /// and only to say "not now".</summary>
    private void ShowLive(MotiveEvent ev)
    {
        LiveAct? act = LivePlanner.From(ev, _config.Live, Game1.player.Name);
        if (act is null)
            return;
        NPC? npc = Game1.getCharacterFromName(act.Npc);
        if (npc is null)
            return;
        var facts = new LiveFacts(
            SinglePlayer: !Context.IsMultiplayer,
            PlayerFree: Context.IsPlayerFree,
            EventUp: Game1.eventUp,
            Festival: Game1.isFestival(),
            SameLocation: npc.currentLocation == Game1.player.currentLocation,
            DistanceTiles: Math.Max(Math.Abs(npc.TilePoint.X - Game1.player.TilePoint.X), Math.Abs(npc.TilePoint.Y - Game1.player.TilePoint.Y)),
            // isEmoting is public (NPC.cs:145); textAboveHeadTimer is protected int (NPC.cs:160),
            // so an already-showing bubble cannot be read — our own isEmoting flag is the busy
            // signal the gate gets.
            NpcBusy: npc.isEmoting,
            NpcVisible: !npc.IsInvisible);

        int now = Now(TimeUtils.TickIndex(Game1.timeOfDay));
        if (LiveGate.WhyNot(act, facts, now, _liveOptions) is { } why)
        {
            Monitor.Log("[live] " + LivePlanner.SkippedLine(act, why), LogLevel.Trace);
            return;
        }

        bool wasOpen = _live.IsOpen(act.Act);
        string? breaker = _live.Run(act.Act, () =>
        {
            if (act.Act == Act.Emote)
                npc.doEmote(act.EmoteId);
            else
                npc.showTextAboveHead(act.Text, duration: _liveOptions.BubbleMs);
        });
        if (breaker is not null)
        {
            // Error exactly once (the trip); the repeats while the switch stays off are Trace.
            Monitor.Log("[live] " + breaker, wasOpen ? LogLevel.Error : LogLevel.Trace);
            return;
        }
        Monitor.Log("[live] " + LivePlanner.ShownLine(act), LogLevel.Info);
        _liveLedger.Shown(act);
    }

    /// <summary>Like TomorrowLine, but for today at the current time: "spring 12 (Friday), sunny,
    /// 4:20 PM" — the card's day line while the motives worker weighs acts.</summary>
    private static string NowLine()
        => $"{SeasonWord(Game1.season)} {Game1.dayOfMonth} ({Game1.Date.DayOfWeek}), {WeatherWord()}, "
           + Game1.getTimeOfDayString(Game1.timeOfDay); // getTimeOfDayString: Game1.cs:15924, verified

    /// <summary>Why the NPC thinks the player is there, for the log.</summary>
    private static string DescribeLead(Whereabouts lead) => lead.Source switch
    {
        WhereaboutsSource.Told when lead.HopCount > 1 => $"{lead.ToldBy} heard you were there {Ago(lead.AgeTicks)}",
        WhereaboutsSource.Told => $"{lead.ToldBy} saw you there {Ago(lead.AgeTicks)}",
        WhereaboutsSource.SeenToday => $"saw you there {Ago(lead.AgeTicks)}",
        WhereaboutsSource.Habit => $"you're usually there at this hour ({lead.HabitShare * 100:0}% of the block, evidence {lead.Evidence:0})",
        _ => lead.Source.ToString(),
    };

    /// <summary>A tick age in words: 10 minutes per tick.</summary>
    private static string Ago(int ticks) => ticks switch
    {
        <= 0 => "just now",
        < 6 => $"{ticks * 10} minutes ago",
        < 12 => "an hour ago",
        _ => $"{ticks / 6} hours ago",
    };

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
        _planCollectedLinesToday = -1;
        _planToday = Array.Empty<IntentCandidate>();

        IReadOnlyDictionary<string, string> homes = ResolveHomes();
        var snapshots = _memory.Diaries
            .Where(kv => kv.Value.Entries.Count > 0)
            .Select(kv =>
            {
                string npc = kv.Key;
                _recentLines.TryGetValue(npc, out List<PlanPersistence.RecentLine>? said);
                // Cite cooldown: recently delivered (kind, subject) pairs dock the news score.
                var citations = PlanPersistence.RecentCitations(said, GameClock.DayIndex(Now(0)), _plannerOptions.CiteCooldownDays);
                var news = new NewsContext(npc, homes, BeliefsOf(npc), _regions, HeartsFor(npc), citations);
                // The NPC card is built here, on the game thread, and passed as a copy
                // (docs/spec/laya.md, "The NPC card").
                string card = NpcCard.Render(npc, TemperamentOf(npc), VoiceSheets.Voice(npc),
                    HeartsFor(npc), TomorrowLine());
                return new NpcMemorySnapshot(npc, VoiceSheets.Voice(npc), kv.Value.Entries.ToList(),
                    PlanPersistence.LinesFor(said), news, card);
            })
            .ToList();
        if (snapshots.Count == 0)
            return;

        int today = GameClock.DayIndex(Now(0));
        int seed = today; // deterministic per day
        _planJob = IntentPlanJob.Start(
            budget => new IntentPlanner(Guarded("plan", budget), new LineRenderer(), new Newsworthiness(), _plannerOptions)
                .Plan(snapshots, seed, sourceDay: today),
            TimeSpan.FromMilliseconds(_config.PlanningBudgetMs));
        _planJobStartedUtc = DateTime.UtcNow; // the plan perf record measures from here to collection
        Monitor.Log($"[shadow] planning tomorrow's intents in the background ({snapshots.Count} NPC diaries).", LogLevel.Info);
    }

    /// <summary>The temperament line for the NPC card from Data/Characters (plain words, per
    /// docs/spec/laya.md). The mapping lives in NpcDecision.CardText, shared with the card
    /// exporter, so the eval's cards always match the mod's wording.</summary>
    private static string TemperamentOf(string npc)
    {
        if (Game1.characterData is null || !Game1.characterData.TryGetValue(npc, out CharacterData? data))
            return "unknown";
        return CardText.TemperamentWords(data.Manner.ToString(), data.SocialAnxiety.ToString(), data.Optimism.ToString());
    }

    /// <summary>The card's "today" line, describing the delivery day (the plan runs at DayEnding
    /// but the lines arrive tomorrow morning): tomorrow's date in English (the checkpoint is
    /// English), tomorrow's weather, and a fixed morning time. Verified: the weatherForTomorrow
    /// values are the game's weather strings ("Sun", "Rain", "Snow", "Wind", "Storm") plus
    /// "Wedding" on a wedding morning (Game1.cs:182/3171/8837).</summary>
    private static string TomorrowLine()
    {
        // TotalDays has a setter that recomputes day/season/year (WorldDate.cs, verified).
        WorldDate tomorrow = new(Game1.Date) { TotalDays = Game1.Date.TotalDays + 1 };
        string weather = Game1.weatherForTomorrow switch
        {
            "Rain" or "Storm" => "rainy",
            "Snow" => "snowy",
            "Wind" => "windy",
            "Wedding" => "your wedding day",
            _ => "sunny",
        };
        return $"{SeasonWord(tomorrow.Season)} {tomorrow.DayOfMonth} ({tomorrow.DayOfWeek}), {weather}, morning";
    }

    private static string SeasonWord(Season season) => season switch
    {
        Season.Spring => "spring",
        Season.Summer => "summer",
        Season.Fall => "fall",
        Season.Winter => "winter",
        _ => season.ToString().ToLowerInvariant(),
    };

    /// <summary>Day-end diary notes: PassedBy and BirthdayForgotten (docs/spec/diary.md). Pure memory
    /// plus game facts (hearts, birthdays, gifts); logged at Trace like every new diary kind.</summary>
    private void NoteDayEnd()
    {
        int now = Now(119); // the day's last live tick (1:50 AM)

        var names = new HashSet<string>(VillagerNames(), StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Friendship> pair in Game1.player.friendshipData.Pairs)
            names.Add(pair.Key);

        IReadOnlyList<(string Npc, DiaryEntry Entry)> notes = _memory.DayEndNotes(
            names, now, HeartsFor, BirthdayFor, GiftedToday, _talkedToday, _diaryOptions);
        foreach ((string npc, DiaryEntry entry) in notes)
            Monitor.Log($"[shadow] diary {npc}: {entry.Kind} {entry.Subject} ({entry.Detail})", LogLevel.Trace);

        NoteFestival(names, now);
        _festivalAttended = false;
        _festivalActors.Clear();
        _festivalTalked.Clear();
    }

    /// <summary>Festival / MissedFestival at day end (docs/spec/diary.md, "Triggers and game
    /// hooks"): "attended" means Game1.isFestival() was true on any tick today; the actors are
    /// the names captured while the event ran (clones, so names only).</summary>
    private void NoteFestival(IReadOnlyCollection<string> names, int now)
    {
        if (!Enum.TryParse(Game1.currentSeason, ignoreCase: true, out Season season))
            return;
        if (!Utility.isFestivalDay(Game1.dayOfMonth, season))
            return; // passive festivals (Night Market etc.) are separate, per the spec

        // The date key (e.g. "spring13") is the stable festival id; the FestivalDates VALUES are
        // localized display names, which must never reach a saved diary.
        string dateKey = Utility.getSeasonKey(season) + Game1.dayOfMonth;
        if (!DataLoader.Festivals_FestivalDates(Game1.temporaryContent).ContainsKey(dateKey))
            return;

        IReadOnlyList<(string Npc, DiaryEntry Entry)> notes = FestivalNotes.AtDayEnd(
            dateKey, _festivalAttended, _festivalActors, _festivalTalked, names, HeartsFor, _diaryOptions, now);
        foreach ((string npc, DiaryEntry entry) in notes)
        {
            _memory.Note(npc, entry); // unlike DayEndNotes, FestivalNotes only builds: save them here
            Monitor.Log($"[shadow] diary {npc}: {entry.Kind} {entry.Subject} ({entry.Detail})", LogLevel.Trace);
        }
    }

    /// <summary>While the festival runs (time does not pass), note that the player attended and
    /// keep the actors' names. The actors are event clones (EventActor), so names only.</summary>
    private void CaptureFestival()
    {
        if (!Game1.isFestival())
            return;
        bool firstCaptureOfFestival = !_festivalAttended;
        _festivalAttended = true;
        if (Game1.CurrentEvent is { } ev)
        {
            foreach (NPC actor in ev.actors)
                if (actor is not null && !string.IsNullOrEmpty(actor.Name))
                    _festivalActors.Add(actor.Name);
        }
        if (firstCaptureOfFestival)
        {
            // The playtest log's festival record: written once, when the festival is first seen
            // (the day-end FestivalNotes still carry the full actor set into the diaries).
            int tick = TimeUtils.TickIndex(Game1.timeOfDay);
            _playtest.Append(new GameRecord("festival",
                $"attended=1; actors={string.Join(", ", _festivalActors.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}")
            {
                Tick = Now(tick < 0 ? 119 : tick),
            });
        }
    }

    /// <summary>Special orders complete through SpecialOrder.CheckCompletion, not Quest.questComplete:
    /// detect a newly completed order by diffing team.completedSpecialOrders and write QuestHelped
    /// for the order's requester (docs/spec/diary.md, "Triggers and game hooks").</summary>
    private void NoteSpecialOrders(int now)
    {
        foreach (string key in Game1.player.team.completedSpecialOrders)
        {
            if (!_seenSpecialOrders.Add(key))
                continue;
            // Resolve from the order DATA by key first: the order can already be gone from
            // team.specialOrders (removed once everyone has claimed, SpecialOrder.cs:903).
            string? requester = null;
            if (DataLoader.SpecialOrders(Game1.content) is { } orders && orders.TryGetValue(key, out SpecialOrderData? data))
                requester = data?.Requester;
            if (string.IsNullOrEmpty(requester))
                foreach (SpecialOrder order in Game1.player.team.specialOrders)
                    if (string.Equals(order.questKey.Value, key, StringComparison.OrdinalIgnoreCase))
                        requester = order.requester.Value;
            if (string.IsNullOrEmpty(requester))
            {
                Monitor.Log($"[shadow] special order {key} completed but no requester was found; no QuestHelped written.", LogLevel.Trace);
                continue;
            }
            _memory.Note(requester, QuestNotes.ToDiaryEntry(new QuestDetails(requester, QuestNotes.Special, now)));
            Monitor.Log($"[shadow] diary {requester}: QuestHelped Player (Special)", LogLevel.Trace);
        }
    }

    /// <summary>Villager names from every loaded location (same coverage as CollectPresences).</summary>
    private static IEnumerable<string> VillagerNames()
    {
        IEnumerable<GameLocation> locations = Game1.locations;
        if (Game1.player.currentLocation is { } playerLocation)
            locations = locations.Append(playerLocation);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GameLocation location in locations)
        {
            if (location is null)
                continue;
            foreach (NPC npc in location.characters)
                if (npc.IsVillager)
                    names.Add(npc.Name);
        }
        return names;
    }

    /// <summary>Matches the game's own isBirthday(): Birthday_Season is Utility.getSeasonKey(
    /// data.BirthSeason.Value), compared to Game1.currentSeason (1.6.15 decompile, NPC.cs:822 and
    /// :4815). Game1.characterData is read at use time, so content-pack edits to Data/Characters
    /// are seen.</summary>
    private static bool BirthdayFor(string npc)
        => Game1.characterData is not null
           && Game1.characterData.TryGetValue(npc, out CharacterData? data)
           && data.BirthSeason is { } season
           && string.Equals(Utility.getSeasonKey(season), Game1.currentSeason, StringComparison.OrdinalIgnoreCase)
           && data.BirthDay == Game1.dayOfMonth;

    private static bool GiftedToday(string npc)
        => Game1.player.friendshipData.TryGetValue(npc, out Friendship? friendship) && friendship.GiftsToday > 0;

    /// <summary>NPC -> home location: the regions.json `homes` table wins, then Data/Characters
    /// (first unconditional Home entry, else the first; Condition handling still to verify —
    /// docs/spec/diary.md). Read at use time so content packs are seen.</summary>
    private Dictionary<string, string> ResolveHomes()
    {
        var homes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string npc, string location) in _regions.Homes)
            homes[npc] = location;

        foreach ((string npc, CharacterData data) in Game1.characterData ?? new Dictionary<string, CharacterData>())
        {
            if (homes.ContainsKey(npc))
                continue;
            string? home = data.Home?.FirstOrDefault(h => string.IsNullOrEmpty(h.Condition))?.Location
                           ?? data.Home?.FirstOrDefault()?.Location;
            if (!string.IsNullOrEmpty(home))
                homes[npc] = home;
        }
        return homes;
    }

    /// <summary>The observer's routine beliefs about each subject, keyed by subject name, as
    /// snapshots: the planner reads them on the plan job's thread while the game thread keeps
    /// observing (AGENTS.md: pass copies between threads).</summary>
    private Dictionary<string, RoutineBelief> BeliefsOf(string observer)
    {
        var result = new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase);
        string prefix = observer + ">";
        foreach ((string key, RoutineBelief belief) in _memory.Beliefs)
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result[key[prefix.Length..]] = belief.Snapshot();
        return result;
    }

    /// <summary>The news score for one of <paramref name="npc"/>'s diary entries, with a fresh
    /// context (ambient chat's shareable-event ranking; docs/spec/ledger-gossip.md).</summary>
    private double NewsScore(string npc, DiaryEntry entry)
    {
        var news = new NewsContext(npc, ResolveHomes(), BeliefsOf(npc), _regions, HeartsFor(npc),
            Array.Empty<(string, string)>());
        return _news.Score(entry, news);
    }

    private static readonly Newsworthiness _news = new();

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

        // An empty plan has several possible causes; the log must not blame one of them
        // (the threshold) when the plan failed outright.
        string summary = error is not null
            ? "[shadow] collected overnight plan: no lines (the plan failed; see the error above)."
            : plan.Candidates.Count == 0
                ? "[shadow] collected overnight plan: no lines (no candidates: nothing newsworthy to cite, or the model answered below the speak threshold for everyone)."
                : $"[shadow] collected overnight plan: {plan.Candidates.Count} line(s) for today.";
        Monitor.Log(summary, LogLevel.Info);
        _planCollectedLinesToday = plan.Candidates.Count;
        _planToday = plan.Candidates.ToList();

        int tick = TimeUtils.TickIndex(Game1.timeOfDay);
        int now = Now(Math.Max(0, tick));
        // The plan job's wall-clock cost, from StartPlanning to here (the model calls dominate).
        _playtest.Append(new PerfRecord("plan", (DateTime.UtcNow - _planJobStartedUtc).TotalMilliseconds) { Tick = now });
        for (int i = 0; i < plan.Candidates.Count; i++)
        {
            IntentCandidate candidate = plan.Candidates[i];
            _intentsToday.Add(candidate.Npc);
            Monitor.Log($"[shadow] {candidate.Npc} would say: \"{candidate.Line}\" ({candidate.Reason})", LogLevel.Info);
            AddFeed(now, "Line", candidate.Npc, $"{candidate.Npc} would say: \"{candidate.Line}\" ({candidate.Reason})");
            _playtest.Append(new PlanRecord(candidate.Npc, candidate.Line, candidate.Reason, candidate.News, i)
            {
                Tick = now,
            });
        }

        _planJob.Dispose();
        _planJob = null;
    }

    /// <summary>One Info line every two game hours so the pipeline is visible in the console
    /// without flooding it. The formatting lives in <see cref="NpcInitiation.Heartbeat"/> (pure,
    /// tested); this only assembles the current values.</summary>
    private void LogHeartbeat(int tick)
    {
        if (!Heartbeat.ShouldFire(tick))
            return;

        Heartbeat.PlanState plan = _planJob is null ? Heartbeat.PlanState.None
            : (_planJob.IsCompleted ? Heartbeat.PlanState.Ready : Heartbeat.PlanState.Running);
        if (_model is LayaDecisionClient laya)
        {
            (double median, double p95) = laya.Latency();
            Monitor.Log(
                Heartbeat.Format(tick, _memory.Diaries.Count, MotiveDrive.Strongest(_motives.LatestDecisions), _backlogAtTickStart,
                    _motives.Dropped, plan, laya.Calls, laya.CallFallbacks, median, p95,
                    planCollectedLines: _planCollectedLinesToday),
                LogLevel.Info);
        }
        else
        {
            Monitor.Log(
                Heartbeat.Format(tick, _memory.Diaries.Count, MotiveDrive.Strongest(_motives.LatestDecisions), _backlogAtTickStart,
                    _motives.Dropped, plan, planCollectedLines: _planCollectedLinesToday),
                LogLevel.Info);
        }
    }

    // ---- NPC Minds viewer (read-only) -------------------------------------------------------------

    /// <summary>Starts the loopback viewer server (config MindsViewer). A port already in use only
    /// logs a warning; the mod runs the same without the viewer.</summary>
    private void StartMindsViewer()
    {
        if (!_config.MindsViewer)
            return;
        var server = new MindsServer(_calls, MindsSnapshot.Idle(++_mindsSeq, BackendName()));
        if (server.TryStart(_config.MindsViewerPort, out string? error))
        {
            _minds = server;
            Monitor.Log($"NPC Minds viewer: open http://127.0.0.1:{server.Port}/ in a browser (read-only).", LogLevel.Info);
        }
        else
        {
            server.Dispose();
            Monitor.Log($"NPC Minds viewer is off: port {_config.MindsViewerPort} is not available ({error}). Set MindsViewerPort in config.json to another port.", LogLevel.Warn);
        }
    }

    /// <summary>Builds the viewer's snapshot on the game thread and hands it to the server. It
    /// reads memory, the ladder's last finished state and the plan, and changes nothing; a
    /// failure only means the viewer shows the previous snapshot.</summary>
    private void PublishMinds()
    {
        if (_minds is null || !Context.IsWorldReady)
            return;
        try
        {
            int tick = TimeUtils.TickIndex(Game1.timeOfDay);
            int now = Now(tick < 0 ? 119 : tick);

            MindsStats stats = _model is LayaDecisionClient laya
                ? LayaStats(laya)
                : new MindsStats(_motives.Backlog, _motives.Dropped, -1, -1, -1, -1);
            string planState = _planJob is null ? "none" : _planJob.IsCompleted ? "ready" : "running";

            // Tonight's-news preview: the same NewsContext the planner gets, but with the live
            // beliefs (the builder runs right here on the game thread and only reads them).
            IReadOnlyDictionary<string, string> homes = ResolveHomes();
            var beliefs = new Dictionary<string, Dictionary<string, RoutineBelief>>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, RoutineBelief belief) in _memory.Beliefs)
            {
                int split = key.IndexOf('>');
                if (split <= 0)
                    continue;
                string observer = key[..split];
                if (!beliefs.TryGetValue(observer, out Dictionary<string, RoutineBelief>? mine))
                    beliefs[observer] = mine = new Dictionary<string, RoutineBelief>(StringComparer.OrdinalIgnoreCase);
                mine[key[(split + 1)..]] = belief;
            }
            NewsContext NewsFor(string npc) => new(npc, homes,
                beliefs.TryGetValue(npc, out Dictionary<string, RoutineBelief>? mine) ? mine : new Dictionary<string, RoutineBelief>(),
                _regions, HeartsFor(npc), Array.Empty<(string, string)>());

            var inputs = new MindsInputs(++_mindsSeq, now, BackendName(), _model is not LayaDecisionClient || _layaUp,
                stats, planState, null, _lastLadderInputs, _intentsToday.ToList(), _planToday,
                _feed.Newest(), NewsFor, TemperamentViewOf, _spread.Copy(), _calibration,
                Motives: _motives.LatestDecisions,
                MotiveStates: _motives.LatestStates,
                RegardFor: npc => _regard.Book.Of(npc, MemoryStore.PlayerName),
                LastMotiveLines: _lastMotiveLines);
            _minds.Publish(_mindsBuilder.Build(_memory, inputs));
        }
        catch (Exception ex)
        {
            Monitor.Log($"NPC Minds snapshot failed (the game is unaffected): {ex.Message}", LogLevel.Trace);
        }
    }

    /// <summary>The seed temperament table shipped beside regions.json (docs/spec/temperament.md),
    /// with the overrides file applied last. Missing or unreadable: a warning, and the viewer shows
    /// no temperament; nothing else reads it yet.</summary>
    private TemperamentTable? LoadTemperaments()
    {
        try
        {
            string path = Path.Combine(Helper.DirectoryPath, "temperament.json");
            if (!File.Exists(path))
            {
                Monitor.Log("temperament.json is missing from the mod folder; the viewer will not show temperaments.", LogLevel.Warn);
                return null;
            }
            TemperamentTable table = TemperamentTable.FromJson(File.ReadAllText(path));
            string overrides = Path.Combine(Helper.DirectoryPath, "temperament-overrides.json");
            if (File.Exists(overrides))
                table = table.WithOverrides(TemperamentTable.OverridesFromJson(File.ReadAllText(overrides)));
            return table;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Could not read the temperament table; the viewer will not show temperaments. {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    /// <summary>One NPC's temperament for the viewer, built once per save (the table and
    /// Data/Characters don't change while playing).</summary>
    /// <summary>The committed Laya calibration table (data/laya-calibration.json), shown beside
    /// the viewer's spread panel numbers. Missing or unreadable: the panel just shows no
    /// calibration column; nothing else reads it. It never feeds a decision.</summary>
    private LayaCalibration? LoadCalibration()
    {
        try
        {
            string path = Path.Combine(Helper.DirectoryPath, "laya-calibration.json");
            if (!File.Exists(path))
            {
                Monitor.Log("laya-calibration.json is missing from the mod folder; the spread panel will not compare against it.", LogLevel.Warn);
                return null;
            }
            return LayaCalibration.FromJson(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Monitor.Log($"Could not read laya-calibration.json; the spread panel will not compare against it. {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    private TemperamentView? TemperamentViewOf(string npc)
    {
        if (_temperaments is null)
            return null;
        if (!_temperamentViews.TryGetValue(npc, out TemperamentView? view))
        {
            bool seeded = _temperaments.Rows.ContainsKey(npc);
            string? game = Game1.characterData is not null && Game1.characterData.ContainsKey(npc) ? TemperamentOf(npc) : null;
            _temperamentViews[npc] = view = MindsSnapshotBuilder.TemperamentOf(_temperaments.Of(npc), seeded, game);
        }
        return view;
    }

    private MindsStats LayaStats(LayaDecisionClient laya)
    {
        (double median, double p95) = laya.Latency();
        // The viewer's red "questions failing" header + a one-time Warn when the failures streak
        // reaches 5 (docs/spec/debug-tools.md), and one Info when it clears again.
        string? modelError = null;
        if (laya.ConsecutiveFailures >= 5)
        {
            modelError = laya.LastError;
            if (!_layaWarned)
            {
                _layaWarned = true;
                Monitor.Log($"Laya questions keep failing ({laya.ConsecutiveFailures} in a row; last: {laya.LastError}). Decisions fall back until it recovers.", LogLevel.Warn);
            }
        }
        else if (_layaWarned)
        {
            _layaWarned = false;
            Monitor.Log("Laya questions are succeeding again.", LogLevel.Info);
        }
        return new MindsStats(_motives.Backlog, _motives.Dropped, laya.Calls, laya.CallFallbacks, median, p95, modelError);
    }

    /// <summary>One line for the viewer's event feed, stamped with the game clock.</summary>
    private void AddFeed(int absoluteTick, string kind, string? npc, string text)
    {
        if (_minds is null)
            return;
        string when = MindsSnapshotBuilder.Clock(GameClock.FromAbsoluteTick(absoluteTick).Tick);
        _feed.Add(seq => new FeedItem(seq, absoluteTick, when, kind, npc, text));
    }

    private string BackendName() => _model switch
    {
        LayaDecisionClient => "Laya",
        VariedFakeDecisionClient => "Varied",
        _ => "Fake",
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _playtest?.Flush(); // a clean unload keeps the day's buffered records
            _playtest?.Dispose();
            _minds?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ---- playtest log (docs/spec/debug-tools.md, "Playtest log") ----------------------------------

    /// <summary>
    /// The 6:00 playtest block: the new day's weather, the per-NPC memory census, and the game
    /// events the diary hooks caught, then a flush. The 6:00 tick is already the new date, so
    /// "the day" here is the one that just ended — the same window the trim counters read below
    /// accumulated in (Diary.TakeTrimmedToday is read and reset here); counting the new day
    /// instead would report an empty day, every day, and no entry would ever be counted.
    /// </summary>
    private void PlaytestMorning(int now)
    {
        _playtest.Append(new GameRecord("weather", WeatherWord()) { Tick = now });

        int censusDay = GameClock.DayIndex(now) - 1;
        foreach (string npc in _memory.Diaries.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            Diary diary = _memory.Diaries[npc];
            int added = diary.Entries.Count(e => GameClock.DayIndex(e.AbsoluteTick) == censusDay);
            _playtest.Append(new MemoryRecord(npc, diary.Entries.Count, added, diary.TakeTrimmedToday(),
                _memory.Ledger.EntryCountFor(npc), _memory.Beliefs.Values.Count(b => b.Observer == npc))
            {
                Tick = now,
            });

            // The same diaries, same window: the game facts the diary hooks caught (gifts,
            // quests completed, conversations). Other kinds are not game events.
            foreach (DiaryEntry entry in diary.Entries)
            {
                if (GameClock.DayIndex(entry.AbsoluteTick) != censusDay || PlaytestGameKind(entry.Kind) is not { } kind)
                    continue;
                IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(entry.Detail);
                _playtest.Append(new GameRecord(kind, entry.Detail ?? "")
                {
                    Npc = npc,
                    Item = detail.TryGetValue("name", out string? item) ? item : null,
                    Taste = detail.TryGetValue("taste", out string? taste) ? taste : null,
                    Tick = entry.AbsoluteTick,
                });
            }
        }

        _playtest.Flush();
    }

    /// <summary>The weather word for the playtest log's daily game record. Storms are rainy too,
    /// so lightning is checked first. Verified against the installed 1.6.15 decompile:
    /// <c>Game1.isRaining</c>, <c>isSnowing</c> and <c>isLightning</c> are the game's own members
    /// (there is no static <c>IsRaining</c> property on Game1).</summary>
    private static string WeatherWord()
        => Game1.isLightning ? "storm"
         : Game1.isSnowing ? "snow"
         : Game1.isRaining ? "rain"
         : "clear";

    /// <summary>The playtest game-record kind for a diary entry (the GameRecord doc's "game"
    /// row: gifts, quests completed, conversations), or null for kinds that are not game facts.</summary>
    private static string? PlaytestGameKind(string? kind) => kind switch
    {
        "GiftReceived" => "gift",
        "QuestHelped" => "quest",
        "Talked" => "talked",
        _ => null,
    };

    /// <summary>A playtest gossip record for one Heard diary line: the line's detail carries who
    /// told it and the original kind (MemoryStore.TryShareEvent writes "from" and "kind"). A Heard
    /// line carries no hop count of its own (that lives in the ledger), so Hops is 0.</summary>
    private static GossipRecord HeardRecord(string listener, DiaryEntry heard)
    {
        IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(heard.Detail);
        return new GossipRecord(
            Teller: detail.TryGetValue("from", out string? from) ? from : "",
            Listener: listener,
            OriginalKind: detail.TryGetValue("kind", out string? kind) ? kind : "",
            Subject: heard.Subject,
            Hops: 0,
            Kind: "heard",
            Answered: null)
        {
            Tick = heard.AbsoluteTick,
        };
    }

    // ---- persistence ----------------------------------------------------------------------------

    private void SaveMemory()
    {
        var data = new Dictionary<string, string>
        {
            ["version"] = MemoryStore.CurrentVersion.ToString(),
            ["memory"] = _memory.ToJson(),
            ["regard"] = _regard.Book.ToJson(),
            ["motives"] = _motives.LatestJson,
        };
        if (_historySeeded)
            data["historySeeded"] = "1"; // or the next load would seed the history a second time

        // The plan survives save-and-quit (persistence.md keys; additive, no version bump).
        if (_planToday.Count > 0)
            data["intents"] = new PlanPersistence.SavedPlan(
                GameClock.DayIndex(Now(0)), false, _planToday.ToList()).ToJson();
        if (_recentLines.Count > 0)
            data["recentLines"] = PlanPersistence.RecentLine.ToJson(
                _recentLines.Values.SelectMany(lines => lines));

        Helper.Data.WriteSaveData(SaveKey, data);
        Monitor.Log($"[shadow] Memory saved ({_memory.Diaries.Count} NPC diaries, {_memory.Beliefs.Count} beliefs; plan {_planToday.Count} line(s) kept for today).", LogLevel.Info);
    }

    private void LoadMemory()
    {
        var model = Helper.Data.ReadSaveData<Dictionary<string, string>>(SaveKey);
        _memory = new MemoryStore(MemorySeed());
        if (model is null)
        {
            NewRegard(null);
            _motives = NewMotives(null);
            return;
        }

        // Only step-4/5 saves have no version; anything versioned is read as the current format.
        if (model.ContainsKey("version"))
        {
            if (model.TryGetValue("memory", out string? memoryJson))
                _memory = MemoryStore.FromJson(memoryJson);
            // An old "ladder" value (the retired urge ladder, D31) is left unread and not written again.
            _memory.RemoveDiary("null"); // junk from before the quest-target guard (week review, finding 2)

            // A saved plan is only today's: an older one belongs to a day that already happened.
            if (model.TryGetValue("intents", out string? intentsJson)
                && PlanPersistence.SavedPlan.FromJson(intentsJson) is { } saved
                && !saved.Delivered && saved.Day == GameClock.DayIndex(Now(0)))
            {
                _planToday = saved.Candidates.ToList();
                foreach (IntentCandidate candidate in saved.Candidates)
                    _intentsToday.Add(candidate.Npc);
                _planCollectedLinesToday = saved.Candidates.Count;
            }
            if (model.TryGetValue("recentLines", out string? linesJson))
            {
                foreach (PlanPersistence.RecentLine line in PlanPersistence.RecentLine.FromJson(linesJson))
                {
                    if (!_recentLines.TryGetValue(line.Npc, out List<PlanPersistence.RecentLine>? mine))
                        _recentLines[line.Npc] = mine = new List<PlanPersistence.RecentLine>();
                    mine.Add(line);
                }
            }
            NewRegard(model.GetValueOrDefault("regard")); // missing or damaged values load empty
            _motives = NewMotives(model.GetValueOrDefault("motives"));
            return;
        }

        // Version 1 (steps 4-5): ticks had no year. Migrate relative to today.
        _memory = MemoryStore.FromVersion1(model, new GameTime(GameClock.SeasonIndex(Game1.currentSeason), Game1.dayOfMonth, 0, Game1.year));
        Monitor.Log("Migrated memory from the previous save format (added the year to old timestamps).", LogLevel.Info);
        NewRegard(null); // version-1 saves predate regard and motives
        _motives = NewMotives(null);
    }
}
