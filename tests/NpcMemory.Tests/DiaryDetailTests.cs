using Xunit;

namespace NpcMemory.Tests;

public class DiaryDetailTests
{
    [Fact]
    public void FormatAndParse_RoundTrip()
    {
        string formatted = DiaryDetail.Format(("item", "Sunflower"), ("taste", "Love"));
        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse(formatted);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("Sunflower", parsed["item"]);
        Assert.Equal("Love", parsed["taste"]);
    }

    [Fact]
    public void Format_StripsDelimitersAndDialogueCharacters()
    {
        string formatted = DiaryDetail.Format(("name", "a;b=c#d$e%f{g[h"));
        Assert.Equal("name=abcdefgh", formatted);
    }

    [Fact]
    public void Format_SkipsEmptyKeys()
    {
        Assert.Equal("", DiaryDetail.Format(("", "value")));
        Assert.Equal("", DiaryDetail.Format());
    }

    [Fact]
    public void Parse_PlainString_IsEmpty()
    {
        Assert.Empty(DiaryDetail.Parse("SeedShop"));
        Assert.Empty(DiaryDetail.Parse(""));
        Assert.Empty(DiaryDetail.Parse(null));
    }

    [Fact]
    public void Parse_IgnoresMalformedPairs()
    {
        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse("noEquals;=novalue;item=Sunflower");
        Assert.Single(parsed);
        Assert.Equal("Sunflower", parsed["item"]);
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse("taste=Love");
        Assert.Equal("Love", parsed["TASTE"]);
    }

    [Fact]
    public void Parse_ValueMayContainEquals()
    {
        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse("item=a=b");
        Assert.Equal("a=b", parsed["item"]);
    }

    [Fact]
    public void Parse_ValuesAreTrimmed()
    {
        IReadOnlyDictionary<string, string> parsed = DiaryDetail.Parse(" item = Sunflower ");
        Assert.Equal("Sunflower", parsed["item"]);
    }

    [Fact]
    public void Format_StripsTheDelimitersAndDialogueCommandCharacters()
    {
        // Pins the exact sanitizer list (the LineSanitizer set lives in NpcIntents, which
        // NpcMemory cannot reference; this test stops the two lists drifting apart). The list is
        // ; = # $ % { [ — note `]` is NOT stripped, matching LineSanitizer.
        string detail = DiaryDetail.Format(("it#em", "Sun$flower{1"), ("note", "hi%[there"),
            ("place", "Town"), ("k", "a;b"), ("x", "c=d"), ("ok", "a]b"));

        Assert.Equal("item=Sunflower1;note=hithere;place=Town;k=ab;x=cd;ok=a]b", detail);
    }
}
