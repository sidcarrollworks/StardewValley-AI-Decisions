using NpcMemory;
using Xunit;

namespace NpcDiaryEvents.Tests;

public class SawGiftNotesTests
{
    private static SawGiftDetails Details(
        string? observerNpc = "Penny",
        string? recipientNpc = "Haley",
        string? displayName = "Sunflower",
        int taste = 0,
        int absoluteTick = 4608)
        => new(observerNpc!, recipientNpc!, displayName!, taste, absoluteTick);

    [Fact]
    public void ToDiaryEntry_SawGift_ParsesToTheExpectedDetail()
    {
        DiaryEntry entry = SawGiftNotes.ToDiaryEntry(Details(
            observerNpc: "Penny",
            recipientNpc: "Haley",
            displayName: "Sunflower",
            taste: 0,
            absoluteTick: 4608));

        Assert.Equal(4608, entry.AbsoluteTick);
        Assert.Equal("Haley", entry.Subject); // the recipient, not the observer
        Assert.Equal("SawGift", entry.Kind);
        Assert.Equal("giver=Player;name=Sunflower;taste=Love", entry.Detail);

        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse(entry.Detail);
        Assert.Equal("Player", parsed["giver"]);
        Assert.Equal("Sunflower", parsed["name"]);
        Assert.Equal("Love", parsed["taste"]);
        Assert.Equal(3, parsed.Count);
    }

    [Theory]
    [InlineData(2, "Like")]
    [InlineData(4, "Dislike")]
    [InlineData(6, "Hate")]
    [InlineData(7, "Love")]
    [InlineData(8, "Neutral")]
    public void ToDiaryEntry_UsesTheSharedTasteLabels(int taste, string expectedTaste)
    {
        IReadOnlyDictionary<string, string> parsed =
            DiaryDetail.Parse(SawGiftNotes.ToDiaryEntry(Details(taste: taste)).Detail);

        Assert.Equal(expectedTaste, parsed["taste"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_MissingObserver_Throws(string? observerNpc)
    {
        Assert.Throws<ArgumentException>(() =>
            SawGiftNotes.ToDiaryEntry(Details(observerNpc: observerNpc)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_MissingRecipient_Throws(string? recipientNpc)
    {
        Assert.Throws<ArgumentException>(() =>
            SawGiftNotes.ToDiaryEntry(Details(recipientNpc: recipientNpc)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_MissingDisplayName_Throws(string? displayName)
    {
        Assert.Throws<ArgumentException>(() =>
            SawGiftNotes.ToDiaryEntry(Details(displayName: displayName)));
    }

    [Fact]
    public void ToDiaryEntry_SameInputs_ProduceIdenticalEntries()
    {
        SawGiftDetails first = Details(taste: 6, absoluteTick: 96);
        SawGiftDetails second = first with { };

        DiaryEntry firstEntry = SawGiftNotes.ToDiaryEntry(first);
        DiaryEntry secondEntry = SawGiftNotes.ToDiaryEntry(second);

        Assert.Equal(firstEntry, secondEntry);
        Assert.Equal(firstEntry.Detail, secondEntry.Detail);
    }
}
