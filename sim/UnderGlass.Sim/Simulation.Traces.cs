namespace UnderGlass.Sim;

/// <summary>
/// Traces (design principle 1): an act can leave something behind, so a scandal nobody saw can
/// still be found later. The finder learns that it happened, not who did it ("someone").
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The share of an act's juiciness anyone gets from finding its trace, the victim
    /// included. A story with no name in it is mild news: a scattered bin (2.0) or missing stock
    /// (2.25) is told only on the day it is found, so traces mostly feed reports to the mayor.
    /// (At 1.0 for the victim and 0.75 for others, finders pushed witnessed scandals past the 0a
    /// band: 37% over 70% of the town.)</summary>
    public const double FoundFactor = 0.5;

    private readonly List<(int ActId, TraceKind Kind, int Until)> _traces = new();

    private void LeaveTrace(Act act, ActKind kind, int m)
    {
        if (kind.Trace is not { } trace || _noTrace.Contains(act.Id)) // a scenario's NoTrace
            return;
        _traces.Add((act.Id, trace, m + trace.LastsMinutes));
        _log.Add($"{m} trace {act.Id} {trace.Name} at {act.Location}");
    }

    /// <summary>Each tick, everyone awake in the place who doesn't already know about the act may
    /// notice its trace: the keeper by their stock count, anyone by sight of the spot.</summary>
    private void CheckTraces(int m)
    {
        int t = Clock.OfDay(m);
        double hours = Clock.TickMinutes / 60.0;
        for (int i = 0; i < _traces.Count; i++)
        {
            var (actId, trace, until) = _traces[i];
            if (until <= m)
            {
                _traces.RemoveAt(i--);
                continue;
            }
            Act act = _acts[actId];
            Location place = _places[act.Location];
            _ao.Keepers.TryGetValue(act.Location, out string? keeper);
            double perTick = 1 - Math.Pow(1 - Math.Min(1, trace.NoticePerHour), hours);
            foreach (Person p in _people)
            {
                string who = p.V.Name;
                if (p.Asleep || p.Place != act.Location || who == act.Actor || _beliefs[who].ContainsKey(actId))
                    continue;
                if (trace.KeeperOnly && who != keeper)
                    continue;
                double clarity = trace.KeeperOnly ? 1 : Perception.Instant(place, p.At, act.At, t, _po);
                if (clarity <= 0 || Rng.Unit(_seed, "found", actId.ToString(), who, m.ToString()) >= perTick * clarity)
                    continue;
                double juice = KindOf(act).Juiciness * FoundFactor;
                _log.Add($"{m} found {who} {actId} {trace.Name}");
                string? target = _fo.Enabled && KindOf(act).Affect?.Target == TargetIs.Keeper ? act.Target : null;
                Add(who, new Belief(actId, act.Kind, null, 0, clarity, Source.Found, juice, m, Array.Empty<string>(), Target: target), m);
            }
        }
    }
}
