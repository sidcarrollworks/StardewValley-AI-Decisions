namespace UnderGlass.Sim.Lives;

/// <summary>Experimental shared-home evening plans. Times and strengths are ours, not canon.</summary>
public sealed record EveningOptions
{
    public bool Enabled { get; init; }
    public bool Record { get; init; } = true;
    public int StartMinute { get; init; } = 18 * 60;
    public int EndMinute { get; init; } = 20 * 60;
    public int TravelMinutes { get; init; } = 60;
    public double PlanBase { get; init; } = .1;
    public double Intended { get; init; } = .2;
    public double Disappointment { get; init; } = .05;
    public double InvestmentScale { get; init; } = .25;
    public int ModelCallsPerDay { get; init; } = 2;
    public string Model { get; init; } = "laya";
    public IReadOnlyList<string> Participants { get; init; } = Array.Empty<string>();
    public bool PassiveExpectations { get; init; } = true;

    public EveningOptions CopyValidated()
    {
        if (StartMinute is < 720 or >= 1440 || EndMinute <= StartMinute || EndMinute > 1440
            || TravelMinutes is < 1 or > 180 || ModelCallsPerDay is < 0 or > 2
            || !double.IsFinite(PlanBase) || PlanBase is <= 0 or > 1000
            || !double.IsFinite(Intended) || Intended is < 0 or > 1
            || !double.IsFinite(Disappointment) || Disappointment is < 0 or > 1
            || !double.IsFinite(InvestmentScale) || InvestmentScale is <= 0 or > 1
            || string.IsNullOrWhiteSpace(Model) || Model.Length > 128
            || Participants is null || Participants.Any(string.IsNullOrWhiteSpace)
            || Participants.Distinct(StringComparer.Ordinal).Count() != Participants.Count)
            throw new ArgumentException("Invalid evening settings.");
        return this with { Participants = Array.AsReadOnly(Participants.Order(StringComparer.Ordinal).ToArray()) };
    }
}

public enum EveningStage { Considering, Pending, Agreed, Playing, Completed, Interrupted, Missed, Unavailable, NotNow, Refused }
public sealed record EveningPlan(string Id, string Asker, string Asked, string Place, int OfferedAt,
    int Start, int End, EveningStage Stage, int? AnswerAt = null, int? ActId = null, int? FinishedAt = null);
public sealed record EveningUpdate(int At, EveningPlan Plan, string Reason);
public sealed record EveningChoice(string Phase, string? PlanId, ChoiceDecision Decision,
    ChoiceComparison? Comparison, LivesDecisionRecord? Evaluation = null, string? Context = null);
public sealed record EveningOutcome(string PlanId, int At, string Holder, string Kind, double Felt = 0,
    int? ObservedMinutes = null, bool? SawPartner = null, double? ExpectedAtAgreement = null, double? Investment = null);

public static class EveningPolicy
{
    public static ExpectationKey AnswerKey(string partner) => new(ExpectationKind.Answers, partner, "evening:PlayedGame");
    public static ExpectationKey AvailabilityKey(string partner) => new(ExpectationKind.Availability, partner, "evening:PlayedGame");
    public static ExpectationKey FollowKey(string partner, string place) => new(ExpectationKind.FollowThrough, partner, "PlayedGame:" + place + ":evening");
    public static MemoryObject Situation(string partner, string place) => new(MemoryObjectKind.Situation, "evening:PlayedGame:" + partner + ":" + place);
}

/// <summary>Observer-only counts of offer gates; not a character belief or a choice.</summary>
public sealed record EveningGate(int Day, string Holder, string Partner, string Reason, int FirstAt, int LastAt, int Checks);
