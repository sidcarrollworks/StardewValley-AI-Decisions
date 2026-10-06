namespace UnderGlass.Sim;

/// <summary>
/// Families (design rule 17; Sid, 2026-10-06). Kin never report each other, don't spread stories
/// that hurt each other, don't suspect each other, leave each other out when questioned, and give
/// alibis. Trouble inside a family stays inside: a keeper who learns their own kin took from them
/// has it out at home, a row that others can overhear, instead of going to the mayor.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The act kind for a family row; its actor is the one who took.</summary>
    public const string FamilyRow = "FamilyRow";

    private readonly List<(int ActId, string Keeper, string Culprit)> _rows = new();
    private readonly HashSet<int> _rowed = new();

    /// <summary>Whether two people are family (either way round). Household alone is not kin.</summary>
    private bool AreKin(string a, string b)
        => a != b && _index.TryGetValue(a, out int i) && _cast[i].KinOf(b) is not null;

    /// <summary>Names with the person's own kin left out.</summary>
    private IReadOnlyList<string>? WithoutKin(string who, IReadOnlyList<string>? names)
        => names?.Where(n => !AreKin(who, n)).ToList();

    /// <summary>A keeper learned their own kin took from them: a row at home, not a report.</summary>
    private void KeepItInTheFamily(int actId, string keeper, string culprit, int m)
    {
        if (!_rowed.Add(actId))
            return;
        _rows.Add((actId, keeper, culprit));
        _log.Add($"{m} kept-in-family {keeper} {culprit} {actId}");
    }

    /// <summary>Each tick: rows waiting for the two to be together and free.</summary>
    private void Rows(int m)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var (actId, keeper, culprit) = _rows[i];
            Person k = _people[_index[keeper]], c = _people[_index[culprit]];
            if (!Free(k, m) || !Free(c, m) || k.Place != c.Place || k.At.Chebyshev(c.At) > _po.FarTiles)
                continue;
            if (_kinds.FirstOrDefault(x => x.Name == FamilyRow) is { } kind)
            {
                Begin(m, kind, c, injected: false);
                k.BusyUntil = Math.Max(k.BusyUntil, c.BusyUntil);
            }
            _log.Add($"{m} row {keeper} {culprit} for {actId}");
            _rows.RemoveAt(i--);
        }
    }
}
