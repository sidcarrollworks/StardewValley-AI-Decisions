using NpcMemory;
using NpcSchedules;

namespace NpcIntents;

/// <summary>
/// Memory-only context for newsworthiness scoring, built on the game thread into each
/// <see cref="NpcMemorySnapshot"/> so the planner stays game-independent (docs/spec/diary.md,
/// "Deterministic rules"). Homes maps NPC names to their home LOCATION names (the mod resolves
/// Data/Characters); Beliefs are the OBSERVER's routine beliefs about each subject, keyed by
/// subject name; Hearts is the observer's hearts with the player. RecentCitations is empty until
/// the recent-lines persistence lands (intents.md step 5).
/// </summary>
public sealed record NewsContext(
    string Observer,
    IReadOnlyDictionary<string, string> Homes,
    IReadOnlyDictionary<string, RoutineBelief> Beliefs,
    RegionMap Regions,
    int Hearts,
    IReadOnlyList<(string Kind, string Subject)>? RecentCitations = null);
