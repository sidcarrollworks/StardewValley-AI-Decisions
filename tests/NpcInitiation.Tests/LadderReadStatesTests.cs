using NpcDecision;
using NpcMemory;
using Xunit;

namespace NpcInitiation.Tests;

/// <summary>ReadStates gives the viewer a read-only copy of the saved ladder state.</summary>
public sealed class LadderReadStatesTests
{
    [Fact]
    public void ReadsBackWhatToJsonWrote()
    {
        var ladder = new InitiationLadder(new FakeDecisionClient(), seed: 1, new InitiationOptions { BaseGainPerTick = 0.5, HeartsGainPerTick = 0 });
        var view = new LedgerView("Sam", "Player", LedgerDetail.NamedSpot, "Town", 0, 0, 10, "1,1");
        ladder.Tick(10, new[] { new InitiationInput("Sam", view, false, 0), new InitiationInput("Abigail", view with { Observer = "Abigail" }, false, 0) }, _ => new Diary());

        IReadOnlyList<LadderNpcState> states = InitiationLadder.ReadStates(ladder.ToJson());

        Assert.Equal(new[] { "Abigail", "Sam" }, states.Select(s => s.Npc));
        Assert.Equal(ladder.Urge("Sam"), states[1].Urge);
        Assert.Equal(ladder.Rung("Sam"), states[1].Rung);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{}")]
    public void NothingUsableGivesAnEmptyList(string? json)
        => Assert.Empty(InitiationLadder.ReadStates(json));
}
