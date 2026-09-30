using System.Text.Json;
using NpcMemory;

namespace NpcIntents;

/// <summary>
/// One NPC's memory at sleep time — everything the intent planner reasons over. The planner is
/// game-independent: the mod builds these snapshots from the NPC's own diary and voice sheet.
/// </summary>
public sealed record NpcMemorySnapshot(
    string Npc,
    string Voice,                                // short voice description (VoiceSheets.Voice)
    IReadOnlyList<DiaryEntry> RecentDiary,       // recent entries, oldest first
    IReadOnlyList<string> RecentLines,           // lines this NPC already said (dedupe/cooldown)
    NewsContext? News = null,                    // newsworthiness context (docs/spec/diary.md)
    string? Card = null);                        // prebuilt NPC card (docs/spec/laya.md); null -> legacy context

/// <summary>
/// One would-be line for tomorrow, with its cited diary entry for legibility (the player should
/// be able to see WHY the NPC said it).
/// </summary>
public sealed record IntentCandidate(
    string Npc,
    string Line,
    DiaryEntry Source,
    string Reason,
    double News = 0);                            // newsworthiness of the cited entry (0 = unknown)

/// <summary>A day's worth of intents. JSON-round-trippable for save persistence.</summary>
public sealed class IntentPlan
{
    private readonly List<IntentCandidate> _candidates;

    public IntentPlan(IEnumerable<IntentCandidate> candidates)
        => _candidates = candidates.ToList();

    public IReadOnlyList<IntentCandidate> Candidates => _candidates;

    public string ToJson() => JsonSerializer.Serialize(_candidates);

    public static IntentPlan FromJson(string json)
        => new(JsonSerializer.Deserialize<List<IntentCandidate>>(json) ?? new List<IntentCandidate>());
}
