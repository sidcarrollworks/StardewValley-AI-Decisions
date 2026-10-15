using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private LivesRoutingOptions _livesRouting = new();
    private HistoryPack? _livesHistory;
    private readonly Dictionary<string, PersonalMemory> _livesMemory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _livesSequence = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Holder, int Act), LearnedFeeling> _livesFelt = new();
    private readonly List<RoutingChoice> _livesChoices = new();
    private readonly List<RoutingSkipped> _livesSkipped = new();
    private readonly List<RoutingPerception> _livesPerceptions = new();
    private List<LivesMemoryChange> _livesChanges = new();
    private bool _livesRunStarted;

    private sealed record LearnedFeeling(double Amount, string? Person, string? Place,
        string Activity, KnowledgeKind How, string? Teller, double Credence,
        double IdentityConfidence = 1, double Clarity = 1, string Basis = "Event");
    private sealed class RoutingSink(List<LivesMemoryChange> changes) : ILivesSink
    {
        public void Changed(LivesMemoryChange change) => changes.Add(change);
        public void Recalled(RecallSnapshot recall) { } // decision receipts carry their own bounded recall
    }

    /// <summary>Explicit opt-in. No canon pack is loaded implicitly. Configure once, before Run.</summary>
    public void ConfigureLivesRouting(LivesRoutingOptions options, HistoryPack? history = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_livesRunStarted || _livesMemory.Count != 0)
            throw new InvalidOperationException("Configure Lives routing once, before running.");
        if (options.CompareHistory is null || options.Memory is null || options.Choices is null)
            throw new ArgumentException("Missing Lives routing settings.");
        if (!options.Enabled)
        {
            if (history is not null || options.CompareHistory.Count != 0)
                throw new ArgumentException("History and comparisons require enabled Lives routing.");
            _livesRouting = options;
            return;
        }
        HistoryPack pack = (history ?? new HistoryPack(1, Array.Empty<HistorySource>(), Array.Empty<HistoryFact>(),
            Array.Empty<HistoryAccount>(), Array.Empty<HistoryReading>())).CopyValidated();
        if (pack.Accounts.Any(a => !_index.ContainsKey(a.Holder)))
            throw new ArgumentException("History names a holder outside this cast.");
        var ownIds = pack.Facts.Select(f => f.Id).Concat(pack.Accounts.Select(a => a.Id)).Concat(pack.Readings.Select(r => r.Id))
            .ToHashSet(StringComparer.Ordinal);
        if (ownIds.Any(id => id.StartsWith("act:", StringComparison.Ordinal)))
            throw new ArgumentException("History record IDs must not use the live act: cause namespace.");
        if (options.CompareHistory.Any(id => !ownIds.Contains(id)))
            throw new ArgumentException("A comparison names an unknown history record.");
        options.Memory.Validate();
        options.Choices.Validate();
        // Build into temporary state: invalid input cannot partially configure a town.
        var changes = new List<LivesMemoryChange>();
        var sink = options.Record ? new RoutingSink(changes) : null;
        var memories = _names.ToDictionary(n => n, n => new PersonalMemory(n, pack, options.Memory, sink), StringComparer.Ordinal);
        _livesRouting = options with { CompareHistory = Array.AsReadOnly(options.CompareHistory.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()) };
        foreach (var pair in memories) { _livesMemory.Add(pair.Key, pair.Value); _livesSequence.Add(pair.Key, 0); }
        _livesChanges = changes;
        _livesHistory = pack;
    }

    /// <summary>Frozen observer/test view; routing never reads another person's view.</summary>
    public MemoryFrame? LivesMemoryOf(string holder) => _livesMemory.TryGetValue(holder, out var memory) ? memory.Capture() : null;

    private void AdvanceLivesDay(int day)
    {
        foreach (PersonalMemory memory in _livesMemory.Values) memory.AdvanceToDay(day);
    }

    /// <summary>Returns null to preserve the exact old draw when no relevant memory is recalled.</summary>
    private (ChoiceDecision Decision, ChoiceComparison? Comparison, string[] Ids)? LivesRoute(Person p, int m,
        List<(Haunt? Haunt, double Weight)> options)
    {
        if (!_livesRouting.Enabled) return null;
        if (options.Count > 5 || options.Any(o => !double.IsFinite(o.Weight) || o.Weight < 0) || options.Sum(o => o.Weight) <= 0)
        {
            if (_livesRouting.Record) _livesSkipped.Add(new(p.V.Name, m, "legacy-menu-outside-choice-bounds", options.Count));
            return null;
        }
        string Place(Haunt? h) => h is null || ReferenceEquals(h, BedtimeHabit) ? p.V.Home : h.Place;
        string[] ids = options.Select((o, i) => $"{i}:{OptionName(o.Haunt)}").ToArray();
        var choices = options.Select((o, i) => new ChoiceOption(ids[i],
            ReferenceEquals(o.Haunt, BedtimeHabit) ? "Go to bed." : o.Haunt is null ? "Spend time at home." : $"Spend time at {o.Haunt.Place}.",
            o.Weight, new[] { new MemoryObject(MemoryObjectKind.Place, Place(o.Haunt)) }, Array.Empty<ExpectationKey>())).ToArray();
        // Available destinations are already known routine options. No remote person or location is read.
        var cues = choices.SelectMany(c => c.Objects.Select(o => "place:" + o.Key))
            .Append("season:" + Calendar.SeasonNames[Calendar.Season(Clock.Day(m))])
            .Append("daytype:" + (Clock.Weekday(m) >= 5 ? "weekend" : "weekday"))
            .Append("person:" + p.V.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var opportunity = new ChoiceOpportunity($"route:{m}:{p.V.Name}", p.V.Name, m, "free-time", cues, choices,
            CharacterOf(_index[p.V.Name]).Boldness, Mood(p.V.Name) < 0);
        MemoryFrame frame = _livesMemory[p.V.Name].Capture();
        ChoiceDecision decision = LivesChoices.Decide(frame, opportunity, _seed, _livesRouting.Choices);
        ChoiceComparison? comparison = null;
        if (_livesRouting.Record)
        {
            string[] removed = _livesRouting.CompareHistory.Where(id => frame.HistoryIds.Contains(id, StringComparer.Ordinal)).ToArray();
            if (removed.Length > 0) comparison = LivesChoices.Compare(frame, opportunity, removed, _seed, _livesRouting.Choices);
        }
        return (decision, comparison, ids);
    }

    private void NoteLivesRoute(Person p, int m, Haunt? pick,
        (ChoiceDecision Decision, ChoiceComparison? Comparison, string[] Ids)? routing, int chosen)
    {
        if (!_livesRouting.Record || routing is not { } r) return;
        _livesChoices.Add(new(r.Decision, r.Comparison, r.Ids[chosen],
            pick is null || ReferenceEquals(pick, BedtimeHabit) ? p.V.Home : pick.Place, r.Decision.Legacy));
    }

    private void RetractLivesFeeling(string holder, int act, int at)
    {
        if (!_livesRouting.Enabled || !_livesFelt.Remove((holder, act))) return;
        _livesMemory[holder].Retract("act:" + act, at);
    }

    private readonly HashSet<(string Holder, int Act)> _livesAcquired = new();

    // Synchronize the holder's final felt reading, not observer appraisals, world truth or regard.
    private void RememberLivesFeeling(string holder, Belief belief, int cause, double amount, string basis, int at)
    {
        if (!_livesRouting.Enabled) return;
        if (belief.Source == Source.Found || belief.Kind == "Refused" || cause == _index[holder])
        {
            RetractLivesFeeling(holder, belief.ActId, at);
            return;
        }
        bool told = belief.Source == Source.Told;
        string? teller = told ? belief.Chain.FirstOrDefault() : null;
        if (told && teller is null) return; // no invented teller for incomplete legacy data
        double credence = told ? Math.Clamp(PresenceCredence(holder, teller!), 0, 1) : 1;
        string? place = told ? null : _acts[belief.ActId].Location; // only the witness perceived this place
        KnowledgeKind how = told ? KnowledgeKind.Told
            : belief.Target == holder ? KnowledgeKind.Lived : KnowledgeKind.Witnessed;
        SyncLivesFeeling(holder, belief.ActId, new(Math.Clamp(amount, -1, 1), cause >= 0 ? _names[cause] : null,
            place, belief.Kind, how, teller, credence, belief.Confidence, belief.Clarity, basis), at);
    }

    private double PresenceCredence(string holder, string teller)
        => _index.TryGetValue(teller, out int t)
            ? Feelings.Credence(_fam[_index[holder], t], E(_index[holder], t), U(_index[holder]), _fo) : 0.5;

    private void RememberLivesUndergone(Act act, Affect row, double amount, int at)
    {
        if (!_livesRouting.Enabled || act.Kind == "Refused") return;
        SyncLivesFeeling(act.Actor, act.Id, new(Math.Clamp(amount, -1, 1), row.Freedom > 0 ? act.Target : null,
            act.Location, act.Kind, KnowledgeKind.Lived, null, 1), at);
    }

    private void SyncLivesFeeling(string holder, int act, LearnedFeeling reading, int at)
    {
        var key = (holder, act);
        if (_livesFelt.TryGetValue(key, out var old) && (old == reading || old.How == KnowledgeKind.Lived && reading.How != KnowledgeKind.Lived)) return;
        if (reading.Amount == 0) { RetractLivesFeeling(holder, act, at); return; }
        PersonalMemory memory = _livesMemory[holder];
        double amount = reading.Amount;
        if (old is not null)
        {
            // rec.Mood is cumulative and never ages. A changed identity/provenance is not a
            // newly felt event: carry forward its retained, faded amount and add only the
            // actual new affect. All projections of this adapter's cause have equal amounts.
            // With no retained projection, a metadata-only correction must not revive it.
            double retained = memory.Snapshot().Associations.Where(a => !a.Seed && a.CauseId == "act:" + act)
                .OrderByDescending(a => Math.Abs(a.Amount)).Select(a => a.Amount).FirstOrDefault();
            amount = Math.Clamp(retained + reading.Amount - old.Amount, -1, 1);
        }
        var objects = new List<MemoryObject> { new(MemoryObjectKind.Activity, reading.Activity) };
        if (reading.Person is not null) objects.Add(new(MemoryObjectKind.Person, reading.Person));
        if (reading.Place is not null) objects.Add(new(MemoryObjectKind.Place, reading.Place));
        var cues = objects.Select(o => o.Kind.ToString().ToLowerInvariant() + ":" + o.Key).ToArray();
        var experience = new MemoryExperience(_livesSequence[holder]++, at, "act:" + act, reading.How,
            objects.Select(o => new AssociationInput(o, amount, cues)).ToArray(), Array.Empty<ExpectationInput>(), reading.Teller, reading.Credence);
        if (_livesAcquired.Contains(key)) memory.Correct(experience);
        else { memory.Learn(experience); _livesAcquired.Add(key); }
        _livesFelt[key] = reading;
        if (_livesRouting.Record) _livesPerceptions.Add(new(holder, at, act, reading.How,
            reading.IdentityConfidence, reading.Clarity, reading.Basis, reading.Teller, reading.Credence));
    }
}
