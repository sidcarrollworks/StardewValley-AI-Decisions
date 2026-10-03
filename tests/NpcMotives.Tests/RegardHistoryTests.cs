using NpcMemory;
using NpcTemperament;
using Xunit;
using Xunit.Abstractions;

namespace NpcMotives.Tests;

/// <summary>History at install (docs/spec/vanilla-sources.md): regard seeded once from the
/// game's gift log, heart events seen and relationship status.</summary>
public sealed class RegardHistoryTests
{
    private static readonly Temperament T = Temperament.Neutral;
    private readonly ITestOutputHelper _out;

    public RegardHistoryTests(ITestOutputHelper output) => _out = output;

    private static double Of(NpcHistory h) => RegardHistory.SeedOf(h, T)?.Regard ?? 0;

    [Fact]
    public void ExamplesStayInTheirBands()
    {
        foreach (NpcHistory h in new[]
                 {
                     new NpcHistory("OneLoved", Loved: 1),
                     new NpcHistory("Haley", Loved: 12, Liked: 15, Neutral: 4, HeartEventsSeen: 2),
                     new NpcHistory("FiftyLoved", Loved: 50),
                     new NpcHistory("OneHated", Hated: 1),
                     new NpcHistory("FiftyHated", Hated: 50),
                     new NpcHistory("Married", Status: HistoryStatus.Married),
                     new NpcHistory("Divorced", Loved: 20, Status: HistoryStatus.Divorced),
                 })
            _out.WriteLine($"{h.Npc}: {RegardHistory.SeedOf(h, T)?.Line ?? "none"}");

        // The bands these give (a neutral temperament): one loved gift about +0.09; a favourite
        // with 31 gifts and 2 heart events about +0.77, near the 0.8 cap.
        Assert.InRange(Of(new NpcHistory("OneLoved", Loved: 1)), 0.08, 0.10);
        Assert.InRange(Of(new NpcHistory("Haley", Loved: 12, Liked: 15, Neutral: 4, HeartEventsSeen: 2)), 0.7, 0.8);
    }

    [Fact]
    public void NoHistoryNoSeed_AndNeutralGiftsLeaveNoMark()
    {
        Assert.Null(RegardHistory.SeedOf(new NpcHistory("Gus"), T));
        Assert.Null(RegardHistory.SeedOf(new NpcHistory("Gus", Neutral: 30), T));
    }

    [Fact]
    public void OneGiftCountsHalfOfWhatItWouldInPlay()
    {
        // In play, a loved gift's lasting mark is RegardBook.Apply's; from history it counts half.
        var book = new RegardBook();
        var o = new MotiveOptions();
        book.Apply("Haley", new DiaryEntry(0, MemoryStore.PlayerName, "GiftReceived", "taste=Love"), Array.Empty<DiaryEntry>(), T, o);
        double inPlay = book.Of("Haley", MemoryStore.PlayerName);
        HistorySeed seed = RegardHistory.SeedOf(new NpcHistory("Haley", Loved: 1), T)!;
        Assert.Equal(inPlay * o.HistoryFade, seed.Raw, 6);
        Assert.InRange(seed.Regard, inPlay * o.HistoryFade * 0.95, inPlay * o.HistoryFade);
    }

    [Fact]
    public void MoreGiftsMeanMoreWarmth_ButNeverPastTheCap()
    {
        double few = Of(new NpcHistory("Haley", Loved: 3));
        double many = Of(new NpcHistory("Haley", Loved: 12, Liked: 15, Neutral: 4, HeartEventsSeen: 2));
        double fifty = Of(new NpcHistory("Haley", Loved: 50));
        Assert.True(0 < few && few < many && many < fifty);
        Assert.True(fifty <= new MotiveOptions().HistoryMaxWarmth);
    }

    [Fact]
    public void HatedGiftsSeedAGrudge_ThatStopsShortOfThePenalty()
    {
        var o = new MotiveOptions();
        Assert.True(Of(new NpcHistory("Shane", Hated: 2)) < 0);
        double worst = Of(new NpcHistory("Shane", Hated: 200));
        Assert.True(worst >= -o.HistoryMaxGrudge && -worst < o.GrudgeThreshold);
        // Good and bad net out.
        Assert.Equal(0, Of(new NpcHistory("Shane", Loved: 5, Hated: 5))); // equal marks cancel
        Assert.True(Of(new NpcHistory("Shane", Loved: 6, Hated: 5)) > 0);
        Assert.True(Of(new NpcHistory("Shane", Liked: 1, Hated: 3)) < 0);
    }

    [Fact]
    public void SensitiveVillagersTakeHistoryHarder()
    {
        Temperament thick = T with { Sensitivity = 0.1 };
        Temperament thin = T with { Sensitivity = 0.9 };
        var h = new NpcHistory("Emily", Loved: 4);
        Assert.True(RegardHistory.SeedOf(h, thin)!.Regard > RegardHistory.SeedOf(h, thick)!.Regard);
    }

    [Fact]
    public void StatusSetsAFloorOrALastingGrudge()
    {
        var o = new MotiveOptions();
        Assert.Equal(o.HistoryDatingFloor, Of(new NpcHistory("Leah", Status: HistoryStatus.Dating)), 6);
        Assert.Equal(o.HistoryMarriedFloor, Of(new NpcHistory("Leah", Status: HistoryStatus.Married)), 6);
        // A floor, not a replacement: a long, warm history stays above it.
        Assert.True(Of(new NpcHistory("Leah", Loved: 40, HeartEventsSeen: 8, Status: HistoryStatus.Dating)) > o.HistoryDatingFloor);
        // Divorce outweighs the gifts before it.
        Assert.Equal(-o.HistoryDivorcedGrudge, Of(new NpcHistory("Leah", Loved: 40, Status: HistoryStatus.Divorced)), 6);
    }

    [Fact]
    public void SeedingAddsOntoRegardEarnedInPlay()
    {
        var book = new RegardBook();
        book.Set("Abigail", MemoryStore.PlayerName, -0.2); // earned in play, after the install
        var histories = new[]
        {
            new NpcHistory("Haley", Loved: 6),
            new NpcHistory("Abigail", Loved: 6),
            new NpcHistory("Gus"), // nothing to seed
        };
        IReadOnlyList<HistorySeed> applied = RegardHistory.Seed(book, histories, _ => T);

        Assert.Equal(new[] { "Abigail", "Haley" }, applied.Select(s => s.Npc)); // name order
        double haley = book.Of("Haley", MemoryStore.PlayerName);
        Assert.Equal(applied[1].After, haley, 6);
        Assert.Equal(-0.2, applied[0].Before, 6);
        Assert.Equal(-0.2 + haley, book.Of("Abigail", MemoryStore.PlayerName), 3); // the same history, added on
        Assert.Equal(0, book.Of("Gus", MemoryStore.PlayerName));
        Assert.StartsWith("seeded regard from history: Abigail -0.20 -> +", applied[0].Line);
    }

    [Fact]
    public void WhatTheModAlreadySawIsLeftOut()
    {
        // The game's log has 8 gifts to Haley; the mod's diary already noted 3 of them (and other
        // entries that are not the player's gifts), so only 5 are seeded.
        var diary = new[]
        {
            new DiaryEntry(10, MemoryStore.PlayerName, "GiftReceived", "taste=Love;item=(O)1"),
            new DiaryEntry(20, MemoryStore.PlayerName, "GiftReceived", "taste=Love;item=(O)1"),
            new DiaryEntry(30, MemoryStore.PlayerName, "GiftReceived", "taste=Neutral;item=(O)2"),
            new DiaryEntry(40, MemoryStore.PlayerName, "Talked", "hearts=4"),
            new DiaryEntry(50, "Alex", "GiftReceived", "taste=Love;item=(O)1"),
        };
        NpcHistory seen = RegardHistory.SeenInDiary("Haley", diary);
        Assert.Equal(new NpcHistory("Haley", Loved: 2, Neutral: 1), seen);

        NpcHistory game = new("Haley", Loved: 6, Neutral: 1, Hated: 1, Status: HistoryStatus.Dating);
        Assert.Equal(new NpcHistory("Haley", Loved: 4, Neutral: 0, Hated: 1, Status: HistoryStatus.Dating), game.Except(seen));
        Assert.Equal(0, new NpcHistory("Haley", Loved: 1).Except(seen).Loved); // never below 0
    }

    [Fact]
    public void TheLineSaysWhy()
    {
        HistorySeed seed = RegardHistory.SeedOf(new NpcHistory("Haley", Loved: 12, Liked: 15, Neutral: 4, HeartEventsSeen: 2), T)!;
        Assert.StartsWith("seeded regard from history: Haley +0.", seed.Line);
        Assert.EndsWith("(31 gifts: 12 loved, 15 liked, 4 neutral; 2 heart events)", seed.Line);
        Assert.Equal("1 gift: 1 hated; divorced", RegardHistory.SeedOf(new NpcHistory("Sam", Hated: 1, Status: HistoryStatus.Divorced), T)!.Because);
    }
}
