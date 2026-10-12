using System.Net;
using System.Text;
using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>What the actor knows of where and when reaches both models, from the actor's own
/// records only, and the tape treats it as part of the request.</summary>
public class ReflectionSceneContextTests
{
    private const int Day = Clock.MinutesPerDay;

    private static Villager Person(string name, int x, string household = "") => new(name, household.Length == 0 ? name : household, "villager",
        new Temperament(1, 0.5, 0.5, 0.6), new Body(100, -1), null,
        new[] { new Haunt("Room", new Tile(x, 2), 0, Day, 1) }, new Dictionary<string, double>(), Array.Empty<string>());

    private static readonly ActKind Gift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Help = new("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1));
    private static readonly ActKind Argument = new("Argued", 3, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen));

    private static TownData Town(params Villager[] people)
    {
        FeelingOptions f = FeelingOptions.WithDesire();
        f.AnswerOn = f.ReturnOn = f.MakeUpOn = f.RetaliateOn = f.AvoidOn = f.WithdrawOn = false;
        return new TownData
        {
            Cast = people.Length == 0 ? new[] { Person("Ann", 3), Person("Bob", 5) } : people,
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { Gift, Help, Argument }, Feelings = f,
            Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 },
            Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    private sealed class Mind : IReflectionMind
    {
        public List<ReflectionRequest> Requests { get; } = new();
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken token = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ReflectionAnswer("Perhaps.", request.Choices.First(c => c.Kind.Length > 0).Id,
                request.Choices.ToDictionary(c => c.Id, c => c.Id == "reject" ? 1.0 : 0.0), "test"));
        }
    }

    private static ReflectionOptions Enabled() => new() { Enabled = true, QuietMinutes = 30, DailyChance = 1 };

    [Fact]
    public async Task SceneComesFromTheActorsOwnRecords()
    {
        TownData town = Town() with { Familiarity = new[] { ("Ann", "Bob", 0.5) } };
        var sim = new Simulation(1, town);
        // Two earlier kindnesses the day before, then the argument that is reflected on.
        sim.Place(new[]
        {
            new Scenario("k1", "GaveGift", "Bob", "Ann", Day: 0, From: Clock.At(9), To: Clock.At(9) + 1),
            new Scenario("k2", "HelpedSomeone", "Ann", "Bob", Day: 0, From: Clock.At(9, 30), To: Clock.At(9, 31)),
            new Scenario("hurt", "Argued", "Bob", "Ann", Day: 1, From: Clock.At(15), To: Clock.At(15) + 1),
        });
        sim.ConfigureReflection(Enabled());
        var mind = new Mind();
        // Three days: each person has three eligible memories and one reflection a day, so every
        // placed act, the argument included, is reflected on; which day is a seeded draw, so
        // check every request against the three placed acts rather than assuming the order.
        SimResult r = await sim.RunAsync(3, mind);
        Assert.Contains(mind.Requests, q => q.Actor == "Ann" && q.Source!.Kind == "Argued");
        Assert.NotEmpty(mind.Requests.Where(q => q.Actor == "Ann"));
        foreach (ReflectionRequest q in mind.Requests.Where(q => q.Actor == "Ann"))
        {
            ReflectionScene scene = q.Scene!;
            Act source = r.Acts[q.SourceActId];
            Act[] pair = r.Acts.Where(a => a.Id != source.Id && a.Tick <= q.Tick
                && (a.Actor == "Ann" && a.Target == "Bob" || a.Actor == "Bob" && a.Target == "Ann")).ToArray();
            Assert.Equal("Room", scene.SourcePlace);
            Assert.Equal("Room", scene.Place);
            Assert.Equal(Clock.Day(q.Tick) - Clock.Day(source.Tick), scene.DaysAgo); // calendar days, as a person counts
            Assert.Equal(ReflectionText.TimeOfDay(q.Tick), scene.TimeOfDay);
            Assert.Equal("familiar", scene.Relationship);
            // The pair's week excludes the source itself and counts both directions.
            Assert.Equal(pair.Count(a => a.Kind != "Argued"), scene.KindThisWeek);
            Assert.Equal(pair.Count(a => a.Kind == "Argued"), scene.UnkindThisWeek);
            Act? last = pair.OrderBy(a => a.Tick).LastOrDefault();
            Assert.Equal(last is null ? null : $"{(last.Kind == "Argued" ? "unkind" : "kind")} {ReflectionText.Age(Clock.Day(q.Tick) - Clock.Day(last.Tick))}", scene.LastBetween);
            if (source.Kind == "Argued")
            {
                Assert.Contains("a direct clash", scene.Reasons);
                Assert.Contains("you remember Bob argued with you", q.Memory);
            }
            Assert.Contains($"this week {scene.KindThisWeek} kind/{scene.UnkindThisWeek} unkind", scene.Summary);
            // The recording keeps the scene with the request; the observer can show it.
            ReflectionRecord record = Assert.Single(r.Reflections, t => t.Request.Id == q.Id);
            Assert.Equal(scene.Summary, record.Request.Scene!.Summary);
        }
        // Across the two days at least one request looked back at a day-0 act from day 1.
        Assert.Contains(mind.Requests, q => q.Actor == "Ann" && q.Scene!.DaysAgo == 1);
    }

    [Fact]
    public async Task SceneUsesTheBelievedSubjectAndHouseholdNeverTheTrueActor()
    {
        Villager bob = Person("Bob", 5) with { Temperament = new Temperament(1, 0.9, 0.5, 0.2) };
        TownData town = Town(Person("Ann", 3), bob, Person("Cara", 30, household: "Bob")) with
        {
            Perception = new PerceptionOptions { KnowWhoStranger = 2, KnowWhoPerFamiliarity = 0 },
            Familiarity = new[] { ("Bob", "Ann", 0.0), ("Bob", "Cara", 1.0) },
        };
        town.Feelings.Start = new Dictionary<(string, string), double> { [("Bob", "Cara")] = -1 };
        town.Feelings.GuessPerHate = 100_000;
        var sim = new Simulation(1, town);
        sim.Place(new[] { new Scenario("misidentified", "GaveGift", "Ann", "Bob", Day: 0, From: Clock.At(10), To: Clock.At(10) + 1) });
        sim.ConfigureReflection(Enabled());
        var mind = new Mind();
        await sim.RunAsync(1, mind);

        ReflectionRequest q = Assert.Single(mind.Requests, q => q.Actor == "Bob");
        Assert.Equal("Cara", q.Subject);
        // Bob and Cara share a household in this town; Ann, the true giver, is a stranger to Bob.
        Assert.Equal("household", q.Scene!.Relationship);
        Assert.DoesNotContain("Ann", q.Scene.Summary);
    }

    [Fact]
    public void EveryTargetedCatalogKindHasAVerb()
    {
        IEnumerable<ActKind> kinds = ActCatalog.Returns.Concat(ActCatalog.Company).Concat(ActCatalog.Welcome)
            .Concat(ActCatalog.Repair).Concat(ActCatalog.Sides).Concat(DefaultTown.Acts());
        foreach (ActKind kind in kinds.Where(k => k.Affect is { Target: TargetIs.Chosen, Patient: Patient.Target }))
            Assert.True(ReflectionText.MemoryVerb(kind.Name) != "interacted with", $"{kind.Name} has no memory verb");
        Assert.Equal("stood up for", ReflectionText.MemoryVerb("StoodUpFor"));
        Assert.Equal("interacted with", ReflectionText.MemoryVerb("SomethingNew"));
    }

    [Fact]
    public void WordsForTimeRelationshipAndReasonsAreStable()
    {
        Assert.Equal("morning", ReflectionText.TimeOfDay(Clock.At(9)));
        Assert.Equal("afternoon", ReflectionText.TimeOfDay(Day + Clock.At(14)));
        Assert.Equal("evening", ReflectionText.TimeOfDay(Clock.At(20)));
        Assert.Equal("night", ReflectionText.TimeOfDay(Clock.At(23)));
        Assert.Equal("stranger", ReflectionText.Relationship(0.1, false, 0.15));
        Assert.Equal("acquaintance", ReflectionText.Relationship(0.2, false, 0.15));
        Assert.Equal("close", ReflectionText.Relationship(0.9, false, 0.15));
        Assert.Equal("household", ReflectionText.Relationship(0.0, true, 0.15));
        Assert.Equal("today", ReflectionText.Age(0));
        Assert.Equal("3 days ago", ReflectionText.Age(3));
        Assert.Equal("Square", ReflectionText.PlaceWords("Square", "SamHouse", "Jas", "Ranch"));
        Assert.Equal("home", ReflectionText.PlaceWords("Home:SamHouse", "SamHouse", "Jas", "Ranch"));
        Assert.Equal("Jas's home", ReflectionText.PlaceWords("Home:Ranch", "SamHouse", "Jas", "Ranch"));
        Assert.Equal("the Saloon home", ReflectionText.PlaceWords("Home:Saloon", "SamHouse", "Jas", "Ranch"));
        Assert.Equal("the Saloon home", ReflectionText.PlaceWords("Home:Saloon", "SamHouse", "Nobody", null));
        Assert.Null(ReflectionText.ReasonPhrase("direct-involvement"));
        Assert.Equal("after a recent hurt between you", ReflectionText.ReasonPhrase("kindness-after-hurt"));
        Assert.Equal("brand-new-code", ReflectionText.ReasonPhrase("brand-new-code"));
    }

    private static ReflectionScene Scene() => new("Saloon", 2, "Home", "evening", "familiar", 2, 1, "unkind yesterday",
        new[] { "after a recent hurt between you" });

    private static ReflectionRequest Request(ReflectionScene? scene, string? thought = null, int lineLength = 60) => new(
        "scene-packet", 1800, "Penny", "Robin", 7,
        "At d1 12:00, you remember Robin gave a gift to you (GaveGift); confidence 1.00.",
        "Penny, age 25: mood 0.10; regard for Robin 0.30. Bold 0.20; understanding 0.80; sensitive 0.80; self-regard 0.40; expression 0.30. Quiet free time. Choose for this person, not for a well-behaved town.",
        new[] { "gift", "help", "confront", "thank", "defer", "reject" }.Select(id => new ReflectionChoice(id,
            id switch { "gift" => "GaveGift", "help" => "HelpedSomeone", "confront" => "Argued", "thank" => "Thanked", _ => "" },
            new string('l', lineLength))).ToArray(),
        new ReflectionSourceFacts("GaveGift", false, 0.2, 0.3),
        Proposal: thought is null ? null : new ReflectionProposal("p", thought, "gift", new[] { "t" }), Scene: scene);

    [Fact]
    public void TapeRejectsADifferentOrMissingScene()
    {
        ReflectionRequest recorded = Request(Scene());
        var answer = new ReflectionAnswer("Hm.", "gift", recorded.Choices.ToDictionary(c => c.Id, _ => 1.0), "authored");
        var tape = new RecordedReflectionMind(new[] { new ReflectionRecord(recorded, answer, "gift", Array.Empty<ReflectionEvent>()) });
        Assert.Same(answer, tape.ReflectAsync(recorded with { Scene = Scene() }).Result);
        Assert.Throws<InvalidOperationException>(() => tape.ReflectAsync(recorded with { Scene = Scene() with { DaysAgo = 3 } }).Result);
        Assert.Throws<InvalidOperationException>(() => tape.ReflectAsync(recorded with { Scene = null }).Result);
        var older = new RecordedReflectionMind(new[] { new ReflectionRecord(recorded with { Scene = null }, answer, "gift", Array.Empty<ReflectionEvent>()) });
        Assert.Throws<InvalidOperationException>(() => older.ReflectAsync(recorded).Result);
        // A scene survives the recording's JSON round trip, summary included, and an old file without one still reads.
        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        ReflectionRequest back = JsonSerializer.Deserialize<ReflectionRequest>(JsonSerializer.Serialize(recorded, json), json)!;
        Assert.Equal(recorded.Scene!.Summary, back.Scene!.Summary);
        Assert.Null(JsonSerializer.Deserialize<ReflectionRequest>(JsonSerializer.Serialize(recorded with { Scene = null }, json), json)!.Scene);
    }

    [Fact]
    public async Task BothModelsGetTheSceneAndTheEvaluatorKeepsItsRequiredTextWhenItDoesNotFit()
    {
        using var handler = new Handler();
        using var mind = new ResilientReflectionMind(handler, new ReflectionMindOptions
        {
            LayaBaseUrl = "http://127.0.0.1:8000", GenerationBaseUrl = "http://127.0.0.1:8080", GenerationModel = "m",
            EvaluationMode = ReflectionEvaluationMode.Raw,
        });
        ReflectionAnswer answer = await mind.ReflectAsync(Request(Scene()));
        Assert.Equal("local-llm+laya", answer.Backend);
        using var generator = JsonDocument.Parse(answer.GenerationPrompt!);
        Assert.Contains("A small imagined detail is allowed only if it fits that place", generator.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        using var context = JsonDocument.Parse(generator.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        JsonElement scene = context.RootElement.GetProperty("scene");
        Assert.Equal("Saloon", scene.GetProperty("encounterPlace").GetString());
        Assert.Equal("2 days ago", scene.GetProperty("encounterAge").GetString());
        Assert.Equal("evening", scene.GetProperty("nowTimeOfDay").GetString());
        Assert.Equal(1, scene.GetProperty("unkindActsBetweenYouThisWeek").GetInt32());
        Assert.Equal("after a recent hurt between you", scene.GetProperty("whyItMattered")[0].GetString());
        using var packet = JsonDocument.Parse(answer.LayaPrompt!);
        string state = packet.RootElement.GetProperty("state").GetString()!;
        Assert.Contains("\nScene: 2 days ago at Saloon; now Home, evening; familiar; this week 2 kind/1 unkind, last unkind yesterday; after a recent hurt between you", state);
        Assert.Contains("Known memory: At d1 12:00", state);
        AssertBudget(packet);

        // Six long lines and a long thought leave no room: the scene line is left out whole, the
        // required context and memory remain, and nothing falls back.
        ReflectionAnswer tight = await mind.ReflectAsync(Request(Scene(), lineLength: 118));
        Assert.Equal("local-llm+laya", tight.Backend);
        using var tightPacket = JsonDocument.Parse(tight.LayaPrompt!);
        string tightState = tightPacket.RootElement.GetProperty("state").GetString()!;
        Assert.DoesNotContain("Scene:", tightState);
        Assert.Contains("Known memory: At d1 12:00, you remember Robin gave a gift to you (GaveGift); confidence 1.00.", tightState);
        Assert.Contains("Character context: Penny, age 25: mood 0.10; regard for Robin 0.30.", tightState);
        AssertBudget(tightPacket);

        // No scene on the request: nothing is invented for either model.
        ReflectionAnswer none = await mind.ReflectAsync(Request(null));
        using var noneGenerator = JsonDocument.Parse(none.GenerationPrompt!);
        using var noneContext = JsonDocument.Parse(noneGenerator.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(JsonValueKind.Null, noneContext.RootElement.GetProperty("scene").ValueKind);
        Assert.DoesNotContain("Scene:", JsonDocument.Parse(none.LayaPrompt!).RootElement.GetProperty("state").GetString());
    }

    private static void AssertBudget(JsonDocument packet)
    {
        JsonElement question = packet.RootElement.GetProperty("questions").GetProperty("q");
        int chars = packet.RootElement.GetProperty("state").GetString()!.Length + question.GetProperty("instructions").GetString()!.Length
            + question.GetProperty("criteria").EnumerateObject().Sum(c => c.Name.Length + c.Value.GetString()!.Length);
        Assert.InRange(chars, 1, ResilientReflectionMind.LayaTextLimit);
    }

    private sealed class Handler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var packet = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            string body;
            if (request.RequestUri!.AbsolutePath.EndsWith("chat/completions", StringComparison.Ordinal))
            {
                using var scene = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                string id = scene.RootElement.GetProperty("choices")[0].GetProperty("id").GetString()!;
                body = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(
                    new { thought = "Maybe I could bring Robin something small at the saloon.", suggestedChoice = id }) } } } });
            }
            else
            {
                var weights = packet.RootElement.GetProperty("questions").GetProperty("q").GetProperty("criteria")
                    .EnumerateObject().ToDictionary(c => c.Name, _ => 1.0);
                body = JsonSerializer.Serialize(new { answers = new { q = new { probabilities = weights } } });
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
