using System.Text.Json;
using System.Text.Json.Nodes;

namespace NpcMemory;

/// <summary>
/// The "last seen" ledger from the project brief. One entry per subject per observer
/// (including the player), recorded on the ten-minute tick when co-located. Detail degrades
/// with age: NamedSpot -> Location -> Region -> EarlierToday -> Gone (gone the next day).
/// Gossip passes on the already-coarsened view and never adds detail; two-hop cap on asking.
/// </summary>
public sealed class Ledger
{
    // Degradation thresholds, in ticks (1 tick = 10 game minutes). All public and tunable.
    public int SpotTtl { get; set; } = 120;     // NamedSpot while age < 120 (2 hours)
    public int LocationTtl { get; set; } = 720; // Location while age < 720 (12 hours)
    public int RegionTtl { get; set; } = 1200;  // Region while age < 1200 (20 hours)
    public int GoneTtl { get; set; } = 1440;    // EarlierToday for [1200,1440); Gone at 1440+ (a day)

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
        public int AbsoluteTick;
        public int HopCount;

        /// <summary>
        /// Coarsest detail this entry may ever be viewed at. Set when knowledge arrives by gossip:
        /// a told fact can never be recalled in more detail than the teller had at the time.
        /// Null for first-hand entries, which decay purely by age.
        /// </summary>
        public LedgerDetail? DetailCap;
    }

    // observer -> subject -> entry. Both levels compare names case-insensitively.
    private readonly Dictionary<string, Dictionary<string, Entry>> _byObserver =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Record a first-hand sighting of `subject` by `observer` at the given place and tick.
    /// Replaces any earlier entry for the same (observer, subject) pair.</summary>
    public void Record(string observer, string subject, string location, string region, int absoluteTick)
        => Store(observer, subject, new Entry
        {
            Location = location,
            Region = region,
            AbsoluteTick = absoluteTick,
            HopCount = 0,
        });

    /// <summary>The coarsened view of `subject` as `observer` knows it now, or null if never seen.
    /// The `Place` field carries the location name (NamedSpot/Location) or region name (Region),
    /// and is null for EarlierToday/Gone.</summary>
    public LedgerView? View(string observer, string subject, int nowTick)
        => TryGetEntry(observer, subject, out var entry) ? BuildView(observer, subject, entry, nowTick) : null;

    /// <summary>Tell `listener` where `subject` is, from `speaker`'s already-coarsened knowledge.
    /// The listener stores the view at hop+1, never with more detail than the speaker had, and
    /// refuses beyond two hops. Returns false when the gossip is refused (nothing stored).</summary>
    public bool Gossip(string speaker, string listener, string subject, int nowTick)
    {
        // (a) nothing to pass on: a speaker who never saw the subject says nothing.
        if (!TryGetEntry(speaker, subject, out var source)) return false;

        int age = GameClock.AgeTicks(source.AbsoluteTick, nowTick);
        var speakerView = BuildView(speaker, subject, source, nowTick);

        // (b) two-hop cap on asking.
        int hopCount = speakerView.HopCount + 1;
        if (hopCount > MaxHopCount) return false;

        // (c) the listener can never learn more than the speaker knows right now. The listener's
        // "natural" detail is what their own lookup would yield under the same thresholds, with the
        // age still measured from the source entry's tick (gossip never re-freshens a fact); the
        // stored knowledge is the coarser of that and the speaker's current detail.
        var natural = DetailAt(age);
        var cap = (LedgerDetail)Math.Max((int)natural, (int)speakerView.Detail);

        Store(listener, subject, new Entry
        {
            Location = source.Location,
            Region = source.Region,
            AbsoluteTick = source.AbsoluteTick,
            HopCount = hopCount,
            DetailCap = cap,
        });
        return true;
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
                    ["absoluteTick"] = entry.AbsoluteTick,
                    ["hopCount"] = entry.HopCount,
                    ["detailCap"] = entry.DetailCap.HasValue ? JsonValue.Create((int)entry.DetailCap.Value) : null,
                });
            }
        }

        var root = new JsonObject
        {
            ["spotTtl"] = SpotTtl,
            ["locationTtl"] = LocationTtl,
            ["regionTtl"] = RegionTtl,
            ["goneTtl"] = GoneTtl,
            ["entries"] = entries,
        };
        return root.ToJsonString();
    }

    public static Ledger FromJson(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var ledger = new Ledger();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return ledger;

        if (root.TryGetProperty("spotTtl", out var spot)) ledger.SpotTtl = spot.GetInt32();
        if (root.TryGetProperty("locationTtl", out var location)) ledger.LocationTtl = location.GetInt32();
        if (root.TryGetProperty("regionTtl", out var region)) ledger.RegionTtl = region.GetInt32();
        if (root.TryGetProperty("goneTtl", out var gone)) ledger.GoneTtl = gone.GetInt32();

        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in entries.EnumerateArray())
            {
                var entry = new Entry
                {
                    Location = Text(item, "location"),
                    Region = Text(item, "region"),
                    AbsoluteTick = item.TryGetProperty("absoluteTick", out var tick) ? tick.GetInt32() : 0,
                    HopCount = item.TryGetProperty("hopCount", out var hop) ? hop.GetInt32() : 0,
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
        var detail = DetailAt(age);
        if (entry.DetailCap is { } cap && (int)cap > (int)detail) detail = cap;
        return new LedgerView(observer, subject, detail, PlaceFor(detail, entry), age, entry.HopCount, entry.AbsoluteTick);
    }

    /// <summary>Detail implied by age alone (LedgerDetail is ordered finest -> coarsest).</summary>
    private LedgerDetail DetailAt(int age)
        => age < SpotTtl ? LedgerDetail.NamedSpot
         : age < LocationTtl ? LedgerDetail.Location
         : age < RegionTtl ? LedgerDetail.Region
         : age < GoneTtl ? LedgerDetail.EarlierToday
         : LedgerDetail.Gone;

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
