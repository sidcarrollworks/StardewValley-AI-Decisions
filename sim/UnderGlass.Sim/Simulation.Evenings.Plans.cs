using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private readonly Dictionary<string, string> _eveningGames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Partner, double Margin, string Item, int At)> _eveningAlternatives = new(StringComparer.Ordinal);

    private void EveningAfterRefusal(Person holder, Person asker, Good good, double margin, int m)
    {
        if (_evenings.Enabled && EveningParticipant(holder.V.Name) && EveningParticipant(asker.V.Name)
            && margin > 0 && WontFetch(_index[holder.V.Name], good))
            _eveningAlternatives[holder.V.Name] = (asker.V.Name, margin, good.Name, m);
    }

    private string? EveningWish(Person p, IReadOnlyList<string> cues, int m)
    {
        if (_livesHistory is null) return null;
        var accounts = _livesHistory.Accounts.Where(a => a.Holder == p.V.Name).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var wishes = _livesHistory.Readings.Where(r => accounts.Contains(r.AccountId)
            && r.Review.Status == ReviewStatus.Reviewed && r.Stance == ReadingStance.None
            && r.Object == new MemoryObject(MemoryObjectKind.Activity, "PlayedGame")
            && r.Cues.Any(c => c.Key == "wish:activity:PlayedGame")).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        return _livesMemory[p.V.Name].Capture().Recall(cues, m, Mood(p.V.Name) < 0).Items.FirstOrDefault(r => wishes.Contains(r.Id))?.Id;
    }

    private bool EveningEncounter(Person p, Person other, int m) => CompanyFree(p, m) && CompanyFree(other, m)
        && p.V.Age >= 5 && other.V.Age >= 5
        && CompanySees(p, other) && p.At.Chebyshev(other.At) <= _po.NearTiles
        && !_companyByPerson.ContainsKey(p.V.Name) && !_companyByPerson.ContainsKey(other.V.Name);

    private void RunEvenings(int m, bool tick)
    {
        if (!_evenings.Enabled) return;
        foreach (string id in _openEvenings.Order(StringComparer.Ordinal).ToArray())
        {
            EveningPlan plan = _eveningPlans[id];
            if (plan.Stage == EveningStage.Pending && !_eveningQuestionHolders.Contains(plan.Asked)) AnswerEveningPlan(plan, m);
            else if (plan.Stage == EveningStage.Agreed) AdvanceEveningPlan(plan, m);
        }
        RunEveningExpectations(m, tick);
        if (!tick || Clock.OfDay(m) >= _evenings.StartMinute - _evenings.TravelMinutes) return;
        foreach (var entry in _eveningAlternatives.ToArray())
        {
            Person holder = _people[_index[entry.Key]], partner = _people[_index[entry.Value.Partner]];
            if (m - entry.Value.At > 30) { _eveningAlternatives.Remove(entry.Key); continue; }
            if (_eveningPerson.ContainsKey(entry.Key) || EveningReserved(entry.Key) || _eveningPerson.ContainsKey(partner.V.Name)
                || EveningReserved(partner.V.Name) || holder.V.Home != partner.V.Home || holder.V.Household != partner.V.Household
                || !EveningEncounter(holder, partner, m)) continue;
            _eveningAlternatives.Remove(entry.Key);
            OfferEveningPlan(holder, partner, m, entry.Value.Margin, entry.Value.Item);
        }
        foreach (Person p in _people)
        {
            if (!EveningParticipant(p.V.Name)) continue;
            foreach (Person other in _people)
            {
                if (p == other || !EveningParticipant(other.V.Name) || p.V.Home != other.V.Home
                    || p.V.Household != other.V.Household || !_places.ContainsKey(p.V.Home)) continue;
                string? gate = EveningOfferGate(p, other, m);
                if (gate is not null) { CountEveningGate(p, other, m, gate); continue; }
                OfferEveningPlan(p, other, m);
                if (EveningReserved(p.V.Name)) break;
            }
        }
    }

    // Observer diagnostics aggregate the first blocking gate per directed pair/day. These
    // counters are never read by decisions and expose live availability only to observers.
    private readonly Dictionary<(int Day, string Holder, string Partner, string Reason), EveningGate> _eveningGates = new();
    private void CountEveningGate(Person p, Person other, int m, string reason)
    {
        if (!_evenings.Record) return;
        var key = (Clock.Day(m), p.V.Name, other.V.Name, reason);
        _eveningGates[key] = _eveningGates.TryGetValue(key, out var old)
            ? old with { LastAt = m, Checks = old.Checks + 1 }
            : new(key.Item1, key.Item2, key.Item3, reason, m, m, 1);
    }

    private string? EveningOfferGate(Person p, Person other, int m)
    {
        if (!CompanyFree(p, m)) return "self-unavailable";
        if (_eveningPerson.ContainsKey(p.V.Name) || EveningReserved(p.V.Name)) return "self-plan-active";
        if (_eveningPerson.ContainsKey(other.V.Name) || EveningReserved(other.V.Name)) return "partner-plan-active";
        if (_eveningConsidered.GetValueOrDefault(CompanyPair(p.V.Name, other.V.Name), -1) == Clock.Day(m)) return "already-considered-today";
        if (!CompanyFree(other, m)) return "partner-unavailable";
        if (p.V.Age < 5 || other.V.Age < 5) return "age";
        if (!CompanySees(p, other) || p.At.Chebyshev(other.At) > _po.NearTiles) return "no-local-encounter";
        if (_companyByPerson.ContainsKey(p.V.Name) || _companyByPerson.ContainsKey(other.V.Name)) return "company-active";
        return null;
    }

    private void OfferEveningPlan(Person p, Person other, int m, double? refusedMargin = null, string? refusedItem = null)
    {
        int midnight = m - Clock.OfDay(m), start = midnight + _evenings.StartMinute;
        // A proposed window is ours, not a sourced schedule. Habits never forbid an offer.
        int end = midnight + _evenings.EndMinute;
        if (end - start < _kindByName["PlayedGame"].DurationMinutes) return;
        string[] cues = EveningCues(other.V.Name, p.V.Home);
        string? warm = CompanyOpening(_livesMemory[p.V.Name].Capture(), other.V.Name, cues, m);
        string? wish = EveningWish(p, cues, m);
        if (warm is null && wish is null) { CountEveningGate(p, other, m, "no-recalled-opening"); return; }
        CountEveningGate(p, other, m, "considered");
        _eveningConsidered[CompanyPair(p.V.Name, other.V.Name)] = Clock.Day(m);
        var plan = new EveningPlan($"{m}:{p.V.Name}:{other.V.Name}", p.V.Name, other.V.Name, p.V.Home, m, start, end, EveningStage.Considering);
        var options = new[] {
            new ChoiceOption("carry-on", "Leave this evening open.", 1,
                new[] { new MemoryObject(MemoryObjectKind.Activity, "Reading") }, Array.Empty<ExpectationKey>()),
            new ChoiceOption("offer", $"Ask {other.V.Name} to play a game at home this evening.", refusedMargin is { } margin ? Math.Min(margin, 1000) : _evenings.PlanBase,
                CompanyOpeningObjects(other.V.Name), new[] { EveningPolicy.AnswerKey(other.V.Name), EveningPolicy.AvailabilityKey(other.V.Name), EveningPolicy.FollowKey(other.V.Name, p.V.Home),
                    CompanyPolicy.WhereaboutsKey(other.V.Name, p.V.Home, start) },
                RequiredRecallIds: warm is null ? new[] { wish! } : null,
                WarmRecallObjects: warm is null ? null : CompanyOpeningObjects(other.V.Name)) };
        AskEvening(p, m, refusedMargin is null ? "offer" : "commitment-refusal", plan.Id,
            (refusedItem is null ? "" : $"I declined to fetch {refusedItem}; I will not provide it. I can still offer company. ")
            + $"{other.V.Name} is here. Consider a game at our shared home from {Clock.OfDay(start)/60}:{Clock.OfDay(start)%60:00} to {Clock.OfDay(end)/60}:{Clock.OfDay(end)%60:00} today.", cues, options,
            (selected, at) => {
                if (selected != "offer") return;
                if (!EveningEncounter(p, other, at) || _eveningPerson.ContainsKey(p.V.Name) || _eveningPerson.ContainsKey(other.V.Name))
                { RecordEvening(plan with { Stage = EveningStage.Unavailable, FinishedAt = at }, at, "The encounter ended before an offer could be made."); return; }
                _openEvenings.Add(plan.Id); _eveningPerson[p.V.Name] = plan.Id; _eveningPerson[other.V.Name] = plan.Id;
                RecordEvening(plan with { Stage = EveningStage.Pending }, at, "Offered an evening together; awaiting an answer.");
            }, refusedMargin is null ? null : new[] { new ChoiceConstraint("refused-despite-positive-margin", refusedMargin, Array.Empty<string>()) });
    }

    private void AnswerEveningPlan(EveningPlan plan, int m)
    {
        Person p = _people[_index[plan.Asker]], other = _people[_index[plan.Asked]];
        if (!EveningEncounter(p, other, m)) { EndEvening(plan, EveningStage.Unavailable, m, "The encounter ended before an answer."); return; }
        var answer = CompanyPolicy.Answer(plan.Id, m, other.V.Name,
            AskMargin(_index[other.V.Name], _index[p.V.Name], AskKind.Invitation, -1, m), AskStateOf(_index[other.V.Name], m), 0, _companyOptions.AnswerScale);
        var alternatives = EveningOtherPlaces(other, plan.Start);
        var options = answer.Options.Select(a => new ChoiceOption(a.Id,
            a.Id == "yes" ? "Yes, let's play at home this evening." : a.Id == "no" ? "No, I don't want to plan that." : "Not now. Ask me another time.",
            a.Probability, a.Id == "yes" ? CompanyOpeningObjects(p.V.Name) : alternatives, Array.Empty<ExpectationKey>())).ToArray();
        AskEvening(other, m, "answer", plan.Id, $"{p.V.Name} has asked me to play a game at our shared home this evening.",
            EveningCues(p.V.Name, plan.Place).Concat(alternatives.Select(o => "place:" + o.Key)).ToArray(), options,
            (selected, at) => {
                if (!EveningEncounter(p, other, at)) { EndEvening(plan, EveningStage.Unavailable, at, "No answer was heard before the encounter ended."); return; }
                bool yes = selected == "yes", later = selected == "not-now";
                LearnEvening(p.V.Name, plan.Id, at, selected, new[] { new ExpectationInput(later ? EveningPolicy.AvailabilityKey(other.V.Name) : EveningPolicy.AnswerKey(other.V.Name), yes, EveningCues(other.V.Name, plan.Place)) });
                if (!yes) { EndEvening(plan, later ? EveningStage.NotNow : EveningStage.Refused, at, later ? "Asked to leave the decision for another time." : "Declined the plan."); return; }
                LearnEvening(p.V.Name, plan.Id, at, "available", new[] { new ExpectationInput(EveningPolicy.AvailabilityKey(other.V.Name), true, EveningCues(other.V.Name, plan.Place)) });
                foreach (string holder in new[] { plan.Asker, plan.Asked })
                {
                    string partner = holder == plan.Asker ? plan.Asked : plan.Asker;
                    MemoryFrame frame = _livesMemory[holder].Capture();
                    double expected = frame.Expectation(EveningPolicy.FollowKey(partner, plan.Place)).Chance
                        * frame.Expectation(CompanyPolicy.WhereaboutsKey(partner, plan.Place, plan.Start)).Chance;
                    double investment = Math.Clamp((_evenings.Intended + frame.Sides(CompanyOpeningObjects(partner)).Warm) / _evenings.InvestmentScale, 0, 1);
                    _eveningInvestment[(plan.Id, holder)] = (expected, investment);
                }
                RecordEvening(plan with { Stage = EveningStage.Agreed, AnswerAt = at }, at, "Both agreed; each can reconsider when the evening comes.");
            });
    }

    private MemoryObject[] EveningOtherPlaces(Person p, int m) => p.V.Haunts.Where(h => h.Open(Clock.OfDay(m)) && h.Place != p.V.Home)
        .Select(h => h.Place).Concat(_gatherings.Where(g => g.On(m) && Admits(g, p, m) && !NeverPicks(p, g) && g.Place != p.V.Home).Select(g => g.Place))
        .Distinct(StringComparer.Ordinal).Select(place => new MemoryObject(MemoryObjectKind.Place, place)).ToArray();

    private bool ContinueEveningGoal(Person p, int m, bool checkpoint)
    {
        if (!_evenings.Enabled || p.Why != "evening-plan") return false;
        if (checkpoint || !_eveningPerson.TryGetValue(p.V.Name, out string? id) || _eveningPlans[id].End <= m)
        { p.Why = ""; return false; }
        return true;
    }

    private void AdvanceEveningPlan(EveningPlan plan, int m)
    {
        if (m >= plan.End) { MissEveningPlan(plan, m); return; }
        foreach (string holder in new[] { plan.Asker, plan.Asked }.Order(StringComparer.Ordinal))
        {
            Person p = _people[_index[holder]];
            string partner = holder == plan.Asker ? plan.Asked : plan.Asker;
            if (m >= plan.Start && !p.Asleep && p.Place == plan.Place)
            {
                _eveningPresence[(plan.Id, holder)] = _eveningPresence.GetValueOrDefault((plan.Id, holder)) + 1;
                if (CompanySees(p, _people[_index[partner]])) _eveningSeen.Add((plan.Id, holder));
            }
            if (m < plan.Start - _evenings.TravelMinutes || !CompanyFree(p, m) || EveningReserved(holder) || !_eveningPicks.Add((plan.Id, holder))) continue;
            MemoryObject[] otherPlaces = EveningOtherPlaces(p, m);
            var options = new[] {
                new ChoiceOption("keep", $"Go home for the game with {partner}.", 1,
                    CompanyObjects(partner, plan.Place).Append(EveningPolicy.Situation(partner, plan.Place)).ToArray(),
                    new[] { EveningPolicy.FollowKey(partner, plan.Place), CompanyPolicy.ActivityKey(partner, plan.Place) }, Intended: _evenings.Intended),
                new ChoiceOption("carry-on", "Carry on with my other plans.", 1, otherPlaces, Array.Empty<ExpectationKey>()) };
            AskEvening(p, m, "follow-through", plan.Id, $"I agreed to a game with {partner} at home this evening. I can go or carry on with my own plans.",
                EveningCues(partner, plan.Place).Concat(otherPlaces.Select(o => "place:" + o.Key)).ToArray(), options,
                (selected, at) => {
                    if (_eveningPlans[plan.Id].Stage != EveningStage.Agreed || at >= plan.End) return;
                    bool keep = selected == "keep"; _eveningKept[(plan.Id, holder)] = keep;
                    if (keep && CompanyFree(p, at)) Goal(p, plan.Place, Middle(plan.Place), plan.End, "evening-plan", null);
                    // Work and the body still take precedence; no route is forced by an agreement.
                });
        }
        Person asker = _people[_index[plan.Asker]], asked = _people[_index[plan.Asked]];
        if (m < plan.Start || m + _kindByName["PlayedGame"].DurationMinutes > plan.End
            || !_eveningKept.GetValueOrDefault((plan.Id, plan.Asker)) || !_eveningKept.GetValueOrDefault((plan.Id, plan.Asked))
            || asker.Place != plan.Place || asked.Place != plan.Place || !EveningEncounter(asker, asked, m)) return;
        string episodeId = "evening:" + plan.Id;
        _eveningGames.Add(episodeId, plan.Id);
        StartCompanyGame(new(episodeId, plan.Asker, plan.Asked, plan.Place, m, plan.End, CompanyStage.Playing), m);
        RecordEvening(plan with { Stage = EveningStage.Playing, ActId = _companyEpisodes[episodeId].ActId }, m, "They began the agreed game.");
    }

    private void MissEveningPlan(EveningPlan plan, int m)
    {
        foreach (string holder in new[] { plan.Asker, plan.Asked }.Order(StringComparer.Ordinal))
        {
            string partner = holder == plan.Asker ? plan.Asked : plan.Asker;
            if (!_eveningKept.GetValueOrDefault((plan.Id, holder)))
            { if (_evenings.Record) _eveningOutcomes.Add(new(plan.Id, m, holder, "chose-other-plans")); continue; }
            if (_eveningPresence.GetValueOrDefault((plan.Id, holder)) < Math.Min(15, plan.End - plan.Start))
            { if (_evenings.Record) _eveningOutcomes.Add(new(plan.Id, m, holder, "did-not-witness-window")); continue; }
            var value = _eveningInvestment[(plan.Id, holder)];
            double felt = -_evenings.Disappointment * value.Expected * value.Investment;
            bool saw = _eveningSeen.Contains((plan.Id, holder));
            LearnEvening(holder, plan.Id, m, saw ? "evening-did-not-happen" : "not-seen-during-plan",
                saw ? Array.Empty<ExpectationInput>() : new[] { new ExpectationInput(EveningPolicy.FollowKey(partner, plan.Place), false, EveningCues(partner, plan.Place)) },
                felt, EveningPolicy.Situation(partner, plan.Place), _eveningPresence.GetValueOrDefault((plan.Id, holder)), saw,
                value.Expected, value.Investment);
        }
        EndEvening(plan, EveningStage.Missed, m, "The agreed window ended without a shared game; each account is limited to what that person observed.");
    }

    private void SyncEveningGame(CompanyEpisode episode, CompanyStage stage, int m, string reason)
    {
        if (!_eveningGames.TryGetValue(episode.Id, out string? id)) return;
        EveningPlan plan = _eveningPlans[id];
        foreach (string holder in new[] { plan.Asker, plan.Asked }.Order(StringComparer.Ordinal))
        {
            string partner = holder == plan.Asker ? plan.Asked : plan.Asker;
            LearnEvening(holder, id, m, stage == CompanyStage.Completed ? "completed" : "interrupted",
                new[] { new ExpectationInput(EveningPolicy.FollowKey(partner, plan.Place), true, EveningCues(partner, plan.Place)) });
        }
        EndEvening(plan, stage == CompanyStage.Completed ? EveningStage.Completed : EveningStage.Interrupted, m, reason);
    }
}
