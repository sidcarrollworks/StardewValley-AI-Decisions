namespace UnderGlass.Sim;

/// <summary>
/// The scenario harness (batch 2 spec 5.2-5.3, slice m-0): acts placed in chosen scenes, and the
/// circle each scandal is measured against. It only places what a check asks for and records what
/// it measures: a run with no scenario draws nothing more, logs nothing more and ends as it did.
/// </summary>
public sealed partial class Simulation
{
    private readonly List<Scenario> _pendingScenarios = new();
    private readonly Dictionary<int, string> _scenarioActs = new();
    private readonly Dictionary<int, IReadOnlyList<string>> _circles = new();
    private readonly HashSet<int> _noTrace = new();
    private readonly List<(int At, int ActId, string What)> _followUps = new();
    private readonly HashSet<int> _forceSway = new();
    private double[,]? _famAtDayStart;

    /// <summary>Places these scenarios in the run (see <see cref="Scenario"/>). Call before Run, as
    /// SetTrait is. A scenario the town can't stage (a kind, person or place it doesn't have, an
    /// empty window) is refused, and so is one that asks for a debt or for a follow-up other than
    /// "sway", which come with later slices.</summary>
    public void Place(IEnumerable<Scenario> scenarios)
    {
        foreach (Scenario s in scenarios)
        {
            if (!_kindByName.ContainsKey(s.Kind))
                throw new ArgumentException($"scenario {s.Name}: the town has no act kind {s.Kind}");
            if (s.Actor != Harness.Anyone && !_index.ContainsKey(s.Actor))
                throw new ArgumentException($"scenario {s.Name}: nobody called {s.Actor}");
            if (s.Target is { } t && !_index.ContainsKey(t))
                throw new ArgumentException($"scenario {s.Name}: nobody called {t}");
            if (s.Places?.FirstOrDefault(p => !_places.ContainsKey(p)) is { } missing)
                throw new ArgumentException($"scenario {s.Name}: the town has no place {missing}");
            if (s.Day < 0 || s.From < 0 || s.To > Clock.MinutesPerDay || s.From >= s.To || s.GiveUpDays < 1
                || s.MinInRange < 0 || s.MinInRange > s.MaxInRange)
                throw new ArgumentException($"scenario {s.Name}: its window or its range is empty");
            if (s.Amount != 0)
                throw new NotSupportedException($"scenario {s.Name}: a debt needs batch 2's debts (b2-12)");
            if (s.Then?.FirstOrDefault(f => f.What != Sway) is { } unbuilt)
                throw new NotSupportedException($"scenario {s.Name}: the follow-up {unbuilt.What} isn't built yet (batch 2 spec 5.3)");
            if (s.Then?.FirstOrDefault(f => f.AfterMinutes < 0) is { } early)
                throw new ArgumentException($"scenario {s.Name}: a follow-up comes after the act, not {early.AfterMinutes} minutes before");
            _pendingScenarios.Add(s);
        }
    }

    /// <summary>Each minute, after the scheduled acts: each pending scenario, in the order placed,
    /// begins if its scene holds (see <see cref="Scenario"/>), or is dropped once its days are up.</summary>
    private void StartScenarios(int m)
    {
        for (int i = 0; i < _followUps.Count; i++)
        {
            var (at, id, what) = _followUps[i];
            if (at > m)
                continue;
            if (what == Sway)
                _forceSway.Add(id); // the mayor lets the accused off when he decides this case
            _log.Add($"{m} scene {_scenarioActs[id]} {what} {id}");
            _followUps.RemoveAt(i--);
        }
        int day = Clock.Day(m), t = Clock.OfDay(m);
        for (int i = 0; i < _pendingScenarios.Count; i++)
        {
            Scenario s = _pendingScenarios[i];
            if (day >= s.Day + s.GiveUpDays)
            {
                _log.Add($"{m} scene dropped {s.Name}");
                _pendingScenarios.RemoveAt(i--);
                continue;
            }
            if (day < s.Day || t < s.From || t >= s.To)
                continue;
            ActKind kind = _kindByName[s.Kind];
            Person? actor = null;
            if (s.Actor == Harness.Anyone)
            {
                var able = _people.Where(p => InScene(s, kind, p, m)).ToList();
                if (able.Count > 0 && Rng.Unit(_seed, "scene", s.DrawKey, m.ToString()) < 1.0 / PlacedMeanWait)
                    actor = able[Rng.Range(_seed, 0, able.Count - 1, "scene-actor", s.DrawKey, m.ToString())];
            }
            else if (InScene(s, kind, _people[_index[s.Actor]], m))
                actor = _people[_index[s.Actor]];
            if (actor is null)
                continue;
            int id = _acts.Count; // the act Begin is about to add
            _scenarioActs[id] = s.Name;
            if (s.NoTrace)
                _noTrace.Add(id);
            Begin(m, kind, actor, injected: true, target: s.Target);
            foreach (FollowUp f in s.Then ?? Array.Empty<FollowUp>())
                _followUps.Add((m + f.AfterMinutes, id, f.What));
            _pendingScenarios.RemoveAt(i--);
        }
    }

    /// <summary>The follow-up that has the mayor let the accused off when he decides the case
    /// (batch 2 spec 5.3; S6 reads it once let-offs are acts, b2-17).</summary>
    public const string Sway = "sway";

    /// <summary>Whether p could do the scenario's act here and now, in its scene.</summary>
    private bool InScene(Scenario s, ActKind kind, Person p, int m)
    {
        IReadOnlyList<string> places = s.Places ?? kind.Allowed;
        if (!Free(p, m) || !kind.FitsAge(p.V.Age) || places.Count > 0 && !places.Contains(p.Place)
            || p.V.Job?.Place == p.Place || s.Target is null && !HasTarget(kind, p, m) || s.Target == p.V.Name)
            return false;
        int a = _index[p.V.Name], inRange = 0;
        foreach (Person o in _people)
        {
            // As SceneOf counts InRange: awake, in the place, within FarTiles.
            if (o == p || o.Asleep || o.Place != p.Place || o.At.Chebyshev(p.At) > _po.FarTiles)
                continue;
            inRange++;
            int oi = _index[o.V.Name];
            if (s.Onlookers == Onlookers.Loners && CharacterOf(oi).Chattiness > Scenario.LonerChattiness
                || s.Onlookers == Onlookers.KinOnly && !Close(a, oi))
                return false;
        }
        if (inRange < s.MinInRange || inRange > s.MaxInRange)
            return false;
        if (!s.AtHub && !s.AtFestival)
            return true;
        if (s.AtFestival && Calendar.FestivalOn(Clock.Day(m)) is null)
            return false;
        return _gatherings.Any(g => g.Place == p.Place && g.On(m) && p.At.Chebyshev(g.Center) <= g.Radius);
    }

    /// <summary>At the start of each day: familiarity as it stands, for the circles of the day's acts.</summary>
    private void KeepDayStart()
    {
        _famAtDayStart ??= new double[_names.Length, _names.Length];
        Array.Copy(_fam, _famAtDayStart, _fam.Length);
    }

    /// <summary>A scandal's or a scenario act's circle (batch 2 spec 5.1, 5.4): everyone else who
    /// knew its actor at familiarity <see cref="ReachMetrics.CircleAt"/> or more as its day began, in
    /// name order. Reach is measured against them as well as against the town.</summary>
    private void RecordCircle(Act act, ActKind kind)
    {
        if (!kind.IsScandal && !_scenarioActs.ContainsKey(act.Id))
            return;
        double[,] fam = _famAtDayStart ?? _fam;
        int a = _index[act.Actor];
        var circle = new List<string>();
        for (int i = 0; i < _names.Length; i++)
            if (i != a && fam[i, a] >= ReachMetrics.CircleAt)
                circle.Add(_names[i]);
        _circles[act.Id] = circle;
    }
}
