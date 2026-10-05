using System.Text.Json;
using System.Text.Json.Serialization;
using NpcMemory;

namespace NpcMinds.Playtest;

/// <summary>
/// One line of the playtest log (docs/spec/debug-tools.md, "Playtest log"). Every record type
/// is one sealed record with a <see cref="Type"/> discriminator; adding a type later is one
/// record class here and one <c>Append</c>/<c>QueueFromWorker</c> call at the producer. The
/// motives types (decision, stress, regard) are in MotiveRecords.cs; social waits for town life.
/// </summary>
public abstract record PlaytestRecord
{
    /// <summary>The in-game tick (GameClock.AbsoluteTick) the line was recorded at. Ordered
    /// first so every line starts with <c>{"tick":...</c> (docs/spec/debug-tools.md).</summary>
    [JsonPropertyOrder(-2)]
    public int Tick { get; init; }

    /// <summary>The record discriminator, matching the spec table's <c>type</c> column. Each
    /// record puts <c>[JsonPropertyOrder(-1)]</c> on its override so it serializes right after
    /// the tick (an attribute on the abstract property is not inherited by overrides).</summary>
    public abstract string Type { get; }
}

/// <summary>
/// Ground truth positions: every villager's location and tile, plus the player's. The ONLY
/// record that may carry live positions — built from CollectPresences' output (AGENTS.md rule 2)
/// and never fed into a decision. Tiles are integers so the file stays small.
/// </summary>
public sealed record PresenceRecord(string Name, string Location, int X, int Y) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "presence";
}

/// <summary>
/// One ladder step attempt, outcome, or a cap/cooldown that blocked an attempt. Becomes
/// <c>decision</c> once motives replace the urge.
/// </summary>
public sealed record LadderRecord(
    string Npc,
    string Kind,         // "attempt", "blocked", or whatever the ladder's event Kind says
    string Step,         // the InitiationStep name
    double UrgeBefore,
    double UrgeAfter,
    double? Threshold,   // the step's threshold at the attempt
    double? ModelP,      // the model's yes/no answer (null when the model was not asked)
    string Detail,       // the ladder's Reason text
    string? LeadPlace) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "ladder";
}

/// <summary>
/// One Heard diary line written by MemoryStore.Chat, or one AskAround answer.
/// </summary>
public sealed record GossipRecord(
    string Teller,
    string Listener,
    string OriginalKind,
    string Subject,
    int Hops,
    string Kind,         // "heard" or "ask"
    bool? Answered) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "gossip";
}

/// <summary>
/// One game fact the diary hooks care about: weather, a festival, a gift, a quest, a talk.
/// </summary>
public sealed record GameRecord(
    string Kind,         // "weather", "festival", "gift", "quest", "talked"
    string Detail,
    string? Npc = null,
    string? Item = null,
    string? Taste = null) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "game";
}

/// <summary>
/// One candidate line of the overnight plan, with its news score.
/// </summary>
public sealed record PlanRecord(string Npc, string Line, string Reason, double News, int Rank) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "plan";
}

/// <summary>
/// One NPC's per-day memory census — the 500-entry diary cap check the motives design relies on.
/// </summary>
public sealed record MemoryRecord(
    string Npc,
    int DiaryEntries,
    int AddedToday,
    int TrimmedToday,
    int LedgerEntries,
    int Beliefs) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "memory";
}

/// <summary>
/// One decision-model call, from the same place RecordingDecisionClient sees it. Written from
/// the worker thread into the log's queue, drained on the game thread (AGENTS.md rule 5).
/// </summary>
public sealed record ModelCallRecord(
    string Caller,
    string Kind,         // "yesno", "choice", "score", "batch"
    string? Npc,
    string Template,     // the question with the NPC's name replaced by <npc>
    string Question,
    double? Answer,
    double Ms,
    bool FellBack,
    string? Error = null) : PlaytestRecord // why it fell back; null (left out of the line) when answered
{
    [JsonPropertyOrder(-1)]
    public override string Type => "model";
}

/// <summary>
/// Milliseconds spent in one section of the mod's tick: the whole tick, Observe, the ladder
/// hand-off, or the plan job.
/// </summary>
public sealed record PerfRecord(string Section, double Ms) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "perf";
}

/// <summary>Shared serialization and the pure presence-delta builder.</summary>
public static class PlaytestRecords
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>One JSON line for a record; used by the writer and the round-trip tests.</summary>
    public static string ToLine(PlaytestRecord record)
        => JsonSerializer.Serialize(record, record.GetType(), Json);

    /// <summary>
    /// Presence records ONLY for entries whose location or tile changed since the last tick
    /// (the player always logs, so the player's movement is never missed). Pure: takes exactly
    /// a CollectPresences-shaped list and a per-name previous-position map, and mutates nothing
    /// but the map's contents for the next tick.
    /// </summary>
    /// <summary>The gossip record for one Heard line written by <c>MemoryStore.Chat</c>: its
    /// hops from the person it started with, stamped when it was told (a Heard keeps the event's
    /// tick; playtest 2026-10-05: records showed hops 0 and a day-old tick).</summary>
    public static GossipRecord Heard(string listener, DiaryEntry heard)
    {
        IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(heard.Detail);
        return new GossipRecord(
            Teller: detail.TryGetValue("from", out string? from) ? from : "",
            Listener: listener,
            OriginalKind: detail.TryGetValue("kind", out string? kind) ? kind : "",
            Subject: heard.Subject,
            Hops: Gossip.HopsOf(heard),
            Kind: "heard",
            Answered: null)
        {
            Tick = Gossip.HeardAt(heard),
        };
    }

    public static List<PresenceRecord> PresenceDeltas(
        IReadOnlyList<Presence> current,
        Dictionary<string, (string Location, int X, int Y)> previous,
        int tick)
    {
        var changed = new List<PresenceRecord>(current.Count);
        foreach (Presence presence in current)
        {
            if (!presence.IsPlayer
                && previous.TryGetValue(presence.Name, out (string Location, int X, int Y) last)
                && last.Location == presence.Location && last.X == presence.X && last.Y == presence.Y)
                continue; // nothing moved; the last record still describes this villager
            previous[presence.Name] = (presence.Location, presence.X, presence.Y);
            changed.Add(new PresenceRecord(presence.Name, presence.Location, presence.X, presence.Y)
            {
                Tick = tick,
            });
        }
        return changed;
    }
}
