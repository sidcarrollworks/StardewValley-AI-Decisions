using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    private sealed class EveningObservation(string holder, ExpectationKey key, string place, int start)
    {
        public string Holder { get; } = holder;
        public ExpectationKey Key { get; } = key;
        public string Place { get; } = place;
        public int Start { get; } = start;
        public int PresentMinutes;
        public bool Seen;
        public bool AgreedPlan;
    }

    private sealed record EveningConcern(string Id, string Holder, string Subject, string ChoiceId, int Since);
    private readonly List<EveningObservation> _eveningObservations = new();
    private readonly Dictionary<(string Holder, string Subject), EveningConcern> _eveningConcerns = new();
    private int _eveningObservationBand = -1;

    /// <summary>Own expected whereabouts, checked through local perception. A miss means
    /// "I did not see them while I was here", never a discovered remote position or rejection.
    /// Current-band state is bounded by the holder's relevant keys, not the run-long event log.</summary>
    private void RunEveningExpectations(int m, bool tick)
    {
        if (!_evenings.Enabled || !_evenings.PassiveExpectations) return;
        int band = m / 180;
        if (band != _eveningObservationBand)
        {
            _eveningObservationBand = band;
            _eveningObservations.Clear();
            foreach (Person holder in _people.Where(p => EveningParticipant(p.V.Name)))
                foreach (ExpectationSnapshot expectation in _livesMemory[holder.V.Name].Snapshot().Expectations)
                {
                    ExpectationKey key = expectation.Key;
                    // An optimistic empty-key prior is not evidence that someone is expected.
                    if (key.Kind != ExpectationKind.Whereabouts || expectation.Chance <= .5
                        || !expectation.Entries.Any(entry => entry.Success)
                        || key.Subject == holder.V.Name || !_index.ContainsKey(key.Subject)
                        || !EveningParticipant(key.Subject)) continue;
                    int last = key.Context.LastIndexOf(':');
                    int previous = last <= 0 ? -1 : key.Context.LastIndexOf(':', last - 1);
                    if (previous <= 0) continue;
                    string place = key.Context[..previous];
                    if (!_places.ContainsKey(place) || CompanyPolicy.WhereaboutsKey(key.Subject, place, m) != key) continue;
                    _eveningObservations.Add(new(holder.V.Name, key, place, band * 180));
                }
        }

        foreach (EveningObservation observation in _eveningObservations)
        {
            Person holder = _people[_index[observation.Holder]];
            // The participant knows their agreement. Keep its outcome in follow-through only,
            // even when an agreed plan ends before this observation band does.
            if (_eveningPerson.TryGetValue(observation.Holder, out string? planId)
                && _eveningPlans.TryGetValue(planId, out EveningPlan? plan)
                && _eveningInvestment.ContainsKey((planId, observation.Holder))
                && (plan.Asker == observation.Key.Subject || plan.Asked == observation.Key.Subject)
                && plan.Start < observation.Start + 180 && plan.End > observation.Start)
                observation.AgreedPlan = true;
            if (!holder.Asleep && !holder.PendingCollapse && holder.Place == observation.Place)
            {
                observation.PresentMinutes++;
                // CompanySees is the existing local sight gate; no remote location enters a choice.
                if (CompanySees(holder, _people[_index[observation.Key.Subject]])) observation.Seen = true;
            }
            if (m == observation.Start + 179) FinishEveningObservation(observation, m);
        }
        if (tick) ActOnEveningConcerns(m);
    }

    private void FinishEveningObservation(EveningObservation observation, int m)
    {
        // One sighting proves found. A missed window needs the whole window observed; a brief
        // visit or sleeping through part of it cannot establish that the holder failed to see them.
        if (observation.AgreedPlan || !observation.Seen && observation.PresentMinutes < 180) return;
        string holderName = observation.Holder, subject = observation.Key.Subject;
        string id = $"expectation:{holderName}:{subject}:{Uri.EscapeDataString(observation.Place)}:{observation.Start}";
        string kind = observation.Seen ? "whereabouts-found" : "whereabouts-missed";
        string[] cues = { "person:" + subject, "place:" + observation.Place, "time:evening" };
        _livesMemory[holderName].Learn(new(_livesSequence[holderName]++, m, "evening:" + id,
            KnowledgeKind.Witnessed, Array.Empty<AssociationInput>(),
            new[] { new ExpectationInput(observation.Key, observation.Seen, cues) }));
        if (_evenings.Record) _eveningOutcomes.Add(new(id, m, holderName, kind));
        if (observation.Seen || _eveningConcerns.ContainsKey((holderName, subject))
            || !_kindByName.ContainsKey("HelpedSomeone")) return;

        Person holder = _people[_index[holderName]];
        if (holder.Asleep || _eveningQuestionHolders.Contains(holderName)) return;
        string choiceId = $"evening:expectation-miss:none:{holderName}:{m}";
        AskEvening(holder, m, "expectation-miss", null,
            $"You expected {subject} at {observation.Place}, but did not see them during the window you observed. You do not know where they went or why. A kind gesture would wait for a real encounter.",
            cues, new[]
            {
                new ChoiceOption("kindness", $"Offer {subject} a hand when we next meet.", 1,
                    new[] { new MemoryObject(MemoryObjectKind.Person, subject) }, Array.Empty<ExpectationKey>()),
                new ChoiceOption("leave-alone", "Leave it alone for now.", 1,
                    Array.Empty<MemoryObject>(), Array.Empty<ExpectationKey>()),
            }, (selected, at) =>
            {
                if (selected != "kindness") return;
                _eveningConcerns[(holderName, subject)] = new(id, holderName, subject, choiceId, at);
                if (_evenings.Record) _eveningOutcomes.Add(new(id, at, holderName, "concern-waiting"));
            }, new[] { new ChoiceConstraint("observed-window-minutes", observation.PresentMinutes,
                new[] { "evening:" + id }) });
    }

    private void ActOnEveningConcerns(int m)
    {
        foreach (EveningConcern concern in _eveningConcerns.Values.OrderBy(c => c.Holder, StringComparer.Ordinal)
                     .ThenBy(c => c.Subject, StringComparer.Ordinal).ToArray())
        {
            if (m - concern.Since >= 3 * Clock.MinutesPerDay)
            {
                _eveningConcerns.Remove((concern.Holder, concern.Subject));
                if (_evenings.Record) _eveningOutcomes.Add(new(concern.Id, m, concern.Holder, "concern-expired"));
                continue;
            }
            Person holder = _people[_index[concern.Holder]], other = _people[_index[concern.Subject]];
            int h = _index[concern.Holder], s = _index[concern.Subject], day = Clock.Day(m);
            ActKind kind = _kindByName["HelpedSomeone"];
            // The same physical/cap checks as reflection's deferred acts, plus the ordinary
            // avoid/rate target gate. An intention never moves its subject or reaches through work.
            if (!CompanyFree(holder, m) || !CompanyFree(other, m) || holder.Walking || other.Walking
                || EveningReserved(concern.Holder) || EveningReserved(concern.Subject)
                || _companyByPerson.ContainsKey(concern.Holder) || _companyByPerson.ContainsKey(concern.Subject)
                || !InReach(holder, other, m) || !kind.FitsAge(holder.V.Age)
                || kind.Allowed.Count > 0 && !kind.Allowed.Contains(holder.Place)
                || !RateTargetAllowed(kind, h, s, m)
                || _slotsUsed.GetValueOrDefault((h, s, day)) >= _fo.SlotsPerDay
                || IsLight(kind) && LightCapReached(kind, h, m)) continue;
            _eveningConcerns.Remove((concern.Holder, concern.Subject));
            _slotsUsed[(h, s, day)] = _slotsUsed.GetValueOrDefault((h, s, day)) + 1;
            _pursuedActs.Add(_acts.Count);
            Begin(m, kind, holder, false, concern.Subject, fromReflection: true,
                cause: new(-1, m, concern.Holder, "evening-concern", ChoiceId: concern.ChoiceId));
            if (_evenings.Record) _eveningOutcomes.Add(new(concern.Id, m, concern.Holder, "concern-acted"));
        }
    }
}
