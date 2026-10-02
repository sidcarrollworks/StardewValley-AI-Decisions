using NpcMemory;
using NpcMotives;
using NpcTemperament;
using Xunit;
using static NpcMotives.Tests.MotivesEngineTests;

namespace NpcMotives.Tests;

/// <summary>Regard (the saved, plastic part), the yield point, retention, drift, hearsay and
/// the daily mood roll (docs/spec/motives.md, "Acceptance tests").</summary>
public sealed class RegardAndMoodTests
{
    private readonly MotiveOptions _o = new();

    [Fact]
    public void AStandUpLeavesALastingMark_AShortTalkBarelyDoes()
    {
        var book = new RegardBook();
        double standUp = book.Apply("Shane", E(0, "StoodUp", "place=Saloon"), Array.Empty<DiaryEntry>(), Shane, _o);
        Assert.True(standUp < -0.3);
        Assert.Equal(standUp, book.Of("Shane", "Player"), 6);
        Assert.Equal(-standUp, book.GrudgeOf("Shane", "Player"), 6);

        double talk = new RegardBook().Apply("Shane", E(0, "Talked"), Array.Empty<DiaryEntry>(), Shane, _o);
        Assert.InRange(talk, 0, 0.05);
    }

    [Fact]
    public void YieldPoint_ThreeIgnoresInFiveDaysDent_TwoDoNot()
    {
        var diary = new List<DiaryEntry> { E(3 * Day, "IgnoredBy", "Bubble") };
        var book = new RegardBook();
        Assert.Equal(0, book.Apply("Haley", E(2 * Day, "IgnoredBy", "Bubble"), diary, Temperament.Neutral, _o));
        diary.Add(E(2 * Day, "IgnoredBy", "Bubble"));
        double third = book.Apply("Haley", E(Day, "IgnoredBy", "Bubble"), diary, Temperament.Neutral, _o);
        Assert.True(third < 0);

        // Spread over more than the window, the same three never yield.
        var spread = new List<DiaryEntry> { E(12 * Day, "IgnoredBy", "Bubble"), E(6 * Day, "IgnoredBy", "Bubble") };
        Assert.Equal(0, new RegardBook().Apply("Haley", E(0, "IgnoredBy", "Bubble"), spread, Temperament.Neutral, _o));
    }

    [Fact]
    public void Retention_PamKeepsLessThanRobin_ButEveryoneKeepsASevereHurt()
    {
        var sameSensitivity = Temperament.Neutral;
        var hated = E(0, "GiftReceived", "taste=Hate;item=(O)1");
        double pam = new RegardBook().Apply("Pam", hated, Array.Empty<DiaryEntry>(), sameSensitivity, _o);
        double robin = new RegardBook().Apply("Robin", hated, Array.Empty<DiaryEntry>(), sameSensitivity, _o);
        Assert.True(Math.Abs(pam) < Math.Abs(robin));

        var severe = E(0, "StoodUp", "place=Farm;seen=1"); // 0.85 x 1.0 sensitivity factor: severe
        double pamSevere = new RegardBook().Apply("Pam", severe, Array.Empty<DiaryEntry>(), sameSensitivity, _o);
        double robinSevere = new RegardBook().Apply("Robin", severe, Array.Empty<DiaryEntry>(), sameSensitivity, _o);
        Assert.Equal(robinSevere, pamSevere, 6);
    }

    [Fact]
    public void Drift_GrudgesHealByForgiveness_WarmthFadesSlowly()
    {
        var book = new RegardBook();
        book.Set("Shane", "Player", -0.5);
        book.Set("Emily", "Player", -0.5);
        book.Set("Robin", "Player", 0.5);
        Temperament Of(string npc) => npc == "Emily" ? Temperament.Neutral with { Forgiveness = 0.68 } : Shane;
        book.Drift(Of, _o);
        Assert.True(book.Of("Emily", "Player") > book.Of("Shane", "Player")); // Emily forgives faster
        Assert.InRange(book.Of("Robin", "Player"), 0.49, 0.5);
    }

    [Fact]
    public void Regard_RoundTripsAndDropsMalformedKeys()
    {
        var book = new RegardBook();
        book.Set("Shane", "Player", -0.42);
        book.Set("Sam", "Sebastian", 0.6);
        RegardBook back = RegardBook.FromJson(book.ToJson());
        Assert.Equal(-0.42, back.Of("Shane", "Player"), 4);
        Assert.Equal(0.6, back.Of("Sam", "Sebastian"), 4);
        Assert.Equal(0, RegardBook.FromJson("{\"nobar\":0.5,\"|x\":0.2}").Count);
        Assert.Equal(0, RegardBook.FromJson(null).Count);
        Assert.Equal(0, RegardBook.FromJson("not json").Count); // a damaged value never fails the load
    }

    [Fact]
    public void Seed_OnlyFillsEmptyPairs()
    {
        var book = new RegardBook();
        book.Set("Jodi", "Vincent", 0.9);
        int seeded = book.Seed(new[] { ("Jodi", "Vincent", 0.8), ("Jodi", "Sam", 0.8) });
        Assert.Equal(1, seeded);
        Assert.Equal(0.9, book.Of("Jodi", "Vincent"), 6);
    }

    [Fact]
    public void Hearsay_IsElasticUntilConfirmed()
    {
        var heard = E(Day / 2, "Heard", "from=Emily;kind=GiftReceived;subject=Player;taste=Hate");
        IReadOnlyList<Stress> elastic = Stresses.Elastic(new[] { heard }, Now, Temperament.Neutral, 0, _o);
        Stress s = Assert.Single(elastic);
        Assert.Equal(Motive.Hurt, s.Motive);

        var book = new RegardBook();
        Assert.Equal(0, book.Apply("Haley", heard, Array.Empty<DiaryEntry>(), Temperament.Neutral, _o)); // never plastic as is
        double half = book.ApplyConfirmed("Haley", heard, 0.5, Temperament.Neutral, _o);
        Assert.True(half < 0);

        // Confirmed hearsay marks the listener less than the event marked the person it happened to.
        double heardInFull = new RegardBook().ApplyConfirmed("Haley", heard, 1, Temperament.Neutral, _o);
        double firstHand = new RegardBook().Apply("Emily", E(Day / 2, "GiftReceived", "taste=Hate"),
            Array.Empty<DiaryEntry>(), Temperament.Neutral, _o);
        Assert.Equal(firstHand * _o.HearsayFactor, heardInFull, 6);
    }

    [Fact]
    public void ElasticStressFadesWithTime()
    {
        DiaryEntry gift = E(0, "GiftReceived", "taste=Love;item=(O)1");
        double fresh = Stresses.Elastic(new[] { gift }, Now, Temperament.Neutral, 0, _o).Single().Strength;
        double later = Stresses.Elastic(new[] { gift }, Now + 2 * Day, Temperament.Neutral, 0, _o).Single().Strength;
        Assert.True(later < fresh);
        Assert.Empty(Stresses.Elastic(new[] { gift }, Now + 4 * Day, Temperament.Neutral, 0, _o)); // past the window
    }

    [Fact]
    public void Jealousy_OnlyWhenDrawnToTheGiver()
    {
        DiaryEntry saw = E(Day / 4, "SawGift", "giver=Player;taste=Love;name=Rose", subject: "Leah");
        Assert.Empty(Stresses.Elastic(new[] { saw }, Now, Temperament.Neutral, heartsWithPlayer: 4, _o));
        Stress s = Assert.Single(Stresses.Elastic(new[] { saw }, Now, Temperament.Neutral, heartsWithPlayer: 9, _o));
        Assert.Equal(Motive.Jealous, s.Motive);
        Assert.Equal("Player", s.Subject);
    }

    [Fact]
    public void MoodRoll_IsDeterministicBoundedAndRarelyOutOfCharacter()
    {
        var a = MoodRoll.Roll("Shane", 42, 7, Shane, _o);
        var b = MoodRoll.Roll("Shane", 42, 7, Shane, _o);
        Assert.Equal(a, b);

        int tails = 0, n = 4000;
        for (int day = 0; day < n; day++)
        {
            var (roll, tail) = MoodRoll.Roll("Evelyn", day, 7, Robin, _o);
            Assert.InRange(roll, -1, 1);
            if (tail)
            {
                tails++;
                Assert.True(roll < 0, "a tail day for a sunny character is a bad day");
            }
        }
        Assert.InRange(tails, n / 40 / 2, n / 40 * 2); // about 1 in 40
    }

    [Fact]
    public void Mood_EarnedOutweighsTheRoll()
    {
        // A loved gift yesterday: even the worst roll leaves a positive outlook.
        var diary = new[] { E(Day, "GiftReceived", "taste=Love;item=(O)1"), E(Day / 2, "QuestHelped", "quest=ItemDelivery") };
        IReadOnlyList<Stress> stresses = Stresses.Elastic(diary, Now, Robin, 6, _o);
        for (int seed = 0; seed < 50; seed++)
            Assert.True(MoodRoll.Today("Robin", Now, seed, Robin, stresses, _o).Outlook > 0);
    }
}
