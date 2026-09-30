using Xunit;

namespace NpcDecision.Tests;

public class NpcCardTests
{
    [Fact]
    public void Render_ProducesTheCardFormat()
    {
        string card = NpcCard.Render("Haley", "manners polite, outgoing, optimistic",
            "sunny, a little vain, warms up slowly", 4, "spring 12 (Tuesday), sunny, 7:30 PM");

        Assert.Equal(
            "npc: Haley\n"
            + "temperament: manners polite, outgoing, optimistic\n"
            + "voice: sunny, a little vain, warms up slowly\n"
            + "hearts with the player: 4 of 10\n"
            + "today: spring 12 (Tuesday), sunny, 7:30 PM",
            card);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingTemperamentOrVoice_RendersUnknown(string? missing)
    {
        string card = NpcCard.Render("Haley", missing!, missing!, 4, "spring 12 (Tuesday), sunny");

        Assert.Contains("temperament: unknown\n", card);
        Assert.Contains("voice: unknown\n", card);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(10, 10)]
    [InlineData(99, 10)]
    public void Hearts_AreClampedToZeroThroughTen(int hearts, int expected)
    {
        Assert.Contains($"hearts with the player: {expected} of 10", NpcCard.Render("Haley", "t", "v", hearts, "today"));
    }

    [Fact]
    public void Render_IsDeterministic()
    {
        string a = NpcCard.Render("Haley", "t", "v", 4, "today");
        for (int i = 0; i < 5; i++)
            Assert.Equal(a, NpcCard.Render("Haley", "t", "v", 4, "today"));
    }
}
