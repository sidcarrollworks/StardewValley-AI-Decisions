namespace UnderGlass.Sim;

/// <summary>One invitation (invitation spec): at a minute, who asked whom to which company act, the
/// answer and its margin, and the act that followed (the game or drink on a yes, the refusal on a no).
/// Tired, Busy, Low: the asked person's state then (AskState); Bond: theirs with the asker; Eager:
/// their eagerness to please. Recorded so an observer can see why.</summary>
public sealed record Invitation(int Tick, string Asker, string Asked, string Kind, bool Accepted, double Margin, int ActId,
    double Tired = 0, double Busy = 0, double Low = 0, double Bond = 0, double Eager = 0);

/// <summary>
/// Invitations (agreed order step 3; invitation spec). Before Invitations, a game or a drink was
/// one-sided: the one who began it made the other busy for its length. With it on, the other person
/// is asked first, through batch 2's general Ask (AskKind.Invitation), and the answer is real: a yes
/// begins the game or drink as before; a no begins Refused instead, the asked person's act toward the
/// asker, which the asker feels and anyone in sight can see, and the asker does not invite that
/// person again for InviteAgainDays. Acts a fixture injects are not asked.
/// </summary>
public sealed partial class Simulation
{
    private readonly List<Invitation> _invitations = new();
    /// <summary>After a refusal: the day from which the asker may invite that person again.</summary>
    private readonly Dictionary<(int Asker, int Asked), int> _inviteAgain = new();
    /// <summary>Acts that answered an invitation, for speech: the company kind asked for, and whether
    /// it was a yes (the act is the game or drink) or a no (the act is the refusal).</summary>
    private readonly Dictionary<int, (string Kind, bool Accepted)> _answered = new();

    private bool Inviting => _fo.Acts.Invitations && _fo.Acts.Company && Steering && !_fo.Acts.Watch;

    private static bool Invites(ActKind kind) => kind.Name is "PlayedGame" or "TreatedToDrink";

    /// <summary>Asks before a game or a drink begins. Returns true when the answer was no and the
    /// refusal has begun in its place (the caller then begins nothing); false when the act goes ahead.</summary>
    private bool Asked(int m, ActKind kind, Person asker, string target)
    {
        if (!Inviting || !Invites(kind) || !_index.TryGetValue(target, out int t))
            return false;
        int a = _index[asker.V.Name], actId = _acts.Count;
        var (yes, margin) = Ask(t, a, AskKind.Invitation, actId, m);
        AskState state = AskStateOf(t, m);
        _invitations.Add(new Invitation(m, asker.V.Name, target, kind.Name, yes, Math.Round(margin, 4), actId,
            Math.Round(state.Tired, 4), state.Busy, Math.Round(state.Low, 4),
            Math.Round(AskMath.Bond(Close(t, a), Math.Max(0, St(_names[t], _names[a])), _fam[t, a]), 4),
            Math.Round(AskMath.Eagerness(CharacterOf(t)), 4)));
        _answered[actId] = (kind.Name, yes);
        if (yes)
            return false;
        _inviteAgain[(a, t)] = Clock.Day(m) + _fo.Acts.InviteAgainDays;
        Begin(m, _kindByName["Refused"], _people[t], injected: false, target: asker.V.Name);
        return true;
    }

    /// <summary>The asked person's own state (self-knowledge, not the world's truth about anyone else):
    /// how tired by the body's own measure (the one that sends them to bed), whether they are due to
    /// leave for work within InviteBusyMinutes, and how far their mood is below even.</summary>
    private AskState AskStateOf(int i, int m)
    {
        Person p = _people[i];
        int now = Clock.OfDay(m);
        double tired = Math.Clamp(_bo.Tiredness(p.Energy / p.V.Body.MaxEnergy, now), 0, 1);
        double busy = p.V.Job is { } job && job.WorksOn(Clock.Weekday(m))
            && job.Start - (job.Commute ?? _bo.CommuteMinutes) - now is var until && until >= 0 && until <= _fo.Acts.InviteBusyMinutes ? 1 : 0;
        return new AskState(tired, busy, Math.Max(0, -MoodOf(i)));
    }

    /// <summary>The asker was turned down by this person less than InviteAgainDays ago.</summary>
    private bool InviteWaits(Person asker, Person asked, int m)
        => Inviting && _inviteAgain.TryGetValue((_index[asker.V.Name], _index[asked.V.Name]), out int day) && Clock.Day(m) < day;
}
