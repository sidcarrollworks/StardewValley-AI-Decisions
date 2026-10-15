using UnderGlass.Sim.Lives;

namespace UnderGlass.Sim;

public sealed partial class Simulation
{
    // Kept after the episode ends: a later telling or correction of the act must not pay a
    // participant the ordinary target-kindness feeling on top of their shared experience.
    private readonly Dictionary<int, string> _companyGameActs = new();
    private readonly HashSet<int> _companyGamesInProgress = new();

    private bool CompanyManagedAct(int actId) => _companyGameActs.ContainsKey(actId);

    /// <summary>The answer has already been sampled. Begin the normal witnessed act without
    /// asking again; its participants feel the shared activity only if it really completes.</summary>
    private void StartCompanyGame(CompanyEpisode episode, int m)
    {
        ActKind kind = _kindByName["PlayedGame"];
        int actId = _acts.Count;
        var playing = episode with { Stage = CompanyStage.Playing, ActId = actId };
        _companyGameActs.Add(actId, episode.Id);
        _companyGamesInProgress.Add(actId);
        SetCompanyEpisode(playing, m, "The game began.");
        Begin(m, kind, _people[_index[episode.Asker]], injected: false, target: episode.Asked,
            companyAccepted: true);
    }

    /// <summary>Continuity is a live execution gate, never evidence for choosing a destination.
    /// Work, bodies, another activity or losing contact can end a game before its full duration.</summary>
    private void CheckCompanyGames(int m)
    {
        if (!_companyOptions.Enabled)
            return;
        foreach (int actId in _companyGamesInProgress.Order().ToArray())
        {
            CompanyEpisode episode = _companyEpisodes[_companyGameActs[actId]];
            Act act = _acts[actId];
            if (CompanyGameInterruption(episode, act, m) is not { } reason)
                continue;
            _companyGamesInProgress.Remove(act.Id);
            _watching.Remove(act.Id);
            ReleaseCompanyBusy(episode, act, m);
            EndCompany(episode, CompanyStage.Interrupted, m, reason);
            LearnCompanyGame(episode, m, completed: false);
        }
    }

    private string? CompanyGameInterruption(CompanyEpisode episode, Act act, int m)
    {
        Person asker = _people[_index[episode.Asker]], asked = _people[_index[episode.Asked]];
        foreach (Person person in new[] { asker, asked })
        {
            if (person.Asleep || person.PendingCollapse || person.DetainedUntil > m)
                return $"{person.V.Name} could no longer take part.";
            if (WorkingNow(person, m))
                return $"{person.V.Name} had to leave for work.";
            if (BodyTired(person, Clock.OfDay(m)))
                return $"{person.V.Name} became too tired to continue.";
            if (OtherCompanyOccupation(person.V.Name, act.Id, m))
                return $"{person.V.Name} became occupied by something else.";
        }
        if (asker.Place != act.Location || asked.Place != act.Location
            || asker.At.Chebyshev(asked.At) > _po.NearTiles
            || Perception.LineOfSight(_places[act.Location], asker.At, asked.At, _po) <= 0)
            return "They could no longer play together here.";
        return null;
    }

    /// <summary>Only acts that actually reserve this participant's time count. Merely being
    /// watched, spoken about or the target of a given act does not take over their activity.</summary>
    private bool OtherCompanyOccupation(string name, int ownAct, int m)
        => _watching.Keys.Any(id => id != ownAct && _acts[id] is { } other
            && other.Tick + KindOf(other).DurationMinutes - 1 >= m
            && (other.Actor == name || other.Target == name
                && KindOf(other).Affect is { Target: TargetIs.Chosen or TargetIs.Kin }));

    private void ReleaseCompanyBusy(CompanyEpisode episode, Act act, int m)
    {
        int ownedUntil = act.Tick + KindOf(act).DurationMinutes - 1;
        foreach (string name in new[] { episode.Asker, episode.Asked })
        {
            Person person = _people[_index[name]];
            if (person.BusyUntil == ownedUntil && !OtherCompanyOccupation(name, act.Id, m))
                person.BusyUntil = m - 1;
        }
    }

    /// <summary>Called after the normal finish path has delivered witnessed accounts. Returning
    /// true also prevents a second legacy completion for a managed act that has already ended.</summary>
    private bool CompleteCompanyGame(Act act, int m)
    {
        if (!_companyGameActs.TryGetValue(act.Id, out string? episodeId))
            return false;
        CompanyEpisode episode = _companyEpisodes[episodeId];
        if (episode.Stage != CompanyStage.Playing)
            return true;
        _companyGamesInProgress.Remove(act.Id);
        EndCompany(episode, CompanyStage.Completed, m, "They finished the game together.");
        LearnCompanyGame(episode, m, completed: true);
        return true;
    }

    private void LearnCompanyGame(CompanyEpisode episode, int m, bool completed)
    {
        Act act = _acts[episode.ActId!.Value];
        foreach (string holder in new[] { episode.Asker, episode.Asked }.OrderBy(n => n, StringComparer.Ordinal))
        {
            string partner = holder == episode.Asker ? episode.Asked : episode.Asker;
            double felt = completed ? (KindOf(act).Affect?.Joy ?? 0) * Sens(_index[holder]) : 0;
            if (felt != 0)
            {
                AddMood(_index[holder], felt, act.Id);
                _feltLog.Add(new Felt(m, holder, act.Id, "SharedActivity", "Participated", felt, null, 0, 0));
            }
            LearnCompany(holder, episode, m, completed ? "completed" : "interrupted",
                new[] { new ExpectationInput(CompanyPolicy.ActivityKey(partner, act.Location), completed,
                    CompanyCues(partner, act.Location)) }, felt, act.Location, partner);
        }
    }
}
