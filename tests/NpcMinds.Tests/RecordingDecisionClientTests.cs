using NpcDecision;
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
}
