namespace UnderGlass.Sim;

/// <summary>
/// The act catalog's rules (acts spec 3; Sid, 2026-10-08). Slice acts-0 builds the seams the gate
/// reads: Fits, which every act the gate offers must pass, and the pride term in its cost. Every
/// shipped row passes Fits (its hours are the whole day and its gate asks for nothing more) and has
/// no pride, so the town is unchanged. The per-head draws, the arrivals, what follows an act, the
/// new motives, the warm budget and the cold reading come with the slices that use them.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Whether an act fits the actor a, the target o and the minute (acts spec 3.1): its
    /// hours; its card, if it needs one; the actor's familiarity with and regard for the target; and
    /// its audience, if it needs one.</summary>
    private bool Fits(ActKind kind, int a, int o, int m)
    {
        if (!kind.OpenAt(Clock.OfDay(m)))
            return false;
        if (GateOf(kind) is not { } g)
            return true;
        Person p = _people[a];
        if (g.NeedsCard && !(p.V.Acts.TryGetValue(kind.Name, out double w) && w > 0))
            return false;
        if (g.MinFamiliarity > 0 && _fam[a, o] < g.MinFamiliarity)
            return false;
        if (g.MinRegard > -1 && St(_names[a], _names[o]) < g.MinRegard)
            return false;
        return g.MinAudience <= 0 || Audience(p, o) >= g.MinAudience;
    }

    /// <summary>Others awake in the actor's place, within FarTiles and in sight, not counting the target.</summary>
    private int Audience(Person p, int o)
    {
        int n = 0;
        for (int q = 0; q < _people.Length; q++)
        {
            Person x = _people[q];
            if (x != p && q != o && !x.Asleep && x.Place == p.Place && x.At.Chebyshev(p.At) <= _po.FarTiles
                && Perception.LineOfSight(_places[p.Place], p.At, x.At, _po) > 0)
                n++;
        }
        return n;
    }

    /// <summary>Pride (acts spec 2.2): a kind with a pride weight costs more for the proud, by
    /// PrideWeight x (self-regard - 0.5). No shipped row has one.</summary>
    private double Pride(ActKind kind, int h)
        => GateOf(kind) is { PrideWeight: not 0 } g ? g.PrideWeight * (CharacterOf(h).SelfRegard - 0.5) : 0;
}
