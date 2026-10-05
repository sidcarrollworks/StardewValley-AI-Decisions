namespace NpcMemory;

/// <summary>Helpers for the tick's presence list.</summary>
public static class Presences
{
    /// <summary>Whether the mod watches a character: a villager the game lets the player befriend.
    /// Non-social characters (the casino's Bouncer, Mister Qi, Gunther, Marlon...) live in their maps
    /// from day one, so without this every one of them was tracked, and could "chat" about a player
    /// they never met (playtest 2026-10-05).</summary>
    public static bool Tracks(bool isVillager, bool canSocialize) => isVillager && canSocialize;

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
