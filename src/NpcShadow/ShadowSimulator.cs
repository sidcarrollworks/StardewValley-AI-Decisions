using NpcMemory;
using NpcSchedules;

namespace NpcShadow;

/// <summary>
/// Shadow-mode harness: simulate a span of days of NPC activity from schedules, drive the
/// memory layer (NpcMemory: ledger, diary, routine beliefs), and log everything the mod WOULD
/// have recorded — without changing any game state. Deterministic given the seed.
///
/// Model: a single OBSERVER watches a set of SUBJECT NPCs. The observer is stationary at
/// <see cref="ObserverHomeRegion"/> unless its own schedule was registered via
/// <see cref="AddSubject"/>. Each ten-minute tick, a subject in the SAME LOCATION as the observer
/// (not merely the same region; schedule tiles are destinations, so distance is not modelled)
/// causes a first-hand ledger recording and a routine-belief observation; the diary gets one
/// entry at the start of each co-located span, as the live mod does.
/// Memory persists across the days of a run, so ledger decay (through "gone the next day") and
/// pair unlock (240 ticks of co-presence) both manifest. Ledger decay and unlock are logged.
/// </summary>
public sealed class ShadowSimulator
{
    public string Observer { get; }

    /// <summary>Where the observer stands when it has no schedule of its own.</summary>
    public string ObserverHomeRegion { get; set; } = "Town";
    public string ObserverHomeLocation { get; set; } = "Town";

    /// <summary>Gossip and initiation are later steps (they need the decision layer) and are
    /// intentionally not simulated here.</summary>
    public ShadowSimulator(RegionMap regions, string observer)
    {
        Observer = observer;
        _regions = regions;
    }

    private readonly RegionMap _regions;
    private readonly Dictionary<string, Dictionary<string, string>> _subjects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoutineBelief> _beliefs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The observer's last-seen ledger, populated by the most recent <see cref="Run"/>.</summary>
    public Ledger Ledger { get; private set; } = new();

    /// <summary>The observer's diary, populated by the most recent <see cref="Run"/>.</summary>
    public Diary Diary { get; private set; } = new();

    /// <summary>The observer's routine belief per subject, populated by <see cref="Run"/>.</summary>
    public IReadOnlyDictionary<string, RoutineBelief> Beliefs => _beliefs;

    /// <summary>Register a subject NPC's raw schedule (key -> script). Registering the observer
    /// itself gives it a real schedule instead of the stationary home.</summary>
    public void AddSubject(string npc, Dictionary<string, string> schedules)
        => _subjects[npc] = schedules;

    /// <summary>
    /// Simulate <paramref name="dayCount"/> consecutive days starting at
    /// <paramref name="season"/>/<paramref name="startDay"/> (wrapping seasons), and return the
    /// shadow log. Memory is reset at the start of a run and persists across its days.
    /// </summary>
    public ShadowLog Run(string season, int startDay, int dayCount, ExtractorOptions options, int seed)
    {
        var planner = new DayPlanner(_regions);
        Ledger = new Ledger();
        Diary = new Diary();
        _beliefs.Clear();

        // Subject names in a stable order, so the run (and the log) is deterministic.
        var subjectNames = _subjects.Keys
            .Where(n => !string.Equals(n, Observer, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (string name in subjectNames)
            _beliefs[name] = new RoutineBelief(Observer, name, _regions.BlockMinutes);

        var log = new ShadowLog();
        log.Add(0, Observer, "Start",
            $"simulating {dayCount} day(s) from {season} day {startDay} (seed {seed}); observer {Observer}");

        // These persist across days: ledger decay and pair unlock are multi-day phenomena.
        var prevDetail = new Dictionary<string, LedgerDetail>(StringComparer.OrdinalIgnoreCase);
        var prevUnlocked = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        int blockMinutes = _regions.BlockMinutes;

        for (int i = 0; i < dayCount; i++)
        {
            (string curSeason, int curDay) = AddDays(season, startDay, i);
            int daySeed = seed + i;

            DayPlan observerPlan = ResolveObserver(planner, curSeason, curDay, options, daySeed);
            var plans = subjectNames
                .Select(n => planner.Resolve(n, _subjects[n], curSeason, curDay, options, daySeed))
                .ToList();
            var planByName = plans.ToDictionary(p => p.Npc, p => p, StringComparer.OrdinalIgnoreCase);

            log.Add(0, Observer, "DayStart", $"{curSeason} day {curDay}", day: i + 1);

            // Co-location spans do NOT cross the day boundary (positions reset overnight).
            var prevCoLocated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int tick = 0; tick < TimeUtils.TicksPerDay; tick++)
            {
                int absTick = GameClock.AbsoluteTick(new GameTime(GameClock.SeasonIndex(curSeason), curDay, tick));
                string observerLocation = observerPlan.LocationByTick[tick];
                var nowCoLocated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DayPlan plan in plans)
                {
                    if (!string.Equals(plan.LocationByTick[tick], observerLocation, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // first-hand sighting: ledger and routine belief every tick, diary once per span
                    nowCoLocated.Add(plan.Npc);
                    Ledger.Record(Observer, plan.Npc, plan.LocationByTick[tick], plan.RegionByTick[tick], absTick,
                        plan.SpotByTick.Length > tick ? plan.SpotByTick[tick] : null);
                    if (!prevCoLocated.Contains(plan.Npc))
                        Diary.Append(new DiaryEntry(absTick, plan.Npc, "Saw", plan.LocationByTick[tick]));

                    RoutineBelief belief = _beliefs[plan.Npc];
                    belief.Observe(plan.RegionByTick[tick], TimeUtils.BlockIndex(tick, blockMinutes), absTick);
                    belief.NoteCoPresence(1);
                }

                // one "Saw" line at the START of each contiguous co-located span, not one per tick
                foreach (string subject in nowCoLocated.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    if (prevCoLocated.Contains(subject))
                        continue;
                    DayPlan plan = planByName[subject];
                    log.Add(tick, Observer, "Saw",
                        $"{subject} at {plan.LocationByTick[tick]} ({plan.RegionByTick[tick]})", day: i + 1);
                }
                prevCoLocated = nowCoLocated;

                // decay lines: only log when the view actually COARSENS (LedgerDetail enum
                // values increase with coarseness). A re-sighting refreshes the entry back to
                // NamedSpot; that is already captured by the "Saw" line, not a "Decayed" line.
                foreach (DayPlan plan in plans)
                {
                    LedgerView? view = Ledger.View(Observer, plan.Npc, absTick);
                    if (view == null)
                        continue;
                    prevDetail.TryGetValue(plan.Npc, out LedgerDetail previous);
                    if ((int)view.Detail > (int)previous)
                        log.Add(tick, Observer, "Decayed",
                            $"memory of {plan.Npc} decayed to {view.Detail}", day: i + 1);
                    prevDetail[plan.Npc] = view.Detail;
                }

                // unlock lines: enough accumulated co-presence to know the subject's routine
                foreach (DayPlan plan in plans)
                {
                    RoutineBelief belief = _beliefs[plan.Npc];
                    prevUnlocked.TryGetValue(plan.Npc, out bool wasUnlocked);
                    if (belief.Unlocked && !wasUnlocked)
                        log.Add(tick, Observer, "Unlocked",
                            $"unlocked routine knowledge of {plan.Npc}", day: i + 1);
                    prevUnlocked[plan.Npc] = belief.Unlocked;
                }
            }
        }

        return log;
    }

    private DayPlan ResolveObserver(DayPlanner planner, string season, int day, ExtractorOptions options, int seed)
    {
        if (_subjects.TryGetValue(Observer, out Dictionary<string, string>? schedules))
            return planner.Resolve(Observer, schedules, season, day, options, seed);

        var regionByTick = new string[TimeUtils.TicksPerDay];
        var locationByTick = new string[TimeUtils.TicksPerDay];
        Array.Fill(regionByTick, ObserverHomeRegion);
        Array.Fill(locationByTick, ObserverHomeLocation);
        return new DayPlan
        {
            Npc = Observer,
            HomeRegion = ObserverHomeRegion,
            HomeLocation = ObserverHomeLocation,
            KeyChain = "<stationary>",
            RegionByTick = regionByTick,
            LocationByTick = locationByTick,
        };
    }

    private static (string Season, int Day) AddDays(string season, int day, int offset)
    {
        int seasonIdx = GameClock.SeasonIndex(season);
        int absDay = seasonIdx * GameClock.DaysPerSeason + (day - 1) + offset;
        int newSeason = (absDay / GameClock.DaysPerSeason) % GameClock.SeasonsPerYear;
        int newDay = absDay % GameClock.DaysPerSeason + 1;
        return (GameClock.SeasonName(newSeason), newDay);
    }
}
