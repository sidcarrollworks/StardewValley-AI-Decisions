namespace NpcMemory;

/// <summary>Where an answer to "where is X?" came from, from least to most reliable.</summary>
public enum WhereaboutsSource
{
    Unknown,    // nobody knows and no habit is learned well enough
    Habit,      // no sighting today; where the seeker usually sees X at this hour
    Told,       // a neighbour passed on a sighting from today (one or two hops)
    SeenToday,  // the seeker saw X earlier today
    SeenNow,    // the seeker can see X this very tick
}

/// <summary>
/// One NPC's best answer to "where is <see cref="Subject"/>?" right now, built only from its own
/// memory: its ledger (own sightings and what it was told) and its routine belief. Never from
/// live positions. <see cref="Place"/> is a location name, or a region name for Region detail
/// and habits; null when the seeker has nothing to go on.
/// </summary>
public sealed record Whereabouts(
    string Seeker,
    string Subject,
    WhereaboutsSource Source,
    string? Place,
    LedgerDetail? Detail,     // the ledger detail behind a sighting or tip; Region for a habit
    int AgeTicks,             // how old the sighting is (0 for a habit)
    int HopCount,             // 0 own sighting, 1 told by someone who saw it, 2 told second-hand
    string? ToldBy,           // who passed it on, for Told
    double HabitShare,       // for Habit: the region's share of that hour's weight (0..1)
    double Evidence = 0)     // for Habit: the block's total evidence behind the lead
{
    /// <summary>True when the answer names somewhere to go.</summary>
    public bool HasPlace => !string.IsNullOrEmpty(Place);
}

/// <summary>Who an NPC asked (everyone it was with) and whose answer it kept (it was fresher).</summary>
public sealed record AskResult(IReadOnlyList<string> Asked, IReadOnlyList<string> Told);

/// <summary>Tuning for <see cref="MemoryStore.LookFor"/>. Not saved, so changes apply to old saves.</summary>
public sealed class WhereaboutsOptions
{
    /// <summary>How much evidence a 2-hour block needs before the habit counts as a lead
    /// (docs/spec/find.md): roughly ticks spent together, hearts making each tick with the player
    /// count for more. Raised from 3.0 after the in-game week: three co-located ticks on one day
    /// made "you're usually there 100% of the time" claims (week review, finding 6).</summary>
    public double MinHabitEvidence { get; set; } = 12.0;

    /// <summary>...and only if one region holds at least this share of that weight.</summary>
    public double MinHabitShare { get; set; } = 0.5;
}
