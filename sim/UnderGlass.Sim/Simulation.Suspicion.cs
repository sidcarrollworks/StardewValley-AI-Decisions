namespace UnderGlass.Sim;

/// <summary>
/// Piecing together "someone" (Sid, 2026-10-06: "they should be able to piece together from
/// people around"). Everyone remembers who they saw, where and when (sightings, from perception:
/// same place, within 8 tiles, in line of sight). Someone who saw or found a scandal without seeing
/// who did it recalls who was around the place at the time, and suspects them, strangers more than
/// friends. Suspicion travels with the story, and reaches the authority as "nearby" names.
/// </summary>
public sealed partial class Simulation
{
    private readonly Dictionary<(string Observer, string Subject), List<(string Place, int From, int To)>> _sightings = new();

    /// <summary>Each tick: who each awake person can see now, kept as spans of time per place.</summary>
    private void See(int m)
    {
        foreach (Person o in _people)
        {
            if (o.Asleep)
                continue;
            Location place = _places[o.Place];
            foreach (Person s in _people)
            {
                if (s == o || s.Place != o.Place || s.At.Chebyshev(o.At) > _po.FarTiles
                    || Perception.LineOfSight(place, o.At, s.At, _po) <= 0)
                    continue;
                var key = (o.V.Name, s.V.Name);
                if (!_sightings.TryGetValue(key, out var spans))
                    _sightings[key] = spans = new List<(string, int, int)>();
                if (spans.Count > 0 && spans[^1].Place == o.Place && spans[^1].To >= m - Clock.TickMinutes)
                    spans[^1] = (o.Place, spans[^1].From, m);
                else
                    spans.Add((o.Place, m, m));
                Habit(o, s, m);
            }
        }
    }

    /// <summary>Sightings older than three days are forgotten (called at midnight).</summary>
    private void ForgetSightings(int m)
    {
        foreach (var spans in _sightings.Values)
            spans.RemoveAll(s => s.To < m - 3 * Clock.MinutesPerDay);
    }

    /// <summary>Who <paramref name="observer"/> saw at <paramref name="place"/> between the two
    /// minutes, in name order.</summary>
    private IEnumerable<string> SeenAt(string observer, string place, int since, int until)
        => _names.Where(n => n != observer && _sightings.TryGetValue((observer, n), out var spans)
                             && spans.Any(s => s.Place == place && s.To >= since && s.From <= until));

    /// <summary>When the holder of a first-hand belief places the act: a witness around the
    /// minutes it took; a finder in the hours before the find. Hearsay carries no time.</summary>
    private (int Since, int Until)? Window(Belief b)
    {
        if (b.Source == Source.Told)
            return null;
        if (b.Source == Source.Witnessed)
            return (b.GotTick - KindOf(_acts[b.ActId]).DurationMinutes - _go.SuspectSeenMinutes, b.GotTick + _go.SuspectSeenMinutes);
        return (b.GotTick - _go.SuspectFoundHours * 60, b.GotTick);
    }

    /// <summary>Each tick: anyone holding a "someone" scandal they saw or found, once its window
    /// has passed, recalls who was around and suspects them. Strangers are suspected before
    /// friends (law 9, generalising from a kind of person); the keeper of the place is not.</summary>
    private void PieceTogether(int m)
    {
        foreach (Person p in _people)
        {
            string who = p.V.Name;
            var pending = _scandalBeliefs[who].Select(id => _beliefs[who][id])
                .Where(b => b.Actor is null && b.Suspects is null && b.Source != Source.Told).ToList(); // in act order
            foreach (Belief b in pending)
            {
                var (since, until) = Window(b)!.Value;
                if (m < until)
                    continue;
                Act act = _acts[b.ActId];
                _ao.Keepers.TryGetValue(act.Location, out string? keeper);
                var suspects = SeenAt(who, act.Location, since, until)
                    .Where(n => n != keeper && !AreKin(who, n)) // nobody suspects their own kin
                    // S6 (III P26, VERIFY): with feelings steering, the disliked are suspected first.
                    .OrderByDescending(n => Steering ? 1.2 - Familiarity(who, n) - _fo.SuspectPerRegard * St(who, n) : 1.2 - Familiarity(who, n))
                    .ThenBy(n => n, StringComparer.Ordinal)
                    .Take(_go.MaxSuspects)
                    .ToList();
                if (suspects.Count > 0)
                    _log.Add($"{m} suspects {who} {b.ActId} {string.Join(",", suspects)}");
                Add(who, b with { Suspects = suspects }, m);
            }
        }
    }
}
