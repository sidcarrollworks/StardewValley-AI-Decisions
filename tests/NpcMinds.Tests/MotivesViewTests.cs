using System.Text.Json;
using NpcDecision;
using NpcInitiation;
using NpcIntents;
using NpcMemory;
using NpcMinds.Playtest;
using NpcMotives;
using NpcTemperament;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>Motives in the viewer and the playtest log (docs/spec/debug-tools.md): the card shows
/// what the runner last weighed, each motives record is one JSON line with every part, and the
/// close-call questions group and compare in the spread panel.</summary>
public sealed class MotivesViewTests
{
    private const int Day = GameClock.TicksPerDay;
    private static readonly int Now = 10 * Day + 60;
    private static readonly Temperament Shane = new(0.30, 0.74, 0.27, 0.30, 0.5, 0.16, Anger: 0.49, Happiness: 0.35, Sadness: 0.76);

    private sealed class Answer : IDecisionClient
    {
        private readonly double _p;
        public Answer(double p) => _p = p;
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => options.Select(_ => 1.0 / options.Count).ToArray();
        public double Score(string context, double min, double max) => min;
        public double YesNo(string context, string proposition) => _p;
    }

    /// <summary>Shane, stood up two days ago: a hostile letter that is a close call.</summary>
    private static (MotivesRunner Runner, MotiveEvent Event, MotiveInputs Inputs) ShaneWrites(double p = 0.48)
    {
        var diary = new[] { new DiaryEntry(Now - 2 * Day, "Player", "StoodUp", "place=Saloon") };
        MotiveInputs inputs = MotiveInputBuilder.Build("Shane", Now, Shane, 4, diary, -0.1, null, null, 0, seed: 12345, metInGame: true);
        var runner = new MotivesRunner(new Answer(p));
        MotiveEvent e = Assert.Single(runner.Tick(Now, new[] { inputs }));
        return (runner, e, inputs);
    }

    private static MindsInputs Inputs(MotivesRunner? runner, IReadOnlyDictionary<string, string>? lines = null)
        => new(1, Now, "Fake", true, MindsStats.None, "none", null, Array.Empty<InitiationInput>(), Array.Empty<string>(),
            Array.Empty<IntentCandidate>(), Array.Empty<FeedItem>(),
            Motives: runner?.LatestDecisions(), MotiveStates: runner?.States(),
            RegardFor: npc => npc == "Shane" ? -0.53 : 0, LastMotiveLines: lines);

    private static MemoryStore Memory()
    {
        var memory = new MemoryStore();
        memory.Note("Shane", new DiaryEntry(Now - 2 * Day, "Player", "StoodUp", "place=Saloon"));
        memory.Note("Sam", new DiaryEntry(Now - 5, "Player", "Saw", "Town"));
        return memory;
    }

    [Fact]
    public void TheCardShowsWhatTheRunnerLastWeighed()
    {
        (MotivesRunner runner, MotiveEvent e, _) = ShaneWrites();
        var lines = new Dictionary<string, string> { ["Shane"] = MotiveText.Line(e) };
        MindsSnapshot snapshot = new MindsSnapshotBuilder().Build(Memory(), Inputs(runner, lines));

        MotivesView shane = snapshot.Npcs.Single(n => n.Name == "Shane").Motives!;
        Assert.Equal("Hurt", shane.Chosen);
        Assert.True(shane.Net < 0);
        Assert.Equal("letter", shane.Best);
        Assert.Equal("close", shane.Call);
        ActView letter = shane.Acts.Single(a => a.Act == "letter");
        Assert.True(letter.Hostile);
        Assert.Equal("close", letter.Call);
        Assert.Equal(-0.53, shane.Regard);
        Assert.Equal(1, shane.AttemptsToday);
        Assert.Equal("letter", shane.WaitingAct);
        Assert.Null(shane.OpenAct);
        Assert.Contains("would write you a cold letter", shane.LastLine);
        Assert.Equal("Hurt", shane.Motives[0].Motive);

        // An NPC the runner never weighed has no motives view.
        Assert.Null(snapshot.Npcs.Single(n => n.Name == "Sam").Motives);
    }

    [Fact]
    public void WithoutTheRunnerTheCardsHaveNoMotives_AndTheJsonSaysSo()
    {
        MindsSnapshot snapshot = new MindsSnapshotBuilder().Build(Memory(), Inputs(null));
        Assert.All(snapshot.Npcs, n => Assert.Null(n.Motives));

        // The page reads camelCase JSON; the motives field is there for the page to test.
        (MotivesRunner runner, _, _) = ShaneWrites();
        string json = JsonSerializer.Serialize(new MindsSnapshotBuilder().Build(Memory(), Inputs(runner)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"motives\":{\"motives\":[", json);
        Assert.Contains("\"chosenStrength\":", json);
    }

    [Fact]
    public void EveryMotivesRecordIsOneLineWithItsParts()
    {
        (_, MotiveEvent e, _) = ShaneWrites();
        using JsonDocument decision = JsonDocument.Parse(PlaytestRecords.ToLine(MotiveRecords.Decision(e)));
        JsonElement d = decision.RootElement;
        Assert.Equal(Now, d.GetProperty("tick").GetInt32());
        Assert.Equal("decision", d.GetProperty("type").GetString());
        Assert.Equal("act", d.GetProperty("kind").GetString());
        Assert.Equal("Shane", d.GetProperty("npc").GetString());
        Assert.Equal("Hurt", d.GetProperty("motive").GetString());
        Assert.Equal("Letter", d.GetProperty("act").GetString());
        Assert.True(d.GetProperty("hostile").GetBoolean());
        Assert.Equal("close", d.GetProperty("call").GetString());
        Assert.Equal(0.48, d.GetProperty("modelP").GetDouble());
        Assert.True(d.GetProperty("tilted").GetDouble() > 0.5); // a bad day tips a hostile close call to yes
        Assert.Equal(0.16, d.GetProperty("boldness").GetDouble());
        Assert.Equal(0.55, d.GetProperty("cost").GetDouble(), 6); // a letter's 0.25 + the hostile surcharge 0.30
        foreach (string part in new[] { "strength", "motives", "net", "outlook", "earned", "roll", "familiarity", "intensity", "frustration", "effective", "margin" })
            Assert.True(d.TryGetProperty(part, out _), part);
        Assert.False(d.TryGetProperty("grudge", out _)); // null parts are left out

        var note = new RegardNote("Shane", "Player", "StoodUp", 0.868, 0.5, 1, true, false, -0.1, -0.534, "StoodUp (place=Saloon)");
        using JsonDocument stress = JsonDocument.Parse(PlaytestRecords.ToLine(MotiveRecords.Stress(note, Now)));
        Assert.Equal("stress", stress.RootElement.GetProperty("type").GetString());
        Assert.Equal(-0.434, stress.RootElement.GetProperty("plastic").GetDouble(), 6);
        Assert.True(stress.RootElement.GetProperty("severe").GetBoolean());

        using JsonDocument regard = JsonDocument.Parse(PlaytestRecords.ToLine(MotiveRecords.Regard(note, Now)));
        Assert.Equal("regard", regard.RootElement.GetProperty("type").GetString());
        Assert.Equal(-0.1, regard.RootElement.GetProperty("before").GetDouble());
        Assert.Equal(-0.534, regard.RootElement.GetProperty("after").GetDouble());

        var book = new RegardBook();
        book.Set("Shane", "Player", -0.5);
        RegardRecord snap = Assert.Single(MotiveRecords.Snapshot(book, Now));
        Assert.Equal(("Shane", "Player", "snapshot"), (snap.Observer, snap.Subject, snap.Cause));
    }

    [Fact]
    public void CloseCallQuestionsGroupAndFollowBoldnessInTheSpreadPanel()
    {
        // Built by the real code, so a wording change breaks this test.
        string template(Act act, bool hostile) => SpreadTable.ReplaceNpc(MotivesEngine.CloseCallProposition("Haley", act, hostile), "Haley");
        Assert.Equal("close_friendly", LayaCalibration.NormalizeTemplate(template(Act.WalkUp, false)));
        Assert.Equal("close_hostile", LayaCalibration.NormalizeTemplate(template(Act.WalkUp, true)));
        Assert.Equal("hold_against", LayaCalibration.NormalizeTemplate(
            SpreadTable.ReplaceNpc(MotiveText.GrudgeProposition("Haley"), "Haley")));
        foreach (Act act in new[] { Act.Emote, Act.Bubble, Act.QueuedLine, Act.Letter, Act.Visit, Act.Interrupt })
            Assert.Equal(("boldness", +1), LayaCalibration.TraitForTemplate(template(act, false)));
    }
}
