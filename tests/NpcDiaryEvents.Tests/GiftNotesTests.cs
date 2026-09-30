using NpcMemory;
using Xunit;

namespace NpcDiaryEvents.Tests;

public class GiftNotesTests
{
    private static GiftDetails Details(
        string? recipientNpc = "Haley",
        string? qualifiedItemId = "(O)18",
        string? displayName = "Sunflower",
        int taste = 0,
        bool isBirthday = false,
        bool isWinterStar = false,
        int absoluteTick = 4608)
        => new(recipientNpc!, qualifiedItemId!, displayName!, taste, isBirthday, isWinterStar, absoluteTick);

    [Theory]
    [InlineData(0, "Love")]     // gift_taste_love
    [InlineData(2, "Like")]     // gift_taste_like
    [InlineData(4, "Dislike")]  // gift_taste_dislike
    [InlineData(6, "Hate")]     // gift_taste_hate
    [InlineData(7, "Love")]     // Stardrop Tea (gift_taste_stardropTea) is universally loved
    [InlineData(8, "Neutral")]  // gift_taste_neutral
    [InlineData(1, "Neutral")]  // unknown values fall back to Neutral
    [InlineData(-5, "Neutral")]
    [InlineData(99, "Neutral")]
    public void TasteLabel_MapsGiftTasteValues(int taste, string expected)
    {
        Assert.Equal(expected, GiftNotes.TasteLabel(taste));
    }

    [Fact]
    public void ToDiaryEntry_GiftReceived_ParsesToTheExpectedDetail()
    {
        DiaryEntry entry = GiftNotes.ToDiaryEntry(Details(
            recipientNpc: "Haley",
            qualifiedItemId: "(O)18",
            displayName: "Sunflower",
            taste: 0,
            absoluteTick: 4608));

        Assert.Equal(4608, entry.AbsoluteTick);
        Assert.Equal("Player", entry.Subject);
        Assert.Equal("GiftReceived", entry.Kind);

        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse(entry.Detail);
        Assert.Equal("(O)18", parsed["item"]);
        Assert.Equal("Sunflower", parsed["name"]);
        Assert.Equal("Love", parsed["taste"]);
        Assert.Equal("0", parsed["birthday"]);
        Assert.False(parsed.ContainsKey("festival"));
    }

    [Fact]
    public void ToDiaryEntry_FormatsThePinnedDetailString()
    {
        // Pins the exact wire format: key order and the birthday encoding.
        Assert.Equal(
            "item=(O)18;name=Sunflower;taste=Love;birthday=0",
            GiftNotes.ToDiaryEntry(Details()).Detail);
    }

    [Theory]
    [InlineData(2, "Like")]
    [InlineData(4, "Dislike")]
    [InlineData(6, "Hate")]
    [InlineData(8, "Neutral")]
    public void ToDiaryEntry_WritesTheTasteLabel(int taste, string expectedTaste)
    {
        IReadOnlyDictionary<string, string> parsed =
            DiaryDetail.Parse(GiftNotes.ToDiaryEntry(Details(taste: taste)).Detail);

        Assert.Equal(expectedTaste, parsed["taste"]);
    }

    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public void ToDiaryEntry_BirthdayFlagIsWritten(bool isBirthday, string expected)
    {
        IReadOnlyDictionary<string, string> parsed =
            DiaryDetail.Parse(GiftNotes.ToDiaryEntry(Details(isBirthday: isBirthday)).Detail);

        Assert.Equal(expected, parsed["birthday"]);
    }

    [Fact]
    public void ToDiaryEntry_WinterStar_AddsFestivalWinterStar()
    {
        string? detail = GiftNotes.ToDiaryEntry(Details(isWinterStar: true)).Detail;

        Assert.Equal("item=(O)18;name=Sunflower;taste=Love;birthday=0;festival=WinterStar", detail);
        Assert.Equal("WinterStar", DiaryDetail.Parse(detail)["festival"]);
    }

    [Fact]
    public void ToDiaryEntry_NotWinterStar_OmitsFestival()
    {
        IReadOnlyDictionary<string, string> parsed =
            DiaryDetail.Parse(GiftNotes.ToDiaryEntry(Details(isWinterStar: false)).Detail);

        Assert.False(parsed.ContainsKey("festival"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_MissingRecipient_Throws(string? recipientNpc)
    {
        Assert.Throws<ArgumentException>(() =>
            GiftNotes.ToDiaryEntry(Details(recipientNpc: recipientNpc)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_MissingItemId_Throws(string? qualifiedItemId)
    {
        Assert.Throws<ArgumentException>(() =>
            GiftNotes.ToDiaryEntry(Details(qualifiedItemId: qualifiedItemId)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_MissingDisplayName_Throws(string? displayName)
    {
        Assert.Throws<ArgumentException>(() =>
            GiftNotes.ToDiaryEntry(Details(displayName: displayName)));
    }

    [Fact]
    public void ToDiaryEntry_SameInputs_ProduceIdenticalEntries()
    {
        GiftDetails first = Details(taste: 7, isBirthday: true, isWinterStar: true, absoluteTick: 96);
        GiftDetails second = first with { };

        DiaryEntry firstEntry = GiftNotes.ToDiaryEntry(first);
        DiaryEntry secondEntry = GiftNotes.ToDiaryEntry(second);

        Assert.Equal(firstEntry, secondEntry);
        Assert.Equal(firstEntry.Detail, secondEntry.Detail);
    }
}
