namespace NpcSchedules;

/// <summary>A parsed schedule point (one slash-delimited field), mirroring NPC.cs parseMasterScheduleImpl.</summary>
public sealed record SchedulePoint(
    int Time,           // game time value after arrival adjustment (walk time is ignored by this tool)
    bool ArriveBy,      // 'a' prefix in the source script
    string Location,    // destination location name (already resolved for omitted/'bed')
    int X,
    int Y,
    int Facing,
    string? Animation,
    string? Message)
{
    /// <summary>Location as written in the script, before omitted-location/bed resolution.</summary>
    public string RawLocation { get; init; } = Location;
}

/// <summary>Result of evaluating one schedule script through the command chain.</summary>
public sealed class ParsedScript
{
    /// <summary>The schedule key whose script actually produced the points (after GOTO chains).</summary>
    public string EffectiveKey { get; init; } = "";

    /// <summary>Points, in script order. May be empty (script failed or had no points).</summary>
    public List<SchedulePoint> Points { get; set; } = new();

    /// <summary>Time-0 spawn point, if the script had one. Sets where the NPC counts before the first point.</summary>
    public SchedulePoint? Spawn { get; set; }

    /// <summary>True when the chain resolved to GOTO NO_SCHEDULE: the NPC has no schedule today.</summary>
    public bool NoSchedule { get; init; }

    /// <summary>The chain of keys followed to get here, e.g. "spring_4 -> spring".</summary>
    public List<string> KeyChain { get; init; } = new();

    public string KeyChainLabel => string.Join(" -> ", KeyChain);

    /// <summary>Location the NPC is at before the first scheduled point (spawn point, else home).</summary>
    public string StartLocation { get; set; } = "";

    /// <summary>True when the game would have failed to parse this script (exception -> empty schedule).</summary>
    public bool ParseFailed { get; init; }
}

/// <summary>One region x block cell for an NPC's year-wide routine counts.</summary>
public sealed record RoutineCell(string Region, string Block, int Ticks, double Share);

/// <summary>What a full-year simulation produced for one NPC.</summary>
public sealed class NpcRoutine
{
    public string Name { get; init; } = "";
    public string HomeLocation { get; set; } = "";
    public string HomeRegion { get; set; } = RegionMap.OtherRegion;
    public int BlockMinutes { get; init; } = 120;
    public List<RoutineCell> Cells { get; } = new();
    public Dictionary<string, int> KeyDays { get; } = new();   // key chain -> number of days it was used
    public List<string> UnmappedLocations { get; } = new();    // locations with no region mapping
    public List<string> Warnings { get; } = new();

    /// <summary>region x block tick matrix, index [block][regionName].</summary>
    public Dictionary<string, int[]> RegionTicks { get; init; } = new();

    /// <summary>Year total ticks (112 days x 120, or fewer if some days had no schedule).</summary>
    public int TotalTicks { get; set; }

    public List<RoutineCell> BuildCells(RegionMap regions)
    {
        var result = new List<RoutineCell>();
        int blockCount = TimeUtils.BlockCount(regions.BlockMinutes);
        foreach ((string region, int[] byBlock) in RegionTicks.OrderBy(p => p.Key))
            for (int b = 0; b < blockCount; b++)
                if (byBlock[b] > 0)
                    result.Add(new RoutineCell(region, TimeUtils.BlockLabel(b, regions.BlockMinutes),
                        byBlock[b], TotalTicks > 0 ? (double)byBlock[b] / TotalTicks : 0));
        return result;
    }
}

/// <summary>Options for the year simulation. All public state; same inputs always give the same output.</summary>
public sealed class ExtractorOptions
{
    /// <summary>Friendship points the observing player has with the target NPC. Used for day_hearts, season_dow_hearts, dow_hearts keys.</summary>
    public int Hearts { get; set; }

    /// <summary>Hearts with other NPCs, used by NOT friendship commands. Keys are NPC names.</summary>
    public Dictionary<string, int> Friends { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Letters/world-state IDs the player has received. Used by MAIL commands and the Pam 'bus' key.</summary>
    public HashSet<string> MailReceived { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional simulation seed. Null means derive it from the NPC name (still deterministic).</summary>
    public int? Seed { get; set; }
}

/// <summary>How much the observer trusts the prior: family gets a strong prior, friends a weak one.</summary>
public enum RelationshipKind { Family, Friend, Other }

public sealed class PriorOptions
{
    public RelationshipKind Kind { get; set; } = RelationshipKind.Other;
    public double FamilyStrength { get; set; } = 40;
    public double FriendStrength { get; set; } = 8;
    public double OtherStrength { get; set; } = 3;
    /// <summary>Max fraction of a block's mass smeared into each neighbour.</summary>
    public double MaxSmear { get; set; } = 0.25;
    /// <summary>Std-dev of the log-normal multiplier applied per region.</summary>
    public double RegionNoiseSigma { get; set; } = 0.35;
}
