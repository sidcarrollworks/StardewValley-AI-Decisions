using NpcDecision;
using NpcMemory;
using Xunit;

namespace NpcInitiation.Tests;

/// <summary>
/// The initiation ladder in shadow mode: urge growth, mildest fitting step, escalation after
/// being ignored, caps, day rollover, the decision gate, determinism and persistence.
/// </summary>
public sealed class InitiationLadderTests
{
    private const int Day = GameClock.TicksPerDay;

    /// <summary>A decision client that answers every yes/no with a fixed (settable) probability.</summary>
    private sealed class StubDecision : IDecisionClient
    {
        public double P { get; set; }
        public int YesNoCalls { get; private set; }
        public StubDecision(double p) => P = p;

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
            => options.Select(_ => 1.0 / options.Count).ToList();

        public double Score(string context, double min, double max) => min;

        public double YesNo(string context, string proposition)
        {
            YesNoCalls++;
            return P;
        }
    }

    private sealed class Diaries
    {
        private readonly Dictionary<string, Diary> _byNpc = new(StringComparer.OrdinalIgnoreCase);
        public Diary For(string npc)
        {
            if (!_byNpc.TryGetValue(npc, out var d))
                _byNpc[npc] = d = new Diary();
            return d;
        }
    }

    // ---- Fixtures: the NPC's own view of the player ------------------------------------------

    private static LedgerView Near(string npc, int tick)
        => new(npc, "Player", LedgerDetail.NamedSpot, "Farm", 0, 0, tick);

    private static LedgerView SeenEarlier(string npc, int tick)
        => new(npc, "Player", LedgerDetail.Location, "Town", 5, 0, tick - 5);

    private static LedgerView Gone(string npc, int tick)
        => new(npc, "Player", LedgerDetail.Gone, null, 200, 0, tick - 200);

    private static LedgerView Hearsay(string npc, int tick)
        => new(npc, "Player", LedgerDetail.NamedSpot, "Farm", 0, 1, tick);

    private static InitiationOptions Opts(double gain, Action<InitiationOptions>? tweak = null)
    {
        var o = new InitiationOptions { BaseGainPerTick = gain, HeartsGainPerTick = 0.0 };
        tweak?.Invoke(o);
        return o;
    }

    private static InitiationInput In(string npc, LedgerView? view, int hearts = 0, bool intent = false)
        => new(npc, view, intent, hearts);

    /// <summary>Tick one NPC with a view built from the tick.</summary>
    private static IReadOnlyList<InitiationEvent> TickOne(
        InitiationLadder ladder, Diaries diaries, int tick, string npc,
        Func<string, int, LedgerView?> view, int hearts = 0, bool intent = false)
        => ladder.Tick(tick, new[] { In(npc, view(npc, tick), hearts, intent) }, diaries.For);

    private static List<InitiationEvent> Run(
        InitiationLadder ladder, Diaries diaries, int from, int toExclusive, string npc,
        Func<string, int, LedgerView?> view, int hearts = 0)
    {
        var all = new List<InitiationEvent>();
        for (int t = from; t < toExclusive; t++)
            all.AddRange(TickOne(ladder, diaries, t, npc, view, hearts));
        return all;
    }

    private static InitiationEvent SingleAttempt(IEnumerable<InitiationEvent> events)
        => Assert.Single(events, e => e.Kind == "Attempt");

    // ---- Urge ------------------------------------------------------------------------------

    [Fact]
    public void NoPlayerViewNeverAttemptsAndUrgeStaysZero()
    {
        var decision = new StubDecision(1.0);
        var ladder = new InitiationLadder(decision, 1, Opts(0.5));
        var events = Run(ladder, new Diaries(), 0, 3 * Day, "Abigail", (_, _) => null, hearts: 10);

        Assert.Empty(events);
        Assert.Equal(0.0, ladder.Urge("Abigail"));
        Assert.Equal(0, decision.YesNoCalls);
    }

    [Fact]
    public void UrgeGrowsPerTickAndHeartsRaiseTheGrowth()
    {
        var ladder = new InitiationLadder(new StubDecision(0.0), 1);
        TickOne(ladder, new Diaries(), 0, "Abigail", Gone, hearts: 0);
        TickOne(ladder, new Diaries(), 0, "Sam", Gone, hearts: 10);
        Assert.Equal(0.004, ladder.Urge("Abigail"), 9);
        Assert.Equal(0.009, ladder.Urge("Sam"), 9);

        TickOne(ladder, new Diaries(), 1, "Abigail", Gone, hearts: 0);
        Assert.Equal(0.008, ladder.Urge("Abigail"), 9);
    }

    [Fact]
    public void UrgeIsClampedToOne()
    {
        var ladder = new InitiationLadder(new StubDecision(0.0), 1, Opts(0.6));
        Run(ladder, new Diaries(), 0, 5, "Abigail", Near);
        Assert.Equal(1.0, ladder.Urge("Abigail"));
    }

    [Fact]
    public void UnknownNpcHasZeroUrgeAndRung()
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1);
        Assert.Equal(0.0, ladder.Urge("Nobody"));
        Assert.Equal(0, ladder.Rung("Nobody"));
        Assert.Null(ladder.NoteResponded("Nobody", 0));
    }

    // ---- Mildest fitting step --------------------------------------------------------------

    [Theory]
    [InlineData(0.31)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void NearPicksTheMildestStep(double gain)
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1, Opts(gain));
        var attempt = SingleAttempt(TickOne(ladder, new Diaries(), 0, "Abigail", Near));
        Assert.Equal(InitiationStep.Emote, attempt.Step);
    }

    [Fact]
    public void BelowTheFirstThresholdNothingHappens()
    {
        var decision = new StubDecision(1.0);
        var ladder = new InitiationLadder(decision, 1, Opts(0.29));
        Assert.Empty(TickOne(ladder, new Diaries(), 0, "Abigail", Near));
        Assert.Equal(0, decision.YesNoCalls); // no candidate -> the client is not asked
    }

    [Fact]
    public void SeenTodayButNotNearQueuesALine()
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0));
        var attempt = SingleAttempt(TickOne(ladder, new Diaries(), 10, "Abigail", SeenEarlier, hearts: 10));
        Assert.Equal(InitiationStep.QueuedLine, attempt.Step);

        // A named spot that is not this very tick is not Near either.
        var ladder2 = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0));
        var aged = new LedgerView("Abigail", "Player", LedgerDetail.NamedSpot, "Farm", 2, 0, 8);
        var attempt2 = SingleAttempt(ladder2.Tick(10, new[] { In("Abigail", aged, 10) }, new Diaries().For));
        Assert.Equal(InitiationStep.QueuedLine, attempt2.Step);
    }

    [Fact]
    public void NotSeenTodayMailsOnlyFriends()
    {
        var friend = new InitiationLadder(new StubDecision(1.0), 1, Opts(0.8));
        var attempt = SingleAttempt(TickOne(friend, new Diaries(), 0, "Abigail", Gone, hearts: 2));
        Assert.Equal(InitiationStep.Mail, attempt.Step);

        var stranger = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0));
        Assert.Empty(TickOne(stranger, new Diaries(), 0, "Abigail", Gone, hearts: 1));

        var lowUrge = new InitiationLadder(new StubDecision(1.0), 1, Opts(0.79));
        Assert.Empty(TickOne(lowUrge, new Diaries(), 0, "Abigail", Gone, hearts: 5));
    }

    [Fact]
    public void HearsayIsNeitherNearNorSeenToday()
    {
        // Hearsay of the player standing right here, this tick: still only Mail-eligible.
        var friend = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0));
        var attempt = SingleAttempt(TickOne(friend, new Diaries(), 0, "Abigail", Hearsay, hearts: 5));
        Assert.Equal(InitiationStep.Mail, attempt.Step);

        var stranger = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0));
        Assert.Empty(TickOne(stranger, new Diaries(), 0, "Abigail", Hearsay, hearts: 0));
    }

    [Fact]
    public void AttemptWritesATriedToReachDiaryEntry()
    {
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new StubDecision(1.0), 1, Opts(0.35));
        TickOne(ladder, diaries, 7, "Abigail", Near);

        var entry = Assert.Single(diaries.For("Abigail").Entries);
        Assert.Equal(new DiaryEntry(7, "Player", "TriedToReach", "Emote"), entry);
    }

    // ---- Ignored / responded ---------------------------------------------------------------

    [Fact]
    public void IgnoredAttemptPenalizesEscalatesAndEntersTheDiary()
    {
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new StubDecision(1.0), 1,
            Opts(0.35, o => { o.ResponseWindowTicks = 2; o.CooldownTicks = 0; o.RecordIgnoredBy = true; }));

        Assert.Equal(InitiationStep.Emote, SingleAttempt(TickOne(ladder, diaries, 0, "Abigail", Near)).Step);
        Assert.Empty(TickOne(ladder, diaries, 1, "Abigail", Near)); // still within the window, open attempt

        var events = TickOne(ladder, diaries, 2, "Abigail", Near);
        var ignored = Assert.Single(events, e => e.Kind == "Ignored");
        Assert.Equal(InitiationStep.Emote, ignored.Step);
        Assert.Equal(0.70, ignored.UrgeBefore, 9);
        Assert.Equal(0.50, ignored.UrgeAfter, 9);

        // Rung escalated past Emote, so the next attempt (same tick, urge 0.85) is a Bubble.
        Assert.Equal(InitiationStep.Bubble, SingleAttempt(events).Step);
        Assert.Equal(1, ladder.Rung("Abigail"));

        Assert.Contains(new DiaryEntry(2, "Player", "IgnoredBy", "Emote"), diaries.For("Abigail").Entries);
    }

    [Fact]
    public void IgnoredAttempt_WritesNoDiaryLineWhileShadow()
    {
        // Week review finding 3: shadow mode must not record "you ignored me" for an attempt the
        // player never saw. The default (RecordIgnoredBy off) keeps the penalty and escalation
        // but writes nothing to the diary.
        var diaries = new Diaries();
        var ladder = new InitiationLadder(new StubDecision(1.0), 1,
            Opts(0.35, o => { o.ResponseWindowTicks = 2; o.CooldownTicks = 0; }));

        Assert.Equal(InitiationStep.Emote, SingleAttempt(TickOne(ladder, diaries, 0, "Abigail", Near)).Step);
        Assert.Empty(TickOne(ladder, diaries, 1, "Abigail", Near));

        var events = TickOne(ladder, diaries, 2, "Abigail", Near);
        Assert.Contains(events, e => e.Kind == "Ignored");
        Assert.Equal(0.85, ladder.Urge("Abigail"), 9); // 0.70 - 0.2 penalty, then this tick's +0.35 growth
        Assert.DoesNotContain(diaries.For("Abigail").Entries, e => e.Kind == "IgnoredBy");
        Assert.Contains(diaries.For("Abigail").Entries, e => e.Kind == "TriedToReach"); // the attempt itself stays
    }

    [Fact]
    public void FromJson_SkipsANullNpc()
    {
        // Playtest review: the saved ladder carried an NPC literally named "null" (the game's
        // no-target quest marker) at urge 0. FromJson must drop it like the diary does.
        string json = "{\"Day\":3,\"Npcs\":[{\"Npc\":\"null\",\"Urge\":0.5,\"Rung\":0,\"Day\":3},{\"Npc\":\"Haley\",\"Urge\":0.4,\"Rung\":0,\"Day\":3}]}";

        var ladder = InitiationLadder.FromJson(json, new StubDecision(1.0), 1);

        Assert.Equal(0.0, ladder.Urge("null")); // gone: unknown NPCs read as 0
        Assert.Equal(0.4, ladder.Urge("Haley"), 9); // the real NPC survives
    }

    [Fact]
    public void IgnorePenaltyClampsAtZero()
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1,
            Opts(0.31, o => { o.ResponseWindowTicks = 1; o.IgnorePenalty = 5.0; o.CooldownTicks = 100; }));
        TickOne(ladder, new Diaries(), 0, "Abigail", Near);
        var ignored = Assert.Single(TickOne(ladder, new Diaries(), 1, "Abigail", Near), e => e.Kind == "Ignored");
        Assert.Equal(0.0, ignored.UrgeAfter);
        Assert.Equal(0.31, ladder.Urge("Abigail"), 9); // regrown after the penalty
    }

    [Fact]
    public void RespondedRelievesUrgeResetsRungAndIsNeverIgnored()
    {
        var diaries = new Diaries();
        var decision = new StubDecision(1.0);
        var ladder = new InitiationLadder(decision, 1,
            Opts(0.35, o => { o.ResponseWindowTicks = 1; o.CooldownTicks = 0; }));

        TickOne(ladder, diaries, 0, "Abigail", Near);   // Emote
        TickOne(ladder, diaries, 1, "Abigail", Near);   // ignored (0.35 -> 0.15, grows to 0.50) -> rung 1, Bubble
        Assert.Equal(1, ladder.Rung("Abigail"));

        var responded = ladder.NoteResponded("abigail", 1);
        Assert.NotNull(responded);
        Assert.Equal("Responded", responded!.Kind);
        Assert.Equal(InitiationStep.Bubble, responded.Step);
        Assert.Equal(0.50, responded.UrgeBefore, 9);
        Assert.Equal(0.25, responded.UrgeAfter, 9);
        Assert.Equal(0, ladder.Rung("Abigail"));
        Assert.Null(ladder.NoteResponded("Abigail", 1)); // nothing open any more

        decision.P = 0.0;
        var later = Run(ladder, diaries, 2, 20, "Abigail", Near);
        Assert.DoesNotContain(later, e => e.Kind == "Ignored");
    }

    // ---- Cooldown and caps -----------------------------------------------------------------

    [Fact]
    public void CooldownSpacesAttempts()
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1,
            Opts(1.0, o => { o.ResponseWindowTicks = 1; o.CooldownTicks = 5; o.MaxAttemptsPerNpcPerDay = 10; }));
        var attempts = Run(ladder, new Diaries(), 0, 12, "Abigail", Near)
            .Where(e => e.Kind == "Attempt").Select(e => e.AbsoluteTick).ToList();
        Assert.Equal(new[] { 0, 5, 10 }, attempts);
    }

    [Fact]
    public void PerNpcDailyCapResetsOnANewDay()
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1,
            Opts(1.0, o => { o.ResponseWindowTicks = 1; o.CooldownTicks = 0; }));
        var day0 = Run(ladder, new Diaries(), 0, Day, "Abigail", Near).Count(e => e.Kind == "Attempt");
        Assert.Equal(2, day0);

        var day1 = Run(ladder, new Diaries(), Day, Day + 10, "Abigail", Near).Where(e => e.Kind == "Attempt").ToList();
        Assert.Equal(2, day1.Count);
        Assert.Equal(Day, day1[0].AbsoluteTick);
    }

    [Fact]
    public void GlobalDailyCapIsSharedAndResetsOnANewDay()
    {
        var ladder = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0, o => o.MaxAttemptsPerDay = 3));
        var names = new[] { "Sam", "abigail", "Emily", "Leah", "Haley" };
        InitiationInput[] Inputs(int t) => names.Select(n => In(n, Near(n, t))).ToArray();

        var first = ladder.Tick(0, Inputs(0), new Diaries().For);
        // Processed in OrdinalIgnoreCase order; the first three get through.
        Assert.Equal(new[] { "abigail", "Emily", "Haley" }, first.Select(e => e.Npc).ToArray());
        // Later today: the three attempts were ignored, but the global cap blocks everyone.
        Assert.DoesNotContain(ladder.Tick(50, Inputs(50), new Diaries().For), e => e.Kind == "Attempt");

        var nextDay = ladder.Tick(Day, Inputs(Day), new Diaries().For);
        Assert.Equal(3, nextDay.Count(e => e.Kind == "Attempt"));
    }

    [Fact]
    public void ForcedDialogueIsCappedPerWeek()
    {
        // Only ForcedDialogue can fit (all milder thresholds unreachable).
        var ladder = new InitiationLadder(new StubDecision(1.0), 1, Opts(1.0, o =>
        {
            o.StepThresholds = new[] { 2.0, 2.0, 2.0, 2.0, 2.0, 0.95 };
            o.ResponseWindowTicks = 1;
            o.CooldownTicks = 0;
        }));
        InitiationInput[] Inputs(int t) => new[] { In("Abigail", Near("Abigail", t)), In("Sam", Near("Sam", t)) };

        var d0 = ladder.Tick(0, Inputs(0), new Diaries().For);
        var forced = SingleAttempt(d0);
        Assert.Equal(InitiationStep.ForcedDialogue, forced.Step);
        Assert.Equal("Abigail", forced.Npc);

        // Rest of the week (days 0..6): no more forced dialogue for anyone.
        for (int t = 1; t < 7 * Day; t++)
            Assert.DoesNotContain(ladder.Tick(t, Inputs(t), new Diaries().For), e => e.Kind == "Attempt");

        // Day 7 is a new week.
        Assert.Equal(InitiationStep.ForcedDialogue, SingleAttempt(ladder.Tick(7 * Day, Inputs(7 * Day), new Diaries().For)).Step);
    }

    // ---- Day rollover and intent -----------------------------------------------------------

    [Fact]
    public void OvernightHalvesUrgeAndResetsRung()
    {
        var decision = new StubDecision(1.0);
        var ladder = new InitiationLadder(decision, 1, Opts(0.1, o => o.ResponseWindowTicks = 1));
        var diaries = new Diaries();

        TickOne(ladder, diaries, 0, "Abigail", Near); // 0.1
        TickOne(ladder, diaries, 1, "Abigail", Near); // 0.2
        TickOne(ladder, diaries, 2, "Abigail", Near); // 0.3 -> Emote
        TickOne(ladder, diaries, 3, "Abigail", Near); // ignored: 0.3 - 0.2 = 0.1, then grows to 0.2; rung 1
        Assert.Equal(1, ladder.Rung("Abigail"));
        Assert.Equal(0.2, ladder.Urge("Abigail"), 9);

        decision.P = 0.0;
        TickOne(ladder, diaries, Day, "Abigail", Near); // 0.2 * 0.5 + 0.1
        Assert.Equal(0.2, ladder.Urge("Abigail"), 9);
        Assert.Equal(0, ladder.Rung("Abigail"));
    }

    [Fact]
    public void IntentBoostIsAppliedOncePerDay()
    {
        var ladder = new InitiationLadder(new StubDecision(0.0), 1, Opts(0.0));
        var diaries = new Diaries();
        for (int t = 0; t < 5; t++)
            TickOne(ladder, diaries, t, "Abigail", Gone, intent: true);
        Assert.Equal(0.25, ladder.Urge("Abigail"), 9);

        TickOne(ladder, diaries, Day, "Abigail", Gone, intent: true);
        Assert.Equal(0.125 + 0.25, ladder.Urge("Abigail"), 9);
        TickOne(ladder, diaries, Day + 1, "Abigail", Gone, intent: true);
        Assert.Equal(0.375, ladder.Urge("Abigail"), 9);
    }

    // ---- Decision gate ---------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(1.0, 2)]
    public void DecisionProbabilityGatesAttempts(double p, int expected)
    {
        var decision = new StubDecision(p);
        var ladder = new InitiationLadder(decision, 1, Opts(1.0, o => o.ResponseWindowTicks = 1));
        var attempts = Run(ladder, new Diaries(), 0, 40, "Abigail", Near).Count(e => e.Kind == "Attempt");
        Assert.Equal(expected, attempts);
        Assert.True(decision.YesNoCalls > 0);
    }

    // ---- Determinism -----------------------------------------------------------------------

    private static List<InitiationEvent> Scenario(int seed)
    {
        var ladder = new InitiationLadder(new StubDecision(0.3), seed);
        var diaries = new Diaries();
        var names = new[] { "Sam", "Abigail", "Emily", "Leah" };
        var events = new List<InitiationEvent>();
        for (int t = 0; t < 4 * Day; t++)
        {
            var inputs = names.Select((n, i) =>
            {
                LedgerView view = ((t / 7 + i) % 3) switch
                {
                    0 => Near(n, t),
                    1 => SeenEarlier(n, t),
                    _ => Gone(n, t),
                };
                return In(n, view, hearts: 2 + i * 3, intent: i % 2 == 0);
            }).Reverse().ToArray();
            events.AddRange(ladder.Tick(t, inputs, diaries.For));
            if (t % 11 == 0)
                events.Add(ladder.NoteResponded("Emily", t) ?? new InitiationEvent(t, "-", "-", 0, 0, 0, "-"));
        }
        return events;
    }

    [Fact]
    public void SameSeedSameEventsDifferentSeedCanDiffer()
    {
        var a = Scenario(42);
        var b = Scenario(42);
        Assert.Contains(a, e => e.Kind == "Attempt");
        Assert.Equal(a, b);

        Assert.Contains(Enumerable.Range(1, 20), s => !Scenario(s).SequenceEqual(a));
    }

    // ---- Persistence -----------------------------------------------------------------------

    [Fact]
    public void JsonRoundTripPreservesStateButNotOptions()
    {
        var options = Opts(0.35, o => { o.ResponseWindowTicks = 1; o.CooldownTicks = 0; o.MaxAttemptsPerDay = 3; });
        var ladder = new InitiationLadder(new StubDecision(1.0), 7, options);
        var diaries = new Diaries();
        TickOne(ladder, diaries, 0, "Abigail", Near);    // Emote, open
        TickOne(ladder, diaries, 1, "Abigail", Near);    // ignored -> rung 1; Bubble attempt, open
        TickOne(ladder, diaries, 1, "Sam", Near);        // Sam: Emote (global count 3 now)

        string json = ladder.ToJson();
        Assert.DoesNotContain("BaseGainPerTick", json);
        Assert.DoesNotContain("StepThresholds", json);

        // Restore under different tuning: state comes back, the new options apply.
        var restored = InitiationLadder.FromJson(json, new StubDecision(1.0), 7,
            Opts(0.35, o => { o.ResponseWindowTicks = 1; o.CooldownTicks = 0; o.MaxAttemptsPerDay = 4; }));
        Assert.Equal(json, restored.ToJson());
        Assert.Equal(ladder.Urge("Abigail"), restored.Urge("Abigail"));
        Assert.Equal(1, restored.Rung("Abigail"));
        Assert.Equal(ladder.Urge("Sam"), restored.Urge("Sam"));

        // Global counter survived (3 used): with a cap of 4, only one more attempt today.
        var more = restored.Tick(2, new[] { In("Leah", Near("Leah", 2)), In("Maru", Near("Maru", 2)) }, diaries.For);
        Assert.Equal(new[] { "Leah" }, more.Where(e => e.Kind == "Attempt").Select(e => e.Npc).ToArray());

        // Open attempt survived.
        var responded = restored.NoteResponded("Sam", 2);
        Assert.NotNull(responded);
        Assert.Equal(InitiationStep.Emote, responded!.Step);

        // Per-NPC daily count survived: Abigail already made 2 attempts today.
        restored.NoteResponded("Abigail", 2);
        var abigail = Enumerable.Range(3, 20)
            .SelectMany(t => restored.Tick(t, new[] { In("Abigail", Near("Abigail", t)) }, diaries.For));
        Assert.DoesNotContain(abigail, e => e.Kind == "Attempt");
    }

    [Fact]
    public void FromJsonOfEmptyStateIsAFreshLadder()
    {
        var fresh = new InitiationLadder(new StubDecision(1.0), 1);
        var restored = InitiationLadder.FromJson(fresh.ToJson(), new StubDecision(1.0), 1, Opts(0.35));
        Assert.Equal(InitiationStep.Emote, SingleAttempt(TickOne(restored, new Diaries(), 0, "Abigail", Near)).Step);
    }
}
