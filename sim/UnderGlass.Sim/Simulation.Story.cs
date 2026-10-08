namespace UnderGlass.Sim;

/// <summary>
/// The record the story measures need and the rest of a run doesn't keep (acts spec 7.3): each
/// spell a pair spends in a feud, read at each night's close. No rule reads it, and it writes
/// nothing to the log, so every run is unchanged.
/// </summary>
public sealed partial class Simulation
{
    private readonly Dictionary<(int, int), int> _feudFrom = new();
    private readonly List<FeudSpell> _feudSpells = new();

    /// <summary>At a night's close, for the pair i &lt; j: a spell starts on the first night both
    /// hold the other at <see cref="FeelingOptions.FeudAt"/> or below, and ends on the first night
    /// either is above it.</summary>
    private void FeudSpellAt(int day, int i, int j)
    {
        bool feud = _regard[i, j] <= _fo.FeudAt && _regard[j, i] <= _fo.FeudAt;
        if (feud)
            _feudFrom.TryAdd((i, j), day);
        else if (_feudFrom.Remove((i, j), out int from))
            _feudSpells.Add(Spell(i, j, from, day));
    }

    private FeudSpell Spell(int i, int j, int from, int to)
        => new(_names[i], _names[j], from, to, Close(i, j),
            _baseline[i, j] <= _fo.FeudAt && _baseline[j, i] <= _fo.FeudAt);

    /// <summary>Every feud spell of the run, those still open at the end with To -1; by start,
    /// then by pair.</summary>
    private IReadOnlyList<FeudSpell> FeudSpells()
        => _feudSpells.Concat(_feudFrom.Select(p => Spell(p.Key.Item1, p.Key.Item2, p.Value, -1)))
            .OrderBy(s => s.From).ThenBy(s => s.A, StringComparer.Ordinal).ThenBy(s => s.B, StringComparer.Ordinal).ToList();
}
