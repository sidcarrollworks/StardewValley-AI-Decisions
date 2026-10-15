namespace UnderGlass.Sim.Lives;

/// <summary>Experimental rule-5/6 settings. The assessment leaves TornAt's exact value open.</summary>
public sealed record ChoiceSettings(double Scale = 5, double TornAt = 0.025, double TopTwoFraction = 0.10)
{
    public void Validate()
    {
        if (!double.IsFinite(Scale) || Scale is <= 0 or > 100
            || !double.IsFinite(TornAt) || TornAt is < 0 or > 1
            || !double.IsFinite(TopTwoFraction) || TopTwoFraction is < 0 or > 1)
            throw new ArgumentException("Invalid choice settings.");
    }
}

/// <summary>The caller supplies physically possible options, in stable draw order. All required
/// recalls must actually occupy a bounded recall slot. A wish can open an option without warmth.
/// When WarmRecallObjects is supplied, at least one recalled warm cause must concern one of those
/// objects. This predicate is reevaluated for each comparison arm, so another memory may support it.</summary>
public sealed record ChoiceOption(string Id, string Line, double Base,
    IReadOnlyList<MemoryObject> Objects, IReadOnlyList<ExpectationKey> Expectations,
    IReadOnlyList<string>? RequiredRecallIds = null, double Intended = 0, double GoingCost = 0,
    IReadOnlyList<MemoryObject>? WarmRecallObjects = null);
public sealed record ChoiceExclusion(string Id, string Code, IReadOnlyList<string> Cites);
public sealed record ChoiceConstraint(string Code, double? Value, IReadOnlyList<string> Cites);
public sealed record ChoiceOpportunity(string Id, string Holder, int At, string Trigger,
    IReadOnlyList<string> Cues, IReadOnlyList<ChoiceOption> Options, double Boldness,
    bool LowMood = false, IReadOnlyList<string>? RequiredRecallIds = null,
    IReadOnlyList<ChoiceExclusion>? Excluded = null, IReadOnlyList<ChoiceConstraint>? Constraints = null);

public enum ChoiceReasonKind { Supports, Conflicts, Shown }
public enum ChoiceReasonVia { Recall, OptionSide, Expectation, State, Routine, Rule }
public sealed record ChoiceReason(ChoiceReasonKind Kind, ChoiceReasonVia Via, string Code,
    double? Value, IReadOnlyList<string> Cites);
public sealed record ChoiceAlternative(ChoiceOption Option, MemorySides Sides,
    IReadOnlyList<ExpectationSnapshot> Expectations, double Chance, double LogWeight,
    double Probability, IReadOnlyList<ChoiceReason> Reasons);

/// <summary>A detached engine receipt, not the v0.2 observer wire format. Legacy has no sampled
/// Lives selection: the simulation must use its original draw. No model is called here.</summary>
public sealed record ChoiceDecision(ChoiceOpportunity Opportunity, RecallSnapshot Recall,
    IReadOnlyList<ChoiceAlternative> Alternatives, IReadOnlyList<ChoiceExclusion> Excluded,
    IReadOnlyList<ChoiceConstraint> Constraints, double Unit, string? SelectedId, bool Legacy,
    bool Available, bool Torn, IReadOnlyList<string> TornReasons, ChoiceSettings Settings);
public sealed record ChoiceDifference(string OptionId, bool ReasonsChanged, double BeforeProbability,
    double AfterProbability);
public sealed record ChoiceComparison(IReadOnlyList<string> RemovedHistoryIds,
    ChoiceDecision WithHistory, ChoiceDecision WithoutHistory, bool Comparable,
    string? NonComparableReason, IReadOnlyList<ChoiceDifference> Differences,
    bool? ReasonsChanged, bool? ProbabilitiesChanged, bool? SelectionChanged,
    IReadOnlyList<string> RemovedDependencyIds);

/// <summary>One pure fallback for real choices and isolated history interventions. It consumes
/// only a frozen holder frame and explicit inputs; observer receipts never feed it.</summary>
public static class LivesChoices
{
    public static ChoiceDecision Decide(MemoryFrame frame, ChoiceOpportunity opportunity, long seed,
        ChoiceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        return Evaluate(frame, opportunity, Rng.Unit(seed, "lives-choice", opportunity.Id), settings);
    }

    public static ChoiceDecision Evaluate(MemoryFrame frame, ChoiceOpportunity opportunity, double unit,
        ChoiceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ChoiceOpportunity input = CopyValidated(opportunity);
        ChoiceSettings rules = settings ?? new();
        Validate(rules, unit);
        if (frame.Holder != input.Holder) throw new ArgumentException("The frame belongs to another holder.");
        RecallSnapshot recall = frame.Recall(input.Cues, input.At, input.LowMood);
        var recalledIds = recall.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var excluded = input.Excluded!.ToList();
        bool available = input.RequiredRecallIds!.All(recalledIds.Contains);
        bool legacy = available && recall.Items.Count == 0;
        if (!available || legacy)
            return new(input, recall, Array.Empty<ChoiceAlternative>(), Freeze(excluded), input.Constraints!,
                unit, null, legacy, available, false, Array.Empty<string>(), rules);

        var alternatives = new List<ChoiceAlternative>();
        foreach (ChoiceOption option in input.Options)
        {
            var missing = option.RequiredRecallIds!.Where(id => !recalledIds.Contains(id)).ToArray();
            if (missing.Length > 0 || option.Base == 0)
            {
                excluded.Add(new(option.Id, missing.Length > 0 ? "required-recall-missing" : "zero-base", Freeze(missing)));
                continue;
            }
            IReadOnlyList<string> warmRecall = Array.Empty<string>();
            if (option.WarmRecallObjects is { } warmObjects)
            {
                var causes = frame.Sides(warmObjects).Causes.Where(c => c.Warm > 0)
                    .Select(c => c.CauseId).ToHashSet(StringComparer.Ordinal);
                warmRecall = Freeze(recall.Items.Where(item => item.Warm > 0 && item.Causes.Any(causes.Contains))
                    .Select(item => item.Id));
                if (warmRecall.Count == 0)
                {
                    excluded.Add(new(option.Id, "missing-warm-recall", Array.Empty<string>()));
                    continue;
                }
            }
            MemorySides sides = frame.Sides(option.Objects);
            var expectations = Freeze(option.Expectations.Distinct().Select(frame.Expectation));
            double chance = expectations.Aggregate(1.0, (value, expectation) => value * expectation.Chance);
            double logWeight = Math.Log(option.Base) + expectations.Sum(e => Math.Log(e.Chance))
                + rules.Scale * (sides.Warm - sides.Cold * (1 - input.Boldness) + option.Intended - option.GoingCost);
            if (!double.IsFinite(logWeight)) throw new ArgumentException("Choice weight exceeds the finite numeric range.");
            var reasons = new List<ChoiceReason>
            {
                new(ChoiceReasonKind.Shown, ChoiceReasonVia.Routine, "base", option.Base, Array.Empty<string>()),
                new(ChoiceReasonKind.Shown, ChoiceReasonVia.State, "boldness", input.Boldness, Array.Empty<string>()),
            };
            if (warmRecall.Count > 0)
                reasons.Add(new(ChoiceReasonKind.Shown, ChoiceReasonVia.Rule, "warm-recall", null, warmRecall));
            foreach (CauseContribution cause in sides.Causes)
            {
                if (cause.Warm > 0) reasons.Add(new(ChoiceReasonKind.Supports, ChoiceReasonVia.OptionSide,
                    "warm", cause.Warm, cause.Routes));
                if (cause.Cold > 0) reasons.Add(new(ChoiceReasonKind.Conflicts, ChoiceReasonVia.OptionSide,
                    "cold", cause.Cold, cause.Routes));
            }
            foreach (ExpectationSnapshot expectation in expectations)
                reasons.Add(new(expectation.Chance < 1 ? ChoiceReasonKind.Conflicts : ChoiceReasonKind.Shown,
                    ChoiceReasonVia.Expectation, "chance:" + expectation.Key.Kind + ":" + expectation.Key.Subject
                        + ":" + expectation.Key.Context, expectation.Chance,
                    Freeze(expectation.Entries.Select(e => e.CauseId))));
            if (option.Intended > 0) reasons.Add(new(ChoiceReasonKind.Supports, ChoiceReasonVia.State,
                "intended", option.Intended, Array.Empty<string>()));
            if (option.GoingCost > 0) reasons.Add(new(ChoiceReasonKind.Conflicts, ChoiceReasonVia.State,
                "going-cost", option.GoingCost, Array.Empty<string>()));
            // Recall is shown context. Only Sides and specific expectation keys enter the number.
            foreach (RecalledMemory item in recall.Items)
                reasons.Add(new(ChoiceReasonKind.Shown, ChoiceReasonVia.Recall, "context-only", null, Freeze(new[] { item.Id })));
            alternatives.Add(new(option, sides, expectations, chance, logWeight, 0, Freeze(reasons)));
        }
        if (alternatives.Count == 0)
            return new(input, recall, Array.Empty<ChoiceAlternative>(), Freeze(excluded), input.Constraints!,
                unit, null, false, false, false, Array.Empty<string>(), rules);

        // Subtracting the largest logarithm avoids exp overflow, including enormous base weights.
        double maximum = alternatives.Max(a => a.LogWeight);
        double total = alternatives.Sum(a => Math.Exp(a.LogWeight - maximum));
        for (int i = 0; i < alternatives.Count; i++)
            alternatives[i] = alternatives[i] with { Probability = Math.Exp(alternatives[i].LogWeight - maximum) / total };
        // A trailing weight can underflow to zero. Rounding in the cumulative sum must never
        // select that zero-probability option when the unit is the last representable draw.
        string selected = alternatives.Last(a => a.Probability > 0).Option.Id;
        double cumulative = 0;
        foreach (ChoiceAlternative alternative in alternatives)
        {
            cumulative += alternative.Probability;
            if (unit < cumulative) { selected = alternative.Option.Id; break; }
        }
        var torn = new List<string>();
        foreach (ChoiceAlternative alternative in alternatives)
        {
            if (alternative.Sides.Warm > rules.TornAt && alternative.Sides.Cold > rules.TornAt)
                torn.Add("mixed-sides:" + alternative.Option.Id);
            if (alternative.Sides.Warm > rules.TornAt && alternative.Chance < 0.5)
                torn.Add("warm-but-unlikely:" + alternative.Option.Id);
        }
        var ranked = alternatives.OrderByDescending(a => a.Probability).ToArray();
        if (ranked.Length > 1 && ranked[1].Probability >= ranked[0].Probability * (1 - rules.TopTwoFraction))
            torn.Add("close-alternatives");
        return new(input, recall, Freeze(alternatives), Freeze(excluded), input.Constraints!, unit, selected,
            false, true, torn.Count > 0, Freeze(torn), rules);
    }

    public static ChoiceComparison Compare(MemoryFrame frame, ChoiceOpportunity opportunity,
        IEnumerable<string> removedHistoryIds, long seed, ChoiceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        return CompareAtUnit(frame, opportunity, removedHistoryIds,
            Rng.Unit(seed, "lives-choice", opportunity.Id), settings);
    }

    /// <summary>The same immutable scene, candidate identities/order, personal state and random
    /// unit are held constant. Recall, admission, reasons and probabilities are recomputed. This
    /// does not simulate later lives or counterfactual model answers.</summary>
    public static ChoiceComparison CompareAtUnit(MemoryFrame frame, ChoiceOpportunity opportunity,
        IEnumerable<string> removedHistoryIds, double unit, ChoiceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(removedHistoryIds);
        var removed = Freeze(removedHistoryIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        MemoryFrame intervention = frame.WithoutHistory(removed);
        ChoiceDecision before = Evaluate(frame, opportunity, unit, settings);
        ChoiceDecision after = Evaluate(intervention, before.Opportunity, unit, settings);
        string? problem = !before.Available || !after.Available ? "opportunity-unavailable"
            : before.Legacy || after.Legacy ? "legacy-draw-required"
            : !before.Alternatives.Select(a => a.Option.Id).SequenceEqual(after.Alternatives.Select(a => a.Option.Id), StringComparer.Ordinal)
                ? "eligible-alternatives-changed" : null;
        if (problem is not null)
            return new(removed, before, after, false, problem, Array.Empty<ChoiceDifference>(), null, null, null,
                intervention.RemovedIds);
        var differences = before.Alternatives.Zip(after.Alternatives, (a, b) => new ChoiceDifference(a.Option.Id,
            !SameReasons(a.Reasons, b.Reasons), a.Probability, b.Probability)).ToArray();
        return new(removed, before, after, true, null, Freeze(differences), differences.Any(d => d.ReasonsChanged),
            differences.Any(d => d.BeforeProbability != d.AfterProbability), before.SelectedId != after.SelectedId,
            intervention.RemovedIds);
    }

    private static bool SameReasons(IReadOnlyList<ChoiceReason> a, IReadOnlyList<ChoiceReason> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First.Kind == pair.Second.Kind && pair.First.Via == pair.Second.Via
            && pair.First.Code == pair.Second.Code && pair.First.Value == pair.Second.Value
            && pair.First.Cites.SequenceEqual(pair.Second.Cites, StringComparer.Ordinal));

    private static ChoiceOpportunity CopyValidated(ChoiceOpportunity opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        Name(opportunity.Id); Name(opportunity.Holder); Name(opportunity.Trigger);
        if (opportunity.At < 0) throw new ArgumentException("A choice needs a nonnegative time.");
        UnitInterval(opportunity.Boldness);
        var cues = Names(opportunity.Cues, 128);
        var required = Names(opportunity.RequiredRecallIds ?? Array.Empty<string>(), 128);
        if (opportunity.Options is null || opportunity.Options.Count is < 1 or > 5 || opportunity.Options.Any(o => o is null))
            throw new ArgumentException("A choice needs one to five options.");
        var options = new List<ChoiceOption>();
        foreach (ChoiceOption option in opportunity.Options)
        {
            Name(option.Id); Name(option.Line, 2000);
            if (!double.IsFinite(option.Base) || option.Base < 0) throw new ArgumentException("Invalid base weight.");
            UnitInterval(option.Intended); UnitInterval(option.GoingCost);
            if (option.Objects is null || option.Objects.Count > 32 || option.Objects.Any(o => o is null)
                || option.WarmRecallObjects is { } warm && (warm.Count is < 1 or > 32 || warm.Any(o => o is null))
                || option.Expectations is null || option.Expectations.Count > 16 || option.Expectations.Any(e => e is null))
                throw new ArgumentException("Invalid option objects or expectations.");
            foreach (MemoryObject obj in option.Objects.Concat(option.WarmRecallObjects ?? Array.Empty<MemoryObject>()))
            { if (!Enum.IsDefined(obj.Kind)) throw new ArgumentException("Unknown object kind."); Name(obj.Key); }
            foreach (ExpectationKey key in option.Expectations)
            { if (!Enum.IsDefined(key.Kind)) throw new ArgumentException("Unknown expectation kind."); Name(key.Subject); Name(key.Context); }
            options.Add(option with { Objects = Freeze(option.Objects.Distinct()), Expectations = Freeze(option.Expectations.Distinct()),
                RequiredRecallIds = Names(option.RequiredRecallIds ?? Array.Empty<string>(), 128),
                WarmRecallObjects = option.WarmRecallObjects is null ? null : Freeze(option.WarmRecallObjects.Distinct()) });
        }
        if (options.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != options.Count)
            throw new ArgumentException("Alternative IDs must be unique.");
        var excluded = new List<ChoiceExclusion>();
        if (opportunity.Excluded?.Count > 128) throw new ArgumentException("Too many exclusions.");
        foreach (ChoiceExclusion item in opportunity.Excluded ?? Array.Empty<ChoiceExclusion>())
        {
            if (item is null) throw new ArgumentException("Null exclusion.");
            Name(item.Id); Name(item.Code); excluded.Add(item with { Cites = Names(item.Cites, 128) });
        }
        if (options.Select(o => o.Id).Concat(excluded.Select(e => e.Id)).Distinct(StringComparer.Ordinal).Count() != options.Count + excluded.Count)
            throw new ArgumentException("Eligible and excluded alternative IDs must be distinct.");
        var constraints = new List<ChoiceConstraint>();
        if (opportunity.Constraints?.Count > 128) throw new ArgumentException("Too many constraints.");
        foreach (ChoiceConstraint item in opportunity.Constraints ?? Array.Empty<ChoiceConstraint>())
        {
            if (item is null) throw new ArgumentException("Null constraint.");
            Name(item.Code);
            if (item.Value is double value && !double.IsFinite(value)) throw new ArgumentException("Invalid constraint value.");
            constraints.Add(item with { Cites = Names(item.Cites, 128) });
        }
        return opportunity with { Cues = cues, RequiredRecallIds = required, Options = Freeze(options),
            Excluded = Freeze(excluded), Constraints = Freeze(constraints) };
    }

    private static void Validate(ChoiceSettings rules, double unit)
    {
        rules.Validate();
        if (!double.IsFinite(unit) || unit is < 0 or >= 1) throw new ArgumentException("The draw must lie in [0, 1).");
    }
    private static void UnitInterval(double value)
    { if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentException("Expected a value in [0, 1]."); }
    private static void Name(string value, int maximum = 200)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Invalid choice name or text.");
        for (int i = 0; i < value.Length; i++)
            if (char.IsSurrogate(value[i]) && (!char.IsHighSurrogate(value[i]) || ++i >= value.Length || !char.IsLowSurrogate(value[i])))
                throw new ArgumentException("Malformed UTF-16 in choice text.");
    }
    private static IReadOnlyList<string> Names(IReadOnlyList<string> values, int limit)
    {
        if (values is null || values.Count > limit) throw new ArgumentException("Invalid choice references.");
        foreach (string value in values) Name(value);
        return Freeze(values.Distinct(StringComparer.Ordinal));
    }
    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}
