using Xunit;

namespace NpcDecision.Tests;

/// <summary>Pins the shared temperament-word mapping used by both the mod's card and the card
/// exporter, so the eval's cards can't drift from the mod's wording.</summary>
public class CardTextTests
{
    [Fact]
    public void TemperamentWordsMatchTheModsCardLine()
    {
        Assert.Equal("manners polite, outgoing, optimistic", CardText.TemperamentWords("Polite", "Outgoing", "Positive"));
        Assert.Equal("manners rude, shy, pessimistic", CardText.TemperamentWords("Rude", "Shy", "Negative"));
        Assert.Equal("manners neutral, neutral, neutral", CardText.TemperamentWords("Neutral", "Neutral", "Neutral"));
        // Unknown or garbage values fall back to neutral, never throw.
        Assert.Equal("manners neutral, neutral, neutral", CardText.TemperamentWords("", "Whatever", ""));
        // Case-insensitive, as the exporter's fixtures and the game's enums may differ in case.
        Assert.Equal("manners polite, outgoing, optimistic", CardText.TemperamentWords("polite", "OUTGOING", "Positive"));
    }

    [Fact]
    public void OptimismWordIsTheCardsAdjective()
    {
        Assert.Equal("optimistic", CardText.OptimismWord("Positive"));
        Assert.Equal("pessimistic", CardText.OptimismWord("Negative"));
        Assert.Equal("neutral", CardText.OptimismWord("Neutral"));
        Assert.Equal("neutral", CardText.OptimismWord(""));
    }
}
