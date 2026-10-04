using System.Diagnostics;
using NpcDecision;
using NpcMemory;
using NpcMotives;
using Xunit;
using static NpcMotives.Tests.MotivesEngineTests;

namespace NpcMotives.Tests;

/// <summary>The motives runner over time, in shadow: pacing, response windows, frustration, using
/// motives up, the model's two questions, the grudge, persistence and the worker
/// (docs/spec/motives.md).</summary>
public sealed class MotivesRunnerTests
{
    /// <summary>A scripted model that counts its calls.</summary>
    internal sealed class Scripted : IDecisionClient
    {
        private readonly double _yes;
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<double>>? _choose;
        private readonly int _sleepMs;
        public int YesNoCalls;
        public int ChooseCalls;
        public readonly List<string> Propositions = new();

        public Scripted(double yes = 0.5, Func<IReadOnlyList<string>, IReadOnlyList<double>>? choose = null, int sleepMs = 0)
        {
            _yes = yes;
            _choose = choose;
            _sleepMs = sleepMs;
        }

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            Interlocked.Increment(ref ChooseCalls);
            return _choose?.Invoke(options) ?? options.Select(_ => 1.0 / options.Count).ToArray();
        }

        public double Score(string context, double min, double max) => (min + max) / 2;

        public double YesNo(string context, string proposition)
        {
            Interlocked.Increment(ref YesNoCalls);
            lock (Propositions)
                Propositions.Add(proposition);
            if (_sleepMs > 0)
                Thread.Sleep(_sleepMs);
            return _yes;
        }
    }

    private static DiaryEntry TalkedDaysAgo(int days) => E(days * Day, "Talked", "hearts=4");

    /// <summary>Pam near the player at 4 hearts, missing them (last talk 5 days ago).</summary>
    private static MotiveInputs PamMissing(bool near = true)
        => Inputs("Pam", Pam, hearts: 4, near: near, diary: new[] { TalkedDaysAgo(5) });

    private static List<MotiveEvent> Run(MotivesRunner runner, MotiveInputs input, int from, int to)
    {
        var events = new List<MotiveEvent>();
        for (int t = from; t <= to; t++)
            events.AddRange(runner.Tick(t, new[] { input }));
        return events;
    }

    [Fact]
    public void NoMotiveNoAct_AndTheModelIsNeverAsked()
    {
        var model = new Scripted(yes: 1);
        var runner = new MotivesRunner(model);
        MotiveInputs robin = Inputs("Robin", Robin, hearts: 0, near: true, met: false) with { KnowsOfPlayer = false }; // a stranger
        List<MotiveEvent> events = Run(runner, robin, 10 * Day, 11 * Day - 1);
        Assert.Empty(events);
        Assert.Equal(0, model.YesNoCalls + model.ChooseCalls);
        Assert.Null(runner.LatestDecisions()["Robin"].Chosen); // weighed every tick, for the viewer
    }

    [Fact]
    public void AnIgnoredAttemptFrustrates_ThenTheDailyCapHolds()
    {
        var runner = new MotivesRunner(new Scripted());
        List<MotiveEvent> events = Run(runner, PamMissing(), Now, Now + 40);

        MotiveEvent first = events.First(e => e.Kind == "Act");
        Assert.Equal(Now, first.AbsoluteTick);
        Assert.Equal(Act.WalkUp, first.Act);
        Assert.Equal(Motive.MissingYou, first.Motive);
        Assert.Equal(0, first.Decision!.Frustration);

        MotiveEvent ignored = events.First(e => e.Kind == "Ignored");
        Assert.Equal(Now + 6, ignored.AbsoluteTick);

        // Pam is bold (0.57): ignored once, she pushes harder on the next attempt.
        MotiveEvent second = events.Where(e => e.Kind == "Act").Skip(1).First();
        Assert.True(second.Decision!.Frustration > 0);

        // Two attempts is the daily cap; after it she may still wave, twice (waves use none of
        // the day's attempts), and then one Blocked line, not one per tick.
        List<MotiveEvent> acts = events.Where(e => e.Kind == "Act").ToList();
        Assert.Equal(new[] { Act.WalkUp, Act.WalkUp, Act.Emote, Act.Emote }, acts.Select(e => e.Act!.Value));
        MotiveEvent blocked = Assert.Single(events, e => e.Kind == "Blocked");
        Assert.Contains("cap: 2 attempts today", blocked.Reason);
        Assert.True(blocked.AbsoluteTick > acts[^1].AbsoluteTick);
        Assert.Equal(2, runner.States().Single().AttemptsToday);
    }

    [Fact]
    public void WavesAndGreetingsUseNoneOfTheTownsAttempts()
    {
        // The town has one attempt today. Alex (first in name order) only greets: his wave or
        // greeting bubble leaves that attempt for Pam, who misses the player and walks up.
        var runner = new MotivesRunner(new Scripted(), new MotiveOptions { MaxAttemptsPerDay = 1, StrongReserve = 0 });
        MotiveInputs alex = Inputs("Alex", Robin, hearts: 6, near: true);
        List<MotiveEvent> events = runner.Tick(Now, new[] { alex, PamMissing() }).ToList();

        MotiveEvent greeted = Assert.Single(events, e => e.Kind == "Act" && e.Npc == "Alex");
        Assert.True(MotiveOptions.IsLight(greeted.Act!.Value, greeted.Motive!.Value));
        Assert.Equal(Act.WalkUp, Assert.Single(events, e => e.Kind == "Act" && e.Npc == "Pam").Act);
        Assert.Equal(0, runner.States().Single(st => st.Npc == "Alex").AttemptsToday);
        Assert.Equal(1, runner.States().Single(st => st.Npc == "Pam").AttemptsToday);
    }

    [Fact]
    public void AMeetingBetweenTicksDecidesAgainWithinTheSameTick()
    {
        // Live test 2026-10-03: the player ran past villagers between two ticks. The mod now notes
        // the meeting and runs the motives again for that villager alone, at the same tick.
        var runner = new MotivesRunner(new Scripted());
        MotiveInputs gus = Inputs("Gus", Robin, hearts: 6, near: false);
        Assert.DoesNotContain(runner.Tick(Now, new[] { gus }), e => e.Kind == "Act"); // the tick: far away
        MotiveEvent wave = Assert.Single(runner.Tick(Now, new[] { gus with { PlayerNear = true, SeenPlayerToday = true } }), e => e.Kind == "Act");
        Assert.True(MotiveOptions.IsLight(wave.Act!.Value, wave.Motive!.Value));
        // The same tick again: the cooldown holds, no second wave.
        Assert.DoesNotContain(runner.Tick(Now, new[] { gus with { PlayerNear = true, SeenPlayerToday = true } }), e => e.Kind == "Act");
    }

    [Fact]
    public void WavesAndGreetingBubblesAreNeverIgnored()
    {
        // Sid, 2026-10-03: "Let's not count waves or greeting bubbles as ignored. They happen often
        // and it would just make the game unfun to have to react to every single one."
        var runner = new MotivesRunner(new Scripted());
        MotiveInputs gus = Inputs("Gus", Robin, hearts: 6, near: true); // only greets
        List<MotiveEvent> events = Run(runner, gus, Now, Now + 30);

        List<MotiveEvent> acts = events.Where(e => e.Kind == "Act").ToList();
        Assert.NotEmpty(acts);
        Assert.All(acts, a => Assert.True(MotiveOptions.IsLight(a.Act!.Value, a.Motive!.Value)));
        Assert.All(acts, a => Assert.Contains("a wave needs no answer", a.Reason));
        Assert.DoesNotContain(events, e => e.Kind == "Ignored");
        Assert.Equal(0, runner.States().Single().IgnoredToday);
        Assert.Null(runner.States().Single().OpenAct);
    }

    [Fact]
    public void AfterTodaysTalkAnActWaitsForNoAnswer()
    {
        // Sid's live test (2026-10-03): after the day's talk the game opens no second conversation,
        // so a wave then could never be answered and always ended up ignored. Now it waits for
        // nothing: no open attempt, no Ignored, no frustration.
        var runner = new MotivesRunner(new Scripted());
        MotiveInputs emily = Inputs("Emily", Robin with { Boldness = 0.74 }, hearts: 2, near: true,
            diary: new[] { E(Day / 4, "GiftReceived", "taste=Hate;item=(O)92;name=Sap") });
        runner.NoteTalked("Emily", Now - 1);
        List<MotiveEvent> events = Run(runner, emily, Now + 5, Now + 30);

        MotiveEvent act = events.First(e => e.Kind == "Act");
        Assert.Contains("no answer expected", act.Reason);
        Assert.DoesNotContain(events, e => e.Kind == "Ignored");
        Assert.Null(runner.States().Single().OpenAct);
        Assert.Equal(0, runner.States().Single().IgnoredToday);

        // The next day the talk is forgotten: an act waits for an answer again.
        var tomorrow = new MotivesRunner(new Scripted());
        tomorrow.NoteTalked("Emily", Now - Day);
        MotiveEvent fresh = Run(tomorrow, emily, Now + 5, Now + 30).First(e => e.Kind == "Act");
        Assert.DoesNotContain("no answer expected", fresh.Reason);
    }

    [Fact]
    public void TalkingAnswersAnOpenAttempt()
    {
        var runner = new MotivesRunner(new Scripted());
        Assert.Contains(runner.Tick(Now, new[] { PamMissing() }), e => e.Kind == "Act");
        MotiveEvent answered = Assert.Single(runner.NoteTalked("Pam", Now + 2));
        Assert.Equal("Responded", answered.Kind);
        Assert.Equal(Act.WalkUp, answered.Act);

        // Answered, so never ignored; and the talk starts a cooldown before the next attempt.
        List<MotiveEvent> after = Run(runner, PamMissing(), Now + 3, Now + 7);
        Assert.DoesNotContain(after, e => e.Kind is "Ignored" or "Act");
        Assert.Equal(0, runner.States().Single().IgnoredToday);
    }

    [Fact]
    public void AQueuedLineDeliversItsNewsOnlyWhenThePlayerComesToTalk()
    {
        // Robin saw the player today but they've gone; her news can only wait as a queued line.
        MotiveInputs robin = Inputs("Robin", Robin, hearts: 4, seenToday: true, news: 4);
        var runner = new MotivesRunner(new Scripted());
        MotiveEvent act = Assert.Single(runner.Tick(Now, new[] { robin }), e => e.Kind == "Act");
        Assert.Equal(Act.QueuedLine, act.Act);
        Assert.Equal(Motive.News, act.Motive);

        runner.Tick(Now + 1, new[] { robin });
        Assert.Contains(runner.LatestDecisions()["Robin"].Motives, m => m.Motive == Motive.News); // not said yet
        runner.NoteTalked("Robin", Now + 2);
        runner.Tick(Now + 9, new[] { robin });
        Assert.DoesNotContain(runner.LatestDecisions()["Robin"].Motives, m => m.Motive == Motive.News); // used up

        // Without a talk, the line quietly expires with the day: no frustration.
        var unheard = new MotivesRunner(new Scripted());
        unheard.Tick(Now, new[] { robin });
        MotiveEvent expired = Assert.Single(unheard.Tick(11 * Day, new[] { robin }), e => e.Kind == "Expired");
        Assert.Equal(11 * Day - 1, expired.AbsoluteTick);
    }

    [Fact]
    public void TheLetterCapSendsTheNextNpcToSomethingElse()
    {
        // Both miss the player and haven't seen them today: a letter each, but one letter a day.
        var runner = new MotivesRunner(new Scripted());
        MotiveInputs pam = PamMissing(near: false);
        MotiveInputs robin = Inputs("Robin", Robin, hearts: 4, diary: new[] { TalkedDaysAgo(5) });
        IReadOnlyList<MotiveEvent> events = runner.Tick(Now, new[] { robin, pam });

        MotiveEvent pamAct = Assert.Single(events, e => e.Npc == "Pam" && e.Kind == "Act"); // name order: Pam first
        Assert.Equal(Act.Letter, pamAct.Act);
        MotiveEvent robinPass = Assert.Single(events, e => e.Npc == "Robin");
        Assert.Equal("Pass", robinPass.Kind);
        Assert.DoesNotContain(robinPass.Decision!.Checks, c => c.Act == Act.Letter);
    }

    [Fact]
    public void ACloseCallIsAskedOnce_ThenWaitsACooldown()
    {
        // Shane, curious about a newcomer he hasn't met: every act is a close call or out of reach.
        var model = new Scripted(yes: 0);
        var runner = new MotivesRunner(model);
        MotiveInputs shane = Inputs("Shane", Shane, hearts: 0, near: true, met: false);

        MotiveEvent pass = Assert.Single(runner.Tick(Now, new[] { shane }));
        Assert.Equal("Pass", pass.Kind);
        Assert.Equal(Act.Bubble, pass.Act);
        Assert.Equal(0, pass.ModelP);
        Assert.Equal("would Shane call out to the player now?", Assert.Single(model.Propositions));

        Run(runner, shane, Now + 1, Now + 5);
        Assert.Equal(1, model.YesNoCalls); // the same inputs give the same answer: no re-asking
        runner.Tick(Now + 6, new[] { shane });
        Assert.Equal(2, model.YesNoCalls);
    }

    [Fact]
    public void TheMoodTipsAFiftyFiftyCloseCall()
    {
        // A plain 0.5 answer: the day's outlook decides (no random draw).
        var runner = new MotivesRunner(new Scripted(yes: 0.5));
        MotiveInputs shane = Inputs("Shane", Shane, hearts: 0, near: true, met: false);
        MotiveEvent e = Assert.Single(runner.Tick(Now, new[] { shane }));
        double outlook = e.Decision!.Mood.Outlook;
        Assert.Equal(outlook >= 0 ? "Act" : "Pass", e.Kind);
        Assert.Equal(0.5 + 0.1 * outlook, e.TiltedP!.Value, 6);
    }

    [Fact]
    public void TheModelPicksWhichMotiveGoesFirst()
    {
        MotiveInputs robin = Inputs("Robin", Robin, hearts: 4, near: true, news: 4, diary: new[] { TalkedDaysAgo(5) });

        // Uniform answers: the strongest (the news) goes first.
        var plain = new Scripted();
        MotiveEvent byStrength = Assert.Single(new MotivesRunner(plain).Tick(Now, new[] { robin }), e => e.Kind == "Act");
        Assert.Equal(Motive.News, byStrength.Motive);
        Assert.Equal(1, plain.ChooseCalls);

        // A model that prefers the second option: the feeling goes first instead.
        var prefersSecond = new Scripted(choose: options => options.Select((_, k) => k == 1 ? 0.9 : 0.1 / (options.Count - 1)).ToArray());
        MotiveEvent byModel = Assert.Single(new MotivesRunner(prefersSecond).Tick(Now, new[] { robin }), e => e.Kind == "Act");
        Assert.Equal(Motive.MissingYou, byModel.Motive);
        Assert.Contains("News", byModel.Choice);
    }

    [Fact]
    public void TheModelOnlyChoosesAmongMotivesThatCouldAct()
    {
        // Robin has news and misses the player, but the town has only its 2 reserved attempts
        // left, neither motive is strong enough for them, and she has waved enough today: nothing
        // could act, so the model is never asked to pick between them.
        var model = new Scripted();
        var runner = new MotivesRunner(model, new MotiveOptions { MaxAttemptsPerDay = 2, StrongIntensity = 2, MaxLightActsPerNpcPerDay = 0 });
        MotiveInputs robin = Inputs("Robin", Robin, hearts: 4, near: true, news: 4, diary: new[] { TalkedDaysAgo(5) });
        MotiveEvent pass = Assert.Single(runner.Tick(Now, new[] { robin }));
        Assert.Equal("Pass", pass.Kind);
        Assert.Equal(0, model.ChooseCalls);

        // With room in the day, both can act, and the model picks.
        var open = new Scripted();
        new MotivesRunner(open).Tick(Now, new[] { robin });
        Assert.Equal(1, open.ChooseCalls);
    }

    [Fact]
    public void AGrudgeAsksOnceADay_AndPenalizesAtMostOnceAWeek()
    {
        // Out of reach of every act (no hearts, not seen today), so only the grudge question runs.
        MotiveInputs shane = Inputs("Shane", Shane, hearts: 0, regard: -0.8);
        var yes = new Scripted(yes: 1);
        var runner = new MotivesRunner(yes);
        MotiveEvent grudge = Assert.Single(runner.Tick(Now, new[] { shane }), e => e.Kind == "Grudge");
        Assert.Equal(0.8, grudge.Grudge!.Value, 6);
        Assert.Equal(0.3, grudge.RegardRelief, 6);

        for (int day = 0; day < 7; day++)
            Assert.DoesNotContain(runner.Tick(Now + day * Day + 1, new[] { shane with { Now = Now + day * Day + 1 } }), e => e.Kind == "Grudge");
        Assert.Contains(runner.Tick(Now + 7 * Day, new[] { shane with { Now = Now + 7 * Day } }), e => e.Kind == "Grudge");

        var no = new Scripted(yes: 0);
        var holds = new MotivesRunner(no);
        Run(holds, shane, Now, Now + 10);
        Assert.Equal(1, no.YesNoCalls); // asked once today, never drawn
    }

    [Fact]
    public void MissingYouSurvivesTheDiaryTrim()
    {
        // The runner saw a talk on day 3; by day 10 the diary has trimmed it away. A week apart,
        // Robin misses the player as if the entry were still there.
        var runner = new MotivesRunner(new Scripted());
        runner.NoteTalked("Robin", 3 * Day + 60);
        runner.Tick(Now, new[] { Inputs("Robin", Robin, hearts: 4) });
        MotiveStrength missing = Assert.Single(runner.LatestDecisions()["Robin"].Motives, m => m.Motive == Motive.MissingYou);
        Assert.Contains("last talked 7 days ago", missing.Source);

        // With no talk on record at all (a save older than the mod's diaries), nothing is missed yet.
        var fresh = new MotivesRunner(new Scripted());
        fresh.Tick(Now, new[] { Inputs("Robin", Robin, hearts: 4) });
        Assert.DoesNotContain(fresh.LatestDecisions()["Robin"].Motives, m => m.Motive == Motive.MissingYou);
    }

    [Fact]
    public void StateRoundTrips_AndDamagedStateStartsFresh()
    {
        var runner = new MotivesRunner(new Scripted());
        runner.Tick(Now, new[] { PamMissing() });
        MotivesRunner back = MotivesRunner.FromJson(runner.ToJson(), new Scripted());
        Assert.Equal(runner.ToJson(), back.ToJson());
        Assert.Contains(back.Tick(Now + 6, new[] { PamMissing() }), e => e.Kind == "Ignored");

        Assert.Empty(MotivesRunner.FromJson("{not json", new Scripted()).States());
        Assert.Empty(MotivesRunner.FromJson(null, new Scripted()).States());
    }

    [Fact]
    public void Deterministic_TheSameDayTwiceGivesTheSameEvents()
    {
        static List<string> Day10(MotivesRunner runner)
        {
            var lines = new List<string>();
            MotiveInputs[] inputs =
            {
                PamMissing(),
                Inputs("Robin", Robin, hearts: 4, near: true, news: 4, diary: new[] { TalkedDaysAgo(5) }),
                Inputs("Shane", Shane, hearts: 8, near: true, diary: new[] { E(Day, "StoodUp", "place=Saloon") }, regard: -0.3),
            };
            for (int t = 10 * Day; t < 11 * Day; t++)
                lines.AddRange(runner.Tick(t, inputs).Select(MotiveText.Line));
            return lines;
        }
        List<string> a = Day10(new MotivesRunner(new VariedFakeDecisionClient()));
        List<string> b = Day10(new MotivesRunner(new VariedFakeDecisionClient()));
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void TheWorkerNeverMakesTheGameThreadWait()
    {
        var motives = new BackgroundMotives(new MotivesRunner(new Scripted(yes: 0, sleepMs: 300)));
        MotiveInputs shane = Inputs("Shane", Shane, hearts: 0, near: true, met: false); // a close call

        var watch = Stopwatch.StartNew();
        Assert.True(motives.EnqueueTick(Now, new[] { shane }));
        Assert.Empty(motives.Drain());
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 200, $"enqueue+drain took {watch.ElapsedMilliseconds} ms");

        Assert.True(motives.WaitIdle(TimeSpan.FromSeconds(10)));
        Assert.Equal("Pass", Assert.Single(motives.Drain()).Kind);
        Assert.True(motives.LatestDecisions.ContainsKey("Shane"));
        Assert.Contains("Shane", motives.LatestJson);
    }

    [Fact]
    public void TheWorkerDropsTicksWhenFarBehind()
    {
        var motives = new BackgroundMotives(new MotivesRunner(new Scripted(yes: 0, sleepMs: 300)), maxBacklog: 2);
        int accepted = 0;
        for (int t = 0; t < 10; t++)
            if (motives.EnqueueTick(Now + t, new[] { Inputs($"Npc{t}", Shane, hearts: 0, near: true, met: false) }))
                accepted++;
        Assert.True(accepted <= 3, $"accepted {accepted}");
        Assert.True(motives.Dropped >= 7);
        Assert.True(motives.WaitIdle(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void TalksFlowThroughTheWorkerInOrder()
    {
        var motives = new BackgroundMotives(new MotivesRunner(new Scripted()));
        motives.EnqueueTick(Now, new[] { PamMissing() });
        motives.EnqueueTalked("Pam", Now + 1);
        Assert.True(motives.WaitIdle(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { "Act", "Responded" }, motives.Drain().Select(e => e.Kind));
        Assert.Null(Assert.Single(motives.LatestStates).OpenAct);
    }
}
