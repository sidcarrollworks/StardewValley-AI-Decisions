namespace UnderGlass.Sim;

/// <summary>Knobs for noticing odd hours (design rule 1, "acting normal"). First guesses.</summary>
public sealed class HabitOptions
{
    /// <summary>An observer needs this many ticks of seeing someone out before they know that
    /// person's hours well enough to find one odd (72 ticks: six hours, over days).</summary>
    public int KnowHoursAfterTicks { get; set; } = 72;
    /// <summary>Odd hours are at night: from NightFrom to NightTo. (Judging "quiet" hours from what
    /// each observer saw was tried first: their view depends on where they happen to be, so a
    /// morning on an empty beach counted as quiet and the town noticed 43 odd outings a season.)</summary>
    public int NightFrom { get; set; } = Clock.At(22);
    public int NightTo { get; set; } = Clock.At(6);
    /// <summary>Curfews for those who live with a parent (rule 17), by the hour after which being
    /// out breaks it: children 20:00, teens 22:00, grown children 1:00.</summary>
    public int ChildCurfew { get; set; } = Clock.At(20);
    public int TeenCurfew { get; set; } = Clock.At(22);
    public int GrownCurfew { get; set; } = Clock.At(1);
}

/// <summary>
/// Acting normal (design rule 1; rule 17 for curfews). Everyone learns, from what they see, the
/// hours each person keeps. Seeing someone out at night, at an hour (or the hours either side)
/// you have never seen them out, is worth talking about: an <c>OutLate</c> story about them. A parent who learns their child was out past curfew has it out at home.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The act kind for being seen out at an odd hour; its actor is the person seen.</summary>
    public const string OutLate = "OutLate";

    private readonly Dictionary<(string Observer, string Subject), int[]> _habits = new();
    private readonly Dictionary<(string Subject, int Day), int> _outLate = new();

    private static bool AtHome(Person p) => p.Place.StartsWith("Home:", StringComparison.Ordinal);

    /// <summary>Called for each sighting: is it odd? Then learn from it.</summary>
    private void Habit(Person o, Person s, int m)
    {
        if (AtHome(s))
            return;
        int hour = Clock.OfDay(m) / 60;
        var key = (o.V.Name, s.V.Name);
        if (!_habits.TryGetValue(key, out int[]? hours))
            _habits[key] = hours = new int[24];
        int t = Clock.OfDay(m);
        bool night = t >= _ho.NightFrom || t < _ho.NightTo;
        bool unusual = hours[hour] == 0 && hours[(hour + 23) % 24] == 0 && hours[(hour + 1) % 24] == 0;
        bool odd = night && unusual && hours.Sum() >= _ho.KnowHoursAfterTicks;
        hours[hour]++;
        if (odd)
            SeenOutLate(o, s, m);
    }

    private void SeenOutLate(Person o, Person s, int m)
    {
        if (_kinds.FirstOrDefault(k => k.Name == OutLate) is not { } kind)
            return;
        var key = (s.V.Name, Clock.Day(m - Clock.At(12))); // one night runs noon to noon
        if (!_outLate.TryGetValue(key, out int actId))
        {
            var act = new Act(_acts.Count, m, s.V.Name, OutLate, s.Place, s.At);
            _acts.Add(act);
            _scenes[act.Id] = SceneOf(act, s);
            _witnesses[act.Id] = 0;
            _outLate[key] = actId = act.Id;
            if (_fo.Enabled)
                _did.Add((s.V.Name, act.Id));
            _log.Add($"{m} act {act.Id} {OutLate} by {s.V.Name} at {s.Place}");
        }
        if (_beliefs[o.V.Name].ContainsKey(actId))
            return;
        _witnesses[actId]++;
        Add(o.V.Name, new Belief(actId, OutLate, s.V.Name, 1, 1, Source.Witnessed, kind.Juiciness, m, Array.Empty<string>()), m);
    }

    /// <summary>The hour after which someone who lives with a parent is breaking curfew, or null.</summary>
    private int? CurfewOf(Villager v)
    {
        bool withParent = v.Family?.Any(f => f.Value is Kin.Parent or Kin.Guardian or Kin.Stepparent
                                             && _index.ContainsKey(f.Key) && _cast[_index[f.Key]].Household == v.Household) == true;
        if (!withParent)
            return null;
        return v.Stage switch { Stage.Child => _ho.ChildCurfew, Stage.Teen => _ho.TeenCurfew, _ => _ho.GrownCurfew };
    }

    /// <summary>A parent (or guardian) at home who learns their child was out past curfew has it
    /// out with them (called whenever someone gets a belief).</summary>
    private void CurfewBroken(string who, Belief b, int m)
    {
        if (b.Kind != OutLate || b.Actor is not { } child || !_index.ContainsKey(child))
            return;
        Villager c = _cast[_index[child]];
        if (c.KinOf(who) is not (Kin.Parent or Kin.Guardian or Kin.Stepparent) || CurfewOf(c) is not { } curfew)
            return;
        int seen = Clock.OfDay(_acts[b.ActId].Tick);
        bool late = curfew >= Clock.At(12) ? seen >= curfew || seen < Clock.At(6) : seen >= curfew && seen < Clock.At(6);
        if (late)
            KeepItInTheFamily(b.ActId, who, child, m);
    }
}
