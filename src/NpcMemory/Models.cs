namespace NpcMemory;

/// <summary>
/// One line in an NPC's private diary: a timestamped fact about a subject. The game keeps
/// only counters and flags; this is the mod's record of what an NPC witnessed or did.
/// </summary>
public sealed record DiaryEntry(
    int AbsoluteTick,   // when it happened (GameClock.AbsoluteTick)
    string Subject,     // who/what it was about (NPC name or "Player")
    string Kind,        // free-form category, e.g. "Saw", "SpokeWith", "ReceivedGift"
    string? Detail = null);

/// <summary>How precisely a last-seen entry is still known, in decreasing detail.</summary>
public enum LedgerDetail
{
    NamedSpot,   // the specific place, recent
    Location,    // the map/location name
    Region,      // coarse region (RegionMap)
    EarlierToday, // no place, just "seen earlier today"
    Gone,        // forgotten
}

/// <summary>
/// What an observer currently knows about where a subject was last seen. The `Place` field
/// holds a location name (NamedSpot/Location), a region name (Region), or null
/// (EarlierToday/Gone). `Spot` is the spot within the location (a tile or named area) and is
/// set only at NamedSpot. `HopCount` is 0 for first-hand, 1 for told-by-someone, 2 for
/// told-by-someone-who-was-told.
/// </summary>
public sealed record LedgerView(
    string Observer,
    string Subject,
    LedgerDetail Detail,
    string? Place,
    int AgeTicks,
    int HopCount,
    int AbsoluteTick,
    string? Spot = null);
