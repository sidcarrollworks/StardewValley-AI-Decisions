using NpcMemory;
using NpcMotives;
using NpcTemperament;
using Xunit;
using static NpcMotives.Tests.MotivesEngineTests;

namespace NpcMotives.Tests;

/// <summary>The game-thread side of motives: regard applied as diary entries are written, the
/// inputs built from memory only, and the shadow log's words.</summary>
public sealed class RegardKeeperAndInputsTests
{
    private static RegardKeeper Keeper(RegardBook? book = null)
        => new(book ?? new RegardBook(), npc => npc switch { "Shane" => Shane, "Pam" => Pam, "Robin" => Robin, _ => Temperament.Neutral });

    [Fact]
    public void TheHookAppliesEachEntryOnce_ThroughTheOneDiaryWriter()
    {
        var keeper = Keeper();
        var store = new MemoryStore(7);
        var notes = new List<RegardNote>();
        store.Noting = (npc, entry, before) =>
        {
            if (keeper.OnNoted(npc, entry, before) is { } note)
                notes.Add(note);
        };

        store.Note("Shane", new DiaryEntry(Now, "Player", "StoodUp", "place=Saloon"));
        store.Note("Shane", new DiaryEntry(Now, "Player", "Saw", "Saloon")); // stirs nothing

        RegardNote stoodUp = Assert.Single(notes);
        Assert.Equal("StoodUp", stoodUp.Kind);
        Assert.True(stoodUp.Severe); // 0.7 x (0.5 + 0.74) is past the severe line: retention ignored
        Assert.Equal(1.0, stoodUp.Retention);
        Assert.Equal(0, stoodUp.Before);
        Assert.Equal(keeper.Book.Of("Shane", "Player"), stoodUp.After);
        Assert.True(stoodUp.After < -0.4);
    }

    [Fact]
    public void TheThirdIgnoreInFiveDaysCrossesTheYieldPoint()
    {
        var keeper = Keeper();
        var diary = new List<DiaryEntry>();
        RegardNote? last = null;
        for (int k = 0; k < 3; k++)
        {
            var entry = new DiaryEntry(Now + k * Day, "Player", "IgnoredBy", "Bubble");
            last = keeper.OnNoted("Haley", entry, diary);
            Assert.Equal(k == 2, last!.YieldCrossed);
            diary.Add(entry);
        }
        Assert.True(last!.After < 0);
    }

    [Fact]
    public void HearsayFromThePersonItHappenedToSticks_FromAWitnessItDoesNot()
    {
        var keeper = Keeper();
        var fromEmily = new DiaryEntry(Now, "Player", "Heard", "from=Emily;kind=GiftReceived;subject=Player;taste=Hate");
        RegardNote confirmed = keeper.OnNoted("Haley", fromEmily, Array.Empty<DiaryEntry>())!;
        Assert.True(confirmed.After < 0);
        Assert.Contains("who was in it", confirmed.Cause);

        var witness = new DiaryEntry(Now, "Player", "Heard", "from=Lewis;kind=SawRummaging;subject=Player");
        RegardNote elastic = Keeper().OnNoted("Haley", witness, Array.Empty<DiaryEntry>())!;
        Assert.Equal(elastic.Before, elastic.After);
        Assert.Contains("not confirmed", elastic.Cause);

        Assert.True(RegardKeeper.FromSource("SawGift", "Sam", "Sam")); // the subject told it themselves
        Assert.False(RegardKeeper.FromSource("SawGift", "Sam", "Penny"));
        // Retold (D25): only the person it started with is the source.
        Assert.True(RegardKeeper.FromSource("GiftReceived", "Haley", "Player", owner: "Haley"));
        Assert.False(RegardKeeper.FromSource("GiftReceived", "Sam", "Player", owner: "Haley"));
    }

    [Fact]
    public void ARetoldStoryStaysHearsay_TheSameStoryFromItsOwnerSticks()
    {
        var retold = new DiaryEntry(Now, "Player", "Heard", "from=Sam;kind=GiftReceived;subject=Player;of=Emily;b=3;j=2.1;at=0;hops=2;taste=Hate");
        RegardNote elastic = Keeper().OnNoted("Haley", retold, Array.Empty<DiaryEntry>())!;
        Assert.Equal(elastic.Before, elastic.After);
        Assert.Contains("heard from Sam; not confirmed", elastic.Cause);

        var fromOwner = retold with { Detail = "from=Emily;kind=GiftReceived;subject=Player;of=Emily;b=3;j=2.1;at=0;hops=1;taste=Hate" };
        Assert.True(Keeper().OnNoted("Haley", fromOwner, Array.Empty<DiaryEntry>())!.After < 0);
    }

    [Fact]
    public void JuicinessFollowsTheStressorTable_WithGossipOnlyValuesForKindsWithoutAFeeling()
    {
        double? j(string kind, string? detail = null) => StressorTable.JuicinessOf(new DiaryEntry(Now, "Player", kind, detail));
        Assert.Equal(4, j("SawRummaging"));
        Assert.Equal(3, j("GiftReceived", "taste=Hate"));
        Assert.Equal(2, j("QuestHelped"));
        Assert.Equal(1.5, j("SawGift", "giver=Player;taste=Like"));
        Assert.Equal(2, j("SawGift", "giver=Player;taste=Love"));
        Assert.Equal(3, j("SawGift", "giver=Player;taste=Hate"));
        Assert.Equal(2, j("TownNews"));
        Assert.Equal(1, j("Festival"));
        Assert.Null(j("Saw", "Town"));           // a plain sighting is never gossip on its own
        Assert.Null(j("GiftReceived", "taste=Neutral"));
    }

    [Fact]
    public void AGrudgeEasesAfterItsPenalty()
    {
        var book = new RegardBook();
        book.Set("Shane", "Player", -0.8);
        RegardNote relief = Keeper(book).Relieve("Shane", "Player", 0.3);
        Assert.Equal(-0.8, relief.Before, 6);
        Assert.Equal(-0.5, relief.After, 6);
        Assert.Equal(-0.5, book.Of("Shane", "Player"), 6);
    }

    [Fact]
    public void InputsComeFromMemoryOnly()
    {
        var near = new LedgerView("Pam", "Player", LedgerDetail.NamedSpot, "Town", 0, 0, Now, "10,10");
        var talked = new[] { new DiaryEntry(Now - 3 * Day, "Player", "Talked", "hearts=4") };
        MotiveInputs i = MotiveInputBuilder.Build("Pam", Now, Pam, 4, talked, -0.2, near, null, 3, seed: 9, card: "npc: Pam");
        Assert.True(i.PlayerNear);
        Assert.True(i.SeenPlayerToday);
        Assert.True(i.HasMetPlayer);
        Assert.True(i.KnowsOfPlayer);
        Assert.Equal(0, i.DaysSinceSighting);
        Assert.Equal(-0.2, i.RegardForPlayer);
        Assert.Equal(3, i.BestNewsScore);
        Assert.Equal("npc: Pam", i.Card);

        // Hearsay is neither near nor seen today; no view at all is "never".
        var told = near with { HopCount = 1, ToldBy = "Gus", AgeTicks = 2 * Day + 5 };
        MotiveInputs heard = MotiveInputBuilder.Build("Pam", Now, Pam, 4, Array.Empty<DiaryEntry>(), 0, told, null, 0, 9);
        Assert.False(heard.PlayerNear);
        Assert.False(heard.SeenPlayerToday);
        Assert.False(heard.HasMetPlayer);
        Assert.Equal(2, heard.DaysSinceSighting);
        Assert.Equal(MotiveInputBuilder.NeverSeenDays,
            MotiveInputBuilder.Build("Pam", Now, Pam, 0, Array.Empty<DiaryEntry>(), 0, null, null, 0, 9).DaysSinceSighting);

        // A habit lead with a place is somewhere to go looking.
        var habit = new Whereabouts("Pam", "Player", WhereaboutsSource.Habit, "Beach", LedgerDetail.Region, 0, 0, null, 0.6, 4);
        MotiveInputs lead = MotiveInputBuilder.Build("Pam", Now, Pam, 4, talked, 0, null, habit, 0, 9);
        Assert.True(lead.HasLead);
        Assert.Equal("Beach", lead.LeadPlace);
    }

    [Fact]
    public void ShadowLinesShowEveryPartOfACloseCall()
    {
        // Stood up two days ago, a small grudge: a hostile letter just within reach, a close call.
        var runner = new MotivesRunner(new MotivesRunnerTests.Scripted(yes: 0.48));
        MotiveInputs shane = Inputs("Shane", Shane, hearts: 4, diary: new[] { E(2 * Day, "StoodUp", "place=Saloon") }, regard: -0.1)
            with { Card = "npc: Shane" };
        MotiveEvent e = Assert.Single(runner.Tick(Now, new[] { shane }));
        Assert.NotNull(e.Decision!.Pending);
        Assert.True(e.Decision.Mood.Outlook < 0);
        // A bad day tilts a hostile close call toward yes: 0.48 becomes more than 0.5.
        Assert.Equal("Act", e.Kind);
        string line = MotiveText.Line(e);
        Assert.StartsWith("Shane: motive Hurt", line);
        Assert.Contains("hostile letter: boldness 0.16 + familiarity 0.02 + intensity", line);
        Assert.Contains("close call; Laya 0.48, outlook -", line);
        Assert.Contains(": yes: would write you a cold letter", line);

        // The same call with a firmer no: the pass says so.
        MotiveEvent pass = Assert.Single(new MotivesRunner(new MotivesRunnerTests.Scripted(yes: 0.3)).Tick(Now, new[] { shane }));
        Assert.Equal("Pass", pass.Kind);
        Assert.EndsWith(": no: passes", MotiveText.Line(pass));

        Assert.Equal("was stood up yesterday",
            MotiveText.GrudgeCauses(new[] { E(Day, "StoodUp", "place=Saloon"), E(0, "Saw") }, Now, 7));
        Assert.Equal("would Shane hold this against the player?", MotiveText.GrudgeProposition("Shane"));
    }

    [Fact]
    public void TheModelStateLeadsWithTheCard_AndNamesTheAct()
    {
        MotiveInputs shane = Inputs("Shane", Shane, hearts: 0, near: true, met: false) with { Card = "npc: Shane\ntemperament: shy" };
        MotiveDecision d = new MotivesEngine().Decide(shane);
        Assert.NotNull(d.Pending);
        string state = MotiveText.CloseCallState(shane, d);
        Assert.StartsWith("npc: Shane", state);
        Assert.Contains("feelings toward the player: curious", state);
        Assert.Contains("thinking about: call out to the player", state);
        Assert.True(state.Length <= NpcDecision.DecisionState.StateBudgetChars);
    }

    [Fact]
    public void ARegardChangeTooSmallToShowIsNotLogged()
    {
        // A talk's lasting mark is about 0.002: the line would read "+0.00 -> +0.00".
        RegardNote talk = new("Gus", "Player", "Talked", 0.1, 0.02, 1, false, false, 0.050, 0.052, "Talked (hearts=0)");
        Assert.Null(MotiveText.RegardLine(talk));

        RegardNote gift = talk with { Kind = "GiftReceived", After = 0.15, Cause = "a loved gift" };
        Assert.Equal("Gus: regard for Player +0.05 -> +0.15 (a loved gift)", MotiveText.RegardLine(gift));
    }
}
