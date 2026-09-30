using NpcMemory;
using Xunit;

namespace NpcDiaryEvents.Tests;

public class QuestNotesTests
{
    [Fact]
    public void ToDiaryEntry_BuildsQuestHelpedEntry()
    {
        var details = new QuestDetails("Abigail", QuestNotes.ItemDelivery, 42_000);

        DiaryEntry entry = QuestNotes.ToDiaryEntry(details);

        Assert.Equal(42_000, entry.AbsoluteTick);
        Assert.Equal("Player", entry.Subject);
        Assert.Equal("QuestHelped", entry.Kind);
        Assert.Equal("quest=ItemDelivery;name=Abigail", entry.Detail);
    }

    [Fact]
    public void ToDiaryEntry_DetailParsesToQuestAndName()
    {
        var details = new QuestDetails("Willy", QuestNotes.Fishing, 123);

        DiaryEntry entry = QuestNotes.ToDiaryEntry(details);

        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse(entry.Detail);
        Assert.Equal(QuestNotes.Fishing, parsed["quest"]);
        Assert.Equal("Willy", parsed["name"]);
    }

    [Theory]
    [InlineData(QuestNotes.ItemDelivery)]
    [InlineData(QuestNotes.Fishing)]
    [InlineData(QuestNotes.SlayMonster)]
    [InlineData(QuestNotes.ResourceCollection)]
    [InlineData(QuestNotes.Special)]
    public void ToDiaryEntry_StoresKnownQuestLabels(string label)
    {
        DiaryEntry entry = QuestNotes.ToDiaryEntry(new QuestDetails("Robin", label, 5));

        Assert.Equal(label, DiaryDetail.Parse(entry.Detail)["quest"]);
    }

    [Fact]
    public void ToDiaryEntry_StoresEmptyQuestLabelAsIs()
    {
        DiaryEntry entry = QuestNotes.ToDiaryEntry(new QuestDetails("Abigail", "", 7));

        Assert.Equal("quest=;name=Abigail", entry.Detail);
        Assert.Equal("Abigail", DiaryDetail.Parse(entry.Detail)["name"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDiaryEntry_ThrowsArgumentExceptionForMissingTargetNpc(string? targetNpc)
    {
        var details = new QuestDetails(targetNpc!, QuestNotes.Special, 1);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => QuestNotes.ToDiaryEntry(details));

        // The contract wants ArgumentException, not its ArgumentNullException subtype.
        Assert.NotEqual(typeof(ArgumentNullException), ex.GetType());
    }
}
