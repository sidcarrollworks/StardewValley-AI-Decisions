using Xunit;
using static NpcMotives.Tests.MotivesEngineTests;

namespace NpcMotives.Tests;

/// <summary>The numbers that replaced the retired urge ladder's urge (D31): the heartbeat's
/// strongest motive, and who asks around for the player.</summary>
public sealed class MotiveDriveTests
{
    private readonly MotivesEngine _engine = new();

    [Fact]
    public void StrongestIsTheChosenMotive_AndSeekingCountsOnlyMotivesThatSendThemLooking()
    {
        var decisions = new Dictionary<string, MotiveDecision>
        {
            // Missing the player (last talk 5 days ago): looks for them.
            ["Pam"] = _engine.Decide(Inputs("Pam", Pam, hearts: 4, diary: new[] { E(5 * Day, "Talked", "hearts=4") })),
            // Grateful for a gift: a strong motive, but nothing to go looking for.
            ["Robin"] = _engine.Decide(Inputs("Robin", Robin, hearts: 6, diary: new[] { E(Day / 2, "GiftReceived", "taste=Love;item=(O)1") })),
            // A stranger: nothing at all.
            ["Linus"] = _engine.Decide(Inputs("Linus", Robin, hearts: 0, met: false) with { KnowsOfPlayer = false }),
        };

        IReadOnlyDictionary<string, double> strongest = MotiveDrive.Strongest(decisions);
        IReadOnlyDictionary<string, double> seeking = MotiveDrive.Seeking(decisions);

        Assert.True(strongest["Pam"] > 0 && strongest["Robin"] > 0);
        Assert.Equal(0, strongest["Linus"]);
        Assert.True(seeking["Pam"] >= new MotiveOptions().AskAroundStrength);
        Assert.Equal(0, seeking["Robin"]);
        Assert.Equal(0, seeking["Linus"]);
    }
}
