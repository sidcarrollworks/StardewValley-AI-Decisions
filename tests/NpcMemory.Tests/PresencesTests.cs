using Xunit;

namespace NpcMemory.Tests;

public class PresencesTests
{
    [Theory]
    [InlineData(true, true, true)]    // Haley, Sandy, Krobus
    [InlineData(true, false, false)]  // the Bouncer, Mister Qi, Gunther
    [InlineData(false, true, false)]  // not a villager (a monster, an animal)
    [InlineData(false, false, false)]
    public void OnlyVillagersWhoCanSocializeAreWatched(bool villager, bool social, bool tracked)
        => Assert.Equal(tracked, Presences.Tracks(villager, social));

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
