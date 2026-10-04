using System.Globalization;
using NpcIntents;
using NpcMotives;
using NpcSchedules;

namespace NpcLive;

/// <summary>
/// The live switches (docs/spec/rollout.md): each behavior has its own, off by default, read once
/// at <c>Entry</c>. Sid asked for <c>Emote</c> and <c>Bubble</c> first, friendly and hostile
/// alike (2026-10-02, D30).
/// </summary>
public sealed class LiveSwitches
{
    public bool Emote { get; set; }
    public bool Bubble { get; set; }

    public bool IsOn(Act act) => act switch
    {
        Act.Emote => Emote,
        Act.Bubble => Bubble,
        _ => false, // nothing else is live yet
    };
}

/// <summary>Tuning for the live acts. Code defaults, not saved.</summary>
public sealed class LiveOptions
{
    /// <summary>An emote is seen from further away than a bubble is read.</summary>
    public int EmoteMaxTiles { get; set; } = 10;
    public int BubbleMaxTiles { get; set; } = 8;

    /// <summary>A decision older than this many ticks when it reaches the game thread is dropped:
    /// the moment has passed.</summary>
    public int MaxDelayTicks { get; set; } = 1;

    /// <summary>How long a bubble stays up (<c>NPC.showTextAboveHead</c>'s duration, ms).</summary>
    public int BubbleMs { get; set; } = 3000;

    /// <summary>A villager shows at most one live act in this many ticks: a safety net under the
    /// runner's own cooldown (6 ticks), in case anything ever shows one villager's acts back to back.</summary>
    public int MinTicksBetweenActs { get; set; } = 3;
}

/// <summary>
/// One act to show in the game: the villager, the act, the emote id (emotes) or the line
/// (bubbles), and when the motives runner decided it.
/// </summary>
public sealed record LiveAct(
    string Npc,
    Act Act,
    Motive Motive,
    bool Hostile,
    int DecidedTick,
    int EmoteId,       // -1 for a bubble
    string? Text);     // null for an emote

/// <summary>
/// Turns the motives runner's decisions into live acts (rollout.md, "Emote" and "Bubble"). Pure
/// and deterministic: the emote follows the motive, the line is a template picked with
/// <see cref="Fnv1a"/> from (npc, motive, tick). The model never writes the text (AGENTS.md rule 6).
/// </summary>
public static class LivePlanner
{
    /// <summary>The live act for a runner event, or null when the event isn't an act, the act
    /// isn't an emote or a bubble, or its switch is off.</summary>
    public static LiveAct? From(MotiveEvent ev, LiveSwitches switches, string playerName = "")
    {
        if (ev.Kind != "Act" || ev.Act is not { } act || ev.Motive is not { } motive || !switches.IsOn(act))
            return null;
        double boldness = ev.Decision?.Boldness ?? 0.5;
        return act == Act.Emote
            ? new LiveAct(ev.Npc, act, motive, ev.Hostile, ev.AbsoluteTick, EmoteFor(motive, ev.Hostile, boldness), null)
            : new LiveAct(ev.Npc, act, motive, ev.Hostile, ev.AbsoluteTick, -1,
                LineFor(ev.Npc, motive, ev.Hostile, ev.AbsoluteTick, playerName));
    }

    /// <summary>
    /// The emote for a motive (ids confirmed in the 1.6.15 decompile, stardew-source-notes.md):
    /// happy 32 for a greeting, heart 20 for gratitude, exclamation 16 for missing the player,
    /// news and worry, question 8 for curiosity, a trade or a request. A hostile emote is angry 12
    /// from the bold (boldness 0.5+) and sad 28 from the shy: a hurt shy villager looks hurt,
    /// not angry. First guesses, for Sid to tune.
    /// </summary>
    public static int EmoteFor(Motive motive, bool hostile, double boldness = 0.5)
    {
        if (hostile)
            return boldness >= 0.5 ? 12 : 28;
        return motive switch
        {
            Motive.Greeting => 32,
            Motive.Grateful => 20,
            Motive.MissingYou or Motive.News or Motive.Worried => 16,
            Motive.Curious or Motive.WantsToTrade or Motive.NeedsHelp => 8,
            _ => 32,
        };
    }

    /// <summary>The plain lines per motive, friendly and hostile: the fallback when a villager has
    /// no voice for the feeling in <see cref="BubbleVoices"/>. "{player}" is the farmer's name.</summary>
    private static readonly IReadOnlyDictionary<Motive, string[]> Friendly = new Dictionary<Motive, string[]>
    {
        [Motive.Greeting] = new[] { "Hi there!", "Oh, hello!", "Hey, {player}!", "Hello!" },
        [Motive.MissingYou] = new[] { "There you are!", "Long time no see!", "{player}! It's been a while." },
        [Motive.News] = new[] { "Oh! I've got news.", "Got a minute? I heard something.", "Wait till you hear this!" },
        [Motive.Grateful] = new[] { "Thanks again!", "I haven't forgotten, {player}. Thank you.", "You're too kind." },
        [Motive.Worried] = new[] { "Are you alright?", "Everything okay, {player}?", "I was starting to worry." },
        [Motive.Curious] = new[] { "What are you up to?", "Ooh, what's that?" },
        [Motive.WantsToTrade] = new[] { "Got anything to trade?" },
        [Motive.NeedsHelp] = new[] { "Could you help me with something?" },
    };

    private static readonly IReadOnlyDictionary<Motive, string[]> Hostile = new Dictionary<Motive, string[]>
    {
        [Motive.Hurt] = new[] { "Oh. It's you.", "Hmph.", "I'm still upset with you." },
        [Motive.Jealous] = new[] { "Looks like you've been busy.", "Hmph. Nice of you to notice me." },
    };

    private static readonly string[] HostileDefault = { "Hmph." };
    private static readonly string[] FriendlyDefault = { "Hello!" };

    /// <summary>The bubble line for a motive, sanitized (AGENTS.md rule 6), deterministic for the
    /// same villager, motive and tick.</summary>
    public static string LineFor(string npc, Motive motive, bool hostile, int tick, string playerName = "")
    {
        // The villager's own voice first (BubbleVoices); the plain lines when it has none for this.
        string[] lines = BubbleVoices.For(npc, motive, hostile);
        if (lines.Length == 0)
            lines = hostile
                ? Hostile.TryGetValue(motive, out string[]? h) ? h : HostileDefault
                : Friendly.TryGetValue(motive, out string[]? f) ? f : FriendlyDefault;
        int pick = (int)((uint)Fnv1a.Seed("bubble", npc, motive.ToString(), tick.ToString(CultureInfo.InvariantCulture)) % (uint)lines.Length);
        string line = lines[pick];
        string name = LineSanitizer.Sanitize(playerName ?? "").Trim();
        // Without a name, drop it with the comma or space before it ("Hey, {player}!" -> "Hey!",
        // "Oh, hello {player}." -> "Oh, hello."), and any punctuation it leaves at the start.
        line = name.Length == 0
            ? System.Text.RegularExpressions.Regex.Replace(
                System.Text.RegularExpressions.Regex.Replace(line, @",?\s*\{player\}", ""), @"^[!.,?]\s*", "")
            : line.Replace("{player}", name);
        return LineSanitizer.Sanitize(line).Trim();
    }

    /// <summary>The <c>[live]</c> log line for an act shown.</summary>
    public static string ShownLine(LiveAct a) => a.Act == Act.Emote
        ? $"{a.Npc} {(a.Hostile ? "glared at" : "waved at")} you (emote {a.EmoteId}, {a.Motive}; decided at tick {a.DecidedTick})"
        : $"{a.Npc} said \"{a.Text}\" ({a.Motive}{(a.Hostile ? ", hostile" : "")}; decided at tick {a.DecidedTick})";

    /// <summary>The <c>[live]</c> log line for an act the gate held back.</summary>
    public static string SkippedLine(LiveAct a, string reason)
        => $"{a.Npc}: {(a.Act == Act.Emote ? "emote" : "bubble")} not shown ({reason})";
}
