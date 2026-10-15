using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class LivesDecisionTests
{
    internal static LivesDecisionRequest Request(double unit = .35)
    {
        var empty = new HistoryPack(1, Array.Empty<HistorySource>(), Array.Empty<HistoryFact>(),
            Array.Empty<HistoryAccount>(), Array.Empty<HistoryReading>());
        var memory = new PersonalMemory("Ada", empty);
        var person = new MemoryObject(MemoryObjectKind.Person, "Bo");
        memory.Learn(new(1, 1, "warm", KnowledgeKind.Lived,
            new[] { new AssociationInput(person, .15, new[] { "Bo" }) }, Array.Empty<ExpectationInput>()));
        memory.Learn(new(2, 2, "cold", KnowledgeKind.Lived,
            new[] { new AssociationInput(person, -.12, new[] { "Bo" }) }, Array.Empty<ExpectationInput>()));
        var options = new[]
        {
            new ChoiceOption("yes", "Let's play together this evening.", 1, new[] { person }, Array.Empty<ExpectationKey>()),
            new ChoiceOption("later", "I can't settle a plan just now.", 1, Array.Empty<MemoryObject>(), Array.Empty<ExpectationKey>()),
            new ChoiceOption("no", "No, I want the evening to myself.", 1, Array.Empty<MemoryObject>(), Array.Empty<ExpectationKey>()),
        };
        var choice = LivesChoices.Evaluate(memory.Capture(), new("offer:Ada:10", "Ada", 10, "answer",
            new[] { "Bo" }, options, .4), unit);
        return new(choice, "Bo just offered a game at home at 18:00. I am free then.");
    }

    [Fact]
    public async Task AuthoredAnswerUsesOriginalChoicesAndOriginalDraw()
    {
        var request = Request();
        var answer = await new AuthoredLivesDecisionMind().EvaluateAsync(request);
        Assert.Equal("authored", answer.Backend);
        Assert.False(answer.Called);
        Assert.Equal(request.Fallback.SelectedId, LivesDecisionEngine.Apply(request, answer));
        Assert.Equal(request.Fallback.Alternatives.Select(a => a.Probability), answer.Weights.Values);
    }

    [Fact]
    public void ModelReweightsButNeverChangesLinesOrDraw()
    {
        var request = Request(.35);
        string before = LivesDecisionEngine.ExactRequest(request);
        var answer = new LivesDecisionAnswer(new Dictionary<string, double> { ["yes"] = 0, ["later"] = 1, ["no"] = 0 }, "test", "laya");
        Assert.Equal("later", LivesDecisionEngine.Apply(request, answer));
        Assert.Equal(before, LivesDecisionEngine.ExactRequest(request));
    }

    [Fact]
    public void ZeroTailCannotBeSelectedByRounding()
    {
        var request = Request(Math.BitDecrement(1));
        var answer = new LivesDecisionAnswer(new Dictionary<string, double> { ["yes"] = double.MaxValue, ["later"] = 1, ["no"] = 0 }, "test", "laya");
        Assert.Equal("yes", LivesDecisionEngine.Apply(request, answer));
    }

    [Fact]
    public void MissingUnknownNegativeZeroAndWrongModelFailClosed()
    {
        var request = Request();
        var valid = new Dictionary<string, double> { ["yes"] = 1, ["later"] = 1, ["no"] = 1 };
        foreach (var invalid in new[]
        {
            new Dictionary<string, double> { ["yes"] = 1 },
            new Dictionary<string, double> { ["yes"] = 1, ["later"] = 1, ["invented"] = 1 },
            new Dictionary<string, double> { ["yes"] = -1, ["later"] = 1, ["no"] = 1 },
            new Dictionary<string, double> { ["yes"] = 0, ["later"] = 0, ["no"] = 0 },
            new Dictionary<string, double> { ["yes"] = double.NaN, ["later"] = 1, ["no"] = 1 },
        }) Assert.Throws<InvalidDataException>(() => LivesDecisionEngine.Apply(request, new(invalid, "test", "laya")));
        Assert.Throws<InvalidDataException>(() => LivesDecisionEngine.Apply(request, new(valid, "test", "different-model")));
    }

    [Fact]
    public void FutureOrOtherHoldersRecallCannotEnterARequest()
    {
        var request = Request();
        foreach (var item in new[] { request.Fallback.Recall.Items[0] with { Holder = "Bo" },
            request.Fallback.Recall.Items[0] with { KnownAt = 11 } })
        {
            var changed = request with { Fallback = request.Fallback with { Recall = request.Fallback.Recall with { Items = new[] { item } } } };
            Assert.Throws<ArgumentException>(() => LivesDecisionEngine.Validate(changed));
        }
    }

    [Fact]
    public async Task TapeRoundTripRetainsTheExactAnswerAndRequiresConsumption()
    {
        var request = Request();
        var answer = LivesDecisionEngine.Fallback(request, "Testing fallback", response: "failed-response", called: true);
        var record = new LivesDecisionRecord(request, answer, LivesDecisionEngine.Apply(request, answer));
        var roundTrip = JsonSerializer.Deserialize<LivesDecisionRecord>(JsonSerializer.Serialize(record))!;
        var tape = new RecordedLivesDecisionMind(new[] { roundTrip });
        Assert.Throws<InvalidOperationException>(tape.RequireConsumed);
        var replay = await tape.EvaluateAsync(request);
        Assert.Equal(JsonSerializer.Serialize(answer), JsonSerializer.Serialize(replay));
        Assert.Equal(0, tape.Remaining);
        tape.RequireConsumed();
        await Assert.ThrowsAsync<InvalidOperationException>(() => tape.EvaluateAsync(request));
    }

    [Fact]
    public async Task TapeRejectsChangedSceneDrawModelLineReasonOrRecall()
    {
        var request = Request();
        var answer = LivesDecisionEngine.Fallback(request);
        var record = new LivesDecisionRecord(request, answer, LivesDecisionEngine.Apply(request, answer));
        var alternatives = request.Fallback.Alternatives.ToArray();
        alternatives[0] = alternatives[0] with { Option = alternatives[0].Option with { Line = "A changed line." } };
        var reasons = request.Fallback.Alternatives.ToArray();
        reasons[0] = reasons[0] with { Reasons = reasons[0].Reasons.Select(r => r with { Code = r.Code + "-changed" }).ToArray() };
        var changed = new[]
        {
            request with { Context = "Different scene" }, request with { Model = "another-model" },
            request with { Fallback = request.Fallback with { Unit = .8 } },
            request with { Fallback = request.Fallback with { Alternatives = alternatives } },
            request with { Fallback = request.Fallback with { Alternatives = reasons } },
            request with { Fallback = request.Fallback with { Recall = request.Fallback.Recall with
                { Items = request.Fallback.Recall.Items.Select(m => m with { Text = "A changed account." }).ToArray() } } },
        };
        foreach (var other in changed)
            await Assert.ThrowsAsync<InvalidOperationException>(() => new RecordedLivesDecisionMind(new[] { record }).EvaluateAsync(other));
    }

    [Fact]
    public void TapeRejectsWrongSelectionAndDuplicateRequests()
    {
        var request = Request();
        var answer = LivesDecisionEngine.Fallback(request);
        var record = new LivesDecisionRecord(request, answer, LivesDecisionEngine.Apply(request, answer));
        Assert.Throws<InvalidDataException>(() => new RecordedLivesDecisionMind(new[] { record with { SelectedId = "invented" } }));
        Assert.Throws<InvalidDataException>(() => new RecordedLivesDecisionMind(new[] { record, record }));
    }
}
