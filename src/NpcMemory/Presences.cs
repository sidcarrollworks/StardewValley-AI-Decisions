namespace NpcMemory;

/// <summary>Helpers for the tick's presence list.</summary>
public static class Presences
{
    /// <summary>
    /// One presence per name, the first seen winning. The game keeps two characters named
    /// "Mister Qi" (the Club and the Qi nut room; playtest 2026-10-05), and with both in the list
    /// every tick moved his ledger entry between them and logged two presence records. Names are
    /// compared exactly, as <c>NPC.Name</c> is the game's key. The player is never dropped.
    /// </summary>
    public static List<Presence> OnePerName(IEnumerable<Presence> all)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Presence>();
        foreach (Presence p in all)
        {
            if (p.IsPlayer || seen.Add(p.Name))
                result.Add(p);
        }
        return result;
    }
}
