using System.Text.Json;
using NpcSchedules;

namespace NpcMemory;

/// <summary>Where one character is on a tick, as the perception layer reads it from the game.
/// Only <see cref="MemoryStore.Observe"/> sees these; they are turned into aged ledger entries and
/// never reach a decision directly.</summary>
public sealed record Presence(string Name, string Location, int X, int Y, bool IsPlayer = false);

/// <summary>
/// All NPC memory for one save, from the NPCs' side: each NPC's ledger of where it last saw the
/// player and other NPCs, its diary of what it witnessed, and its routine belief about each
/// subject. Game-independent so the mod's per-tick logic is testable; the mod only builds the
/// <see cref="Presence"/> list and persists <see cref="ToJson"/>.
/// </summary>
public sealed class MemoryStore
{
    /// <summary>The ledger/diary name of the player.</summary>
    public const string PlayerName = "Player";

    /// <summary>Save-format version. 1 = step-4/5 saves (ticks without a year, player-side memory).</summary>
    public const int CurrentVersion = 2;

    // VERIFY/tune: "seen" means the same location and within this many tiles (Chebyshev square).
    public int CoLocationRadius { get; set; } = Proximity.DefaultRadius;

    /// <summary>Oldest entries are dropped past this many per NPC diary.</summary>
    public int MaxDiaryEntries { get; set; } = 500;

    public Ledger Ledger { get; private set; } = new();

    private readonly Dictionary<string, Diary> _diaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoutineBelief> _beliefs = new(StringComparer.OrdinalIgnoreCase);

    // Pairs ("observer>subject") co-located on the previous observed tick, for span detection.
    private HashSet<string> _prevCoLocated = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastObservedRegion = new(StringComparer.OrdinalIgnoreCase);
    private int _prevTick = int.MinValue;

    // Ticks each NPC was co-located with the PLAYER on the current calendar day, for PassedBy.
    // In memory only (like the span tracker): a reload mid-day restarts the count.
    private readonly Dictionary<string, int> _todayPlayerTicks = new(StringComparer.OrdinalIgnoreCase);
    private int _todayDayIndex = int.MinValue;

    public IReadOnlyDictionary<string, Diary> Diaries => _diaries;

    /// <summary>Beliefs keyed "observer>subject".</summary>
    public IReadOnlyDictionary<string, RoutineBelief> Beliefs => _beliefs;

    /// <summary>An NPC's diary, created on first use.</summary>
    public Diary DiaryOf(string npc)
    {
        if (!_diaries.TryGetValue(npc, out Diary? diary))
        {
            diary = new Diary();
            _diaries[npc] = diary;
        }
        return diary;
    }

    /// <summary>The one diary writer: append and trim to <see cref="MaxDiaryEntries"/>.</summary>
    public void Note(string npc, DiaryEntry entry)
    {
        Diary diary = DiaryOf(npc);
        diary.Append(entry);
        diary.TrimTo(MaxDiaryEntries);
    }

    /// <summary>Drops an NPC's whole diary (load-time cleanup of junk data, e.g. a "null" quest
    /// target recorded before the guard existed).</summary>
    public void RemoveDiary(string npc)
        => _diaries.Remove(npc);

    /// <summary>The region this NPC was in on its last observed tick (span-tracker memory, not a
    /// live position; null until it has been observed). <see cref="LookFor"/> uses it to reject
    /// habit leads to where the seeker already is.</summary>
    public string? LastObservedRegion(string npc)
        => _lastObservedRegion.TryGetValue(npc, out string? region) ? region : null;

    /// <summary>
    /// The NPCs that were co-located with the player at the most recent <see cref="Observe"/> —
    /// the span tracker's memory, never a live position (rule 2). The gift postfix uses this to
    /// pick SawGift witnesses. Name order is the tracker's internal order, so callers that need
    /// determinism must sort.
    /// </summary>
    public IReadOnlyCollection<string> CoLocatedWithPlayerNow()
    {
        const string suffix = ">" + PlayerName;
        var result = new List<string>();
        foreach (string key in _prevCoLocated)
            if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                result.Add(key[..^suffix.Length]);
        return result;
    }

    public RoutineBelief? BeliefOf(string observer, string subject)
        => _beliefs.TryGetValue(Key(observer, subject), out RoutineBelief? belief) ? belief : null;

    /// <summary>How fast an NPC learns the player's routine: hearts raise the learning strength
    /// (brief: "hearts set how fast an NPC learns the player's routine"). NPC subjects learn at 1.</summary>
    public static double PlayerLearningStrength(int hearts) => 1.0 + 0.25 * Math.Clamp(hearts, 0, 14);

    /// <summary>
    /// Record one ten-minute tick. Every NPC observes every other character (the player and other
    /// NPCs) in the same location within <see cref="CoLocationRadius"/> tiles: a ledger entry with the
    /// subject's tile as the spot, a routine-belief observation, and a diary line at the start of each
    /// co-located span (not every tick). The player is never an observer here.
    /// </summary>
    public void Observe(int absoluteTick, IReadOnlyList<Presence> presences, RegionMap regions, Func<string, int>? heartsFor = null)
    {
        int tickOfDay = absoluteTick % GameClock.TicksPerDay;
        int block = TimeUtils.BlockIndex(tickOfDay, regions.BlockMinutes);
        int dayIndex = GameClock.DayIndex(absoluteTick);
        if (dayIndex != _todayDayIndex)
        {
            _todayDayIndex = dayIndex;
            _todayPlayerTicks.Clear();
        }
        // A span continues only from the previous tick of the same day: 1:50 AM and the next 6:00 AM
        // are adjacent ticks, but the night in between breaks every span.
        bool continuesPrevious = absoluteTick == _prevTick + 1
            && GameClock.DayIndex(absoluteTick) == GameClock.DayIndex(_prevTick);
        var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Stable order so diaries and the JSON are deterministic regardless of how the game lists characters.
        var byLocation = presences
            .Where(p => !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.Location))
            .GroupBy(p => p.Location, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byLocation)
        {
            var here = group.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            string region = regions.RegionFor(group.Key) ?? RegionMap.OtherRegion;

            foreach (Presence observer in here)
            {
                if (observer.IsPlayer)
                    continue;

                // Span-tracker memory: where the observer itself was on the last observed tick.
                // LookFor uses it to reject habit leads to the seeker's own region (rule 2 holds:
                // nothing here reads a live position; it is aged by at least one tick).
                _lastObservedRegion[observer.Name] = region;

                foreach (Presence subject in here)
                {
                    if (ReferenceEquals(subject, observer) || string.Equals(subject.Name, observer.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!Proximity.WithinRadius(observer.X, observer.Y, subject.X, subject.Y, CoLocationRadius))
                        continue;

                    string subjectName = subject.IsPlayer ? PlayerName : subject.Name;
                    string key = Key(observer.Name, subjectName);
                    now.Add(key);

                    Ledger.Record(observer.Name, subjectName, subject.Location, region, absoluteTick, $"{subject.X},{subject.Y}");

                    RoutineBelief belief = Belief(observer.Name, subjectName, regions.BlockMinutes);
                    double strength = subject.IsPlayer ? PlayerLearningStrength(heartsFor?.Invoke(observer.Name) ?? 0) : 1.0;
                    belief.Observe(region, block, absoluteTick, strength);
                    belief.NoteCoPresence(1);

                    if (!(continuesPrevious && _prevCoLocated.Contains(key)))
                    {
                        Note(observer.Name, new DiaryEntry(absoluteTick, subjectName, "Saw", subject.Location));
                    }

                    if (subject.IsPlayer)
                    {
                        int ticks = _todayPlayerTicks.TryGetValue(observer.Name, out int t) ? t : 0;
                        _todayPlayerTicks[observer.Name] = ticks + 1;
                    }
                }
            }
        }

        _prevCoLocated = now;
        _prevTick = absoluteTick;
    }

    /// <summary>
    /// The seeker asks the NPCs it can see right now (its own first-hand view of them is this very
    /// tick) whether they know where <paramref name="subject"/> is. Each answer goes through
    /// <see cref="Ledger.Gossip"/>, so it is capped at the teller's detail, limited to two hops, and
    /// only kept when it is fresher than what the seeker already knows. The player is never asked
    /// (not a gossip partner) and neither is the subject. Returns who was asked and whose answer
    /// was kept, both in name order.
    /// </summary>
    public AskResult AskAround(string seeker, string subject, int nowTick)
    {
        var asked = new List<string>();
        var told = new List<string>();
        foreach (string neighbour in Ledger.SubjectsOf(seeker))
        {
            if (string.Equals(neighbour, subject, StringComparison.OrdinalIgnoreCase)
                || string.Equals(neighbour, PlayerName, StringComparison.OrdinalIgnoreCase))
                continue;
            LedgerView? view = Ledger.View(seeker, neighbour, nowTick);
            if (view is not { HopCount: 0, AgeTicks: 0, Detail: LedgerDetail.NamedSpot })
                continue; // not with the seeker right now
            asked.Add(neighbour);
            if (Ledger.Gossip(neighbour, seeker, subject, nowTick))
                told.Add(neighbour);
        }
        return new AskResult(asked, told);
    }

    /// <summary>
    /// The seeker's best answer to "where is <paramref name="subject"/> now?" from its own memory:
    /// its own sighting or a tip from today (the ledger), else where it usually sees the subject at
    /// this hour (its routine belief, when learned well enough), else Unknown. A sighting that has
    /// faded to "earlier today" has no place, so the habit is preferred when there is one.
    /// </summary>
    public Whereabouts LookFor(string seeker, string subject, int nowTick, int blockMinutes, WhereaboutsOptions? options = null)
    {
        options ??= new WhereaboutsOptions();
        LedgerView? view = Ledger.View(seeker, subject, nowTick);

        Whereabouts? sighting = null;
        if (view is not null && view.Detail != LedgerDetail.Gone)
        {
            WhereaboutsSource source = view.HopCount > 0 ? WhereaboutsSource.Told
                : view is { AgeTicks: 0, Detail: LedgerDetail.NamedSpot } ? WhereaboutsSource.SeenNow
                : WhereaboutsSource.SeenToday;
            string? place = view.Place == RegionMap.OtherRegion ? null : view.Place; // an unmapped region is nowhere to go
            sighting = new Whereabouts(seeker, subject, source, place, view.Detail, view.AgeTicks, view.HopCount, view.ToldBy, 0);
            if (sighting.HasPlace)
                return sighting;
        }

        int block = TimeUtils.BlockIndex(nowTick % GameClock.TicksPerDay, blockMinutes);
        if (BeliefOf(seeker, subject)?.BestGuessAt(block) is { } guess
            && guess.Evidence >= options.MinHabitEvidence
            && guess.Share >= options.MinHabitShare
            && guess.Region != RegionMap.OtherRegion
            && !string.Equals(guess.Region, LastObservedRegion(seeker), StringComparison.OrdinalIgnoreCase))
            return new Whereabouts(seeker, subject, WhereaboutsSource.Habit, guess.Region, LedgerDetail.Region, 0, 0, null, guess.Share, guess.Evidence);

        return sighting ?? new Whereabouts(seeker, subject, WhereaboutsSource.Unknown, null, null, 0, 0, null, 0);
    }

    public string ToJson()
    {
        var model = new SaveModel
        {
            Version = CurrentVersion,
            Ledger = Ledger.ToJson(),
            Diaries = _diaries.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(kv => kv.Key, kv => kv.Value.ToJson()),
            Beliefs = _beliefs.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(kv => kv.Key, kv => kv.Value.ToJson()),
        };
        return JsonSerializer.Serialize(model);
    }

    public static MemoryStore FromJson(string json)
    {
        SaveModel model = JsonSerializer.Deserialize<SaveModel>(json) ?? new SaveModel();
        var store = new MemoryStore();
        if (!string.IsNullOrEmpty(model.Ledger))
            store.Ledger = Ledger.FromJson(model.Ledger);
        foreach ((string npc, string diaryJson) in model.Diaries ?? new())
            store._diaries[npc] = Diary.FromJson(diaryJson);
        foreach ((string key, string beliefJson) in model.Beliefs ?? new())
            store._beliefs[key] = RoutineBelief.FromJson(beliefJson);
        return store;
    }

    /// <summary>
    /// Load a version-1 save (step 4/5): ledger, per-NPC diaries and the player's routine beliefs,
    /// all with year-less ticks. Each old tick is placed in the current year if it is not later
    /// than now, else in the previous year (a year-1-scoped tick larger than "now" can only be
    /// from last year). The player's own diary is dropped: nothing reads it.
    /// </summary>
    public static MemoryStore FromVersion1(IReadOnlyDictionary<string, string> model, GameTime now)
    {
        var store = new MemoryStore();
        Func<int, int> remap = old => MigrateYearlessTick(old, now);

        if (model.TryGetValue("ledger", out string? ledgerJson))
        {
            store.Ledger = Ledger.FromJson(ledgerJson);
            store.Ledger.RemapTicks(remap);
        }
        if (model.TryGetValue("npcDiaries", out string? diariesJson)
            && JsonSerializer.Deserialize<Dictionary<string, string>>(diariesJson) is { } diaries)
        {
            foreach ((string npc, string json) in diaries)
            {
                Diary diary = Diary.FromJson(json);
                diary.RemapTicks(remap);
                store._diaries[npc] = diary;
            }
        }
        if (model.TryGetValue("beliefs", out string? beliefsJson)
            && JsonSerializer.Deserialize<Dictionary<string, string>>(beliefsJson) is { } beliefs)
        {
            foreach ((string subject, string json) in beliefs)
                store._beliefs[Key(PlayerName, subject)] = RoutineBelief.FromJson(json);
        }
        return store;
    }

    /// <summary>
    /// Day-end diary notes (docs/spec/diary.md), written through <see cref="Note"/> in NPC name
    /// order and returned for the caller to log. PassedBy: the player spent the day near an NPC
    /// (PassedByMinTicks co-located ticks, PassedByHearts+ hearts), talked to at least one OTHER
    /// NPC, and never to this one. BirthdayForgotten: the NPC's birthday, BirthdayHearts+ hearts,
    /// and no gift from the player today. Deterministic.
    /// </summary>
    public IReadOnlyList<(string Npc, DiaryEntry Entry)> DayEndNotes(
        IEnumerable<string> npcs,
        int absoluteTick,
        Func<string, int> heartsFor,
        Func<string, bool> birthdayFor,
        Func<string, bool> giftedToday,
        IReadOnlyCollection<string> talkedToday,
        DiaryOptions? options = null)
    {
        options ??= new DiaryOptions();
        var talked = new HashSet<string>(talkedToday ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var written = new List<(string, DiaryEntry)>();

        foreach (string npc in _todayPlayerTicks.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            int ticks = _todayPlayerTicks[npc];
            int hearts = heartsFor(npc);
            bool talkedToThisOne = talked.Contains(npc);
            bool talkedToAnother = talked.Any(other => !string.Equals(other, npc, StringComparison.OrdinalIgnoreCase));

            if (ticks >= options.PassedByMinTicks
                && hearts >= options.PassedByHearts
                && !talkedToThisOne
                && talkedToAnother)
            {
                DiaryEntry entry = new(absoluteTick, PlayerName, "PassedBy",
                    DiaryDetail.Format(("ticks", ticks.ToString())));
                Note(npc, entry);
                written.Add((npc, entry));
            }
        }

        foreach (string npc in (npcs ?? Array.Empty<string>()).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (birthdayFor(npc)
                && heartsFor(npc) >= options.BirthdayHearts
                && !giftedToday(npc))
            {
                DiaryEntry entry = new(absoluteTick, PlayerName, "BirthdayForgotten",
                    DiaryDetail.Format(("hearts", heartsFor(npc).ToString())));
                Note(npc, entry);
                written.Add((npc, entry));
            }
        }

        return written;
    }

    /// <summary>Map a year-less (year-1-scoped) tick to an absolute tick, given the current time.</summary>
    public static int MigrateYearlessTick(int oldTick, GameTime now)
    {
        int withinYear = Math.Clamp(oldTick, 0, GameClock.TicksPerYear - 1);
        int nowInYear = GameClock.AbsoluteTick(now with { Year = 1 });
        int year = Math.Max(1, now.Year);
        if (withinYear > nowInYear && year > 1)
            year -= 1;
        return GameClock.AbsoluteTick(GameClock.FromAbsoluteTick(withinYear) with { Year = year });
    }

    private RoutineBelief Belief(string observer, string subject, int blockMinutes)
    {
        string key = Key(observer, subject);
        if (!_beliefs.TryGetValue(key, out RoutineBelief? belief))
        {
            belief = new RoutineBelief(observer, subject, blockMinutes);
            _beliefs[key] = belief;
        }
        return belief;
    }

    private static string Key(string observer, string subject) => $"{observer}>{subject}";

    private sealed class SaveModel
    {
        public int Version { get; set; }
        public string? Ledger { get; set; }
        public Dictionary<string, string>? Diaries { get; set; }
        public Dictionary<string, string>? Beliefs { get; set; }
    }
}
