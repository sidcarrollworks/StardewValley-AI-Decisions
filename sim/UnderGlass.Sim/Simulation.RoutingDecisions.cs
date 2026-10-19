using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private const string RouteStay = "route-stay";
    private ILivesDecisionMind? _routingMind;
    private sealed class RouteQuestion(Person person, LivesDecisionRequest request, ChoiceComparison? comparison,
        string[] ids, Haunt?[] options)
    {
        public Person Person { get; } = person;
        public LivesDecisionRequest Request { get; } = request;
        public ChoiceComparison? Comparison { get; } = comparison;
        public string[] Ids { get; } = ids;
        public Haunt?[] Options { get; } = options;
        public (string, Tile, int, string) Goal { get; } = (person.GoalPlace, person.GoalSpot, person.GoalUntil, person.Why);
        public LivesDecisionRecord? Evaluation;
        public int RecordIndex = -1;
    }
    private readonly Dictionary<string, RouteQuestion> _routePending = new(StringComparer.Ordinal);
    private readonly Queue<RouteQuestion> _routeQuestions = new();

    private bool QueueRoute(Person p, int m, List<(Haunt? Haunt, double Weight)> options,
        (ChoiceDecision Decision, ChoiceComparison? Comparison, string[] Ids)? routing)
    {
        if (_routingMind is null || routing is not { Decision.Available: true, Decision.Legacy: false } r) return false;
        if (_routePending.ContainsKey(p.V.Name)) return true;
        var character = CharacterOf(_index[p.V.Name]);
        // Only this person's current place/body/traits and detached recalled knowledge enter the request.
        string context = FormattableString.Invariant($"Free time at {p.Place}; {Clock.OfDay(m) / 60:00}:{Clock.OfDay(m) % 60:00}. Energy {p.Energy / p.V.Body.MaxEnergy:0.00}; mood {Mood(p.V.Name):0.00}; chattiness {character.Chattiness:0.00}; boldness {character.Boldness:0.00}. Choose where to spend time. No current work, errand or company goal takes priority.");
        // Hold at the real current spot while thinking; a first waking choice has no old goal.
        Goal(p, p.Place, p.At, m, "", null);
        var question = new RouteQuestion(p, new(r.Decision, context, _livesRouting.Model ?? "typed-decisions"),
            r.Comparison, r.Ids, options.Select(o => o.Haunt).ToArray());
        _routePending.Add(p.V.Name, question);
        _routeQuestions.Enqueue(question);
        return true;
    }

    private async Task AnswerRoutes(CancellationToken cancellationToken)
    {
        while (_routeQuestions.TryDequeue(out var q))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var answer = await _routingMind!.EvaluateAsync(q.Request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            q.Evaluation = new(q.Request, answer, LivesDecisionEngine.Apply(q.Request, answer));
            if (_livesRouting.Record)
            {
                int selected = Array.IndexOf(q.Ids, q.Evaluation.SelectedId);
                var pick = q.Options[selected];
                q.RecordIndex = _livesChoices.Count;
                _livesChoices.Add(new(q.Request.Fallback, q.Comparison, q.Evaluation.SelectedId,
                    pick is null || ReferenceEquals(pick, BedtimeHabit) ? q.Person.V.Home : pick.Place,
                    false, q.Evaluation, "pending"));
            }
        }
    }

    private bool RouteReady(Person p, int m) => _routePending.TryGetValue(p.V.Name, out var q)
        && q.Evaluation is not null && q.Request.Fallback.Opportunity.At < m;

    private bool ApplyRoute(Person p, int m)
    {
        if (!RouteReady(p, m)) return false;
        var q = _routePending[p.V.Name];
        if (q.Goal != (p.GoalPlace, p.GoalSpot, p.GoalUntil, p.Why) || p.BusyUntil >= m)
        { ResolveRoute(q, m, "superseded"); return true; }
        int chosen = Array.IndexOf(q.Ids, q.Evaluation!.SelectedId);
        var pick = q.Options[chosen];
        bool bed = ReferenceEquals(pick, BedtimeHabit);
        bool usable = bed ? HabitDue(p, m) is not null
            : pick is null ? _places.ContainsKey(p.V.Home)
            : pick.Open(Clock.OfDay(m)) && (pick.Hub != RouteStay || pick.Place == p.Place && pick.Spot == p.At);
        if (usable && pick?.Hub is { } hub && hub != RouteStay)
            usable = _gatherings.Any(g => g.Name == hub && g.On(m) && Admits(g, p, m) && !NeverPicks(p, g));
        if (!usable) { ResolveRoute(q, m, "unavailable"); return true; }
        if (_nights is not null)
            _drawn = q.Ids.Select((id, i) => new NightOption(OptionName(q.Options[i]), q.Evaluation.Answer.Weights.GetValueOrDefault(id))).ToList();
        SetRouteGoal(p, m, pick, q.Request.Fallback.Opportunity.At);
        ResolveRoute(q, m, "applied");
        return true;
    }

    private void ResolveRoute(RouteQuestion q, int m, string resolution)
    {
        _routePending.Remove(q.Person.V.Name);
        if (q.RecordIndex >= 0)
            _livesChoices[q.RecordIndex] = _livesChoices[q.RecordIndex] with { Resolution = resolution, ResolvedAt = m };
    }

    private void SetRouteGoal(Person p, int m, Haunt? pick, int drawAt)
    {
        if (ReferenceEquals(pick, BedtimeHabit)) { GoToBed(p, _places.ContainsKey(p.V.Home)); return; }
        int stay = Rng.Range(_seed, 60, 180, "stay", p.V.Name, drawAt.ToString());
        if (pick is null) Goal(p, p.V.Home, DefaultTown.Sofa, m + stay, "home", null);
        else Goal(p, pick.Place, pick.Spot, m + stay, "haunt", pick);
    }
}
