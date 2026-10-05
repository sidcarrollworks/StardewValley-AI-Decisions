using Xunit;

namespace NpcMemory.Tests;

/// <summary>A full diary forgets what matters least first (Sid, 2026-10-05).</summary>
public class DiaryKeepTests
{
    private const int Day = GameClock.TicksPerDay;
    private static readonly DiaryKeepOptions O = new();
    private static double NoRegard(string _) => 0;

    [Fact]
    public void PlainSightingsGoBeforeEvents_EvenOlderEvents()
    {
        var entries = new List<DiaryEntry>
        {
            new(0 * Day + 10, "Player", "Talked", "hearts=1"),   // day 0
            new(5 * Day + 10, "Sam", "Saw", "Town"),            // day 5, a plain sighting
            new(6 * Day + 10, "Penny", "Saw", "Town"),          // day 6, a plain sighting
            new(20 * Day + 10, "Player", "Saw", "Farm"),        // today: protected
        };
        List<DiaryEntry> kept = DiaryKeep.Keep(entries, 2, NoRegard, O);
        Assert.Equal(new[] { "Talked", "Saw" }, kept.Select(e => e.Kind));
        Assert.Equal(20 * Day + 10, kept[1].AbsoluteTick); // order kept; today's sighting stays
    }

    [Fact]
    public void ASightingThatWitnessedSomethingIsKeptLikeTheEvent()
    {
        var entries = new List<DiaryEntry>
        {
            new(5 * Day + 10, "Player", "Saw", "Saloon"),
            new(5 * Day + 12, "Player", "SawRummaging", "place=Saloon"), // within 6 ticks
            new(5 * Day + 40, "Gus", "Saw", "Saloon"),                   // nothing happened
            new(20 * Day, "Player", "Talked", null),
        };
        List<DiaryEntry> kept = DiaryKeep.Keep(entries, 3, NoRegard, O);
        Assert.DoesNotContain(kept, e => e.Subject == "Gus");
        Assert.Contains(kept, e => e.Kind == "Saw" && e.Subject == "Player");
    }

    [Fact]
    public void EntriesAboutSomeoneTheyCareLittleAboutGoFirst_StrongFeelingsEitherWayStay()
    {
        var entries = new List<DiaryEntry>
        {
            new(5 * Day, "Pierre", "Talked", null),
            new(5 * Day, "Player", "Talked", null),
            new(5 * Day, "Shane", "Talked", null),
            new(20 * Day, "Player", "Talked", null),
        };
        double Regard(string who) => who switch { "Player" => 0.6, "Shane" => -0.8, _ => 0 };
        List<DiaryEntry> kept = DiaryKeep.Keep(entries, 3, Regard, O);
        Assert.DoesNotContain(kept, e => e.Subject == "Pierre");
        Assert.Contains(kept, e => e.Subject == "Shane"); // a grudge is remembered
    }

    [Fact]
    public void WhenEverythingIsRecentTheOldestGoFirst_AndTheSameInputKeepsTheSameEntries()
    {
        var entries = Enumerable.Range(0, 5).Select(i => new DiaryEntry(i, "Player", "Saw", "Town")).ToList();
        List<DiaryEntry> kept = DiaryKeep.Keep(entries, 3, NoRegard, O);
        Assert.Equal(new[] { 2, 3, 4 }, kept.Select(e => e.AbsoluteTick));
        Assert.Equal(kept, DiaryKeep.Keep(entries, 3, NoRegard, O));
    }

    [Fact]
    public void AFullDiaryTrimsABatchAndUsesTheStoresRegard()
    {
        var store = new MemoryStore { MaxDiaryEntries = 20, RegardOf = (npc, who) => who == "Player" ? 0.5 : 0 };
        for (int d = 0; d < 20; d++)
            store.Note("Haley", new DiaryEntry(d * Day, d % 2 == 0 ? "Player" : "Alex", "Talked", null));
        store.Note("Haley", new DiaryEntry(25 * Day, "Player", "Saw", "Town")); // the 21st: over the cap

        IReadOnlyList<DiaryEntry> left = store.DiaryOf("Haley").Entries;
        Assert.Equal(18, left.Count); // 10% of the cap at once
        Assert.Equal(3, store.DiaryOf("Haley").TrimmedToday);
        // The oldest entries about Alex (no regard) went before those about the player.
        Assert.Equal(new[] { 1, 3, 5 }, Enumerable.Range(0, 20).Where(d => d % 2 == 1)
            .Where(d => !left.Any(e => e.AbsoluteTick == d * Day)).ToArray());
    }

    [Fact]
    public void WeightHalvesOverTheHalfLife()
    {
        var e = new DiaryEntry(0, "Player", "Talked", null);
        double fresh = DiaryKeep.Score(e, 0, 0, false, O);
        Assert.Equal(fresh / 2, DiaryKeep.Score(e, 21 * Day, 0, false, O), 6);
        Assert.Equal(0.1 * fresh, DiaryKeep.Score(e with { Kind = "Saw" }, 0, 0, false, O), 6);
        Assert.Equal(fresh, DiaryKeep.Score(e with { Kind = "Saw" }, 0, 0, true, O), 6);
        Assert.Equal(1.5, DiaryKeep.Score(e, 0, -2, false, O), 6);
    }
}
