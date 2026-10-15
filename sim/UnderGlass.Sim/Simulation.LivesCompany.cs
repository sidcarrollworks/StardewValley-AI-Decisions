using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private CompanyOptions _companyOptions = new();
    private readonly Dictionary<string, CompanyEpisode> _companyEpisodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _companyByPerson = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), int> _companyConsidered = new();
    private readonly List<CompanyUpdate> _companyUpdates = new();
    private readonly List<CompanyChoice> _companyChoices = new();
    private readonly List<CompanyAnswer> _companyAnswers = new();
    private readonly List<CompanyOutcome> _companyOutcomes = new();
    private readonly HashSet<(string Holder, string Cause)> _companyLearned = new();

    public void ConfigureLivesCompany(CompanyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_livesRunStarted || _companyOptions.Enabled)
            throw new InvalidOperationException("Configure company once, before running.");
        options.Validate();
        if (options.Enabled && (!_livesRouting.Enabled || !Steering || !_fo.Acts.Company
            || !_fo.Acts.Invitations || _fo.Acts.Watch || !_kindByName.ContainsKey("PlayedGame")
            || !_kindByName.ContainsKey("Refused")))
            throw new ArgumentException("Company requires Lives routing and the active company and invitations catalog.");
        if (options.Enabled && _livesMemory.Values.Any(memory => memory.Capture().HistoryIds
            .Any(id => id.StartsWith("company:", StringComparison.Ordinal))))
            throw new ArgumentException("History IDs must not use the live company: cause namespace.");
        _companyOptions = options;
    }

    private bool WorkingNow(Person p, int m) => p.V.Job is { } job && job.WorksOn(Clock.Weekday(m))
        && Clock.OfDay(m) >= job.Start - (job.Commute ?? _bo.CommuteMinutes) && Clock.OfDay(m) < job.End;

    private bool CompanyFree(Person p, int m) => Free(p, m) && !WorkingNow(p, m)
        && p.Why is not ("bed" or "held") && !BodyTired(p, Clock.OfDay(m));

    // Only this perception gate reads the other person's actual position. It cannot locate
    // someone remotely, and a sleeping person can be seen without being available to ask.
    private bool CompanySees(Person p, Person other) => !p.Asleep && p.Place == other.Place
        && p.At.Chebyshev(other.At) <= _po.FarTiles
        && Perception.LineOfSight(_places[p.Place], p.At, other.At, _po) > 0;

    private bool CompanyFits(Person p, Person other, int m) => CompanyFree(p, m) && CompanyFree(other, m)
        && CompanySees(p, other) && p.At.Chebyshev(other.At) <= _po.NearTiles
        && p.V.Age >= 5 && other.V.Age >= 5
        && !InviteWaits(p, other, m)
        && (p.Place == p.V.Home && p.Place == other.V.Home && p.V.Household == other.V.Household
            || _kindByName["PlayedGame"].Allowed.Contains(p.Place)
                && KindFits(_kindByName["PlayedGame"], p, other, m, companyProposal: true));

    private static (string, string) CompanyPair(string a, string b)
        => StringComparer.Ordinal.Compare(a, b) < 0 ? (a, b) : (b, a);

    private bool CompanyConsidered(string a, string b, int m)
        => _companyConsidered.TryGetValue(CompanyPair(a, b), out int day) && day == Clock.Day(m);

    private static string[] CompanyCues(string partner, string place)
        => new[] { "person:" + partner, "place:" + place, "activity:PlayedGame" };

    private static MemoryObject[] CompanyObjects(string partner, string place)
        => new[] { new MemoryObject(MemoryObjectKind.Person, partner),
            new MemoryObject(MemoryObjectKind.Place, place), new MemoryObject(MemoryObjectKind.Activity, "PlayedGame") };

    private static MemoryObject[] CompanyOpeningObjects(string partner)
        => new[] { new MemoryObject(MemoryObjectKind.Person, partner), new MemoryObject(MemoryObjectKind.Activity, "PlayedGame") };

    private static ExpectationKey[] CompanyExpectations(string partner, string place)
        => new[] { CompanyPolicy.AnswerKey(partner), CompanyPolicy.AvailabilityKey(partner), CompanyPolicy.ActivityKey(partner, place) };

    private string? CompanyOpening(MemoryFrame frame, string partner, IReadOnlyList<string> cues, int m)
    {
        // A remembered warm cause concerning this person or this activity can suggest company.
        // A warm place alone does not manufacture a wish to play with everyone standing in it.
        var causes = frame.Sides(CompanyOpeningObjects(partner)).Causes
            .Where(c => c.Warm > 0).Select(c => c.CauseId).ToHashSet(StringComparer.Ordinal);
        return frame.Recall(cues, m, Mood(frame.Holder) < 0).Items
            .FirstOrDefault(item => item.Warm > 0 && item.Causes.Any(causes.Contains))?.Id;
    }

    private ChoiceDecision ChooseCompany(Person p, int m, string phase, string? episode,
        IReadOnlyList<string> cues, IReadOnlyList<ChoiceOption> options)
    {
        MemoryFrame frame = _livesMemory[p.V.Name].Capture();
        string choiceId = episode is null ? $"company:{phase}:{m}:{p.V.Name}" : $"company:{phase}:{episode}:{m}";
        var opportunity = new ChoiceOpportunity(choiceId, p.V.Name, m,
            phase, cues, options, CharacterOf(_index[p.V.Name]).Boldness, Mood(p.V.Name) < 0);
        ChoiceDecision choice = LivesChoices.Decide(frame, opportunity, _seed, _livesRouting.Choices);
        if (_companyOptions.Record)
        {
            string[] removed = _livesRouting.CompareHistory.Where(id => frame.HistoryIds.Contains(id, StringComparer.Ordinal)).ToArray();
            var comparison = removed.Length == 0 ? null : LivesChoices.Compare(frame, opportunity, removed, _seed, _livesRouting.Choices);
            string? linkedEpisode = episode ?? (phase == "seek" && choice.SelectedId is not (null or "carry-on")
                ? $"{m}:{p.V.Name}:{choice.SelectedId}" : null);
            _companyChoices.Add(new(phase, linkedEpisode, choice, comparison));
        }
        return choice;
    }

    private string? CompanyKnownPlace(Person p, string partner, int m)
    {
        if (_sightings.TryGetValue((p.V.Name, partner), out var spans))
        {
            var latest = spans.Where(s => s.To <= m && s.To >= m - _companyOptions.SightAgeMinutes)
                .OrderByDescending(s => s.To).FirstOrDefault();
            if (latest.Place is not null) return latest.Place;
        }
        Villager other = _cast[_index[partner]];
        return other.Home == p.V.Home && other.Household == p.V.Household && _places.ContainsKey(p.V.Home) ? p.V.Home : null;
    }

    private bool TrySeekCompany(Person p, int m, List<(Haunt? Haunt, double Weight)> routine)
    {
        if (!_companyOptions.Enabled || !CompanyFree(p, m) || _companyByPerson.ContainsKey(p.V.Name) || EveningReserved(p.V.Name) || p.Why == "evening-plan") return false;
        double carryBase = routine.Sum(o => o.Weight);
        if (!double.IsFinite(carryBase) || carryBase <= 0) return false;
        MemoryFrame frame = _livesMemory[p.V.Name].Capture();
        var candidates = new List<(string Partner, string Place)>();
        foreach (string partner in _names)
        {
            if (partner == p.V.Name || CompanyConsidered(p.V.Name, partner, m)
                || CompanySees(p, _people[_index[partner]])) continue;
            string? place = CompanyKnownPlace(p, partner, m);
            if (place is null || CompanyOpening(frame, partner, CompanyCues(partner, place), m) is null) continue;
            candidates.Add((partner, place));
            if (candidates.Count == _companyOptions.MaxCandidates) break;
        }
        if (candidates.Count == 0) return false;
        string[] cues = candidates.SelectMany(c => CompanyCues(c.Partner, c.Place)).Distinct(StringComparer.Ordinal).ToArray();
        var options = new List<ChoiceOption> { new("carry-on", "Continue with my usual plans.", carryBase,
            Array.Empty<MemoryObject>(), Array.Empty<ExpectationKey>()) };
        foreach (var (partner, place) in candidates)
            if (CompanyOpening(frame, partner, cues, m) is { } opening)
                options.Add(new(partner, $"Look for {partner} at {place} and ask about a game.", _companyOptions.SeekBase,
                    CompanyObjects(partner, place), CompanyExpectations(partner, place)
                        .Append(CompanyPolicy.WhereaboutsKey(partner, place, m)).ToArray(), WarmRecallObjects: CompanyOpeningObjects(partner)));
        if (options.Count == 1) return false;
        ChoiceDecision choice = ChooseCompany(p, m, "seek", null, cues, options);
        if (choice.SelectedId is null or "carry-on") return false;
        var selected = candidates.Single(c => c.Partner == choice.SelectedId);
        var episode = new CompanyEpisode($"{m}:{p.V.Name}:{selected.Partner}", p.V.Name, selected.Partner,
            selected.Place, m, m + _companyOptions.TravelMinutes, CompanyStage.Seeking);
        _companyConsidered[CompanyPair(p.V.Name, selected.Partner)] = Clock.Day(m);
        SetCompanyEpisode(episode, m, "Set out using remembered whereabouts or a known shared home.");
        Goal(p, selected.Place, Middle(selected.Place), episode.Deadline, "company-seek", null);
        return true;
    }

    private bool ContinueCompanyGoal(Person p, int m, bool bedtimeCheckpoint)
    {
        if (!_companyOptions.Enabled || !_companyByPerson.TryGetValue(p.V.Name, out string? id)) return false;
        CompanyEpisode episode = _companyEpisodes[id];
        if (episode.Asker != p.V.Name || episode.Stage is not (CompanyStage.Seeking or CompanyStage.Searching)) return false;
        if (bedtimeCheckpoint)
        {
            EndCompany(episode, CompanyStage.Aborted, m, "Reconsidered the search at the habitual bedtime.");
            return false;
        }
        return p.Why == "company-seek" && m <= episode.Deadline;
    }

    private void RunCompany(int m, bool tick)
    {
        if (!_companyOptions.Enabled) return;
        foreach (CompanyEpisode episode in _companyByPerson.Values.Distinct(StringComparer.Ordinal).Select(id => _companyEpisodes[id]).Where(e => e.Stage is CompanyStage.Seeking
                     or CompanyStage.Searching or CompanyStage.Pending).OrderBy(e => e.Id, StringComparer.Ordinal).ToArray())
        {
            if (episode.Stage == CompanyStage.Pending)
            {
                if (m >= episode.AnswerAt) AnswerCompany(episode, m);
            }
            else AdvanceCompanySearch(episode, m);
        }
        if (!tick) return;
        foreach (Person p in _people)
        {
            if (!CompanyFree(p, m) || _companyByPerson.ContainsKey(p.V.Name) || EveningReserved(p.V.Name) || p.Why == "evening-plan") continue;
            foreach (Person other in _people)
            {
                if (p == other || CompanyConsidered(p.V.Name, other.V.Name, m)
                    || _companyByPerson.ContainsKey(other.V.Name) || EveningReserved(other.V.Name) || other.Why == "evening-plan" || !CompanyFits(p, other, m)) continue;
                OfferCompany(p, other, m, null);
                if (_companyByPerson.ContainsKey(p.V.Name)) break;
            }
        }
    }

    private void AdvanceCompanySearch(CompanyEpisode episode, int m)
    {
        Person p = _people[_index[episode.Asker]], other = _people[_index[episode.Asked]];
        if (!CompanyFree(p, m) || p.Why != "company-seek")
        {
            EndCompany(episode, CompanyStage.Aborted, m, "The search gave way to another obligation or activity.");
            return;
        }
        if (CompanySees(p, other))
        {
            LearnCompany(p.V.Name, episode, m, "found", new[] {
                new ExpectationInput(CompanyPolicy.WhereaboutsKey(other.V.Name, p.Place, m), true, CompanyCues(other.V.Name, p.Place)) });
            if (!CompanyFree(other, m) || _companyByPerson.ContainsKey(other.V.Name))
            {
                // An internal intention or an upcoming duty is not evidence of availability.
                // Only visibly sleeping/collapsing or an ongoing physical act is learned here.
                if (other.Asleep || other.PendingCollapse || other.BusyUntil >= m)
                    LearnCompany(p.V.Name, episode, m, "unavailable", new[] {
                        new ExpectationInput(CompanyPolicy.AvailabilityKey(other.V.Name), false, CompanyCues(other.V.Name, p.Place)) });
                EndCompany(episode, CompanyStage.Unavailable, m, "Found them; the execution gate could not start an offer.");
                return;
            }
            if (CompanyFits(p, other, m)) { OfferCompany(p, other, m, episode); return; }
            // Approaching uses an actually seen position, never a remote target coordinate.
            if (p.At.Chebyshev(other.At) > _po.NearTiles && m < episode.Deadline)
            { Goal(p, p.Place, other.At, episode.Deadline, "company-seek", null); return; }
            EndCompany(episode, CompanyStage.Unavailable, m, "Found them, but a game did not fit here.");
            return;
        }
        if (episode.Stage == CompanyStage.Seeking && p.Place == episode.Place && p.At.Chebyshev(p.GoalSpot) <= 1)
        {
            SetCompanyEpisode(episode with { Stage = CompanyStage.Searching, Deadline = m + _companyOptions.SearchMinutes },
                m, "Arrived; looked around for a bounded time.");
            p.GoalUntil = m + _companyOptions.SearchMinutes;
            return;
        }
        if (m < episode.Deadline) return;
        if (episode.Stage == CompanyStage.Searching)
        {
            LearnCompany(p.V.Name, episode, m, "not-found", new[] {
                new ExpectationInput(CompanyPolicy.WhereaboutsKey(other.V.Name, episode.Place, m), false, CompanyCues(other.V.Name, episode.Place)) });
            EndCompany(episode, CompanyStage.NotFound, m, "Did not see them during the search window; their whereabouts remain unknown.");
        }
        else EndCompany(episode, CompanyStage.Aborted, m, "The travel window ended before the search began.");
    }

    private void OfferCompany(Person p, Person other, int m, CompanyEpisode? existing)
    {
        string[] cues = CompanyCues(other.V.Name, p.Place);
        string? opening = CompanyOpening(_livesMemory[p.V.Name].Capture(), other.V.Name, cues, m);
        if (opening is null)
        {
            if (existing is not null) EndCompany(existing, CompanyStage.Aborted, m, "No relevant warm memory came to mind for an offer.");
            return;
        }
        _companyConsidered[CompanyPair(p.V.Name, other.V.Name)] = Clock.Day(m);
        string id = existing?.Id ?? $"{m}:{p.V.Name}:{other.V.Name}";
        var options = new[] {
            new ChoiceOption("carry-on", "Carry on without asking.", 1, Array.Empty<MemoryObject>(), Array.Empty<ExpectationKey>()),
            new ChoiceOption("offer", $"Ask {other.V.Name} to play a game here.", _companyOptions.OfferBase,
                CompanyObjects(other.V.Name, p.Place), CompanyExpectations(other.V.Name, p.Place), WarmRecallObjects: CompanyOpeningObjects(other.V.Name)) };
        ChoiceDecision choice = ChooseCompany(p, m, "offer", id, cues, options);
        if (choice.SelectedId != "offer")
        {
            if (existing is not null) EndCompany(existing, CompanyStage.Aborted, m, "Chose to carry on without asking.");
            return;
        }
        var episode = existing ?? new CompanyEpisode(id, p.V.Name, other.V.Name, p.Place, m, m + 1, CompanyStage.Pending);
        SetCompanyEpisode(episode with { Stage = CompanyStage.Pending, Place = p.Place, AnswerAt = m + 1 }, m,
            "Offered a game; the other person answers on the next minute.");
    }

    private void AnswerCompany(CompanyEpisode episode, int m)
    {
        Person p = _people[_index[episode.Asker]], other = _people[_index[episode.Asked]];
        if (!CompanyFits(p, other, m))
        {
            EndCompany(episode, CompanyStage.Unavailable, m, "The encounter ended before an answer could be given.");
            return; // No unobserved refusal or explanation is learned.
        }
        CompanyAnswer answer = CompanyPolicy.Answer(episode.Id, m, other.V.Name,
            AskMargin(_index[other.V.Name], _index[p.V.Name], AskKind.Invitation, -1, m), AskStateOf(_index[other.V.Name], m),
            Rng.Unit(_seed, "company-answer", episode.Id), _companyOptions.AnswerScale);
        if (_companyOptions.Record) _companyAnswers.Add(answer);
        bool yes = answer.SelectedId == "yes", later = answer.SelectedId == "not-now";
        LearnCompany(p.V.Name, episode, m, answer.SelectedId, new[] { new ExpectationInput(
            later ? CompanyPolicy.AvailabilityKey(other.V.Name) : CompanyPolicy.AnswerKey(other.V.Name),
            yes, CompanyCues(other.V.Name, p.Place)) });
        if (yes)
        {
            LearnCompany(p.V.Name, episode, m, "available", new[] { new ExpectationInput(
                CompanyPolicy.AvailabilityKey(other.V.Name), true, CompanyCues(other.V.Name, p.Place)) });
            StartCompanyGame(episode, m);
            return;
        }
        if (later) { EndCompany(episode, CompanyStage.NotNow, m, "Asked to leave it for another time."); return; }
        var refused = episode with { ActId = _acts.Count };
        Begin(m, _kindByName["Refused"], other, injected: false, target: p.V.Name);
        EndCompany(refused, CompanyStage.Refused, m, "Declined the game.");
    }

    private void SetCompanyEpisode(CompanyEpisode episode, int m, string reason)
    {
        _companyEpisodes[episode.Id] = episode;
        _companyByPerson[episode.Asker] = episode.Id;
        if (episode.Stage is CompanyStage.Pending or CompanyStage.Playing) _companyByPerson[episode.Asked] = episode.Id;
        if (_companyOptions.Record) _companyUpdates.Add(new(m, episode, reason));
    }

    private void EndCompany(CompanyEpisode episode, CompanyStage stage, int m, string reason)
    {
        var ended = episode with { Stage = stage, FinishedAt = m };
        _companyEpisodes[episode.Id] = ended;
        foreach (string person in new[] { episode.Asker, episode.Asked })
            if (_companyByPerson.GetValueOrDefault(person) == episode.Id) _companyByPerson.Remove(person);
        Person asker = _people[_index[episode.Asker]];
        if (asker.Why == "company-seek") asker.Why = "";
        if (_companyOptions.Record) _companyUpdates.Add(new(m, ended, reason));
        SyncEveningGame(ended, stage, m, reason);
    }

    private void LearnCompany(string holder, CompanyEpisode episode, int m, string kind,
        IReadOnlyList<ExpectationInput> expectations, double felt = 0, string? place = null, string? partner = null)
    {
        string cause = $"company:{episode.Id}:{kind}";
        if (!_companyLearned.Add((holder, cause))) return;
        string whom = partner ?? (holder == episode.Asker ? episode.Asked : episode.Asker);
        string where = place ?? episode.Place;
        var associations = felt == 0 ? Array.Empty<AssociationInput>() : CompanyObjects(whom, where)
            .Select(obj => new AssociationInput(obj, Math.Clamp(felt, -1, 1), CompanyCues(whom, where))).ToArray();
        _livesMemory[holder].Learn(new(_livesSequence[holder]++, m, cause, KnowledgeKind.Lived,
            associations, expectations));
        if (_companyOptions.Record) _companyOutcomes.Add(new(episode.Id, m, holder, kind, felt));
    }
}
