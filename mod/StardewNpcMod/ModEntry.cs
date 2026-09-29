using NpcDecision;
using NpcMemory;
using NpcSchedules;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewNpcMod;

/// <summary>
/// Step 4: the live scaffold. Hooks SaveLoaded / DayStarted / TimeChanged / Saving /
/// ReturnedToTitle. Persists the player's Diary, Ledger and RoutineBeliefs into the save, so
/// memory survives a save/reload instead of resetting each day. Each ten-minute tick it records
/// actual co-location — same location AND within <see cref="CoLocationRadius"/> tiles, never
/// same-region. A typed decision client (fake for now; real Jev/Laya are stubs marked "verify")
/// is wired but only shadow-probed. Shadow mode: records and logs, changes no game state.
/// </summary>
public class ModEntry : Mod
{
    private const string SaveKey = "squid.StardewNpcMod.memory";

    // VERIFY/tune: "seen" means the same location and within this many tiles (Chebyshev square).
    private const int CoLocationRadius = 8;

    // The player is the observer for now; other NPCs become observers later.
    private const string Observer = "Player";

    private RegionMap _regions = null!;
    private Ledger _ledger = new();
    private Diary _diary = new();
    private readonly Dictionary<string, RoutineBelief> _beliefs = new(StringComparer.OrdinalIgnoreCase);
    // Each NPC's own diary of what they witnessed (currently: "Saw Player" at a location). This is
    // the source the overnight-intent planner will reason over.
    private readonly Dictionary<string, Diary> _npcDiaries = new(StringComparer.OrdinalIgnoreCase);

    // Fake client in shadow mode; the real Laya/Jev clients are wired in when their APIs are known.
    private readonly IDecisionClient _decision = new ResilientDecisionClient(new FakeDecisionClient());

    public override void Entry(IModHelper helper)
    {
        _regions = RegionMap.Load(Path.Combine(Helper.DirectoryPath, "regions.json"));

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

        Monitor.Log($"Shadow scaffold ready: co-location radius {CoLocationRadius} tiles, decision client {_decision.GetType().Name}.", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        try
        {
            LoadMemory();
            Monitor.Log($"Memory loaded: {_diary.Entries.Count} diary entries, {_beliefs.Count} routine beliefs.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load memory: {ex}", LogLevel.Error);
        }
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        try
        {
            // Shadow probe only: prove the decision client is reachable in-game. No real feature
            // uses it yet (overnight intents / initiation ladder are out of scope for step 4).
            IReadOnlyList<double> weights = _decision.Choose(
                new[] { "tend the shop", "take a walk", "stay near the player" },
                "what would Abigail do this afternoon");
            double sociability = _decision.Score("how sociable is the player today", 1, 5);
            double lonely = _decision.YesNo("is Abigail lonely", "Abigail has not seen the player recently");
            Monitor.Log($"[shadow] would-ask: choice {string.Join("/", weights.Select(w => $"{w:0.00}"))}, sociability {sociability:0.0}/5, lonely {lonely:0.0}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shadow probe failed: {ex}", LogLevel.Error);
        }
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        // VERIFY: TimeChanged fires on the ten-minute tick (and may also fire at other clock jumps).
        try
        {
            ObserveNearby(e.NewTime);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Observation failed: {ex}", LogLevel.Error);
        }
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
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
        _ledger = new Ledger();
        _diary = new Diary();
        _beliefs.Clear();
        _npcDiaries.Clear();
        Monitor.Log("Memory reset for the title screen.", LogLevel.Info);
    }

    /// <summary>
    /// Record actual co-location each tick: every villager in the player's current location whose
    /// tile is within <see cref="CoLocationRadius"/> tiles (Chebyshev) is "seen". Same region is
    /// NOT enough — only aged, coarsened ledger entries may later feed a decision, never live
    /// positions.
    /// </summary>
    private void ObserveNearby(int newTime)
    {
        int tick = TimeUtils.TickIndex(newTime);
        if (tick < 0)
            return; // outside the 600..2600 live day

        Farmer player = Game1.player;
        GameLocation location = player.currentLocation;
        if (location is null)
            return;

        // VERIFY: absolute tick is year-1-scoped (GameClock models one year); real saves spanning
        // years will need a year counter before this is monotonic across a new-year boundary.
        int absTick = GameClock.AbsoluteTick(new GameTime(GameClock.SeasonIndex(Game1.currentSeason), Game1.dayOfMonth, tick));
        string locName = location.Name; // VERIFY: internal location name ("Town", "SeedShop", ...)
        string region = _regions.RegionFor(locName) ?? RegionMap.OtherRegion;
        int block = TimeUtils.BlockIndex(tick, _regions.BlockMinutes);

        foreach (NPC npc in location.characters)
        {
            if (!npc.IsVillager) // confirmed: property, not method (was isVillager()); drops monsters/animals
                continue;
            if (!Proximity.WithinRadius(player.TilePoint.X, player.TilePoint.Y, npc.TilePoint.X, npc.TilePoint.Y, CoLocationRadius))
                continue;

            string subject = npc.Name;
            _ledger.Record(Observer, subject, locName, region, absTick);
            _diary.Append(new DiaryEntry(absTick, subject, "Saw", locName));
            RoutineBelief belief = GetBelief(subject);
            belief.Observe(region, block, absTick);
            belief.NoteCoPresence(1);

            // The NPC also witnesses the player; their own diary is the overnight-intent source.
            NpcDiary(subject).Append(new DiaryEntry(absTick, "Player", "Saw", locName));
        }
    }

    private Diary NpcDiary(string npc)
    {
        if (!_npcDiaries.TryGetValue(npc, out Diary? diary))
        {
            diary = new Diary();
            _npcDiaries[npc] = diary;
        }
        return diary;
    }

    private RoutineBelief GetBelief(string subject)
    {
        if (!_beliefs.TryGetValue(subject, out RoutineBelief? belief))
        {
            belief = new RoutineBelief(Observer, subject, _regions.BlockMinutes);
            _beliefs[subject] = belief;
        }
        return belief;
    }

    private void SaveMemory()
    {
        var beliefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string subject, RoutineBelief belief) in _beliefs)
            beliefs[subject] = belief.ToJson();

        var npcDiaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string npc, Diary diary) in _npcDiaries)
            npcDiaries[npc] = diary.ToJson();

        Helper.Data.WriteSaveData(SaveKey, new Dictionary<string, string>
        {
            ["diary"] = _diary.ToJson(),
            ["ledger"] = _ledger.ToJson(),
            ["beliefs"] = System.Text.Json.JsonSerializer.Serialize(beliefs),
            ["npcDiaries"] = System.Text.Json.JsonSerializer.Serialize(npcDiaries),
        });
        Monitor.Log($"Memory saved ({_diary.Entries.Count} diary entries, {_beliefs.Count} beliefs, {_npcDiaries.Count} NPC diaries).", LogLevel.Info);
    }

    private void LoadMemory()
    {
        var model = Helper.Data.ReadSaveData<Dictionary<string, string>>(SaveKey);
        if (model is null)
            return;

        if (model.TryGetValue("diary", out string? diaryJson))
            _diary = Diary.FromJson(diaryJson);
        if (model.TryGetValue("ledger", out string? ledgerJson))
            _ledger = Ledger.FromJson(ledgerJson);
        if (model.TryGetValue("beliefs", out string? beliefsJson))
        {
            _beliefs.Clear();
            Dictionary<string, string>? stored =
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(beliefsJson);
            if (stored is not null)
                foreach ((string subject, string json) in stored)
                    _beliefs[subject] = RoutineBelief.FromJson(json);
        }
        if (model.TryGetValue("npcDiaries", out string? npcDiariesJson))
        {
            _npcDiaries.Clear();
            Dictionary<string, string>? stored =
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(npcDiariesJson);
            if (stored is not null)
                foreach ((string npc, string json) in stored)
                    _npcDiaries[npc] = Diary.FromJson(json);
        }
    }
}
