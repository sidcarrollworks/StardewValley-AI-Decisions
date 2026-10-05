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
    public void EveryRoleButSoloSaysWhatItDoes()
    {
        Assert.Null(HostOnly.Notice(PlayerRole.Solo));
        Assert.Contains("host only", HostOnly.Notice(PlayerRole.Farmhand));
        Assert.Contains("stays off", HostOnly.Notice(PlayerRole.Farmhand));
        Assert.Contains("ignored", HostOnly.Notice(PlayerRole.SplitScreenGuest));
        Assert.Contains("other farmers are ignored", HostOnly.Notice(PlayerRole.Host));
    }
}
