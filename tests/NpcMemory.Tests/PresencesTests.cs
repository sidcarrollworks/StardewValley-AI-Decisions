using Xunit;

namespace NpcMemory.Tests;

public class PresencesTests
{
    [Fact]
    public void TwoCharactersWithOneNameCountOnce_TheFirstWins()
    {
        var list = new[]
        {
            new Presence("Mister Qi", "Club", 8, 4),
            new Presence("Gus", "Saloon", 18, 6),
            new Presence("Mister Qi", "QiNutRoom", 7, 4),
            new Presence(MemoryStore.PlayerName, "Farm", 65, 20, IsPlayer: true),
        };
        List<Presence> one = Presences.OnePerName(list);
        Assert.Equal(new[] { "Mister Qi", "Gus", MemoryStore.PlayerName }, one.Select(p => p.Name));
        Assert.Equal("Club", one[0].Location);
        Assert.True(one[2].IsPlayer);
    }
}
