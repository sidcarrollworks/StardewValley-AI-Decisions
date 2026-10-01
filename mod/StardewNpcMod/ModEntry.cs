using HarmonyLib;
using NpcDecision;
using NpcDiaryEvents;
using NpcInitiation;
using NpcIntents;
using NpcMemory;
using NpcMinds;
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
    private BackgroundLadder _ladder = null!;
    private PlayerSearch _search = new();
    private IntentPlanJob? _planJob;

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
        _ladder = NewLadder(null);

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
        helper.Events.Display.MenuChanged += OnMenuChanged;

        ApplyPatches();
        _temperaments = LoadTemperaments();
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
        // the viewer's call log.
        return _config.MindsViewer ? new RecordingDecisionClient(resilient, _calls, caller) : resilient;
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

    private BackgroundLadder NewLadder(string? json)
    {
        int seed = Fnv1a.Seed("ladder", Game1.uniqueIDForThisGame.ToString()); // VERIFY: per-save id
        InitiationLadder ladder = json is null
            ? new InitiationLadder(Guarded("ladder"), seed)
            : InitiationLadder.FromJson(json, Guarded("ladder"), seed);
        return new BackgroundLadder(ladder, _config.LadderMaxBacklog);
    }

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
            var routines = new Dictionary<string, NpcRoutine>(StringComparer.OrdinalIgnoreCase);
            foreach (string npc in Game1.characterData.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(npc, spouse, StringComparison.OrdinalIgnoreCase))
                    continue; // VERIFY: a spouse's schedule is the marriage one; priors about it are wrong
                // VERIFY: schedules live at Data/Schedules/<name> as Dictionary<string, string>.
                Dictionary<string, string> schedules =
                    Game1.content.Load<Dictionary<string, string>>("Data/Schedules/" + npc);
                if (schedules is { Count: > 0 })
                    routines[npc] = extractor.Extract(npc, schedules, new ExtractorOptions());
            }

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
            LoadMemory();
            _talkedToday.Clear(); // a load starts a fresh day; never carry a previous session's set
            _seenSpecialOrders.Clear();
            foreach (string key in Game1.player.team.completedSpecialOrders)
                _seenSpecialOrders.Add(key); // old completions must not re-fire QuestHelped
            SeedPriors(); // family routine priors, once per save (routines.md, step 7)
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
        PublishMinds();
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
        // VERIFY: TimeChanged fires on the ten-minute tick (and may also fire at other clock jumps).
        int tick = TimeUtils.TickIndex(e.NewTime);
        if (tick < 0)
            return; // outside the 600..2600 live day
        int now = Now(tick);
        if (tick == 0)
            _talkedToday.Clear(); // the new day: the day-end notes already used yesterday's set

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
            _memory.Observe(now, CollectPresences(), _regions, HeartsFor);
            AskAround(now);
            foreach (DiaryEntry heard in _memory.Chat(now, NewsScore, _chatOptions))
                Monitor.Log($"[shadow] diary {heard}: {heard.Kind} {heard.Subject} ({heard.Detail})", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Observation failed: {ex}", LogLevel.Error);
        }

        try
        {
            RunLadder(now);
            LogHeartbeat(tick); // before CollectPlan, so "ready" can appear for the finishing tick
            CollectPlan(morning: false);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow ladder failed: {ex}", LogLevel.Error);
        }

        PublishMinds(); // last: the viewer sees this tick's memory, ladder results and plan
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
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        try
        {
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
        _ladder = NewLadder(null);
        _search = new PlayerSearch();
        _intentsToday.Clear();
        _talkedToday.Clear();
        _events.Clear();
        _seenSpecialOrders.Clear();
        _festivalAttended = false;
        _festivalActors.Clear();
        _festivalTalked.Clear();
        _layaUp = true;
        _backlogAtTickStart = 0;
        _planCollectedLinesToday = -1;
        QuestPatch.Reset();
        _feed.Clear();
        _calls.Clear();
        _lastLadderInputs = Array.Empty<InitiationInput>();
        _planToday = Array.Empty<IntentCandidate>();
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
        if (tick < 0)
            return;
        _ladder.EnqueueResponse(speaker.Name, Now(tick));

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

    // ---- initiation ladder (shadow) -------------------------------------------------------------

    /// <summary>NPCs who miss the player ask the NPCs around them (memory only, game thread). The
    /// urges come from the ladder's last finished tick.</summary>
    private void AskAround(int now)
    {
        foreach (SearchEvent ev in _search.Tick(_memory, now, _ladder.LatestUrges))
        {
            LedgerView learned = ev.Learned;
            string heard = learned.HopCount > 1 ? "heard you were" : "saw you";
            string where = learned.Place is null ? "" : $" at {PlaceNames.Display(learned.Place)}";
            string text = $"{ev.Seeker} asked {string.Join(", ", ev.Asked)} about you: {learned.ToldBy} {heard}{where} {Ago(learned.AgeTicks)}.";
            Monitor.Log("[shadow] " + text, LogLevel.Info);
            AddFeed(now, "Asked", ev.Seeker, text);
        }
    }

    /// <summary>Hand the ladder this tick's inputs (each NPC's own ledger view of the player and its
    /// best lead on where the player is, both from memory, never a live position) and log whatever
    /// the worker finished.</summary>
    private void RunLadder(int now)
    {
        var inputs = new List<InitiationInput>();
        foreach (string npc in _memory.Diaries.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            LedgerView? view = _memory.Ledger.View(npc, MemoryStore.PlayerName, now);
            Whereabouts lead = _memory.LookFor(npc, MemoryStore.PlayerName, now, _regions.BlockMinutes, regions: _regions);
            inputs.Add(new InitiationInput(npc, view, _intentsToday.Contains(npc), HeartsFor(npc), lead));
        }
        _lastLadderInputs = inputs; // the viewer shows what the ladder saw (views, leads, hearts)
        _backlogAtTickStart = _ladder.Backlog; // read BEFORE enqueueing: this tick's work is still running
        if (inputs.Count > 0 && !_ladder.EnqueueTick(now, inputs))
            Monitor.Log($"[shadow] ladder is behind the model; skipped a tick ({_ladder.Dropped} so far).", LogLevel.Trace);

        foreach (BackgroundLadder.Result result in _ladder.Drain())
        {
            foreach ((string npc, DiaryEntry entry) in result.DiaryLines)
                _memory.DiaryOf(npc).Append(entry);
            foreach (InitiationEvent ev in result.Events)
            {
                string text = ev switch
                {
                    { Kind: "Attempt", Lead: { } lead } =>
                        $"{ev.Npc} would go looking for you at {PlaceNames.Display(lead.Place)} ({DescribeLead(lead)}; urge {ev.UrgeBefore:0.00}) (shadow: {ev.Npc} did not move)",
                    { Kind: "Attempt" } => $"{ev.Npc} would try {ev.Step} (urge {ev.UrgeBefore:0.00}; {ev.Reason})",
                    _ => $"{ev.Npc}: {ev.Step} {ev.Kind.ToLowerInvariant()} (urge {ev.UrgeBefore:0.00} -> {ev.UrgeAfter:0.00}; {ev.Reason})",
                };
                Monitor.Log("[shadow] " + text, LogLevel.Info);
                AddFeed(ev.AbsoluteTick, ev.Kind, ev.Npc, text);
            }
        }
    }

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
                var news = new NewsContext(npc, homes, BeliefsOf(npc), _regions, HeartsFor(npc),
                    Array.Empty<(string, string)>());
                // The NPC card is built here, on the game thread, and passed as a copy
                // (docs/spec/laya.md, "The NPC card").
                string card = NpcCard.Render(npc, TemperamentOf(npc), VoiceSheets.Voice(npc),
                    HeartsFor(npc), TomorrowLine());
                return new NpcMemorySnapshot(npc, VoiceSheets.Voice(npc), kv.Value.Entries.ToList(),
                    Array.Empty<string>(), news, card);
            })
            .ToList();
        if (snapshots.Count == 0)
            return;

        int today = GameClock.DayIndex(Now(0));
        int seed = today; // deterministic per day
        _planJob = IntentPlanJob.Start(
            budget => new IntentPlanner(Guarded("plan", budget), new LineRenderer(), new Newsworthiness())
                .Plan(snapshots, seed, sourceDay: today),
            TimeSpan.FromMilliseconds(_config.PlanningBudgetMs));
        Monitor.Log($"[shadow] planning tomorrow's intents in the background ({snapshots.Count} NPC diaries).", LogLevel.Info);
    }

    /// <summary>The temperament line for the NPC card from Data/Characters (plain words, per
    /// docs/spec/laya.md).</summary>
    private static string TemperamentOf(string npc)
    {
        if (Game1.characterData is null || !Game1.characterData.TryGetValue(npc, out CharacterData? data))
            return "unknown";
        return $"manners {MannerWord(data.Manner)}, {AnxietyWord(data.SocialAnxiety)}, {OptimismWord(data.Optimism)}";
    }

    private static string MannerWord(NpcManner m) => m switch { NpcManner.Polite => "polite", NpcManner.Rude => "rude", _ => "neutral" };
    private static string AnxietyWord(NpcSocialAnxiety a) => a switch { NpcSocialAnxiety.Outgoing => "outgoing", NpcSocialAnxiety.Shy => "shy", _ => "neutral" };
    private static string OptimismWord(NpcOptimism o) => o switch { NpcOptimism.Positive => "optimistic", NpcOptimism.Negative => "pessimistic", _ => "neutral" };

    /// <summary>The card's "today" line, describing the delivery day (the plan runs at DayEnding
    /// but the lines arrive tomorrow morning): tomorrow's date in English (the checkpoint is
    /// English), tomorrow's weather, and a fixed morning time. VERIFY: the weatherForTomorrow key
    /// to word mapping (the game's own weather strings are "Sun", "Rain", "Snow", "Wind", "Storm").</summary>
    private static string TomorrowLine()
    {
        // TotalDays has a setter that recomputes day/season/year (WorldDate.cs, verified).
        WorldDate tomorrow = new(Game1.Date) { TotalDays = Game1.Date.TotalDays + 1 };
        string weather = Game1.weatherForTomorrow switch
        {
            "Rain" or "Storm" => "rainy",
            "Snow" => "snowy",
            "Wind" => "windy",
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
        _festivalAttended = true;
        if (Game1.CurrentEvent is not { } ev)
            return;
        foreach (NPC actor in ev.actors)
            if (actor is not null && !string.IsNullOrEmpty(actor.Name))
                _festivalActors.Add(actor.Name);
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
        foreach (IntentCandidate candidate in plan.Candidates)
        {
            _intentsToday.Add(candidate.Npc);
            Monitor.Log($"[shadow] {candidate.Npc} would say: \"{candidate.Line}\" ({candidate.Reason})", LogLevel.Info);
            AddFeed(Now(Math.Max(0, tick)), "Line", candidate.Npc, $"{candidate.Npc} would say: \"{candidate.Line}\" ({candidate.Reason})");
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
                Heartbeat.Format(tick, _memory.Diaries.Count, _ladder.LatestUrges, _backlogAtTickStart,
                    _ladder.Dropped, plan, laya.Calls, laya.CallFallbacks, median, p95,
                    planCollectedLines: _planCollectedLinesToday),
                LogLevel.Info);
        }
        else
        {
            Monitor.Log(
                Heartbeat.Format(tick, _memory.Diaries.Count, _ladder.LatestUrges, _backlogAtTickStart,
                    _ladder.Dropped, plan, planCollectedLines: _planCollectedLinesToday),
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
                : new MindsStats(_ladder.Backlog, _ladder.Dropped, -1, -1, -1, -1);
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
                stats, planState, _ladder.LatestJson, _lastLadderInputs, _intentsToday.ToList(), _planToday,
                _feed.Newest(), NewsFor, TemperamentViewOf);
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
        return new MindsStats(_ladder.Backlog, _ladder.Dropped, laya.Calls, laya.CallFallbacks, median, p95);
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
            _minds?.Dispose();
        base.Dispose(disposing);
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
        _memory = new MemoryStore(MemorySeed());
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
            _memory.RemoveDiary("null"); // junk from before the quest-target guard (week review, finding 2)
            return;
        }

        // Version 1 (steps 4-5): ticks had no year. Migrate relative to today.
        _memory = MemoryStore.FromVersion1(model, new GameTime(GameClock.SeasonIndex(Game1.currentSeason), Game1.dayOfMonth, 0, Game1.year));
        Monitor.Log("Migrated memory from the previous save format (added the year to old timestamps).", LogLevel.Info);
    }
}
