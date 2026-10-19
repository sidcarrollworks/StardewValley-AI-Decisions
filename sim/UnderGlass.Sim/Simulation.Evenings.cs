using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private EveningOptions _evenings = new();
    private ILivesDecisionMind? _eveningMind;
    private readonly Dictionary<string, EveningPlan> _eveningPlans = new(StringComparer.Ordinal);
    private readonly HashSet<string> _openEvenings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _eveningPerson = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), int> _eveningConsidered = new();
    private readonly HashSet<(string Plan, string Holder)> _eveningPicks = new();
    private readonly Dictionary<(string Plan, string Holder), bool> _eveningKept = new();
    private readonly Dictionary<(string Plan, string Holder), (double Expected, double Investment)> _eveningInvestment = new();
    private readonly Dictionary<(string Plan, string Holder), int> _eveningPresence = new();
    private readonly HashSet<(string Plan, string Holder)> _eveningSeen = new();
    private readonly Dictionary<string, int> _eveningBudgets = new(StringComparer.Ordinal);
    private int _eveningBudgetDay = -1;
    private readonly List<EveningUpdate> _eveningUpdates = new();
    private readonly List<EveningChoice> _eveningChoices = new();
    private readonly List<EveningOutcome> _eveningOutcomes = new();
    private readonly List<LivesDecisionRecord> _livesDecisions = new();
    private sealed record EveningQuestion(string Phase, string? Plan, ChoiceDecision Fallback,
        ChoiceComparison? Comparison, string Context, Action<string, int> Apply);
    private readonly Queue<EveningQuestion> _eveningQuestions = new();
    private readonly Queue<(int At, EveningQuestion Question, string Selected)> _eveningAnswers = new();
    private readonly HashSet<string> _eveningQuestionHolders = new(StringComparer.Ordinal);

    public void ConfigureLivesEvenings(EveningOptions options, ILivesDecisionMind? mind = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_livesRunStarted || _evenings.Enabled) throw new InvalidOperationException("Configure evenings once before running.");
        EveningOptions copy = options.CopyValidated();
        if (copy.Enabled && (!_companyOptions.Enabled || !_livesRouting.Enabled))
            throw new ArgumentException("Evenings require company and Lives routing.");
        if (copy.Participants.Any(name => !_index.ContainsKey(name))) throw new ArgumentException("Unknown evening participant.");
        if (!copy.Enabled && mind is not null) throw new ArgumentException("An evening mind requires enabled evenings.");
        if (copy.Evaluation == EveningEvaluationPolicy.All && (!copy.Enabled || mind is null))
            throw new ArgumentException("All-choice evening evaluation requires enabled evenings and a decision mind or tape.");
        if (copy.Enabled && _livesMemory.Values.Any(memory => memory.Capture().HistoryIds.Any(id => id.StartsWith("evening:", StringComparison.Ordinal))))
            throw new ArgumentException("History cannot use the evening: runtime namespace.");
        _evenings = copy;
        _eveningMind = mind;
    }

    private bool EveningParticipant(string name) => _evenings.Participants.Count == 0 || _evenings.Participants.Contains(name, StringComparer.Ordinal);
    private bool EveningReserved(string name) => _evenings.Enabled && (_eveningQuestionHolders.Contains(name)
        || _eveningPerson.TryGetValue(name, out string? id) && _eveningPlans[id].Stage == EveningStage.Pending);

    private void AskEvening(Person person, int m, string phase, string? planId, string context,
        IReadOnlyList<string> cues, IReadOnlyList<ChoiceOption> options, Action<string, int> apply,
        IReadOnlyList<ChoiceConstraint>? constraints = null)
    {
        if (!_eveningQuestionHolders.Add(person.V.Name)) return;
        MemoryFrame frame = _livesMemory[person.V.Name].Capture();
        var opportunity = new ChoiceOpportunity($"evening:{phase}:{planId ?? "none"}:{person.V.Name}:{m}", person.V.Name,
            m, phase, cues, options, CharacterOf(_index[person.V.Name]).Boldness, Mood(person.V.Name) < 0,
            Constraints: constraints);
        ChoiceDecision fallback = LivesChoices.Decide(frame, opportunity, _seed, _livesRouting.Choices);
        // Plans and answers remain possible without recalled history: neutral empty-memory weights.
        if (fallback.Legacy)
            fallback = EveningNeutralDecision(fallback);
        string[] removed = _livesRouting.CompareHistory.Where(id => frame.HistoryIds.Contains(id, StringComparer.Ordinal)).ToArray();
        ChoiceComparison? comparison = !_evenings.Record || removed.Length == 0 ? null
            : LivesChoices.Compare(frame, opportunity, removed, _seed, _livesRouting.Choices);
        _eveningQuestions.Enqueue(new(phase, planId, fallback, comparison, context, apply));
    }

    private static ChoiceDecision EveningNeutralDecision(ChoiceDecision empty)
    {
        var options = empty.Opportunity.Options.Where(o => o.Base > 0 && o.WarmRecallObjects is null && o.RequiredRecallIds!.Count == 0).ToArray();
        if (options.Length == 0) return empty;
        double max = options.Max(o => Math.Log(o.Base) + empty.Settings.Scale * (o.Intended - o.GoingCost));
        double total = options.Sum(o => Math.Exp(Math.Log(o.Base) + empty.Settings.Scale * (o.Intended - o.GoingCost) - max));
        var alternatives = options.Select(o => new ChoiceAlternative(o, new(0, 0, Array.Empty<CauseContribution>()),
            Array.Empty<ExpectationSnapshot>(), 1, Math.Log(o.Base) + empty.Settings.Scale * (o.Intended - o.GoingCost),
            Math.Exp(Math.Log(o.Base) + empty.Settings.Scale * (o.Intended - o.GoingCost) - max) / total,
            NeutralReasons(o))).ToArray();
        double sum = 0; string selected = alternatives[^1].Option.Id;
        foreach (var alternative in alternatives) { sum += alternative.Probability; if (empty.Unit < sum) { selected = alternative.Option.Id; break; } }
        var sorted = alternatives.OrderByDescending(a => a.Probability).ToArray();
        bool torn = sorted.Length > 1 && sorted[1].Probability >= sorted[0].Probability * (1 - empty.Settings.TopTwoFraction);
        return empty with { Alternatives = Array.AsReadOnly(alternatives), SelectedId = selected, Legacy = false,
            Torn = torn, TornReasons = torn ? new[] { "close-alternatives" } : Array.Empty<string>() };

        static IReadOnlyList<ChoiceReason> NeutralReasons(ChoiceOption option)
        {
            var reasons = new List<ChoiceReason>
            {
                new(ChoiceReasonKind.Shown, ChoiceReasonVia.Routine, "base", option.Base, Array.Empty<string>()),
            };
            if (option.Intended > 0) reasons.Add(new(ChoiceReasonKind.Supports, ChoiceReasonVia.State,
                "intended", option.Intended, Array.Empty<string>()));
            if (option.GoingCost > 0) reasons.Add(new(ChoiceReasonKind.Conflicts, ChoiceReasonVia.State,
                "going-cost", option.GoingCost, Array.Empty<string>()));
            return reasons.AsReadOnly();
        }
    }

    private async Task AnswerEveningDecisions(CancellationToken cancellationToken, bool authoredOnly = false)
    {
        while (_eveningQuestions.TryDequeue(out EveningQuestion? question))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int day = Clock.Day(question.Fallback.Opportunity.At);
            if (day != _eveningBudgetDay) { _eveningBudgetDay = day; _eveningBudgets.Clear(); }
            string holder = question.Fallback.Opportunity.Holder;
            string? selected = question.Fallback.SelectedId;
            LivesDecisionRecord? evaluation = null;
            if (!authoredOnly && _eveningMind is not null
                && (_evenings.Evaluation == EveningEvaluationPolicy.All
                    || question.Fallback.Torn && _eveningBudgets.GetValueOrDefault(holder) < _evenings.ModelCallsPerDay))
            {
                _eveningBudgets[holder] = _eveningBudgets.GetValueOrDefault(holder) + 1;
                var request = new LivesDecisionRequest(question.Fallback, question.Context, _evenings.Model);
                var answer = await _eveningMind.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
                evaluation = new(request, answer, LivesDecisionEngine.Apply(request, answer));
                selected = evaluation.SelectedId;
                if (_evenings.Record) _livesDecisions.Add(evaluation);
            }
            if (_evenings.Record) _eveningChoices.Add(new(question.Phase, question.Plan, question.Fallback, question.Comparison, evaluation, question.Context));
            if (selected is null) { _eveningQuestionHolders.Remove(holder); continue; }
            _eveningAnswers.Enqueue((question.Fallback.Opportunity.At + 1, question, selected));
        }
    }

    private void ApplyEveningAnswers(int m)
    {
        while (_eveningAnswers.TryPeek(out var next) && next.At <= m)
        {
            _eveningAnswers.Dequeue();
            _eveningQuestionHolders.Remove(next.Question.Fallback.Opportunity.Holder);
            next.Question.Apply(next.Selected, m);
        }
    }

    private void RecordEvening(EveningPlan plan, int m, string reason)
    {
        _eveningPlans[plan.Id] = plan;
        if (_evenings.Record) _eveningUpdates.Add(new(m, plan, reason));
    }

    private void EndEvening(EveningPlan plan, EveningStage stage, int m, string reason)
    {
        RecordEvening(plan with { Stage = stage, FinishedAt = m }, m, reason);
        _openEvenings.Remove(plan.Id);
        foreach (string holder in new[] { plan.Asker, plan.Asked })
        {
            if (_eveningPerson.GetValueOrDefault(holder) == plan.Id) _eveningPerson.Remove(holder);
            Person person = _people[_index[holder]];
            if (person.Why == "evening-plan") { person.Why = ""; person.GoalUntil = m; }
        }
    }

    private string[] EveningCues(string partner, string place) => CompanyCues(partner, place)
        .Append("wish:activity:PlayedGame").Append("activity:Reading").Append("situation:evening").ToArray();

    private void LearnEvening(string holder, string id, int m, string kind, IReadOnlyList<ExpectationInput> expectations,
        double felt = 0, MemoryObject? situation = null, int? observedMinutes = null, bool? sawPartner = null,
        double? expectedAtAgreement = null, double? investment = null)
    {
        var associations = felt == 0 || situation is null ? Array.Empty<AssociationInput>()
            : new[] { new AssociationInput(situation, felt, new[] { "situation:evening", "activity:PlayedGame" }) };
        _livesMemory[holder].Learn(new(_livesSequence[holder]++, m, $"evening:{id}:{kind}", KnowledgeKind.Lived, associations, expectations));
        if (felt != 0) AddMood(_index[holder], felt, -1);
        if (_evenings.Record) _eveningOutcomes.Add(new(id, m, holder, kind, felt, observedMinutes, sawPartner, expectedAtAgreement, investment));
    }
}
