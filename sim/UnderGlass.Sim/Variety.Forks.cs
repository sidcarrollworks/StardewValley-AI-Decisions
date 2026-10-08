namespace UnderGlass.Sim;

// V7, the open future (batch 2 spec 6.3, slice m-2): copies of a run split at day 28
// (Simulation.Fork) and read beside it. Under 25% of forks changing their major stories, a year is
// settled by day 28; over 60%, day 28 decides nothing.
public static partial class Variety
{
    /// <summary>The day V7's forks split from their base (spec 6.3).</summary>
    public const int ForkDay = 28;

    /// <summary>How many of <see cref="Kinds"/>, biggest first, V7 counts as major: ranks 1-13,
    /// WrongVerdict to Hermit (spec 6.3).</summary>
    public const int MajorRanks = 13;

    /// <summary>A run's major stories on days <paramref name="fromDay"/> to <paramref name="toDay"/> - 1:
    /// the Ids (kind and people, without the day) of its events whose kind ranks in the first
    /// <see cref="MajorRanks"/>.</summary>
    public static IReadOnlySet<string> MajorStories(IEnumerable<StoryEvent> events, int fromDay = ForkDay, int toDay = Year)
        => events.Where(e => e.Day >= fromDay && e.Day < toDay && Rank(e.Kind) < MajorRanks)
            .Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// V7 (spec 6.3): over runs and their forks, each read as its story events (<see cref="Of"/>), the
    /// share of forks whose major stories on days <paramref name="fromDay"/> to <paramref name="toDay"/> - 1
    /// differ from their base's; and the share whose headline over those days differs (no headline in
    /// either is the same headline). NaN for both without forks.
    /// </summary>
    public static (double V7, double Headline) Open(
        IReadOnlyList<(IReadOnlyList<StoryEvent> Base, IReadOnlyList<IReadOnlyList<StoryEvent>> Forks)> runs,
        int fromDay = ForkDay, int toDay = Year)
    {
        string? HeadlineOf(IEnumerable<StoryEvent> events) => Headline(events.Where(e => e.Day >= fromDay && e.Day < toDay))?.Id;
        int forks = 0, changed = 0, headlines = 0;
        foreach (var (based, forked) in runs)
        {
            IReadOnlySet<string> major = MajorStories(based, fromDay, toDay);
            string? headline = HeadlineOf(based);
            foreach (IReadOnlyList<StoryEvent> fork in forked)
            {
                forks++;
                if (!MajorStories(fork, fromDay, toDay).SetEquals(major))
                    changed++;
                if (HeadlineOf(fork) != headline)
                    headlines++;
            }
        }
        return forks == 0 ? (double.NaN, double.NaN) : (changed / (double)forks, headlines / (double)forks);
    }
}
