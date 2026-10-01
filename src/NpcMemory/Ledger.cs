using System.Text.Json;
using System.Text.Json.Nodes;

namespace NpcMemory;

/// <summary>
/// The "last seen" ledger from the project brief. One entry per subject per observer
/// (including the player), recorded on the ten-minute tick when co-located. Detail degrades
/// with age within the day: NamedSpot -> Location -> Region -> EarlierToday, and anything from
/// an earlier calendar day is Gone (from the next 6:00, not after a fixed number of ticks).
/// Gossip passes on the already-coarsened view and never adds detail; two-hop cap on asking;
/// gossip never replaces fresher knowledge the listener already has.
/// </summary>
public sealed class Ledger
{
    // Same-day degradation thresholds, in ticks (1 tick = 10 game minutes). Public and tunable,
    // and deliberately NOT saved with the ledger, so retuning them applies to existing saves.
    public int SpotTtl { get; set; } = 12;     // NamedSpot while age < 12 ticks (2 hours)
    public int LocationTtl { get; set; } = 48; // Location while age < 48 ticks (8 hours)
    public int RegionTtl { get; set; } = 96;   // Region while age < 96 ticks (16 hours); EarlierToday after, until the day ends

    /// <summary>Knowledge may be re-told twice: first-hand (0) -> told (1) -> told-by-told (2).</summary>
    private const int MaxHopCount = 2;

    /// <summary>
    /// One (observer, subject) memory: where the subject was when it was recorded, how long ago
    /// that was, and whether the observer saw it or was told about it.
    /// </summary>
    private sealed class Entry
    {
        public string Location = "";
        public string Region = "";

        /// <summary>The spot within the location (a tile like "43,57" or a named area), or null
        /// when none was recorded. Without a spot the finest detail is Location.</summary>
        public string? Spot;

        public int AbsoluteTick;
        public int HopCount;

        /// <summary>Who passed this on (the speaker of the gossip), or null for a first-hand sighting.</summary>
        public string? ToldBy;

        /// <summary>
        /// Coarsest detail this entry may ever be viewed at. Set when knowledge arrives by gossip:
        /// a told fact can never be recalled in more detail than the teller had at the time.
        /// Null for first-hand entries, which decay purely by age and calendar day.
        /// </summary>
        public LedgerDetail? DetailCap;
    }

    // observer -> subject -> entry. Both levels compare names case-insensitively.
    private readonly Dictionary<string, Dictionary<string, Entry>> _byObserver =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Record a first-hand sighting of `subject` by `observer` at the given place and tick.
    /// `spot` is where in the location (tile or named area); it is what makes a view a NamedSpot.
    /// Replaces any earlier entry for the same (observer, subject) pair.</summary>
    public void Record(string observer, string subject, string location, string region, int absoluteTick, string? spot = null)
        => Store(observer, subject, new Entry
        {
            Location = location,
            Region = region,
            Spot = string.IsNullOrWhiteSpace(spot) ? null : spot,
            AbsoluteTick = absoluteTick,
            HopCount = 0,
        });

    /// <summary>The coarsened view of `subject` as `observer` knows it now, or null if never seen.
    /// `Place` carries the location name (NamedSpot/Location) or region name (Region) and is null
    /// for EarlierToday/Gone; `Spot` is set only at NamedSpot.</summary>
    public LedgerView? View(string observer, string subject, int nowTick)
        => TryGetEntry(observer, subject, out var entry) ? BuildView(observer, subject, entry, nowTick) : null;

    /// <summary>Every subject this observer has an entry for, in name order.</summary>
    public IReadOnlyList<string> SubjectsOf(string observer)
        => _byObserver.TryGetValue(observer, out Dictionary<string, Entry>? subjects)
            ? subjects.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();

    /// <summary>How many last-seen entries the observer currently holds (every subject counts).
    /// Read-only, for the playtest log's memory census.</summary>
    public int EntryCountFor(string observer)
        => _byObserver.TryGetValue(observer, out Dictionary<string, Entry>? subjects)
            ? subjects.Count
            : 0;

    /// <summary>Tell `listener` where `subject` is, from `speaker`'s already-coarsened knowledge.
    /// The listener stores the view at hop+1, never with more detail than the speaker had, and
    /// refuses beyond two hops. Also refused: talking to yourself, passing on a forgotten (Gone)
    /// view, and anything not strictly fresher than what the listener already knows (an older
    /// sighting, or the same sighting at no fewer hops). Returns false when refused (nothing
    /// stored).</summary>
    public bool Gossip(string speaker, string listener, string subject, int nowTick)
    {
        // (a) nobody tells themselves anything (that would demote first-hand knowledge to hearsay).
        if (string.Equals(speaker, listener, StringComparison.OrdinalIgnoreCase)) return false;

        // (b) nothing to pass on: a speaker who never saw the subject says nothing.
        if (!TryGetEntry(speaker, subject, out var source)) return false;

        var speakerView = BuildView(speaker, subject, source, nowTick);

        // (c) a forgotten fact is not passed on.
        if (speakerView.Detail == LedgerDetail.Gone) return false;

        // (d) two-hop cap on asking.
        int hopCount = speakerView.HopCount + 1;
        if (hopCount > MaxHopCount) return false;

        // (e) never overwrite fresher knowledge: a later sighting wins; for the same sighting,
        // fewer hops win.
        if (TryGetEntry(listener, subject, out var existing))
        {
            if (existing.AbsoluteTick > source.AbsoluteTick) return false;
            if (existing.AbsoluteTick == source.AbsoluteTick && existing.HopCount <= hopCount) return false;
        }

        // (f) the listener can never learn more than the speaker knows right now. Age is still
        // measured from the source sighting (gossip never re-freshens a fact).
        var natural = DetailAt(source.AbsoluteTick, nowTick, source.Spot is not null);
        var cap = (LedgerDetail)Math.Max((int)natural, (int)speakerView.Detail);

        Store(listener, subject, new Entry
        {
            Location = source.Location,
            Region = source.Region,
            Spot = cap == LedgerDetail.NamedSpot ? source.Spot : null, // never hand over detail the cap hides
            AbsoluteTick = source.AbsoluteTick,
            HopCount = hopCount,
            ToldBy = speaker,
            DetailCap = cap,
        });
        return true;
    }

    /// <summary>Rewrite every entry's tick (save migration, e.g. adding the year to old ticks).</summary>
    public void RemapTicks(Func<int, int> remap)
    {
        foreach (var bySubject in _byObserver.Values)
            foreach (var entry in bySubject.Values)
                entry.AbsoluteTick = remap(entry.AbsoluteTick);
    }

    public string ToJson()
    {
        var entries = new JsonArray();
        foreach (string observer in SortedObservers())
        {
            foreach (string subject in SortedSubjects(observer))
            {
                var entry = _byObserver[observer][subject];
                entries.Add(new JsonObject
                {
                    ["observer"] = observer,
                    ["subject"] = subject,
                    ["location"] = entry.Location,
                    ["region"] = entry.Region,
                    ["spot"] = entry.Spot,
                    ["absoluteTick"] = entry.AbsoluteTick,
                    ["hopCount"] = entry.HopCount,
                    ["toldBy"] = entry.ToldBy,
                    ["detailCap"] = entry.DetailCap.HasValue ? JsonValue.Create((int)entry.DetailCap.Value) : null,
                });
            }
        }

        // Thresholds are code defaults, not save data (older saves may still carry them; they are ignored).
        return new JsonObject { ["entries"] = entries }.ToJsonString();
    }

    public static Ledger FromJson(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var ledger = new Ledger();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return ledger;

        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in entries.EnumerateArray())
            {
                string spot = Text(item, "spot");
                string toldBy = Text(item, "toldBy");
                var entry = new Entry
                {
                    Location = Text(item, "location"),
                    Region = Text(item, "region"),
                    Spot = spot.Length == 0 ? null : spot,
                    AbsoluteTick = item.TryGetProperty("absoluteTick", out var tick) ? tick.GetInt32() : 0,
                    HopCount = item.TryGetProperty("hopCount", out var hop) ? hop.GetInt32() : 0,
                    ToldBy = toldBy.Length == 0 ? null : toldBy,
                    DetailCap = item.TryGetProperty("detailCap", out var cap) && cap.ValueKind == JsonValueKind.Number
                        ? (LedgerDetail)cap.GetInt32()
                        : null,
                };
                ledger.Store(Text(item, "observer"), Text(item, "subject"), entry);
            }
        }

        return ledger;
    }

    // ---- internals -------------------------------------------------------------------------

    private void Store(string observer, string subject, Entry entry)
    {
        if (!_byObserver.TryGetValue(observer, out var bySubject))
        {
            bySubject = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            _byObserver.Add(observer, bySubject);
        }
        bySubject[subject] = entry;
    }

    private bool TryGetEntry(string observer, string subject, out Entry entry)
    {
        if (_byObserver.TryGetValue(observer, out var bySubject) && bySubject.TryGetValue(subject, out var found))
        {
            entry = found;
            return true;
        }
        entry = null!;
        return false;
    }

    private LedgerView BuildView(string observer, string subject, Entry entry, int nowTick)
    {
        int age = GameClock.AgeTicks(entry.AbsoluteTick, nowTick);
        var detail = DetailAt(entry.AbsoluteTick, nowTick, entry.Spot is not null);
        if (entry.DetailCap is { } cap && (int)cap > (int)detail) detail = cap;
        return new LedgerView(observer, subject, detail, PlaceFor(detail, entry), age, entry.HopCount, entry.AbsoluteTick,
            detail == LedgerDetail.NamedSpot ? entry.Spot : null, entry.ToldBy);
    }

    /// <summary>Detail implied by time alone (LedgerDetail is ordered finest -> coarsest). A sighting
    /// from an earlier calendar day is Gone from the next 6:00; within the day it coarsens by age.
    /// Without a recorded spot the finest possible detail is Location.</summary>
    private LedgerDetail DetailAt(int sightingTick, int nowTick, bool hasSpot)
    {
        if (GameClock.DaysBetween(sightingTick, nowTick) > 0)
            return LedgerDetail.Gone;

        int age = GameClock.AgeTicks(sightingTick, nowTick);
        var detail = age < SpotTtl ? LedgerDetail.NamedSpot
                   : age < LocationTtl ? LedgerDetail.Location
                   : age < RegionTtl ? LedgerDetail.Region
                   : LedgerDetail.EarlierToday;
        return detail == LedgerDetail.NamedSpot && !hasSpot ? LedgerDetail.Location : detail;
    }

    private static string? PlaceFor(LedgerDetail detail, Entry entry) => detail switch
    {
        LedgerDetail.NamedSpot => entry.Location,
        LedgerDetail.Location => entry.Location,
        LedgerDetail.Region => entry.Region,
        _ => null,
    };

    private IEnumerable<string> SortedObservers()
        => _byObserver.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

    private IEnumerable<string> SortedSubjects(string observer)
        => _byObserver[observer].Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

    private static string Text(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
