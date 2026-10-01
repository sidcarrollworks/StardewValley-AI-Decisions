using NpcTemperament;
using Xunit;

namespace NpcTemperament.Tests;

public class DialogueTextTests
{
    [Fact]
    public void SplitsPagesAndReadsPortraits()
    {
        var pages = DialogueText.Pages("Hi, @!$h#$b#I'm tired today...$s#$e#Leave me alone.$5");

        Assert.Equal(3, pages.Count);
        Assert.Equal(new DialoguePage("Hi, you!", Mood.Happy), pages[0]);
        Assert.Equal(new DialoguePage("I'm tired today...", Mood.Sad), pages[1]);
        Assert.Equal(new DialoguePage("Leave me alone.", Mood.Angry), pages[2]);
    }

    [Fact]
    public void KeepsTheFirstGenderVariantAndDropsTokensAndActions()
    {
        var pages = DialogueText.Pages("He's nice.^She's nice.");
        Assert.Equal("He's nice.", Assert.Single(pages).Text);

        var tokens = DialogueText.Pages("*sigh* Take this %fork [Gem] {0} please.$l");
        Assert.Equal(new DialoguePage("Take this please.", Mood.Love), Assert.Single(tokens));
    }

    [Fact]
    public void SkipsQuestionAndResponseCommandSegments()
    {
        var pages = DialogueText.Pages("$q 101/102 fallback#Do you like fish?#$r 101 50 fish_yes#Yes!#$r 102 -50 fish_no#No.");

        Assert.Equal("Do you like fish?", Assert.Single(pages).Text);
        Assert.DoesNotContain(pages, p => p.Text.Contains("fallback") || p.Text.Contains("fish_yes"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("$b")]
    [InlineData("#$e#")]
    public void EmptyOrCommandOnlyHasNoPages(string raw) => Assert.Empty(DialogueText.Pages(raw));
}

public class DialogueFeaturesTests
{
    private static List<DialoguePage> P(params string[] lines) => lines.Select(l => new DialoguePage(l, Mood.None)).ToList();

    [Fact]
    public void RatesAreSharesOfPages()
    {
        var pages = P("Thanks so much!", "Why are you talking to me?", "Hmm...", "I heard Pam was out late.");
        pages[0] = pages[0] with { Mood = Mood.Happy };

        var f = DialogueFeatures.Compute(pages, new[] { "Pam" });

        Assert.Equal(4, f.Pages);
        Assert.Equal(0.25, f.Happy);
        Assert.Equal(0.25, f.Thanks);
        Assert.Equal(0.25, f.Dismiss);
        Assert.Equal(0.25, f.Question);
        Assert.Equal(0.25, f.Exclaim);
        Assert.Equal(0.25, f.Trailing);
        Assert.Equal(0.25, f.Gossip);
    }

    [Fact]
    public void NamesMatchCaseSensitively()
    {
        var beach = DialogueFeatures.Compute(P("What a sandy beach."), new[] { "Sandy" });
        var person = DialogueFeatures.Compute(P("Sandy runs the oasis."), new[] { "Sandy" });

        Assert.Equal(0, beach.Gossip);
        Assert.Equal(1, person.Gossip);
    }

    [Fact]
    public void NoPagesGivesZeros()
    {
        var f = DialogueFeatures.Compute(Array.Empty<DialoguePage>(), new[] { "Sam" });
        Assert.Equal(0, f.Pages);
        Assert.Equal(0, f.WordsPerPage);
    }
}
