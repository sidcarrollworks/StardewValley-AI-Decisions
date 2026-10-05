using Xunit;

namespace NpcMemory.Tests;

/// <summary>Single-player for now (docs/spec/multiplayer-compat.md): who the mod runs for.</summary>
public class HostOnlyTests
{
    [Theory]
    [InlineData(false, true, true, PlayerRole.Solo, true)]
    [InlineData(true, true, true, PlayerRole.Host, true)]
    [InlineData(true, false, true, PlayerRole.SplitScreenGuest, false)]
    [InlineData(true, false, false, PlayerRole.Farmhand, false)]
    public void OnlyTheHostsMainScreenRunsTheMod(bool multiplayer, bool mainPlayer, bool onHostComputer, PlayerRole role, bool runs)
    {
        Assert.Equal(role, HostOnly.RoleOf(multiplayer, mainPlayer, onHostComputer));
        Assert.Equal(runs, HostOnly.Runs(role));
    }

    [Fact]
    public void ASplitScreenGuestIsIgnored_AndLeavingForTheTitleKeepsTheHostsMemory()
    {
        // The host's screen loads first (screen 0), then a guest joins on screen 1: the guest's
        // handlers do nothing, and the guest leaving for the title resets nothing.
        PlayerRole host = HostOnly.RoleOf(isMultiplayer: true, isMainPlayer: true, isOnHostComputer: true);
        PlayerRole guest = HostOnly.RoleOf(isMultiplayer: true, isMainPlayer: false, isOnHostComputer: true);
        Assert.True(HostOnly.Runs(host));
        Assert.False(HostOnly.Runs(guest));
        Assert.False(HostOnly.ResetsAtTitle(screenId: 1));
        Assert.False(HostOnly.ResetsAtTitle(screenId: 2)); // a guest who left and rejoined gets a new id

        // The host returning to the title resets, as does a farmhand's own screen (always 0).
        Assert.True(HostOnly.ResetsAtTitle(screenId: 0));
    }

    [Fact]
    public void EveryRoleButSoloSaysWhatItDoes()
    {
        Assert.Null(HostOnly.Notice(PlayerRole.Solo));
        Assert.Contains("host only", HostOnly.Notice(PlayerRole.Farmhand));
        Assert.Contains("stays off", HostOnly.Notice(PlayerRole.Farmhand));
        Assert.Contains("ignored", HostOnly.Notice(PlayerRole.SplitScreenGuest));
        Assert.Contains("other farmers are ignored", HostOnly.Notice(PlayerRole.Host));
    }
}
