using System.Text.Json;
using NpcSchedules;

namespace NpcShadow;

/// <summary>One NPC's resolved whereabouts for a single day (120 ten-minute ticks).</summary>
public sealed class DayPlan
{
    public string Npc { get; init; } = "";
    public string HomeRegion { get; init; } = "";
    public string HomeLocation { get; init; } = "";
    public string KeyChain { get; init; } = "";

    /// <summary>Region name per tick, length 120.</summary>
    public string[] RegionByTick { get; init; } = Array.Empty<string>();

    /// <summary>Location name per tick, length 120.</summary>
    public string[] LocationByTick { get; init; } = Array.Empty<string>();
}

/// <summary>
/// One line of the shadow log: something the mod WOULD have done, logged, not applied.
/// <see cref="Tick"/> is day-relative (0..119); <see cref="Day"/> is 1-based across a multi-day run.
/// </summary>
public sealed record ShadowEvent(int Tick, int TimeOfDay, string Actor, string Kind, string Message, int Day = 1);

/// <summary>The shadow-mode artifact: a human-readable, deterministic log of a simulated span of days.</summary>
public sealed class ShadowLog
{
    private readonly List<ShadowEvent> _events = new();

    public IReadOnlyList<ShadowEvent> Events => _events;

    public void Add(int tick, string actor, string kind, string message, int day = 1)
        => _events.Add(new ShadowEvent(tick, TimeUtils.TimeOfDay(tick), actor, kind, message, day));

    /// <summary>One line per event, in insertion order: "DD HHMM  Actor: message".</summary>
    public string RenderText()
        => string.Join('\n', _events.Select(e => $"{e.Day:00} {e.TimeOfDay:0000}  {e.Actor}: {e.Message}"));

    public string ToJson()
        => JsonSerializer.Serialize(_events);
}
