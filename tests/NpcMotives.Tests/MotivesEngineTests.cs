using NpcMemory;
using NpcMotives;
using NpcTemperament;
using Xunit;

namespace NpcMotives.Tests;

/// <summary>The act rule and motives (docs/spec/motives.md, "Acceptance tests").</summary>
public sealed class MotivesEngineTests
{
    // Seed temperaments from the draft table (fixtures/game/temperament/temperament.json).
    internal static readonly Temperament Shane = new(0.30, 0.74, 0.27, 0.30, 0.5, 0.16, Anger: 0.49, Happiness: 0.35, Sadness: 0.76);
    internal static readonly Temperament Pam = new(0.32, 0.56, 0.32, 0.58, 0.5, 0.57, Anger: 0.50, Happiness: 0.33, Sadness: 0.63);
    internal static readonly Temperament Robin = new(0.60, 0.26, 0.63, 0.80, 0.5, 0.79, Anger: 0.39, Happiness: 0.78, Sadness: 0.46);

    internal const int Day = GameClock.TicksPerDay;
    internal const int Now = 10 * Day + 60; // day 10, 4 pm

    internal static MotiveInputs Inputs(string npc, Temperament t, int hearts = 0, bool near = false,
        IEnumerable<DiaryEntry>? diary = null, double regard = 0, bool seenToday = false, bool met = true,
        double news = 0, int attemptsLeft = 6, int ignored = 0, bool greeted = false, int daysSinceSighting = 0)
        => new(npc, Now, t, hearts, (diary ?? Array.Empty<DiaryEntry>()).ToList(), regard, near,
            seenToday || near, met, true, news, false, greeted, daysSinceSighting, ignored, attemptsLeft, Seed: 12345);

    internal static DiaryEntry E(int ticksAgo, string kind, string? detail = null, string subject = "Player")
        => new(Now - ticksAgo, subject, kind, detail);

    private readonly MotivesEngine _engine = new();

    [Fact]
    public void NoMotiveNoAct_EvenForTheBoldest()
    {
        // Robin is the boldest villager; near the player, a stranger, nothing in her diary.
        MotiveDecision d = _engine.Decide(Inputs("Robin", Robin, hearts: 0, near: true));
        Assert.Null(d.Result);
        Assert.Null(d.Chosen);
        Assert.Contains("no motive", d.Reason);
    }

    [Fact]
    public void NoIdleClock_AFullDayOfTicksChangesNothingWithoutEvents()
    {
        for (int tick = 0; tick < GameClock.TicksPerDay; tick += 6)
        {
            MotiveInputs i = Inputs("Pam", Pam, hearts: 4) with { Now = 10 * Day + tick };
            Assert.Null(_engine.Decide(i).Result);
        }
    }

    [Fact]
    public void Familiarity_ShaneGreetsSomeoneHeKnowsButNotAStranger()
    {
        MotiveDecision stranger = _engine.Decide(Inputs("Shane", Shane, hearts: 0, near: true));
        Assert.Null(stranger.Result);

        MotiveDecision friend = _engine.Decide(Inputs("Shane", Shane, hearts: 8, near: true));
        Assert.Equal(Motive.Greeting, friend.Chosen!.Motive);
        // A greeting reaches at least an emote: a clear one, or the bubble as a close call that
        // falls back to the emote when the model says no.
        Assert.Equal(Act.Emote, _engine.ResolveClose(friend, p: 0.0));
    }

    [Fact]
    public void Greeting_OncePerDay()
    {
        MotiveDecision d = _engine.Decide(Inputs("Pam", Pam, hearts: 4, near: true, greeted: true));
        Assert.DoesNotContain(d.Motives, m => m.Motive == Motive.Greeting);
    }

    [Fact]
    public void IntensityOverridesShyness_StrongHurtReachesAHostileLetter()
    {
        var strong = new[] { E(Day, "StoodUp", "place=Saloon") };
        MotiveDecision hurt = _engine.Decide(Inputs("Shane", Shane, hearts: 4, diary: strong, regard: -0.3));
        Assert.Equal(Motive.Hurt, hurt.Chosen!.Motive);
        Assert.Equal(Act.Letter, hurt.Result);
        Assert.True(hurt.Checks.Single(c => c.Act == Act.Letter).Hostile);

        var mild = new[] { E(Day, "PassedBy", "ticks=8") };
        MotiveDecision passed = _engine.Decide(Inputs("Shane", Shane, hearts: 4, diary: mild));
        Assert.Null(passed.Result);
    }

    [Fact]
    public void HostileActsCostTheSurchargeMore()
    {
        var options = new MotiveOptions();
        MotiveDecision hostile = _engine.Decide(Inputs("Pam", Pam, hearts: 4, near: true,
            diary: new[] { E(Day, "StoodUp", "place=Saloon") }, regard: -0.3));
        ActCheck walk = hostile.Checks.Single(c => c.Act == Act.WalkUp);
        Assert.True(walk.Hostile);
        Assert.Equal(options.ActCost[Act.WalkUp] + options.HostileSurcharge, walk.Cost, 6);
    }

    [Fact]
    public void Netting_GratefulAndHurtTowardOnePersonNetOut()
    {
        // A loved gift and a stand-up: the net is far smaller than either feeling.
        var mixed = new[] { E(Day, "GiftReceived", "taste=Love;item=(O)1"), E(Day, "StoodUp", "place=Saloon") };
        MotiveDecision d = _engine.Decide(Inputs("Robin", Robin, hearts: 6, near: true, diary: mixed));
        double grateful = d.Motives.Single(m => m.Motive == Motive.Grateful).Strength;
        double hurt = d.Motives.Single(m => m.Motive == Motive.Hurt).Strength;
        Assert.True(Math.Abs(d.NetFeeling) < Math.Max(grateful, hurt));
    }

    [Fact]
    public void MotivesAreUsedUp_SharedNewsNoLongerDrives()
    {
        MotiveInputs withNews = Inputs("Robin", Robin, hearts: 4, near: true, news: 4);
        Assert.Contains(_engine.Decide(withNews).Motives, m => m.Motive == Motive.News);
        Assert.DoesNotContain(_engine.Decide(withNews with { NewsShared = true }).Motives, m => m.Motive == Motive.News);
    }

    [Fact]
    public void AvoidingThePlayer_NoFriendlyFaceToFace()
    {
        // Strong hurt makes the net feeling hostile; news then can't be told in person.
        MotiveInputs i = Inputs("Pam", Pam, hearts: 4, near: true, news: 4, regard: -0.6,
            diary: new[] { E(Day, "StoodUp", "place=Saloon") });
        var engine = new MotivesEngine();
        MotiveStrength news = engine.Candidates(i).Single(m => m.Motive == Motive.News);
        MotiveDecision d = engine.DecideFor(i, news);
        Assert.DoesNotContain(d.Checks, c => c.Act is Act.Bubble or Act.WalkUp);
    }

    [Fact]
    public void Caps_TheLastAttemptsAreKeptForStrongMotives()
    {
        MotiveDecision greeting = _engine.Decide(Inputs("Robin", Robin, hearts: 6, near: true, attemptsLeft: 2));
        Assert.Null(greeting.Result);
        Assert.Contains("strong motives", greeting.Reason);

        MotiveDecision hurt = _engine.Decide(Inputs("Robin", Robin, hearts: 6, near: true, attemptsLeft: 2,
            diary: new[] { E(Day / 2, "StoodUp", "place=Saloon;seen=1") }, regard: -0.5));
        Assert.NotNull(hurt.Result);
    }

    [Fact]
    public void Frustration_BoldPushHarder_ShyBackOff()
    {
        var diary = new[] { E(Day, "GiftReceived", "taste=Love;item=(O)1") };
        MotiveDecision boldCalm = _engine.Decide(Inputs("Robin", Robin, hearts: 4, near: true, diary: diary));
        MotiveDecision boldIgnored = _engine.Decide(Inputs("Robin", Robin, hearts: 4, near: true, diary: diary, ignored: 2));
        Assert.True(boldIgnored.Frustration > 0);
        Assert.True(boldIgnored.IntensityTerm > boldCalm.IntensityTerm);

        MotiveDecision shyIgnored = _engine.Decide(Inputs("Shane", Shane, hearts: 4, near: true, diary: diary, ignored: 2));
        Assert.True(shyIgnored.Frustration < 0);
    }

    [Fact]
    public void CloseCalls_TheMoodTipsACoinFlip()
    {
        var engine = new MotivesEngine();
        var pending = new ActCheck(Act.Bubble, Hostile: false, Cost: 0.35, Effective: 0.35, Margin: 0, CallKind.CloseCall, 0.5);
        MotiveDecision Make(double outlook) => new("Sam", Array.Empty<MotiveStrength>(), new Mood(0, 0, false, outlook), 0.2,
            null, 0.6, 0, 0, 0, new[] { pending }, Act.Bubble, pending, "test");

        Assert.Equal(Act.Bubble, engine.ResolveClose(Make(+0.5), p: 0.5));
        Assert.Null(engine.ResolveClose(Make(-0.5), p: 0.5));
        // A model failure (NaN) uses the deterministic fallback.
        Assert.Equal(Act.Bubble, engine.ResolveClose(Make(0), p: double.NaN));
    }

    [Fact]
    public void APlainTalkLiftsTheMoodButIsNothingToThankFor()
    {
        // Playtest 2026-10-02: Haley wrote a thank-you letter after an ordinary chat.
        MotiveDecision d = _engine.Decide(Inputs("Haley", Robin, hearts: 4, diary: new[] { E(Day / 4, "Talked", "hearts=4") }));
        Assert.DoesNotContain(d.Motives, m => m.Motive == Motive.Grateful);
        Assert.True(d.Mood.Earned > 0);
    }

    [Fact]
    public void AWeakMotiveIsNotWorthALetter_HoweverBold()
    {
        // Playtest 2026-10-02: Haley, bold, wrote a letter over "misses you 0.10".
        var lastTalk = new[] { E(Day + Day / 2, "Talked", "hearts=4") };
        MotiveDecision weak = _engine.Decide(Inputs("Haley", Robin, hearts: 4, diary: lastTalk));
        Assert.Equal(Motive.MissingYou, weak.Chosen!.Motive);
        Assert.Null(weak.Result);
        Assert.Contains("too weak for letter (needs 0.30)", weak.Reason);

        // A week apart, the same character's reason is big enough.
        var longAgo = new[] { E(7 * Day, "Talked", "hearts=4") };
        Assert.Equal(Act.Letter, _engine.Decide(Inputs("Haley", Robin, hearts: 4, diary: longAgo)).Result);
    }

    [Fact]
    public void Worried_OnlyAboutSomeoneOnceSeen()
    {
        // Five days without news at 6 hearts: worried. No ledger entry at all: nothing to miss yet.
        MotiveInputs missing = Inputs("Robin", Robin, hearts: 6, daysSinceSighting: 5);
        Assert.Contains(_engine.Decide(missing).Motives, m => m.Motive == Motive.Worried);
        MotiveInputs never = missing with { KnowsOfPlayer = false, DaysSinceSighting = MotiveInputBuilder.NeverSeenDays };
        Assert.DoesNotContain(_engine.Decide(never).Motives, m => m.Motive == Motive.Worried);
    }

    [Fact]
    public void Deterministic_SameInputsSameDecision()
    {
        MotiveInputs i = Inputs("Shane", Shane, hearts: 8, near: true, diary: new[] { E(Day, "GiftReceived", "taste=Love;item=(O)1") });
        MotiveDecision a = _engine.Decide(i), b = _engine.Decide(i);
        Assert.Equal(a.Result, b.Result);
        Assert.Equal(a.Mood, b.Mood);
        Assert.Equal(a.NetFeeling, b.NetFeeling);
    }

    [Fact]
    public void CloseCallPropositionsNameTheAct()
    {
        Assert.Equal("would Shane write the player a cold letter now?", MotivesEngine.CloseCallProposition("Shane", Act.Letter, hostile: true));
        Assert.Equal("would Pam walk over to greet the player now?", MotivesEngine.CloseCallProposition("Pam", Act.WalkUp, hostile: false));
        Assert.Equal("would Pam confront the player now?", MotivesEngine.CloseCallProposition("Pam", Act.WalkUp, hostile: true));
    }
}
