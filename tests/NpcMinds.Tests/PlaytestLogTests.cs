using System.Text.Json;
using NpcMemory;
using NpcMinds.Playtest;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>
/// The playtest log's writer (docs/spec/debug-tools.md, "Playtest log"): each record type is one
/// JSON line, a disabled log writes nothing, a failed write warns once and never throws, day
/// files are one per day, and presence records are deltas built from CollectPresences output.
/// </summary>
public sealed class PlaytestLogTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "npcmod-playtest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>One JSON line, parsed (cloned so the document can go out of scope).</summary>
    private static JsonElement Json(string line)
    {
        using JsonDocument doc = JsonDocument.Parse(line);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void EveryRecordTypeRoundTripsThroughOneJsonLine()
    {
        JsonElement presence = Json(PlaytestRecords.ToLine(
            new PresenceRecord("Abigail", "Town", 12, 7) { Tick = 3 }));
        Assert.Equal(3, presence.GetProperty("tick").GetInt32());
        Assert.Equal("presence", presence.GetProperty("type").GetString());
        Assert.Equal("Abigail", presence.GetProperty("name").GetString());
        Assert.Equal("Town", presence.GetProperty("location").GetString());
        Assert.Equal(12, presence.GetProperty("x").GetInt32());
        Assert.Equal(7, presence.GetProperty("y").GetInt32());

        JsonElement ladder = Json(PlaytestRecords.ToLine(new LadderRecord(
            "Sam", "Attempt", "Bubble", 0.51, 0.51, 0.45, 0.87, "urge reached the step", "Beach") { Tick = 4 }));
        Assert.Equal(4, ladder.GetProperty("tick").GetInt32());
        Assert.Equal("ladder", ladder.GetProperty("type").GetString());
        Assert.Equal("Sam", ladder.GetProperty("npc").GetString());
        Assert.Equal("Attempt", ladder.GetProperty("kind").GetString());
        Assert.Equal("Bubble", ladder.GetProperty("step").GetString());
        Assert.Equal(0.51, ladder.GetProperty("urgeBefore").GetDouble());
        Assert.Equal(0.51, ladder.GetProperty("urgeAfter").GetDouble());
        Assert.Equal(0.45, ladder.GetProperty("threshold").GetDouble());
        Assert.Equal(0.87, ladder.GetProperty("modelP").GetDouble());
        Assert.Equal("urge reached the step", ladder.GetProperty("detail").GetString());
        Assert.Equal("Beach", ladder.GetProperty("leadPlace").GetString());

        JsonElement gossip = Json(PlaytestRecords.ToLine(new GossipRecord(
            "Sam", "Abigail", "GiftReceived", "Player", 1, "heard", null) { Tick = 5 }));
        Assert.Equal(5, gossip.GetProperty("tick").GetInt32());
        Assert.Equal("gossip", gossip.GetProperty("type").GetString());
        Assert.Equal("Sam", gossip.GetProperty("teller").GetString());
        Assert.Equal("Abigail", gossip.GetProperty("listener").GetString());
        Assert.Equal("GiftReceived", gossip.GetProperty("originalKind").GetString());
        Assert.Equal("Player", gossip.GetProperty("subject").GetString());
        Assert.Equal(1, gossip.GetProperty("hops").GetInt32());
        Assert.Equal("heard", gossip.GetProperty("kind").GetString());
        Assert.False(gossip.TryGetProperty("answered", out _)); // null: omitted, not written as null

        JsonElement game = Json(PlaytestRecords.ToLine(new GameRecord(
            "gift", "item=(O)421;name=Sunflower;taste=Love", Npc: "Abigail", Item: "Sunflower", Taste: "Love") { Tick = 6 }));
        Assert.Equal(6, game.GetProperty("tick").GetInt32());
        Assert.Equal("game", game.GetProperty("type").GetString());
        Assert.Equal("gift", game.GetProperty("kind").GetString());
        Assert.Equal("Abigail", game.GetProperty("npc").GetString());
        Assert.Equal("Sunflower", game.GetProperty("item").GetString());
        Assert.Equal("Love", game.GetProperty("taste").GetString());
        Assert.Equal("item=(O)421;name=Sunflower;taste=Love", game.GetProperty("detail").GetString());

        JsonElement plan = Json(PlaytestRecords.ToLine(new PlanRecord(
            "Abigail", "I saw you in town.", "SawGift", 2.5, 1) { Tick = 7 }));
        Assert.Equal(7, plan.GetProperty("tick").GetInt32());
        Assert.Equal("plan", plan.GetProperty("type").GetString());
        Assert.Equal("Abigail", plan.GetProperty("npc").GetString());
        Assert.Equal("I saw you in town.", plan.GetProperty("line").GetString());
        Assert.Equal("SawGift", plan.GetProperty("reason").GetString());
        Assert.Equal(2.5, plan.GetProperty("news").GetDouble());
        Assert.Equal(1, plan.GetProperty("rank").GetInt32());

        JsonElement memory = Json(PlaytestRecords.ToLine(new MemoryRecord(
            "Abigail", 500, 12, 3, 27, 4) { Tick = 8 }));
        Assert.Equal(8, memory.GetProperty("tick").GetInt32());
        Assert.Equal("memory", memory.GetProperty("type").GetString());
        Assert.Equal("Abigail", memory.GetProperty("npc").GetString());
        Assert.Equal(500, memory.GetProperty("diaryEntries").GetInt32());
        Assert.Equal(12, memory.GetProperty("addedToday").GetInt32());
        Assert.Equal(3, memory.GetProperty("trimmedToday").GetInt32());
        Assert.Equal(27, memory.GetProperty("ledgerEntries").GetInt32());
        Assert.Equal(4, memory.GetProperty("beliefs").GetInt32());

        JsonElement model = Json(PlaytestRecords.ToLine(new ModelCallRecord(
            "plan", "batch", "Emily", "does <npc> have news for the player?",
            "does Emily have news for the player?", 0.5, 12.5, false) { Tick = 9 }));
        Assert.Equal(9, model.GetProperty("tick").GetInt32());
        Assert.Equal("model", model.GetProperty("type").GetString());
        Assert.Equal("plan", model.GetProperty("caller").GetString());
        Assert.Equal("batch", model.GetProperty("kind").GetString());
        Assert.Equal("Emily", model.GetProperty("npc").GetString());
        Assert.Equal("does <npc> have news for the player?", model.GetProperty("template").GetString());
        Assert.Equal("does Emily have news for the player?", model.GetProperty("question").GetString());
        Assert.Equal(0.5, model.GetProperty("answer").GetDouble());
        Assert.Equal(12.5, model.GetProperty("ms").GetDouble());
        Assert.False(model.GetProperty("fellBack").GetBoolean());

        JsonElement perf = Json(PlaytestRecords.ToLine(new PerfRecord("tick", 3.25) { Tick = 10 }));
        Assert.Equal(10, perf.GetProperty("tick").GetInt32());
        Assert.Equal("perf", perf.GetProperty("type").GetString());
        Assert.Equal("tick", perf.GetProperty("section").GetString());
        Assert.Equal(3.25, perf.GetProperty("ms").GetDouble());
    }

    [Fact]
    public void NullOptionalFieldsAreOmittedFromTheLine()
    {
        // Threshold/ModelP null: the model was never asked, so there is nothing to carry.
        JsonElement ladder = Json(PlaytestRecords.ToLine(new LadderRecord(
            "Sam", "Ignored", "Approach", 0.62, 0.42, null, null, "window passed", null) { Tick = 11 }));
        Assert.False(ladder.TryGetProperty("threshold", out _));
        Assert.False(ladder.TryGetProperty("modelP", out _));
        Assert.False(ladder.TryGetProperty("leadPlace", out _));

        // Answer null: choice and batch calls have no single answer.
        JsonElement model = Json(PlaytestRecords.ToLine(new ModelCallRecord(
            "ladder", "choice", "Sam", "which option fits best?", "which option fits best?",
            null, 8.0, true) { Tick = 12 }));
        Assert.False(model.TryGetProperty("answer", out _));
        Assert.True(model.GetProperty("fellBack").GetBoolean());
    }

    [Fact]
    public void ADisabledLogWritesNothing()
    {
        string root = Path.Combine(NewTempDir(), "playtest");
        var warnings = new List<string>();
        var log = new PlaytestLog(root, enabled: false, warnings.Add);

        log.OpenDay(1, "spring", 1);
        log.Append(new PresenceRecord("Abigail", "Town", 1, 1) { Tick = 10 });
        log.Flush();

        Assert.False(Directory.Exists(root)); // not even the folder is created
        Assert.Empty(warnings);
    }

    [Fact]
    public void AFailedWriteWarnsOnceAndNeverThrows()
    {
        // A real file where the log wants a folder makes the first write fail for good.
        string temp = NewTempDir();
        string inner = Path.Combine(temp, "inner");
        File.WriteAllText(inner, "a file, not a folder");
        var warnings = new List<string>();
        var log = new PlaytestLog(Path.Combine(inner, "sub"), enabled: true, warnings.Add);

        log.OpenDay(1, "spring", 1);
        log.Append(new PresenceRecord("Abigail", "Town", 1, 1) { Tick = 10 });

        Exception? thrown = Record.Exception(log.Flush);
        Assert.Null(thrown); // Flush never throws into the game loop
        string warning = Assert.Single(warnings);
        Assert.Contains("Playtest log write failed", warning);

        // The log disabled itself after the failure: a second flush is safe and silent.
        Assert.Null(Record.Exception(log.Flush));
        Assert.Single(warnings);
    }

    [Fact]
    public void DayFilesAreOnePerDayAndEarlierDaysStayUntouched()
    {
        string root = NewTempDir();
        var log = new PlaytestLog(root, enabled: true, _ => { });

        log.OpenDay(1, "spring", 1);
        log.Append(new PresenceRecord("Abigail", "Town", 1, 1) { Tick = 10 });
        log.Flush();

        string first = Path.Combine(root, "1-spring-1.jsonl");
        Assert.False(File.Exists(Path.Combine(root, "1-spring-2.jsonl")));
        Assert.StartsWith("{\"tick\":10", Assert.Single(TestFiles.ReadLines(first)));

        log.OpenDay(1, "spring", 2);
        log.Append(new PresenceRecord("Abigail", "Town", 2, 2) { Tick = 20 });
        log.Flush();

        string second = Path.Combine(root, "1-spring-2.jsonl");
        Assert.StartsWith("{\"tick\":20", Assert.Single(TestFiles.ReadLines(second)));
        Assert.Single(TestFiles.ReadLines(first)); // day 1 was not cleared and not appended to

        // Re-opening the same day neither clears the file nor creates a second one.
        log.OpenDay(1, "spring", 2);
        log.Append(new PresenceRecord("Abigail", "Town", 3, 3) { Tick = 21 });
        log.Flush();
        Assert.Equal(2, TestFiles.ReadLines(second).Length);
        Assert.Equal(2, Directory.GetFiles(root).Length);

        // The next day is a new file again; the day before it keeps its two lines.
        log.OpenDay(1, "spring", 3);
        log.Append(new PresenceRecord("Abigail", "Town", 4, 4) { Tick = 30 });
        log.Flush();
        Assert.StartsWith("{\"tick\":30", Assert.Single(TestFiles.ReadLines(Path.Combine(root, "1-spring-3.jsonl"))));
        Assert.Equal(2, TestFiles.ReadLines(second).Length);
    }

    [Fact]
    public void AHeardRecordCarriesItsHopsAndWhenItWasTold()
    {
        var retold = new DiaryEntry(4239, "Leah", "Heard", "from=Caroline;kind=SawGift;subject=Leah;of=Marnie;b=2;j=1.4;at=4359;hops=2;giver=Player");
        GossipRecord r = PlaytestRecords.Heard("Emily", retold);
        Assert.Equal(("Caroline", "Emily", "SawGift", "Leah", 2), (r.Teller, r.Listener, r.OriginalKind, r.Subject, r.Hops));
        Assert.Equal(4359, r.Tick);

        var old = new DiaryEntry(100, "Player", "Heard", "from=Pam;kind=QuestHelped;subject=Player");
        Assert.Equal((1, 100), (PlaytestRecords.Heard("Emily", old).Hops, PlaytestRecords.Heard("Emily", old).Tick));
    }

    [Fact]
    public void PresenceDeltasLogMovesAndAlwaysThePlayer()
    {
        var previous = new Dictionary<string, (string Location, int X, int Y)>();
        var presences = new List<Presence>
        {
            new("A", "Town", 1, 1),
            new("B", "Town", 2, 2),
            new("Player", "Farm", 0, 0, IsPlayer: true),
        };

        // First sight: everyone is logged (the map starts empty).
        List<PresenceRecord> first = PlaytestRecords.PresenceDeltas(presences, previous, 5);
        Assert.Equal(new[] { "A", "B", "Player" }, first.Select(p => p.Name));
        Assert.All(first, p => Assert.Equal(5, p.Tick));

        // Nothing moved: only the player is logged, so their movement is never missed.
        List<PresenceRecord> unchanged = PlaytestRecords.PresenceDeltas(presences, previous, 6);
        Assert.Equal(new[] { "Player" }, unchanged.Select(p => p.Name));

        // A moves: only A and the player.
        var moved = new List<Presence>
        {
            new("A", "Town", 9, 9),
            new("B", "Town", 2, 2),
            new("Player", "Farm", 0, 0, IsPlayer: true),
        };
        List<PresenceRecord> afterMove = PlaytestRecords.PresenceDeltas(moved, previous, 7);
        Assert.Equal(new[] { "A", "Player" }, afterMove.Select(p => p.Name));
        Assert.Equal(9, afterMove[0].X); // the moved tile, not the old one
    }
}
