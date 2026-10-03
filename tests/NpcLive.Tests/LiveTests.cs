using NpcMemory;
using NpcMotives;
using Xunit;

namespace NpcLive.Tests;

/// <summary>Live emotes and bubbles (docs/spec/rollout.md, D30): from the runner's decision to
/// what the game is asked to show, the gate that can still say "not now", the circuit breaker and
/// the ignored-act record.</summary>
public sealed class LiveTests
{
    private const int Tick = 1000;
    private static readonly LiveSwitches Both = new() { Emote = true, Bubble = true };

    private static MotiveEvent Act(string npc, Act act, Motive motive, bool hostile = false, int tick = Tick, string kind = "Act")
        => new(tick, npc, kind, act, motive, hostile, "clear yes");

    private static LiveFacts Clear(int distance = 3) => new(SinglePlayer: true, PlayerFree: true, EventUp: false,
        Festival: false, SameLocation: true, DistanceTiles: distance, NpcBusy: false, NpcVisible: true);

    // ---- what is shown -------------------------------------------------------------------------

    [Fact]
    public void SwitchesAreOffByDefault_AndOnlyEmotesAndBubblesCanGoLive()
    {
        var none = new LiveSwitches();
        Assert.Null(LivePlanner.From(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting), none));
        Assert.Null(LivePlanner.From(Act("Gus", NpcMotives.Act.Bubble, Motive.Greeting), none));
        Assert.Null(LivePlanner.From(Act("Gus", NpcMotives.Act.WalkUp, Motive.Greeting), Both));
        Assert.Null(LivePlanner.From(Act("Gus", NpcMotives.Act.Letter, Motive.Grateful), Both));
        Assert.Null(LivePlanner.From(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting, kind: "Pass"), Both));

        var emotesOnly = new LiveSwitches { Emote = true };
        Assert.NotNull(LivePlanner.From(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting), emotesOnly));
        Assert.Null(LivePlanner.From(Act("Gus", NpcMotives.Act.Bubble, Motive.Greeting), emotesOnly));
    }

    [Fact]
    public void EachMotiveHasItsEmote_AndHostileOnesFollowBoldness()
    {
        Assert.Equal(32, LivePlanner.EmoteFor(Motive.Greeting, false));
        Assert.Equal(20, LivePlanner.EmoteFor(Motive.Grateful, false));
        Assert.Equal(16, LivePlanner.EmoteFor(Motive.MissingYou, false));
        Assert.Equal(16, LivePlanner.EmoteFor(Motive.News, false));
        Assert.Equal(8, LivePlanner.EmoteFor(Motive.Curious, false));
        Assert.Equal(12, LivePlanner.EmoteFor(Motive.Hurt, true, boldness: 0.74)); // Emily glares
        Assert.Equal(28, LivePlanner.EmoteFor(Motive.Hurt, true, boldness: 0.16)); // Shane looks hurt

        LiveAct glare = LivePlanner.From(Act("Emily", NpcMotives.Act.Emote, Motive.Hurt, hostile: true), Both)!;
        Assert.Equal(12, glare.EmoteId); // no decision attached: boldness 0.5, the bold side
        Assert.Null(glare.Text);
        Assert.Equal("Emily glared at you (emote 12, Hurt)", LivePlanner.ShownLine(glare));
    }

    [Fact]
    public void BubbleLinesAreTemplated_Sanitized_AndDeterministic()
    {
        LiveAct hi = LivePlanner.From(Act("Gunther", NpcMotives.Act.Bubble, Motive.Greeting), Both, "Sid")!;
        Assert.Equal(-1, hi.EmoteId);
        Assert.Equal(hi.Text, LivePlanner.From(Act("Gunther", NpcMotives.Act.Bubble, Motive.Greeting), Both, "Sid")!.Text);
        Assert.Contains(hi.Text, new[] { "Hi there!", "Oh, hello!", "Hey, Sid!", "Hello!" });

        // Over many ticks every line comes up, none carries a dialogue command, and a name the
        // game would read as a command is cleaned.
        var seen = new HashSet<string>();
        foreach (Motive m in Enum.GetValues<Motive>())
            foreach (bool hostile in new[] { false, true })
                for (int t = 0; t < 200; t++)
                {
                    string line = LivePlanner.LineFor("Gunther", m, hostile, t, "S#i$d");
                    Assert.False(string.IsNullOrWhiteSpace(line));
                    Assert.DoesNotContain(line, c => "#$%{[".Contains(c));
                    Assert.True(line.Length <= 45, line);
                    seen.Add(line);
                }
        Assert.Contains("Hey, Sid!", seen);
        Assert.Contains("I'm still upset with you.", seen);
    }

    [Fact]
    public void WithoutAPlayerNameTheLineStillReads()
    {
        var lines = Enumerable.Range(0, 300).Select(t => LivePlanner.LineFor("Gunther", Motive.Greeting, false, t)).ToHashSet();
        Assert.Contains("Hey!", lines);
        Assert.DoesNotContain(lines, l => l.Contains("{player}") || l.Contains(", !") || l.StartsWith("!"));
        Assert.Contains("Everything okay?", Enumerable.Range(0, 300).Select(t => LivePlanner.LineFor("Gunther", Motive.Worried, false, t)));
    }

    // ---- the gate --------------------------------------------------------------------------------

    [Fact]
    public void TheGateSaysNotNowForEachUnsafeMoment()
    {
        LiveAct wave = LivePlanner.From(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting), Both)!;
        LiveAct bubble = LivePlanner.From(Act("Gus", NpcMotives.Act.Bubble, Motive.Greeting), Both)!;

        Assert.Null(LiveGate.WhyNot(wave, Clear(), Tick));
        Assert.Null(LiveGate.WhyNot(wave, Clear(), Tick + 1)); // drained one tick later: still fine
        Assert.Contains("moment has passed", LiveGate.WhyNot(wave, Clear(), Tick + 2));
        Assert.Contains("single-player", LiveGate.WhyNot(wave, Clear() with { SinglePlayer = false }, Tick));
        Assert.Equal("an event is running", LiveGate.WhyNot(wave, Clear() with { EventUp = true }, Tick));
        Assert.Equal("a festival is on", LiveGate.WhyNot(wave, Clear() with { Festival = true }, Tick));
        Assert.Equal("the player is busy", LiveGate.WhyNot(wave, Clear() with { PlayerFree = false }, Tick));
        Assert.Equal("the player left", LiveGate.WhyNot(wave, Clear() with { SameLocation = false }, Tick));
        Assert.Equal("not visible", LiveGate.WhyNot(wave, Clear() with { NpcVisible = false }, Tick));
        Assert.Equal("already emoting or speaking", LiveGate.WhyNot(wave, Clear() with { NpcBusy = true }, Tick));

        // A wave carries further than words.
        Assert.Null(LiveGate.WhyNot(wave, Clear(distance: 10), Tick));
        Assert.Equal("too far (9 tiles, at most 8)", LiveGate.WhyNot(bubble, Clear(distance: 9), Tick));
    }

    // ---- the breaker -----------------------------------------------------------------------------

    [Fact]
    public void AThrowingActTurnsOnlyItsOwnSwitchOff()
    {
        var breaker = new LiveBreaker();
        int shown = 0;
        Assert.Null(breaker.Run(NpcMotives.Act.Emote, () => shown++));
        string? error = breaker.Run(NpcMotives.Act.Bubble, () => throw new InvalidOperationException("boom"));
        Assert.Equal("Bubble turned off for this session after an error: InvalidOperationException: boom", error);

        Assert.Equal("Bubble is off after an error", breaker.Run(NpcMotives.Act.Bubble, () => shown++));
        Assert.Null(breaker.Run(NpcMotives.Act.Emote, () => shown++));
        Assert.Equal(2, shown);

        breaker.TripAll();
        Assert.Contains("npcmod_live off", breaker.Run(NpcMotives.Act.Emote, () => shown++));
        Assert.Equal(2, shown);
    }

    // ---- ignored for real --------------------------------------------------------------------

    [Fact]
    public void AnIgnoredActThatWasShownIsWrittenToTheDiary_OneNeverShownIsNot()
    {
        var ledger = new LiveLedger();
        LiveAct wave = LivePlanner.From(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting), Both)!;
        ledger.Shown(wave);

        // Another villager's ignored act was never shown: nothing to write.
        Assert.Null(ledger.OnResolved(Act("Pam", NpcMotives.Act.Emote, Motive.Greeting, tick: Tick + 6, kind: "Ignored")));

        DiaryEntry? entry = ledger.OnResolved(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting, tick: Tick + 6, kind: "Ignored"));
        Assert.Equal(new DiaryEntry(Tick + 6, MemoryStore.PlayerName, "IgnoredBy", "Emote"), entry);
        Assert.Equal(0, ledger.Count);

        // Answered: cleared, nothing written.
        ledger.Shown(wave with { DecidedTick = Tick + 10 });
        Assert.Null(ledger.OnResolved(Act("Gus", NpcMotives.Act.Emote, Motive.Greeting, tick: Tick + 12, kind: "Responded")));
        Assert.Equal(0, ledger.Count);
    }
}
