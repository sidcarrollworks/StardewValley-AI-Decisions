namespace UnderGlass.Sim;

/// <summary>What one person asks of another (acts-batch2 spec 2.3). Append only.</summary>
public enum AskKind { Apology, Door, Invitation, Party, Peace, Loan }

/// <summary>
/// Batch 2's seams (acts-batch2 spec 2.2-2.4; slice b2-0): the Ask, made general from acts-4's
/// apology answer; unseen acts; and an act's joy sized by its amount. Nothing here runs without a
/// batch 2 row or slice, so every run is unchanged.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>
    /// One person asks another (acts-batch2 spec 2.3): what the asked owes the asker against the cost
    /// of saying yes. Regard is the asked person's for the asker; dislike = max(0, -regard);
    /// withdrawn = max(0, -stance).
    /// <list type="bullet">
    /// <item>Apology (acts-4): 0.5 x regard + 0.5 x familiarity + 0.3 x understanding, against
    /// ApologyCost + dislike x (0.5 + retention).</item>
    /// <item>Door: 0.3 + 0.5 x regard + 0.3 x familiarity, against 0.2 + dislike x (0.5 + retention)
    /// + 0.3 x withdrawn.</item>
    /// <item>Invitation, Party: 0.5 x regard + 0.3 x familiarity + 0.2 x chattiness, + 0.2 if fond of
    /// the asker, against 0.15 + dislike x (0.5 + retention) + 0.3 x withdrawn.</item>
    /// <item>Peace: 0.5 x understanding + 0.3 x familiarity + 0.3 x the asked one's own Peace (none
    /// before b2-3), + 0.2 within 3 days of the Winter Star, against 0.2 + dislike x (0.5 +
    /// retention) + 0.3 x max(0, self-regard - 0.5).</item>
    /// <item>Loan: 0.5 x regard + 0.3 x familiarity + 0.3 x understanding, against 0.2 + 0.3 x the
    /// act's amount over the asked household's weekly costs (need and broken promises come with their
    /// slices).</item>
    /// </list>
    /// A margin beyond ClearBand decides; inside it, a draw keyed by what was asked, the act and the
    /// asked person (an apology keeps acts-4's key, "apology" and the act), at logistic(8 x margin)
    /// tilted by mood. (FeelingOptions.CloseCall, the Laya seam, takes a gate's Pursuit and isn't
    /// asked here yet.)
    /// </summary>
    private (bool Yes, double Margin) Ask(int asked, int asker, AskKind what, int actId, int m)
    {
        double regard = St(_names[asked], _names[asker]);
        double liked = Math.Max(0, regard), dislike = Math.Max(0, -regard), withdrawn = Math.Max(0, -Stance(asked));
        double fam = _fam[asked, asker], u = U(asked), keep = 0.5 + Ret(asked);
        Temperament t = CharacterOf(asked);
        (double obliged, double cost) = what switch
        {
            AskKind.Apology => (0.5 * liked + 0.5 * fam + 0.3 * u, _fo.Acts.ApologyCost + dislike * keep),
            AskKind.Door => (0.3 + 0.5 * liked + 0.3 * fam, 0.2 + dislike * keep + 0.3 * withdrawn),
            AskKind.Invitation or AskKind.Party => (0.5 * liked + 0.3 * fam + 0.2 * t.Chattiness + (FondOf(asked, asker, m) > 0 ? 0.2 : 0),
                0.15 + dislike * keep + 0.3 * withdrawn),
            AskKind.Peace => (0.5 * u + 0.3 * fam + (NearWinterStar(Clock.Day(m)) ? 0.2 : 0),
                0.2 + dislike * keep + 0.3 * Math.Max(0, t.SelfRegard - 0.5)),
            AskKind.Loan => (0.5 * liked + 0.3 * fam + 0.3 * u, 0.2 + 0.3 * LoanShare(asked, actId)),
            _ => (0, 1),
        };
        double margin = obliged - cost;
        bool yes = DesireMath.Call(margin, _fo) switch
        {
            "clear" => true,
            "no" => false,
            _ => (what == AskKind.Apology ? Rng.Unit(_seed, "apology", actId.ToString())
                     : Rng.Unit(_seed, "ask", what.ToString(), actId.ToString(), _names[asked]))
                 < DesireMath.Tilted(DesireMath.CloseCallChance(margin), MoodOf(asked), false, _fo),
        };
        if (what != AskKind.Apology) // the apology logs its own line, as acts-4 did
            DesireLog($"{m} ask {_names[asked]} {what} {_names[asker]} act {actId} {(yes ? "yes" : "no")} margin {margin:+0.00;-0.00}");
        return (yes, margin);
    }

    /// <summary>How fond h is of s now (Fond's intensity, as FondMotives works it out).</summary>
    private double FondOf(int h, int s, int m)
    {
        int day = Clock.Day(m);
        int last = Math.Max(_lastContactDay.GetValueOrDefault((h, s)), _lastKindDay.GetValueOrDefault((h, s), int.MinValue));
        return DesireMath.Fond(St(_names[h], _names[s]), day - last, _fo);
    }

    private static bool NearWinterStar(int day)
    {
        for (int d = day - 3; d <= day + 3; d++)
            if (d >= 0 && Calendar.FestivalOn(d) == "Feast of the Winter Star")
                return true;
        return false;
    }

    /// <summary>A loan's amount over the asked household's weekly costs (0 without money).</summary>
    private double LoanShare(int asked, int actId)
    {
        if (!HasMoney || actId < 0 || actId >= _acts.Count)
            return 0;
        double week = WeekCost(_cast[asked].Household);
        return week <= 0 ? 0 : _acts[actId].Amount / week;
    }

    /// <summary>
    /// An act nobody watches as it happens (acts-batch2 spec 2.4): someone stood up, a promise broken,
    /// a found thing kept. It is written, logged and counted as Begin does, with no witnesses, no
    /// busy actor and no gains; each knower gets a named belief at the caller's source and confidence.
    /// </summary>
    private Act BeginUnseen(int m, ActKind kind, string actor, string place, Tile at, string? target, int about,
        IEnumerable<(string Who, Source Source, double Confidence)> knowers)
    {
        if (!_fo.Enabled)
            (target, about) = (null, -1);
        var act = new Act(_acts.Count, m, actor, kind.Name, place, at, false, target, about);
        _acts.Add(act);
        _log.Add($"{m} act {act.Id} {act.Kind} by {act.Actor} at {act.Location}{(target is not null ? " to " + target : "")}");
        Person p = _people[_index[actor]];
        _scenes[act.Id] = SceneOf(act, p);
        _witnesses[act.Id] = 0;
        RecordCircle(act, kind); // batch 2's reach checks: a scandal's circle
        if (_fo.Enabled)
        {
            _did.Add((actor, act.Id));
            BeganAct(act, kind, m);
        }
        foreach (var (who, source, confidence) in knowers.OrderBy(k => k.Who, StringComparer.Ordinal))
            if (who != actor && _beliefs.ContainsKey(who))
                Add(who, new Belief(act.Id, act.Kind, actor, confidence, 1, source, kind.Juiciness, m, Array.Empty<string>(),
                    Target: _fo.Enabled ? target : null), m);
        return act;
    }

    /// <summary>An act's joy (acts-batch2 spec 2.2): a Sized row's scales with the act's amount over
    /// the debtor's (its target's household's) weekly costs, between a quarter and all of it. Every
    /// other row's, as it is.</summary>
    private double JoyOf(Act act, Affect row)
    {
        if (GateOf(KindOf(act)) is not { Sized: true } || !HasMoney || act.Target is not { } t || !_index.TryGetValue(t, out int ti))
            return row.Joy;
        double week = WeekCost(_cast[ti].Household);
        return row.Joy * (week <= 0 ? 1 : Math.Clamp(act.Amount / week, 0.25, 1));
    }
}
