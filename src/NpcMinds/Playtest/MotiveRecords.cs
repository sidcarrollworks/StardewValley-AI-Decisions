using System.Globalization;
using System.Text.Json.Serialization;
using NpcMotives;

namespace NpcMinds.Playtest;

/// <summary>
/// One motives decision or outcome (docs/spec/debug-tools.md, the <c>decision</c> record): every
/// part of the act rule, so each constant can be tuned from data. <see cref="Kind"/> is the
/// runner's event kind in lower case: "act", "pass", "blocked", "responded", "ignored",
/// "expired" or "grudge". Parts that don't apply to a kind are null and left out of the line.
/// </summary>
public sealed record DecisionRecord(
    string Npc,
    string Kind,
    string Subject,
    string? Motive,       // the motive acted on or weighed
    double? Strength,
    string? Motives,      // every motive with its strength, strongest first: "MissingYou 0.34; Greeting 0.12"
    double? Net,          // net feeling toward the subject
    double? Outlook,
    double? Earned,
    double? Roll,
    bool? TailDay,
    string? Act,          // the act taken, or the close call asked about
    bool Hostile,
    double? Cost,
    double? Boldness,
    double? Familiarity,
    double? Intensity,    // the intensity term (IntensityWeight x intensity)
    double? Frustration,
    double? Effective,
    double? Margin,
    string? Call,         // "clear" or "close"
    double? ModelP,       // the close call's model answer, before the tilt
    double? Tilted,       // after the mood tilt
    string? Choice,       // the model's pick among motives, when it was asked
    string Detail,        // the reason: the rule's verdict, the cap, or the outcome
    double? Grudge = null) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "decision";
}

/// <summary>
/// What one diary entry did to a character (the <c>stress</c> record): its stress after
/// sensitivity (the elastic part when fresh) and the lasting mark it left in regard.
/// </summary>
public sealed record StressRecord(
    string Npc,
    string Subject,
    string Kind,
    double Magnitude,
    double PlasticShare,
    double Plastic,       // the change in regard (after minus before)
    double Retention,
    bool Severe,
    bool YieldCrossed,
    string Cause) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "stress";
}

/// <summary>
/// A change in regard (the <c>regard</c> record): observer, subject, before, after and cause.
/// The 6:00 snapshot writes one per pair with cause "snapshot" and before equal to after.
/// </summary>
public sealed record RegardRecord(
    string Observer,
    string Subject,
    double Before,
    double After,
    string Cause) : PlaytestRecord
{
    [JsonPropertyOrder(-1)]
    public override string Type => "regard";
}

/// <summary>Builds the motives records from the runner's events and the regard keeper's notes.
/// Pure; numbers are rounded to 4 places so the lines stay short.</summary>
public static class MotiveRecords
{
    public static DecisionRecord Decision(MotiveEvent e)
    {
        MotiveDecision? d = e.Decision;
        ActCheck? check = d is null ? null
            : e.Act is { } act ? d.Checks.FirstOrDefault(c => c.Act == act) ?? d.Pending
            : d.Pending ?? d.Checks.FirstOrDefault();
        string? motives = d is null ? null : string.Join("; ", d.Motives
            .OrderByDescending(m => m.Strength).ThenBy(m => (int)m.Motive)
            .Select(m => string.Format(CultureInfo.InvariantCulture, "{0} {1:0.00}", m.Motive, m.Strength)));
        return new DecisionRecord(
            e.Npc,
            e.Kind.ToLowerInvariant(),
            MotivesEngine.Player,
            (d?.Chosen?.Motive ?? e.Motive)?.ToString(),
            R(d?.Chosen?.Strength),
            motives,
            R(d?.NetFeeling),
            R(d?.Mood.Outlook),
            R(d?.Mood.Earned),
            R(d?.Mood.Roll),
            d?.Mood.TailDay,
            (e.Act ?? check?.Act)?.ToString(),
            e.Hostile,
            R(check?.Cost),
            R(d?.Boldness),
            R(d?.Familiarity),
            R(d?.IntensityTerm),
            R(d?.Frustration),
            R(check?.Effective),
            R(check?.Margin),
            check is null ? null : check.Call == CallKind.CloseCall ? "close" : check.Call == CallKind.ClearYes ? "clear" : "no",
            R(e.ModelP),
            R(e.TiltedP),
            e.Choice,
            e.Reason,
            R(e.Grudge))
        {
            Tick = e.AbsoluteTick,
        };
    }

    public static StressRecord Stress(RegardNote n, int tick) => new(
        n.Observer, n.Subject, n.Kind, Round(n.Magnitude), Round(n.Plastic), Round(n.After - n.Before),
        Round(n.Retention), n.Severe, n.YieldCrossed, n.Cause)
    {
        Tick = tick,
    };

    public static RegardRecord Regard(RegardNote n, int tick)
        => new(n.Observer, n.Subject, Round(n.Before), Round(n.After), n.Cause) { Tick = tick };

    /// <summary>A regard seeded from history at install (RegardHistory): from 0, cause
    /// "history: 31 gifts: 12 loved, ...".</summary>
    public static RegardRecord History(HistorySeed seed, int tick)
        => new(seed.Npc, NpcMemory.MemoryStore.PlayerName, 0, Round(seed.Regard), "history: " + seed.Because) { Tick = tick };

    /// <summary>The 6:00 snapshot: one record per pair, cause "snapshot".</summary>
    public static IEnumerable<RegardRecord> Snapshot(RegardBook book, int tick)
        => book.Pairs().Select(p => new RegardRecord(p.Observer, p.Subject, Round(p.Value), Round(p.Value), "snapshot") { Tick = tick });

    private static double? R(double? v) => v is { } x && !double.IsNaN(x) ? Round(x) : null;

    private static double Round(double v) => Math.Round(v, 4);
}
