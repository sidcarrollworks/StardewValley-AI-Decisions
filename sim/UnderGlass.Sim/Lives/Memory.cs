namespace UnderGlass.Sim.Lives;

/// <summary>Experimental settings for the personal-memory kernel, not global simulation settings.</summary>
public sealed record MemoryOptions(double SeedStrength = 0.05, double NightlyRetention = 0.975,
    int AssociationsPerObject = 3, int AssociationsPerHolder = 96, int LivedEntriesPerExpectation = 6,
    int RecallCount = 3, int DeriveAfter = 3)
{
    internal void Validate()
    {
        if (!double.IsFinite(SeedStrength) || SeedStrength is < 0 or > 1
            || !double.IsFinite(NightlyRetention) || NightlyRetention is < 0 or > 1
            || AssociationsPerObject < 2 || AssociationsPerHolder < AssociationsPerObject
            || LivedEntriesPerExpectation < 1 || RecallCount < 1 || DeriveAfter < 1)
            throw new ArgumentException("Invalid personal-memory bounds or strengths.");
    }
}

public sealed record AssociationInput(MemoryObject Object, double Amount, IReadOnlyList<string> Cues,
    double PersonalSignificance = 0);
public sealed record ExpectationInput(ExpectationKey Key, bool Success, IReadOnlyList<string> Cues);

/// <summary>One holder's first acquisition of a cause, in monotonically increasing acquisition order.
/// Sequence identifies that acquisition permanently; retries must reuse it. Submit all projections
/// together. The future perception adapter, not a model, owns these receipts and classifications.</summary>
public sealed record MemoryExperience(long Sequence, int KnownAt, string CauseId, KnowledgeKind How,
    IReadOnlyList<AssociationInput> Associations, IReadOnlyList<ExpectationInput> Expectations,
    string? Teller = null, double Credence = 1);

public sealed record AssociationEntry(string Id, MemoryObject Object, string CauseId, double Amount,
    int KnownAt, KnowledgeKind How, string? Teller, bool Seed, double PersonalSignificance,
    IReadOnlyList<string> Cues, double Credence = 1);
public sealed record ExpectationEntry(string CauseId, int KnownAt, bool Success, KnowledgeKind How,
    string? Teller, bool Seed, IReadOnlyList<string>? Cues = null, double Credence = 1);
public sealed record ExpectationSnapshot(ExpectationKey Key, double Chance,
    IReadOnlyList<ExpectationEntry> Entries, bool? Summary, IReadOnlyList<string> SummaryEvidence);
public sealed record MemorySnapshot(string Holder, IReadOnlyList<AssociationEntry> Associations,
    IReadOnlyList<ExpectationSnapshot> Expectations);

/// <summary>Both sides, never netted; aliases of one cause count once, with their routes retained.</summary>
public sealed record CauseContribution(string CauseId, double Warm, double Cold, IReadOnlyList<string> Routes);
public sealed record MemorySides(double Warm, double Cold, IReadOnlyList<CauseContribution> Causes);

public enum RecallKind { Account, Reading, Association, Expectation }
public sealed record RecalledMemory(string Id, string Holder, RecallKind Kind, string Text,
    bool PreRun, int KnownAt, KnowledgeKind How, string? Teller, int Overlap, double Strength,
    double Warm, double Cold, IReadOnlyList<string> Causes, IReadOnlyList<string> Sources);
public sealed record RecallSnapshot(string Holder, int At, IReadOnlyList<string> Cues,
    bool LowMood, IReadOnlyList<RecalledMemory> Items);

/// <summary>Before/after receipts are copies. This is not the versioned Lives recording contract.</summary>
public sealed record LivesMemoryChange(string Holder, int At, string Reason, string? CauseId,
    MemorySnapshot Before, MemorySnapshot After, IReadOnlyList<string> DroppedIds,
    IReadOnlyList<AssociationEntry> AddedAssociations);
public interface ILivesSink
{
    void Changed(LivesMemoryChange change);
    void Recalled(RecallSnapshot recall);
}

/// <summary>
/// The engine's per-holder memory kernel. No Simulation hook, model call, mood/regard mutation or
/// observer-data reader. It can be exercised independently before choices and actions are wired.
/// Active associations are bounded; expectation windows have a fixed size per key. Original
/// pre-run accounts/readings are immutable. Recall uses a cue index, not the event archive.
/// </summary>
public sealed class PersonalMemory
{
    private sealed record Candidate(string Id, RecallKind Kind, string Text, bool PreRun, int KnownAt,
        KnowledgeKind How, string? Teller, double Strength, double Warm, double Cold, double Significance,
        IReadOnlyList<string> Causes, IReadOnlyList<string> Sources, IReadOnlyList<HistoryCue> Cues);

    private readonly string _holder;
    private readonly MemoryOptions _options;
    private readonly ILivesSink? _sink;
    private readonly List<AssociationEntry> _associations = new();
    private readonly Dictionary<ExpectationKey, List<ExpectationEntry>> _expectations = new();
    private readonly Dictionary<MemoryObject, int> _livedCounts = new();
    private readonly Dictionary<string, MemoryObject> _seedObjects = new(StringComparer.Ordinal);
    // Minimal acquisition receipts grow with known causes, separately from bounded active memory.
    // O(1) lookup prevents a forgotten/retracted cause being learned twice, even under a new sequence.
    // They also let reattribution undo dilution after an association has left the active window.
    private sealed record Receipt(long Sequence, IReadOnlyList<MemoryObject> Objects, bool Retracted = false);
    private readonly Dictionary<string, Receipt> _receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Candidate> _history = new(StringComparer.Ordinal);
    // The holder's own dependency graph includes audit-only readings so ablation has explicit,
    // stable identities without exposing another holder's accounts of a shared fact.
    private readonly Dictionary<string, string?> _historyParents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Candidate> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _cueIndex = new(StringComparer.Ordinal);
    private long _sequence = -1;
    private int _knownAt = -1, _day;

    public PersonalMemory(string holder, HistoryPack pack, MemoryOptions? options = null, ILivesSink? sink = null)
    {
        Name(holder, nameof(holder));
        _holder = holder;
        _options = options ?? new MemoryOptions();
        _options.Validate();
        _sink = sink;
        ArgumentNullException.ThrowIfNull(pack);
        HistoryPack input = pack.CopyValidated();
        if (input.Accounts.Select(a => a.Id).Concat(input.Readings.Select(r => r.Id))
            .Any(id => id.StartsWith("association:", StringComparison.Ordinal) || id.StartsWith("expectation:", StringComparison.Ordinal)))
            throw new ArgumentException("Account/reading IDs must not use the runtime association: or expectation: namespaces.");
        MemorySnapshot before = Snapshot();
        var facts = input.Facts.ToDictionary(f => f.Id, StringComparer.Ordinal);
        foreach (HistoryAccount account in input.Accounts.Where(a => a.Holder == holder))
        {
            _historyParents.TryAdd(account.FactId, null);
            _historyParents.Add(account.Id, account.FactId);
            foreach (HistoryReading reading in input.Readings.Where(r => r.AccountId == account.Id))
                _historyParents.Add(reading.Id, account.Id);
            AddHistory(new(account.Id, RecallKind.Account, account.Text, true, -1, account.How, account.Teller,
                _options.SeedStrength, 0, 0, 0, Freeze(new[] { account.FactId }), account.Sources, account.Cues));
            foreach (HistoryReading reading in input.Readings.Where(r => r.AccountId == account.Id
                         && r.Review.Status == ReviewStatus.Reviewed))
            {
                // Authored interpretations can be recalled but never supply a numeric prior.
                bool seeds = reading.Origin == ReadingOrigin.CanonStated
                    && (facts[account.FactId].Kind != HistoryKind.Past || reading.PresentLinkSource is not null);
                double amount = seeds ? reading.Stance switch
                {
                    ReadingStance.Warm => _options.SeedStrength,
                    ReadingStance.Wary => -_options.SeedStrength,
                    _ => 0,
                } : 0;
                IReadOnlyList<HistoryCue> cues = Freeze(reading.Cues.Append(new HistoryCue(ObjectCue(reading.Object)))
                    .GroupBy(c => c.Key, StringComparer.Ordinal).Select(g => g.OrderByDescending(c => c.SourceId is not null).First()));
                AddHistory(new(reading.Id, RecallKind.Reading, reading.Text, true, -1, KnowledgeKind.Reading, null,
                    _options.SeedStrength, Math.Max(0, amount), Math.Max(0, -amount), 0,
                    Freeze(new[] { account.Id, reading.Id }), reading.Sources, cues));
                if (amount != 0)
                {
                    _seedObjects[reading.Id] = reading.Object;
                    // Different readings of one experience retain separate routes, one causal contribution.
                    _associations.Add(new(AssociationId(reading.Id, reading.Object), reading.Object, account.Id,
                        amount, -1, KnowledgeKind.Reading, null, true, 0, CueKeys(cues)));
                }
                if (seeds && reading.Stance == ReadingStance.Expects)
                {
                    ExpectationKey key = reading.Expectation!;
                    _expectations[key] = new() { new(reading.Id, -1, reading.ExpectedSuccess!.Value,
                        KnowledgeKind.Reading, null, true, CueKeys(cues)) };
                }
            }
        }
        var added = Freeze(_associations);
        var trimmed = Trim();
        RebuildIndex();
        Emit(before, -1, "initialize", null, trimmed, added);
    }

    // A decision frame owns its mutable collections but shares the validated immutable records.
    // Acquisition receipts are intentionally absent: queries never need the run-long retry ledger.
    private PersonalMemory(PersonalMemory source)
    {
        _holder = source._holder;
        _options = source._options;
        _sink = null;
        _sequence = source._sequence;
        _knownAt = source._knownAt;
        _day = source._day;
        _associations.AddRange(source._associations);
        foreach (var (key, entries) in source._expectations) _expectations.Add(key, new(entries));
        foreach (var (key, value) in source._livedCounts) _livedCounts.Add(key, value);
        foreach (var (key, value) in source._seedObjects) _seedObjects.Add(key, value);
        foreach (var (key, value) in source._history) _history.Add(key, value);
        foreach (var (key, value) in source._historyParents) _historyParents.Add(key, value);
    }

    public string Holder => _holder;
    public int ActiveAssociationCount => _associations.Count;
    public int AcquisitionReceiptCount => _receipts.Count;

    /// <summary>Freeze the knowledge available for one decision or controlled comparison.
    /// Later learning, retraction and decay cannot change it. Capturing and querying are silent.</summary>
    public MemoryFrame Capture() => new(this);

    internal IReadOnlyList<string> FrameHistoryIds => Freeze(_historyParents.Keys.Order(StringComparer.Ordinal));

    internal PersonalMemory CopyForFrame(IEnumerable<string> removedHistory, out IReadOnlyList<string> removedIds)
    {
        ArgumentNullException.ThrowIfNull(removedHistory);
        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in removedHistory)
        {
            if (id is null || !_historyParents.ContainsKey(id))
                throw new ArgumentException("History IDs must belong to the captured holder.", nameof(removedHistory));
            removed.Add(id);
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var (id, parent) in _historyParents)
                if (parent is not null && removed.Contains(parent)) changed |= removed.Add(id);
        } while (changed);

        var copy = new PersonalMemory(this);
        foreach (string id in removed)
        {
            copy._history.Remove(id);
            if (copy._seedObjects.Remove(id, out var obj))
                copy._associations.RemoveAll(a => a.Seed && a.Id == AssociationId(id, obj));
        }
        foreach (var (key, entries) in copy._expectations.ToArray())
        {
            entries.RemoveAll(e => e.Seed && removed.Contains(e.CauseId));
            if (entries.Count == 0) copy._expectations.Remove(key);
        }
        // Removal only withdraws admitted pre-run evidence. Lived entries, decay, and cumulative
        // dilution stay at the captured values; this is a same-choice comparison, not a rerun.
        copy.RebuildIndex();
        removedIds = Freeze(removed.Order(StringComparer.Ordinal));
        return copy;
    }

    /// <summary>All amounts in this snapshot include the cumulative dilution of initial readings.</summary>
    public MemorySnapshot Snapshot() => new(_holder,
        Freeze(_associations.OrderBy(a => a.Id, StringComparer.Ordinal).Select(Effective)),
        Freeze(_expectations.Keys.OrderBy(KeyId, StringComparer.Ordinal).Select(Expectation)));

    public ExpectationSnapshot Expectation(ExpectationKey key)
    {
        var entries = _expectations.GetValueOrDefault(key) ?? new List<ExpectationEntry>();
        var lived = entries.Where(e => !e.Seed).ToArray();
        bool? summary = lived.Length >= _options.DeriveAfter
            && lived.TakeLast(_options.DeriveAfter).Select(e => e.Success).Distinct().Count() == 1
            ? lived[^1].Success : null;
        return new(key, (entries.Count(e => e.Success) + 1.0) / (entries.Count + 1), Freeze(entries), summary,
            summary is null ? Array.Empty<string>() : Freeze(lived.TakeLast(_options.DeriveAfter).Select(e => e.CauseId)));
    }

    public MemorySides Sides(IEnumerable<MemoryObject> objects)
    {
        var wanted = objects.ToHashSet();
        var causes = _associations.Where(a => wanted.Contains(a.Object)).Select(Effective)
            .GroupBy(a => a.CauseId, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CauseContribution(g.Key, g.Max(a => Math.Max(0, a.Amount)),
                g.Max(a => Math.Max(0, -a.Amount)), Freeze(g.Select(a => a.Id).Order(StringComparer.Ordinal)))).ToArray();
        return new(causes.Sum(c => c.Warm), causes.Sum(c => c.Cold), Freeze(causes));
    }

    /// <summary>False means an acquisition receipt already processed or older than the watermark.
    /// A caller must present first acquisitions in sequence order and retain their original sequence
    /// when retrying, even after evidence leaves an active window. Cause identity is also checked
    /// against the receipt index, so a repeated cause cannot be resurrected under a new sequence.</summary>
    public bool Learn(MemoryExperience experience)
    {
        ArgumentNullException.ThrowIfNull(experience);
        if (experience.Sequence < 0) throw new ArgumentException("Acquisition sequence must be nonnegative.");
        if (experience.Sequence <= _sequence) return false;
        Validate(experience); // atomic: nothing is applied until every projection is valid
        if (_receipts.ContainsKey(experience.CauseId)) return false;
        MemorySnapshot before = Snapshot();
        Apply(experience, before, "learn");
        return true;
    }

    /// <summary>Replace the holder's current reading of an already acquired cause, for example
    /// after attribution is corrected. The acquisition stays one experience, with one receipt;
    /// its previous dilution is undone even if its projections have left the active window.
    /// A newer explicit correction may re-establish a retracted cause; an ordinary Learn retry
    /// cannot. Corrections do not restore unrelated evicted evidence. A zero-amount correction
    /// projection retains the acquisition's dilution/provenance without restoring forgotten feeling.</summary>
    public bool Correct(MemoryExperience experience)
    {
        ArgumentNullException.ThrowIfNull(experience);
        if (experience.Sequence < 0) throw new ArgumentException("Acquisition sequence must be nonnegative.");
        if (experience.Sequence <= _sequence) return false;
        Validate(experience, allowZero: true);
        if (!_receipts.TryGetValue(experience.CauseId, out var receipt))
            throw new ArgumentException("Only an acquired cause can be corrected.", nameof(experience));
        MemorySnapshot before = Snapshot();
        _associations.RemoveAll(a => !a.Seed && a.CauseId == experience.CauseId);
        foreach (var entries in _expectations.Values) entries.RemoveAll(e => !e.Seed && e.CauseId == experience.CauseId);
        if (!receipt.Retracted)
            foreach (MemoryObject obj in receipt.Objects) _livedCounts[obj]--;
        Apply(experience, before, "correct");
        return true;
    }

    private void Apply(MemoryExperience experience, MemorySnapshot before, string reason)
    {
        _sequence = experience.Sequence;
        _knownAt = experience.KnownAt;
        _receipts[experience.CauseId] = new(experience.Sequence, Freeze(experience.Associations.Select(a => a.Object)));
        var added = new List<AssociationEntry>();
        foreach (AssociationInput input in experience.Associations)
        {
            _livedCounts[input.Object] = _livedCounts.GetValueOrDefault(input.Object) + 1;
            // A forgotten cause can be reattributed without reviving its feeling or undoing
            // its historical dilution of a seed. Its object remains on the acquisition receipt.
            if (input.Amount == 0) continue;
            // Told amounts are supplied already felt (including hearsay/credence), not discounted twice.
            var entry = new AssociationEntry(AssociationId(experience.CauseId, input.Object), input.Object, experience.CauseId,
                input.Amount, experience.KnownAt, experience.How, experience.Teller, false, input.PersonalSignificance,
                Freeze(input.Cues.Append(ObjectCue(input.Object)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)), experience.Credence);
            _associations.Add(entry);
            added.Add(entry);
        }
        foreach (ExpectationInput input in experience.Expectations)
        {
            if (!_expectations.TryGetValue(input.Key, out var entries)) _expectations[input.Key] = entries = new();
            entries.Add(new(experience.CauseId, experience.KnownAt, input.Success, experience.How, experience.Teller, false,
                Freeze(input.Cues.Append("person:" + input.Key.Subject).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)), experience.Credence));
            while (entries.Count(e => !e.Seed) > _options.LivedEntriesPerExpectation)
                entries.RemoveAt(entries.FindIndex(e => !e.Seed));
        }
        var trimmed = Trim();
        RebuildIndex();
        Emit(before, experience.KnownAt, reason, experience.CauseId, trimmed, added);
    }

    /// <summary>Apply each elapsed night once. Source readings do not fade or regain authority.</summary>
    public void AdvanceToDay(int day)
    {
        if (day < _day) throw new ArgumentException("Memory cannot advance backwards; rewind uses recorded snapshots.");
        if (day == _day) return;
        int at = checked(day * Clock.MinutesPerDay);
        if (at < _knownAt) throw new ArgumentException("Night boundary precedes current evidence.");
        MemorySnapshot before = Snapshot();
        double factor = Math.Pow(_options.NightlyRetention, day - _day);
        for (int i = 0; i < _associations.Count; i++)
            if (!_associations[i].Seed) _associations[i] = _associations[i] with { Amount = _associations[i].Amount * factor };
        _day = day;
        var trimmed = Trim();
        RebuildIndex();
        Emit(before, at, "decay", null, trimmed);
    }

    /// <summary>Withdraw an active cause after corrected attribution. This does not erase original
    /// history or acquisition receipts, restore evicted evidence, or make a retry a new experience.</summary>
    public void Retract(string causeId, int at)
    {
        Name(causeId, nameof(causeId));
        CheckTime(at);
        if (!_receipts.TryGetValue(causeId, out var receipt) || receipt.Retracted) return;
        MemorySnapshot before = Snapshot();
        _associations.RemoveAll(a => !a.Seed && a.CauseId == causeId);
        foreach (var entries in _expectations.Values) entries.RemoveAll(e => !e.Seed && e.CauseId == causeId);
        foreach (MemoryObject obj in receipt.Objects) _livedCounts[obj]--;
        _receipts[causeId] = receipt with { Retracted = true };
        _knownAt = at;
        RebuildIndex();
        Emit(before, at, "retract", causeId);
    }

    public RecallSnapshot Recall(IEnumerable<string> cues, int at, bool lowMood = false)
    {
        CheckTime(at);
        var keys = cues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (string key in keys) Name(key, "recall cue");
        var candidates = keys.Where(_cueIndex.ContainsKey).SelectMany(k => _cueIndex[k]).Distinct(StringComparer.Ordinal)
            .Select(id => _candidates[id]).Where(c => c.KnownAt <= at)
            .Select(c => (Item: c, Overlap: c.Cues.Where(k => keys.Contains(k.Key, StringComparer.Ordinal))
                .GroupBy(k => k.Key, StringComparer.Ordinal).Sum(g => g.Any(k => k.SourceId is not null) ? 2 : 1)))
            .OrderByDescending(x => x.Overlap).ThenByDescending(x => x.Item.Strength * (1 + x.Item.Significance))
            .ThenBy(x => x.Item.Id, StringComparer.Ordinal).ToList();
        var chosen = candidates.Take(_options.RecallCount).ToList();
        var prior = candidates.FirstOrDefault(c => c.Item.PreRun);
        if (prior.Item is not null && chosen.All(c => !c.Item.PreRun))
        {
            if (chosen.Count == _options.RecallCount) chosen.RemoveAt(chosen.Count - 1);
            chosen.Add(prior);
        }
        if (lowMood)
        {
            // Additional comfort comes from the matched pool, not an unrelated archive scan.
            var extra = candidates.Where(c => c.Item.Warm > 0 && chosen.All(x => x.Item.Id != c.Item.Id))
                .OrderByDescending(c => c.Item.Warm).ThenBy(c => c.Item.Id, StringComparer.Ordinal).FirstOrDefault();
            if (extra.Item is not null) chosen.Add(extra);
        }
        var result = new RecallSnapshot(_holder, at, Freeze(keys), lowMood, Freeze(chosen.Select(x => new RecalledMemory(
            x.Item.Id, _holder, x.Item.Kind, x.Item.Text, x.Item.PreRun, x.Item.KnownAt, x.Item.How, x.Item.Teller,
            x.Overlap, x.Item.Strength, x.Item.Warm, x.Item.Cold, x.Item.Causes, x.Item.Sources))));
        _sink?.Recalled(result);
        return result;
    }

    private AssociationEntry Effective(AssociationEntry entry) => entry.Seed
        ? entry with { Amount = entry.Amount / (1 + _livedCounts.GetValueOrDefault(entry.Object)) } : entry;

    private IReadOnlyList<string> Trim()
    {
        var before = _associations.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        // Exact forgetting (retention zero or floating-point underflow) has no third sign to protect.
        _associations.RemoveAll(a => a.Amount == 0);
        foreach (var group in _associations.GroupBy(a => a.Object).ToArray())
        {
            var ranked = group.OrderByDescending(a => Math.Abs(Effective(a).Amount))
                .ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();
            var keep = ranked.GroupBy(a => Math.Sign(a.Amount)).Select(g => g.First()).ToList();
            keep.AddRange(ranked.Where(a => !keep.Contains(a)).Take(Math.Max(0, _options.AssociationsPerObject - keep.Count)));
            _associations.RemoveAll(a => a.Object == group.Key && !keep.Contains(a));
        }
        while (_associations.Count > _options.AssociationsPerHolder)
        {
            // Evict a whole least-strong object, so global pressure cannot erase just its cold side.
            MemoryObject drop = _associations.GroupBy(a => a.Object)
                .OrderBy(g => g.Sum(a => Math.Abs(Effective(a).Amount)))
                .ThenBy(g => ObjectCue(g.Key), StringComparer.Ordinal).First().Key;
            _associations.RemoveAll(a => a.Object == drop);
        }
        before.ExceptWith(_associations.Select(a => a.Id));
        return Freeze(before.Order(StringComparer.Ordinal));
    }

    private void AddHistory(Candidate item) => _history.Add(item.Id, item);

    private void RebuildIndex()
    {
        _candidates.Clear();
        _cueIndex.Clear();
        foreach (Candidate history in _history.Values)
        {
            // A retained reading's recall strength follows cumulative dilution as well.
            double scale = _seedObjects.TryGetValue(history.Id, out var obj)
                ? 1.0 / (1 + _livedCounts.GetValueOrDefault(obj)) : 1;
            Index(history with { Strength = history.Strength * scale, Warm = history.Warm * scale, Cold = history.Cold * scale });
        }
        foreach (AssociationEntry entry in _associations.Where(a => !a.Seed).Select(Effective))
            Index(new(entry.Id, RecallKind.Association, "", false, entry.KnownAt, entry.How, entry.Teller,
                Math.Abs(entry.Amount), Math.Max(0, entry.Amount), Math.Max(0, -entry.Amount), entry.PersonalSignificance,
                Freeze(new[] { entry.CauseId }), Array.Empty<string>(), Freeze(entry.Cues.Select(c => new HistoryCue(c)))));
        foreach (var key in _expectations.Keys)
        {
            ExpectationSnapshot snapshot = Expectation(key);
            if (snapshot.Entries.Count == 0) continue;
            ExpectationEntry last = snapshot.Entries[^1];
            // An estimate is the holder's reading of its full window, not the last entry's evidence.
            // Individual lived/witnessed/told provenance stays on the referenced snapshot entries.
            Index(new("expectation:" + KeyId(key), RecallKind.Expectation, "", snapshot.Entries.All(e => e.Seed),
                last.KnownAt, KnowledgeKind.Reading, null, 1, 0, 0, 0,
                Freeze(snapshot.Entries.Select(e => e.CauseId)), Array.Empty<string>(),
                Freeze(snapshot.Entries.SelectMany(e => e.Cues ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal)
                    .Select(c => new HistoryCue(c)))));
        }
    }

    private void Index(Candidate item)
    {
        _candidates.Add(item.Id, item);
        foreach (HistoryCue cue in item.Cues)
        {
            if (!_cueIndex.TryGetValue(cue.Key, out var ids)) _cueIndex[cue.Key] = ids = new(StringComparer.Ordinal);
            ids.Add(item.Id);
        }
    }

    private void Emit(MemorySnapshot before, int at, string reason, string? cause,
        IReadOnlyList<string>? trimmed = null, IReadOnlyList<AssociationEntry>? added = null)
    {
        if (_sink is null) return;
        MemorySnapshot after = Snapshot();
        var kept = after.Associations.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var dropped = before.Associations.Where(a => !kept.Contains(a.Id)).Select(a => a.Id).ToList();
        dropped.AddRange(trimmed ?? Array.Empty<string>());
        foreach (ExpectationSnapshot old in before.Expectations)
        {
            var remaining = after.Expectations.Single(e => e.Key == old.Key).Entries.Select(e => e.CauseId).ToHashSet(StringComparer.Ordinal);
            dropped.AddRange(old.Entries.Where(e => !remaining.Contains(e.CauseId)).Select(e => e.CauseId));
        }
        _sink.Changed(new(_holder, at, reason, cause, before, after,
            Freeze(dropped.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)), Freeze(added ?? Array.Empty<AssociationEntry>())));
    }

    private void Validate(MemoryExperience e, bool allowZero = false)
    {
        Name(e.CauseId, "cause");
        CheckTime(e.KnownAt);
        if (!Enum.IsDefined(e.How) || e.How is KnowledgeKind.Inferred or KnowledgeKind.Reading)
            throw new ArgumentException("Only perceived lived, witnessed or told outcomes can be learned.");
        if ((e.How == KnowledgeKind.Told) != (e.Teller is not null)) throw new ArgumentException("Told evidence requires its teller, other modes do not.");
        if (e.Teller is not null) Name(e.Teller, "teller");
        if (!double.IsFinite(e.Credence) || e.Credence is < 0 or > 1) throw new ArgumentException("Invalid credence.");
        if (e.Associations is null || e.Expectations is null) throw new ArgumentException("Missing experience projections.");
        if (e.Associations.Any(a => a is null) || e.Expectations.Any(x => x is null)) throw new ArgumentException("Null projection.");
        if (e.Associations.Select(a => a.Object).Distinct().Count() != e.Associations.Count
            || e.Expectations.Select(a => a.Key).Distinct().Count() != e.Expectations.Count)
            throw new ArgumentException("One cause may project to an object/key only once.");
        foreach (AssociationInput a in e.Associations)
        {
            if (a.Object is null || !Enum.IsDefined(a.Object.Kind)) throw new ArgumentException("Invalid association object.");
            Name(a.Object.Key, "object");
            if (!double.IsFinite(a.Amount) || a.Amount is < -1 or > 1 || !allowZero && a.Amount == 0
                || !double.IsFinite(a.PersonalSignificance) || a.PersonalSignificance is < 0 or > 1)
                throw new ArgumentException("Invalid felt amount/significance.");
            CheckCues(a.Cues);
        }
        foreach (ExpectationInput x in e.Expectations)
        {
            if (x.Key is null || !Enum.IsDefined(x.Key.Kind)) throw new ArgumentException("Invalid expectation key.");
            Name(x.Key.Subject, "expectation subject");
            Name(x.Key.Context, "expectation context");
            CheckCues(x.Cues);
        }
    }

    private void CheckTime(int at)
    {
        if (at < 0 || at < _knownAt || at < _day * Clock.MinutesPerDay)
            throw new ArgumentException("Memory time cannot move backwards; rewind uses recorded snapshots.");
        if (at / Clock.MinutesPerDay != _day)
            throw new ArgumentException("Advance to this day first, so elapsed decay precedes new evidence and recall.");
    }

    private static void CheckCues(IReadOnlyList<string> cues)
    {
        if (cues is null || cues.Count > 16) throw new ArgumentException("Provide at most 16 cue keys.");
        foreach (string cue in cues) Name(cue, "cue");
    }
    private static void Name(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl) || !LivesText.IsWellFormed(value))
            throw new ArgumentException("Invalid " + name + ".");
    }
    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());
    private static IReadOnlyList<string> CueKeys(IEnumerable<HistoryCue> cues) => Freeze(cues.Select(c => c.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    private static string ObjectCue(MemoryObject value) => value.Kind.ToString().ToLowerInvariant() + ":" + value.Key;
    private static string KeyId(ExpectationKey key) => key.Kind + ":" + Uri.EscapeDataString(key.Subject) + ":" + Uri.EscapeDataString(key.Context);
    private static string AssociationId(string cause, MemoryObject value) => "association:" + Uri.EscapeDataString(cause) + ":" + value.Kind + ":" + Uri.EscapeDataString(value.Key);
}
