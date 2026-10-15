namespace UnderGlass.Sim.Lives;

/// <summary>Opt-in immediate company experiment. These are implementation settings, not canon
/// preferences or a future-evening plan. No model call or bedtime cost is introduced here.</summary>
public sealed record CompanyOptions
{
    public bool Enabled { get; init; }
    public bool Record { get; init; } = true;
    public double SeekBase { get; init; } = 0.1;
    public double OfferBase { get; init; } = 0.1;
    public int TravelMinutes { get; init; } = 120;
    public int SearchMinutes { get; init; } = 15;
    public int SightAgeMinutes { get; init; } = 4320;
    public int MaxCandidates { get; init; } = 3;
    public double AnswerScale { get; init; } = 5;

    public void Validate()
    {
        if (!double.IsFinite(SeekBase) || SeekBase <= 0 || !double.IsFinite(OfferBase) || OfferBase <= 0
            || TravelMinutes is < 1 or > Clock.MinutesPerDay || SearchMinutes is < 1 or > Clock.MinutesPerDay
            || SightAgeMinutes is < 1 or > 3 * Clock.MinutesPerDay || MaxCandidates is < 1 or > 3
            || !double.IsFinite(AnswerScale) || AnswerScale is < 0 or > 20)
            throw new ArgumentException("Invalid company settings.");
    }
}

public enum CompanyStage { Seeking, Searching, Pending, Playing, Completed, Interrupted, NotFound, Unavailable, NotNow, Refused, Aborted }
public sealed record CompanyEpisode(string Id, string Asker, string Asked, string Place, int StartedAt,
    int Deadline, CompanyStage Stage, int? AnswerAt = null, int? ActId = null, int? FinishedAt = null);
public sealed record CompanyUpdate(int At, CompanyEpisode Episode, string Reason);
public sealed record CompanyChoice(string Phase, string? EpisodeId, ChoiceDecision Decision, ChoiceComparison? Comparison);
public sealed record CompanyAnswerOption(string Id, string Line, double Probability);
public sealed record CompanyAnswer(string EpisodeId, int At, string Holder, double Margin, AskState State,
    double Unit, string SelectedId, IReadOnlyList<CompanyAnswerOption> Options);
public sealed record CompanyOutcome(string EpisodeId, int At, string Holder, string Kind, double Felt = 0);

/// <summary>The authored three-way answer to an immediate game offer. The caller supplies the
/// existing Ask margin and one stable draw; this policy neither looks at the world nor draws again.
/// Weights are exp(scale * margin), exp(-scale * margin), and, only with a temporary reason,
/// maxState * exp(scale * maxState). The last weight approaches zero as the reason disappears.</summary>
public static class CompanyPolicy
{
    public static CompanyAnswer Answer(string id, int at, string holder, double margin, AskState state,
        double unit, double scale = 5)
    {
        Name(id); Name(holder);
        if (at < 0) throw new ArgumentException("An answer needs a nonnegative time.");
        if (!double.IsFinite(margin)) throw new ArgumentException("The answer margin must be finite.");
        if (!double.IsFinite(scale) || scale is < 0 or > 20) throw new ArgumentException("Invalid answer scale.");
        if (!double.IsFinite(unit) || unit is < 0 or >= 1) throw new ArgumentException("The draw must lie in [0, 1).");
        UnitInterval(state.Tired); UnitInterval(state.Busy); UnitInterval(state.Low);
        var options = new List<(string Id, string Line, double Argument, double Prior)>
        {
            ("yes", "Let’s play.", margin, 1),
        };
        double temporaryReason = Math.Max(state.Tired, Math.Max(state.Busy, state.Low));
        if (temporaryReason > 0) options.Add(("not-now", "Not now. Maybe another time.", temporaryReason, temporaryReason));
        options.Add(("no", "No, I don’t want to play.", -margin, 1));

        // Subtract before scaling: huge finite margins still yield valid tails. A negative
        // overflow here means exp(-infinity)=0. Scale zero retains the temporary reason's prior.
        // Normalize the resulting logarithms again so a tiny temporary reason loses no precision.
        double maximum = options.Max(option => option.Argument);
        double[] logs = options.Select(option => Math.Log(option.Prior)
            + (scale == 0 ? 0 : scale * (option.Argument - maximum))).ToArray();
        double maxLog = logs.Max();
        double[] weights = logs.Select(log => Math.Exp(log - maxLog)).ToArray();
        double total = weights.Sum();
        var answers = Array.AsReadOnly(options.Select((option, i) =>
            new CompanyAnswerOption(option.Id, option.Line, weights[i] / total)).ToArray());
        string selected = answers.Last(option => option.Probability > 0).Id;
        double cumulative = 0;
        foreach (CompanyAnswerOption option in answers)
        {
            cumulative += option.Probability;
            if (unit < cumulative) { selected = option.Id; break; }
        }
        return new(id, at, holder, margin, state, unit, selected, answers);
    }

    public static ExpectationKey AnswerKey(string partner)
    { Name(partner); return new(ExpectationKind.Answers, partner, "company:PlayedGame"); }
    public static ExpectationKey AvailabilityKey(string partner)
    { Name(partner); return new(ExpectationKind.Availability, partner, "company:PlayedGame"); }
    public static ExpectationKey ActivityKey(string partner, string place)
    {
        Name(partner); Name(place);
        string context = "PlayedGame:" + place;
        Name(context);
        return new(ExpectationKind.Activity, partner, context);
    }

    /// <summary>Proposed coarse three-hour/day-type buckets, not a sourced schedule. A key
    /// represents the holder's evidence about this partner and place; it never locates them.</summary>
    public static ExpectationKey WhereaboutsKey(string partner, string place, int minute)
    {
        Name(partner); Name(place);
        if (minute < 0) throw new ArgumentException("A whereabouts key needs a nonnegative time.");
        int from = Clock.OfDay(minute) / 60 / 3 * 3;
        string dayType = Clock.Weekday(minute) >= 5 ? "weekend" : "weekday";
        string context = FormattableString.Invariant($"{place}:{from}-{from + 3}:{dayType}");
        Name(context);
        return new(ExpectationKind.Whereabouts, partner, context);
    }

    private static void UnitInterval(double value)
    { if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentException("Ask state must lie in [0, 1]."); }
    private static void Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid company identifier.");
        for (int i = 0; i < value.Length; i++)
            if (char.IsSurrogate(value[i]) && (!char.IsHighSurrogate(value[i]) || ++i >= value.Length || !char.IsLowSurrogate(value[i])))
                throw new ArgumentException("Malformed UTF-16 in company identifier.");
    }
}
