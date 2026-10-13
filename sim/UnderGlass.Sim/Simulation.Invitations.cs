namespace UnderGlass.Sim;

/// <summary>One invitation (invitation spec): at a minute, who asked whom to which company act, the
/// answer and its margin, and the act that followed (the game or drink on a yes, the refusal on a no).</summary>
public sealed record Invitation(int Tick, string Asker, string Asked, string Kind, bool Accepted, double Margin, int ActId);

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
        _invitations.Add(new Invitation(m, asker.V.Name, target, kind.Name, yes, Math.Round(margin, 4), actId));
        _answered[actId] = (kind.Name, yes);
        if (yes)
            return false;
        _inviteAgain[(a, t)] = Clock.Day(m) + _fo.Acts.InviteAgainDays;
        Begin(m, _kindByName["Refused"], _people[t], injected: false, target: asker.V.Name);
        return true;
    }

    /// <summary>The asker was turned down by this person less than InviteAgainDays ago.</summary>
    private bool InviteWaits(Person asker, Person asked, int m)
        => Inviting && _inviteAgain.TryGetValue((_index[asker.V.Name], _index[asked.V.Name]), out int day) && Clock.Day(m) < day;
}
