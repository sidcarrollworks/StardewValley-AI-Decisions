namespace NpcMemory;

/// <summary>Who this copy of the mod is running for (docs/spec/multiplayer-compat.md).</summary>
public enum PlayerRole
{
    /// <summary>A single-player game: the mod runs as designed.</summary>
    Solo,
    /// <summary>The host of a multiplayer game: the mod runs, watching the host's farmer only.</summary>
    Host,
    /// <summary>A split-screen guest on the host's computer: that screen's handlers do nothing.</summary>
    SplitScreenGuest,
    /// <summary>A farmhand joined to someone else's game: the mod stays off.</summary>
    Farmhand,
}

/// <summary>
/// Single-player for now (Sid, 2026-09-30; docs/spec/multiplayer-compat.md): NPC memory lives on
/// the host's main screen only. A farmhand on a remote host can't read or write the save's mod
/// data and sees only part of the world, so the mod stays off there; a split-screen guest shares
/// the host's process, so its screen's events are ignored. Pure: the mod passes SMAPI's
/// <c>Context</c> flags.
/// </summary>
public static class HostOnly
{
    public static PlayerRole RoleOf(bool isMultiplayer, bool isMainPlayer, bool isOnHostComputer)
    {
        if (isMainPlayer)
            return isMultiplayer ? PlayerRole.Host : PlayerRole.Solo;
        return isOnHostComputer ? PlayerRole.SplitScreenGuest : PlayerRole.Farmhand;
    }

    /// <summary>Whether the mod's game-event handlers run for this role.</summary>
    public static bool Runs(PlayerRole role) => role is PlayerRole.Solo or PlayerRole.Host;

    /// <summary>The one log line for a role that isn't plain single-player, or null.</summary>
    public static string? Notice(PlayerRole role) => role switch
    {
        PlayerRole.Farmhand => "Multiplayer farmhand: NPC memory runs on the host only. The mod stays off in this game; nothing is recorded or saved.",
        PlayerRole.SplitScreenGuest => "Split-screen guest: NPC memory follows the main screen's farmer only; this screen is ignored.",
        PlayerRole.Host => "Multiplayer host: NPC memory watches only your farmer; other farmers are ignored, and live emotes and bubbles stay off.",
        _ => null,
    };
}
