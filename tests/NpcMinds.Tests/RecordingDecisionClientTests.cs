using System.Text.Json;
using NpcDecision;
using NpcMinds.Playtest;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>The call recorder only watches: answers pass through unchanged.</summary>
public sealed class RecordingDecisionClientTests
{
    private sealed class Throws : IDecisionClient
    {
        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context) => throw new InvalidOperationException();
        public double Score(string context, double min, double max) => throw new InvalidOperationException();
        public double YesNo(string context, string proposition) => throw new InvalidOperationException();
    }

    private static (RecordingDecisionClient Client, RingLog<DecisionCall> Log) Recorder(IDecisionClient inner, string caller = "ladder")
    {
        var log = new RingLog<DecisionCall>(50);
        return (new RecordingDecisionClient(new ResilientDecisionClient(inner), log, caller), log);
    }

    [Fact]
    public void AnswersAreExactlyTheWrappedClientsAnswers()
    {
        var plain = new ResilientDecisionClient(new VariedFakeDecisionClient());
        (RecordingDecisionClient recorded, _) = Recorder(new VariedFakeDecisionClient());
        var options = new[] { "a", "b", "c" };

        Assert.Equal(plain.YesNo("npc: Sam", "should Sam wave?"), recorded.YesNo("npc: Sam", "should Sam wave?"));
        Assert.Equal(plain.Choose(options, "ctx"), recorded.Choose(options, "ctx"));
        Assert.Equal(plain.Score("ctx", 0, 10), recorded.Score("ctx", 0, 10));
        var questions = new Question[] { new YesNoQuestion("speak", "does Sam have news for the player?"), new ChoiceQuestion("pick", options) };
        static string Flat(IReadOnlyList<Answer> answers) => string.Join(";", answers.Select(a =>
            $"{a.Id}={a.YesNo}|{a.Score}|{string.Join(",", a.Probabilities ?? Array.Empty<double>())}"));
        Assert.Equal(Flat(plain.Ask("npc: Sam", questions)), Flat(recorded.Ask("npc: Sam", questions)));
    }

    [Fact]
    public void AYesNoIsRecordedWithItsNpcAndAnswer()
    {
        (RecordingDecisionClient client, RingLog<DecisionCall> log) = Recorder(new FakeDecisionClient());

        client.YesNo("urge=0.40; hearts=2", "should Abigail try to get the player's attention with Emote now?");

        DecisionCall call = Assert.Single(log.Newest());
        Assert.Equal("ladder", call.Caller);
        Assert.Equal("yesno", call.Type);
        Assert.Equal("Abigail", call.Npc);
        Assert.Equal(0.5, Assert.Single(call.Answers).Value);
        Assert.False(call.FellBack);
        Assert.Equal("urge=0.40; hearts=2", call.ContextHead);
    }

    [Fact]
    public void ABatchRecordsEveryAnswerAndTheCardsNpc()
    {
        (RecordingDecisionClient client, RingLog<DecisionCall> log) = Recorder(new FakeDecisionClient(), "plan");

        client.Ask("npc: Emily\ntemperament: polite", new Question[]
        {
            new YesNoQuestion("speak", "does Emily have news for the player?"),
            new ChoiceQuestion("pick", new[] { "saw the player", "talked with the player" }),
        });

        DecisionCall call = Assert.Single(log.Newest());
        Assert.Equal("batch", call.Type);
        Assert.Equal("plan", call.Caller);
        Assert.Equal("Emily", call.Npc);
        Assert.Equal(new[] { "yes", "saw the player", "talked with the player" }, call.Answers.Select(a => a.Label));
        Assert.Equal(new[] { 0.5, 0.5, 0.5 }, call.Answers.Select(a => a.Value));
        Assert.Contains("does Emily have news", call.Question);
    }

    [Fact]
    public void AFailedCallIsMarkedAsAFallback()
    {
        (RecordingDecisionClient client, RingLog<DecisionCall> log) = Recorder(new Throws());

        double p = client.YesNo("ctx", "should Sam wave?");

        Assert.Equal(0.5, p);
        Assert.True(Assert.Single(log.Newest()).FellBack);
    }

    [Fact]
    public void ItStillLooksLikeABatchClient()
    {
        (RecordingDecisionClient client, _) = Recorder(new FakeDecisionClient());
        Assert.IsAssignableFrom<IBatchDecisionClient>(client); // the planner's batched path stays on
    }

    [Fact]
    public void LongStatesAreCut()
    {
        (RecordingDecisionClient client, RingLog<DecisionCall> log) = Recorder(new FakeDecisionClient());
        client.YesNo(new string('x', 2000), "should Sam wave?");
        Assert.Equal(RecordingDecisionClient.ContextHeadChars + 3, Assert.Single(log.Newest()).ContextHead.Length);
    }

    [Theory]
    [InlineData("npc: Haley\nvoice: x", null, "Haley")]
    [InlineData("urge=0.4", "should Linus try to get the player's attention with Emote now?", "Linus")]
    [InlineData("", "does Gus have news for the player?", "Gus")]
    [InlineData("nothing here", "is it raining?", "it")]
    [InlineData(null, null, null)]
    public void TheNpcIsReadFromTheCardOrTheProposition(string? context, string? proposition, string? expected)
        => Assert.Equal(expected, RecordingDecisionClient.NpcOf(context, proposition));

    [Fact]
    public void ThePlaytestLogGetsEveryCallTheRecorderWatches()
    {
        string root = Path.Combine(Path.GetTempPath(), "npcmod-playtest-tests", Guid.NewGuid().ToString("N"), "playtest");
        var playtest = new PlaytestLog(root, enabled: true, _ => { });
        var client = new RecordingDecisionClient(new ResilientDecisionClient(new FakeDecisionClient()),
            new RingLog<DecisionCall>(50), "ladder", spread: null, playtest: playtest);

        double answer = client.YesNo("npc: Abigail\nurge=0.31",
            "should Abigail try to get the player's attention with Emote now?");

        Assert.Equal(0.5, answer); // the wrapped client's answer still comes back unchanged
        Assert.Equal(1, playtest.PendingFromWorker); // queued for the game thread, never written here

        playtest.OpenDay(1, "spring", 1);
        playtest.DrainWorkerQueue();
        playtest.Flush();

        string line = Assert.Single(TestFiles.ReadLines(Path.Combine(root, "1-spring-1.jsonl")));
        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement record = doc.RootElement;
        Assert.Equal("model", record.GetProperty("type").GetString());
        Assert.Equal("ladder", record.GetProperty("caller").GetString());
        Assert.Equal("yesno", record.GetProperty("kind").GetString());
        Assert.Equal("Abigail", record.GetProperty("npc").GetString());
        Assert.Equal("should <npc> try to get the player's attention with Emote now?",
            record.GetProperty("template").GetString());
        Assert.Equal(0.5, record.GetProperty("answer").GetDouble());
        Assert.False(record.GetProperty("fellBack").GetBoolean());
        Assert.Equal(0, record.GetProperty("tick").GetInt32()); // no game clock on the worker (yet)
    }
}
