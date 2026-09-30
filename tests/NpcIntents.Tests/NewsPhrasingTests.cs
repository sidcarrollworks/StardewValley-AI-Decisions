using NpcIntents;
using NpcMemory;
using Xunit;

namespace NpcIntents.Tests;

/// <summary>
/// The plain-words news sentences the model sees (week review, finding 5). They mirror the eval
/// set's phrasing (sidecar/eval/run_eval.py), and they must never contain the characters the line
/// sanitizer strips, because the cited option text shows up in logs alongside rendered lines.
/// </summary>
public class NewsPhrasingTests
{
    private const string Stripped = "#$%{[";

    [Fact]
    public void GiftReceived_NamesTheGiftTasteAndBirthday()
    {
        var entry = new DiaryEntry(100, "Player", "GiftReceived",
            DiaryDetail.Format(("item", "(O)421"), ("name", "Sunflower"), ("taste", "Love"), ("birthday", "1")));

        string sentence = NewsPhrasing.Sentence("Haley", entry, 1);

        Assert.Equal("yesterday the player gave Haley a Sunflower (a loved gift, on Haley's birthday)", sentence);
    }

    [Fact]
    public void GiftReceived_WithoutAName_FallsBackToAGenericGift()
    {
        var entry = new DiaryEntry(100, "Player", "GiftReceived", "taste=Hate");

        string sentence = NewsPhrasing.Sentence("Haley", entry, 1);

        Assert.Equal("yesterday the player gave Haley a hated gift", sentence);
    }

    [Fact]
    public void Saw_OfThePlayer_PhrasesThePlace()
    {
        var entry = new DiaryEntry(100, "Player", "Saw", "Town");

        string sentence = NewsPhrasing.Sentence("Willy", entry, 1);

        Assert.Equal("yesterday Willy saw the player at Pelican Town", sentence);
    }

    [Fact]
    public void Festival_TalkedVersusPresent()
    {
        var with = new DiaryEntry(100, "Player", "Festival", "festival=spring13;with=1");
        var without = new DiaryEntry(100, "Player", "Festival", "festival=spring13;with=0");

        Assert.Equal("yesterday Haley went to the festival with the player", NewsPhrasing.Sentence("Haley", with, 1));
        Assert.Equal("yesterday Haley was at the festival too", NewsPhrasing.Sentence("Haley", without, 1));
    }

    [Fact]
    public void DayBeforeYesterdayAndOlder()
    {
        var entry = new DiaryEntry(100, "Player", "Talked", "hearts=2");

        Assert.StartsWith("yesterday", NewsPhrasing.Sentence("Sam", entry, 1));
        Assert.StartsWith("the day before yesterday", NewsPhrasing.Sentence("Sam", entry, 2));
        Assert.StartsWith("3 days ago", NewsPhrasing.Sentence("Sam", entry, 3));
    }

    [Fact]
    public void UnknownKind_FallsBackToTheTelegraphicSummary()
    {
        var entry = new DiaryEntry(100, "Player", "SomethingNew", "abc");

        Assert.Equal("SomethingNew Player (abc)", NewsPhrasing.Sentence("Sam", entry, 1));
    }

    [Theory]
    [InlineData("GiftReceived", "item=1;name=Sunflower;taste=Love;birthday=0", "Player")]
    [InlineData("SawGift", "", "Haley")]
    [InlineData("QuestHelped", "quest=SlayMonster", "Player")]
    [InlineData("MissedFestival", "festival=spring13", "Player")]
    [InlineData("IgnoredBy", "Emote", "Player")]
    [InlineData("IgnoredBy", "Mail", "Player")]
    [InlineData("PassedBy", "ticks=6", "Player")]
    [InlineData("BirthdayForgotten", "hearts=3", "Player")]
    public void Sentences_NeverContainStrippedCharacters(string kind, string detail, string subject)
    {
        var entry = new DiaryEntry(100, subject, kind, detail);

        string sentence = NewsPhrasing.Sentence("Haley", entry, 1);

        Assert.False(string.IsNullOrWhiteSpace(sentence));
        foreach (char c in Stripped)
            Assert.DoesNotContain(c, sentence);
    }

    [Theory]
    [InlineData("Emote", "an emote")]
    [InlineData("Bubble", "a speech bubble")]
    [InlineData("Approach", "an approach")]
    [InlineData("QueuedLine", "a queued line")]
    [InlineData("Mail", "a letter")]
    [InlineData("ForcedDialogue", "a forced conversation")]
    public void IgnoredBy_PhrasesEveryLadderStep(string step, string words)
    {
        var entry = new DiaryEntry(100, "Player", "IgnoredBy", step);

        string sentence = NewsPhrasing.Sentence("Haley", entry, 1);

        Assert.Equal($"yesterday Haley tried to get the player's attention ({words}) and got none", sentence);
    }
}
