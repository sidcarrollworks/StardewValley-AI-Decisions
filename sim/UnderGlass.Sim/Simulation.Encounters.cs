namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private readonly List<EncounterContext> _encounters = new();
    private readonly List<EncounterCause> _encounterCauses = new();
    private readonly List<EncounterAppraisal> _appraisals = new();
    private readonly Dictionary<string, List<EncounterHistory>> _encounterHistory = new(StringComparer.Ordinal);

    private void RecordEncounter(Act act, ActKind kind, EncounterCause? cause)
    {
        bool undergone = kind.Affect?.Patient == Patient.Actor;
        _encounters.Add(new(act.Id, act.Tick, act.Kind, act.Actor, act.Target,
            undergone ? "underwent" : "performed", act.Location, kind.DurationMinutes, act.About));
        if (cause is not null) _encounterCauses.Add(cause with { ActId = act.Id, Tick = act.Tick, Actor = act.Actor });
        // Self-knowledge has explicit roles: a person warned by an official did not choose the warning.
        AppraiseEncounter(new(act.Id, act.Tick, act.Actor, act.Kind, act.Actor, act.Target,
            undergone ? "underwent" : "performed", "self", 1, Array.Empty<string>()));
    }

    private void RecordPerceivedEncounter(string holder, Belief b, int m)
    {
        ActKind kind = _kindByName[b.Kind];
        string role = kind.Affect?.Patient == Patient.Actor && b.Actor == holder ? "underwent"
            : kind.Affect?.Patient != Patient.Actor && b.Target == holder ? "received" : "witness";
        AppraiseEncounter(new(b.ActId, m, holder, b.Kind, b.Actor, b.Target,
            role, b.Source.ToString(), b.Confidence, b.Chain.ToArray()));
    }

    private void AppraiseEncounter(EncounterPerception account)
    {
        if (!_encounterHistory.TryGetValue(account.Holder, out var history))
            _encounterHistory[account.Holder] = history = new();
        // A short history bounds cost; updates are distinct accounts, not extra repetitions.
        history.RemoveAll(h => account.Tick - h.Tick > 7 * Clock.MinutesPerDay || h.ActId == account.ActId);
        ActKind kind = _kindByName[account.Kind];
        string? other = account.Actor == account.Holder ? account.Target : account.Actor;
        bool pairKnown = other is not null && _index.ContainsKey(other) && other != account.Holder;
        bool sameHousehold = account.Actor is not null && account.Target is not null
            && _index.ContainsKey(account.Actor) && _index.ContainsKey(account.Target)
            && HouseholdOf(account.Actor) == HouseholdOf(account.Target);
        double? regard = pairKnown && _fo.Enabled ? E(_index[account.Holder], _index[other!]) : null;
        double familiarity = pairKnown ? Familiarity(account.Holder, other!) : 0;
        _appraisals.Add(EncounterAppraiser.Of(account, kind.Valence, kind.IsScandal, kind.Upheaval,
            sameHousehold, regard, familiarity, history));
        history.Add(new(account.ActId, account.Tick, account.Actor, account.Target, kind.Valence));
    }
}
