using NpcDecision;
using NpcMemory;
using Xunit;

namespace NpcInitiation.Tests;

/// <summary>
/// Review fixes: each step gets a response window that fits it (a letter is answered the next
/// day, a queued line waits for the next chat), passive steps cannot use up the daily cap, an
/// attempt left open overnight is settled on its own day, and a plain conversation relieves urge.
/// </summary>
public sealed class LadderTimingTests
{
    private const int Day = GameClock.TicksPerDay;

    private sealed class Always : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => options.Select(_ => 1.0).ToArray();
        public double Score(string context, double min, double max) => max;
        public double YesNo(string context, string proposition) => 1.0;
    }

    private sealed class Diaries
    {
        private readonly Dictionary<string, Diary> _byNpc = new(StringComparer.OrdinalIgnoreCase);
        public Diary For(string npc) => _byNpc.TryGetValue(npc, out var d) ? d : _byNpc[npc] = new Diary();
    }

    private static LedgerView Near(string npc, int tick) => new(npc, "Player", LedgerDetail.NamedSpot, "Town", 0, 0, tick, "1,1");
    private static LedgerView SeenEarlier(string npc, int tick) => new(npc, "Player", LedgerDetail.Location, "Town", 5, 0, tick - 5);
    private static LedgerView Gone(string npc, int tick) => new(npc, "Player", LedgerDetail.Gone, null, 200, 0, tick - 200);

    private static InitiationOptions Eager(Action<InitiationOptions>? tweak = null)
    {
        var o = new InitiationOptions { BaseGainPerTick = 1.0, HeartsGainPerTick = 0 };
        tweak?.Invoke(o);
        return o;
    }

    private static IReadOnlyList<InitiationEvent> One(InitiationLadder ladder, Diaries diaries, int tick, string npc, LedgerView? view, int hearts = 5)
        => ladder.Tick(tick, new[] { new InitiationInput(npc, view, false, hearts) }, diaries.For);

    // ---- letters ----------------------------------------------------------------------------------

    [Fact]
    public void ALetterIsNotIgnoredAnHourLaterButAtTheEndOfTheNextDay()
    {
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new Always(), 1, Eager());

        Assert.Equal(InitiationStep.Mail, Assert.Single(One(ladder, diaries, 10, "Leah", Gone("Leah", 10))).Step);

        // Later the same day and all of the next day: still waiting for a visit.
        for (int t = 11; t < 2 * Day; t++)
            Assert.DoesNotContain(One(ladder, diaries, t, "Leah", Gone("Leah", t)), e => e.Kind == "Ignored");

        // The day after that it counts as ignored, stamped on the last tick of the day it arrived.
        var ignored = Assert.Single(One(ladder, diaries, 2 * Day, "Leah", Gone("Leah", 2 * Day)), e => e.Kind == "Ignored");
        Assert.Equal(InitiationStep.Mail, ignored.Step);
        Assert.Equal(2 * Day - 1, ignored.AbsoluteTick);
        Assert.Contains(new DiaryEntry(2 * Day - 1, "Player", "IgnoredBy", "Mail"), diaries.For("Leah").Entries);
        Assert.Equal(0, ladder.Rung("Leah")); // the new day still starts from the bottom rung
    }

    [Fact]
    public void VisitingTheNextDayAnswersTheLetter()
    {
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new Always(), 1, Eager());
        One(ladder, diaries, 10, "Leah", Gone("Leah", 10)); // Mail

        InitiationEvent? responded = ladder.NoteResponded("Leah", Day + 30);

        Assert.NotNull(responded);
        Assert.Equal("Responded", responded!.Kind);
        Assert.Equal(InitiationStep.Mail, responded.Step);
    }

    [Fact]
    public void AbsentFriendsCannotUseUpTheDailyCap()
    {
        // The review repro: ten absent friends with high urge, and one NPC standing next to the player.
        var ladder = new InitiationLadder(new Always(), 1, Eager());
        var friends = Enumerable.Range(0, 10).Select(i => $"Friend{i}").ToArray();

        var attempts = new List<InitiationEvent>();
        for (int t = Day; t < Day + 60; t++)
        {
            var inputs = friends.Select(f => new InitiationInput(f, Gone(f, t), false, 4))
                .Append(new InitiationInput("Zed", t >= Day + 48 ? Near("Zed", t) : null, false, 0))
                .ToArray();
            attempts.AddRange(ladder.Tick(t, inputs, new Diaries().For).Where(e => e.Kind == "Attempt"));
        }

        Assert.Single(attempts, e => e.Step == InitiationStep.Mail); // one letter a day, not six
        Assert.Contains(attempts, e => e.Npc == "Zed");             // the nearby NPC still gets its turn
    }

    // ---- queued lines -----------------------------------------------------------------------------

    [Fact]
    public void AnUnheardQueuedLineExpiresQuietlyAtTheEndOfTheDay()
    {
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new Always(), 1, Eager());
        Assert.Equal(InitiationStep.QueuedLine, Assert.Single(One(ladder, diaries, 10, "Gus", SeenEarlier("Gus", 10))).Step);
        double urge = ladder.Urge("Gus");

        for (int t = 11; t < Day; t++)
            Assert.Empty(One(ladder, diaries, t, "Gus", null)); // an hour later it is still waiting

        var expired = Assert.Single(One(ladder, diaries, Day, "Gus", null));
        Assert.Equal("Expired", expired.Kind);
        Assert.Equal(Day - 1, expired.AbsoluteTick);
        Assert.Equal(urge, expired.UrgeAfter);              // no penalty: the player never heard it
        Assert.DoesNotContain(diaries.For("Gus").Entries, e => e.Kind == "IgnoredBy");
        Assert.Equal(0, ladder.Rung("Gus"));
    }

    [Fact]
    public void QueuedLinesHaveTheirOwnDailyCap()
    {
        var ladder = new InitiationLadder(new Always(), 1, Eager());
        var names = new[] { "Alex", "Emily", "Gus", "Pam" };

        var attempts = ladder.Tick(10, names.Select(n => new InitiationInput(n, SeenEarlier(n, 10), false, 0)).ToArray(), new Diaries().For);

        Assert.Equal(new[] { "Alex", "Emily" }, attempts.Where(e => e.Kind == "Attempt").Select(e => e.Npc).ToArray());
    }

    // ---- overnight --------------------------------------------------------------------------------

    [Fact]
    public void AnAttemptLeftOpenOvernightIsSettledOnItsOwnDay()
    {
        // The review repro: an emote just before bed, next seen at 6:10.
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new Always(), 1, new InitiationOptions { BaseGainPerTick = 0.35, HeartsGainPerTick = 0 });
        Assert.Equal(InitiationStep.Emote, Assert.Single(One(ladder, diaries, Day - 2, "Abigail", Near("Abigail", Day - 2))).Step);

        var events = One(ladder, diaries, Day + 1, "Abigail", null);

        var ignored = Assert.Single(events, e => e.Kind == "Ignored");
        Assert.Equal(Day - 1, ignored.AbsoluteTick);                       // stamped on the day it happened
        Assert.Contains(new DiaryEntry(Day - 1, "Player", "IgnoredBy", "Emote"), diaries.For("Abigail").Entries);
        Assert.Equal(0, ladder.Rung("Abigail"));                           // the new day starts at the bottom rung
        Assert.Equal((0.35 - 0.2) * 0.5, ladder.Urge("Abigail"), 9);       // penalty, then the overnight fade
    }

    // ---- conversations ----------------------------------------------------------------------------

    [Fact]
    public void TalkingWithoutAnOpenAttemptStillRelievesUrgeAndStartsTheCooldown()
    {
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new Always(), 1, new InitiationOptions { BaseGainPerTick = 0.2, HeartsGainPerTick = 0 });
        One(ladder, diaries, 0, "Sam", null);            // state exists, no view: urge 0
        ladder.Tick(1, new[] { new InitiationInput("Sam", Near("Sam", 1), false, 0) }, diaries.For); // urge 0.2, below Emote

        Assert.Null(ladder.NoteResponded("Sam", 1));      // nothing open: no event...
        Assert.Equal(0.1, ladder.Urge("Sam"), 9);         // ...but the urge is relieved

        // Urge crosses the Emote threshold at tick 2, but the cooldown after the chat holds it back.
        var held = Enumerable.Range(2, 5).SelectMany(t => One(ladder, diaries, t, "Sam", Near("Sam", t))).ToList();
        Assert.DoesNotContain(held, e => e.Kind == "Attempt");
        Assert.Contains(One(ladder, diaries, 7, "Sam", Near("Sam", 7)), e => e.Kind == "Attempt");
    }
}
