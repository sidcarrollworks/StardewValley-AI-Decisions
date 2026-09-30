using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using NpcDecision;
using Xunit;

namespace NpcDecision.Tests;

/// <summary>
/// Batching (docs/spec/laya.md, "Batch questions"): Laya sends one request for N questions and
/// matches answers by id; the fake and the resilient wrapper loop (and the wrapper fills gaps).
/// </summary>
public class BatchTests
{
    // ---- FakeDecisionClient: loops over the questions ----

    [Fact]
    public void Fake_AnswersEveryQuestion_InOrder_WithIdsPreserved()
    {
        var fake = new FakeDecisionClient();
        Question[] questions =
        {
            new ChoiceQuestion("ch", new[] { "a", "b", "c" }),
            new YesNoQuestion("yn", "is it sunny?"),
            new ScoreQuestion("sc", 1, 5),
        };

        var answers = fake.Ask("state", questions);

        Assert.Equal(new[] { "ch", "yn", "sc" }, answers.Select(a => a.Id));

        // An Answer carries exactly one field, matching its question type.
        Assert.Equal(new[] { 1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0 }, answers[0].Probabilities!);
        Assert.Null(answers[0].Score);
        Assert.Null(answers[0].YesNo);

        Assert.Equal(0.5, answers[1].YesNo!.Value, 10);
        Assert.Null(answers[1].Probabilities);

        Assert.Equal(3.0, answers[2].Score!.Value, 10);
        Assert.Null(answers[2].YesNo);

        Assert.Empty(fake.Ask("state", Array.Empty<Question>()));
    }

    // ---- ResilientDecisionClient ----

    [Fact]
    public void Resilient_PlainInner_LoopsPerQuestion_SoAThrowingInnerFallsBackPerId()
    {
        var inner = new SingleOnlyClient(throwing: true);
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5));
        Question[] questions =
        {
            new ChoiceQuestion("ch", new[] { "a", "b" }),
            new YesNoQuestion("yn", "p"),
            new ScoreQuestion("sc", 0, 4),
        };

        var answers = resilient.Ask("state", questions);

        Assert.Equal(new[] { "ch", "yn", "sc" }, answers.Select(a => a.Id));
        Assert.Equal(new[] { 0.5, 0.5 }, answers[0].Probabilities!); // uniform fallback
        Assert.Equal(0.5, answers[1].YesNo!.Value, 10);               // yes/no fallback
        Assert.Equal(2.0, answers[2].Score!.Value, 10);               // mid-scale fallback
        Assert.Equal(3, inner.TotalCalls);                            // one call per question
        Assert.Equal(3, resilient.Fallbacks);
    }

    [Fact]
    public void Resilient_BatchCapableInner_IsDelegatedExactlyOnce()
    {
        var inner = new BatchInner(_ => new List<Answer>
        {
            Answer.FromChoice("ch", new[] { 0.9, 0.1 }),
            Answer.FromYesNo("yn", 0.7),
            Answer.FromScore("sc", 42.0),
        });
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5));
        Question[] questions =
        {
            new ChoiceQuestion("ch", new[] { "a", "b" }),
            new YesNoQuestion("yn", "p"),
            new ScoreQuestion("sc", 0, 4),
        };

        var answers = resilient.Ask("state", questions);

        Assert.Equal(new[] { "ch", "yn", "sc" }, answers.Select(a => a.Id));
        Assert.Equal(new[] { 0.9, 0.1 }, answers[0].Probabilities!);
        Assert.Equal(0.7, answers[1].YesNo!.Value, 10);
        Assert.Equal(42.0, answers[2].Score!.Value, 10);
        Assert.Equal(1, inner.BatchCalls);
        Assert.Equal(0, inner.SingleCalls);
        Assert.Equal(0, resilient.Fallbacks);
    }

    [Fact]
    public void Resilient_BatchInnerSkippedAnId_FillsItPerQuestion()
    {
        var inner = new BatchInner(_ => new List<Answer>
        {
            Answer.FromChoice("a", new[] { 1.0 }),
            Answer.FromScore("c", 4.0),
        });
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5));
        Question[] questions =
        {
            new ChoiceQuestion("a", new[] { "x" }),
            new YesNoQuestion("b", "p"),
            new ScoreQuestion("c", 0, 4),
        };

        var answers = resilient.Ask("state", questions);

        Assert.Equal(new[] { "a", "b", "c" }, answers.Select(x => x.Id));
        Assert.Equal(0.5, answers[1].YesNo!.Value, 10);
        Assert.Null(answers[1].Probabilities);
        Assert.Null(answers[1].Score);
        Assert.Equal(4.0, answers[2].Score!.Value, 10);
        Assert.Equal(1, inner.BatchCalls);
        Assert.Equal(1, resilient.Fallbacks); // only the skipped id
    }

    [Fact]
    public void Resilient_BatchAnswersAreRebuiltInQuestionOrder_ExtrasDropped()
    {
        var inner = new BatchInner(_ => new List<Answer>
        {
            Answer.FromYesNo("b", 0.8),
            Answer.FromYesNo("a", 0.2),
            Answer.FromYesNo("zzz", 0.5),
        });
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5));

        var answers = resilient.Ask("state", new Question[]
        {
            new YesNoQuestion("a", "p1"),
            new YesNoQuestion("b", "p2"),
        });

        Assert.Equal(new[] { "a", "b" }, answers.Select(x => x.Id));
        Assert.Equal(0.2, answers[0].YesNo!.Value, 10);
        Assert.Equal(0.8, answers[1].YesNo!.Value, 10);
        Assert.Equal(0, resilient.Fallbacks);
    }

    [Fact]
    public void Resilient_ExhaustedBudget_ReturnsPerQuestionFallbacks_WithoutCallingTheInner()
    {
        var inner = new BatchInner(_ => throw new InvalidOperationException("must not be called"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5), cts.Token);
        Question[] questions =
        {
            new ChoiceQuestion("ch", new[] { "a", "b" }),
            new YesNoQuestion("yn", "p"),
            new ScoreQuestion("sc", 0, 4),
        };

        var answers = resilient.Ask("state", questions);

        Assert.Equal(new[] { "ch", "yn", "sc" }, answers.Select(a => a.Id));
        Assert.Equal(new[] { 0.5, 0.5 }, answers[0].Probabilities!);
        Assert.Equal(0.5, answers[1].YesNo!.Value, 10);
        Assert.Equal(2.0, answers[2].Score!.Value, 10);
        Assert.Equal(0, inner.BatchCalls);
        Assert.Equal(3, resilient.Fallbacks);
    }

    [Fact]
    public void Resilient_BatchCallFails_FallsBackForEveryQuestion_AfterOneAttempt()
    {
        var inner = new BatchInner(_ => throw new LayaException("down"));
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(5));
        Question[] questions =
        {
            new ChoiceQuestion("ch", new[] { "a", "b" }),
            new YesNoQuestion("yn", "p"),
            new ScoreQuestion("sc", 0, 4),
        };

        var answers = resilient.Ask("state", questions);

        Assert.Equal(new[] { "ch", "yn", "sc" }, answers.Select(a => a.Id));
        Assert.Equal(new[] { 0.5, 0.5 }, answers[0].Probabilities!);
        Assert.Equal(1, inner.BatchCalls); // one batch attempt, not one call per question
        Assert.Equal(0, inner.SingleCalls);
        Assert.Equal(3, resilient.Fallbacks);
    }

    [Fact]
    public void Resilient_BudgetCutsTheBatchWaitShort()
    {
        var inner = new BatchInner(_ =>
        {
            Thread.Sleep(5000);
            return new List<Answer>();
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var resilient = new ResilientDecisionClient(inner, TimeSpan.FromSeconds(30), cts.Token);

        var watch = Stopwatch.StartNew();
        var answers = resilient.Ask("state", new Question[]
        {
            new YesNoQuestion("a", "p"),
            new YesNoQuestion("b", "q"),
        });
        watch.Stop();

        Assert.Equal(new[] { "a", "b" }, answers.Select(x => x.Id));
        Assert.Equal(new[] { 0.5, 0.5 }, answers.Select(x => x.YesNo!.Value));
        Assert.True(watch.ElapsedMilliseconds < 3000, $"waited {watch.ElapsedMilliseconds} ms");
        Assert.Equal(1, inner.BatchCalls);
        Assert.Equal(2, resilient.Fallbacks);
    }

    // ---- LayaDecisionClient: one POST for the whole batch ----

    [Fact]
    public void Ask_PostsOneRequestWithEveryQuestion_AndReturnsAnswersInOrder()
    {
        // The server answers in a different order than the questions were asked.
        var stub = StubHandler.Json(AnswersBody(
            ("rate", ScoreAnswerBody(2.0)),
            ("which", ChoiceAnswerBody(("o0", 0.2), ("o1", 0.7), ("o2", 0.1))),
            ("upset", NoulAnswerBody(0.87)),
            ("speak", ChoiceAnswerBody(("o0", 0.9), ("o1", 0.1)))));
        using var client = Client(stub);

        Question[] questions =
        {
            new ChoiceQuestion("speak", new[] { "yes", "no" }),
            new ChoiceQuestion("which", new[] { "fishing", "foraging", "mining" }),
            new YesNoQuestion("upset", "Laya is upset with Bob"),
            new ScoreQuestion("rate", 1, 5),
        };

        var answers = client.Ask("Laya had a good morning", questions);

        // One POST, every question in the body in the caller's order, under the caller's ids.
        var req = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("http://127.0.0.1:8000/v1/systemone", req.Uri);
        Assert.StartsWith("application/json", req.ContentType);

        using var doc = JsonDocument.Parse(req.Body!);
        Assert.Equal("Laya had a good morning", doc.RootElement.GetProperty("state").GetString());
        Assert.Equal("typed-decisions", doc.RootElement.GetProperty("model").GetString());

        var sent = doc.RootElement.GetProperty("questions").EnumerateObject().ToArray();
        Assert.Equal(new[] { "speak", "which", "upset", "rate" }, sent.Select(q => q.Name));

        var speak = sent[0].Value;
        Assert.Equal("choice", speak.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(speak.GetProperty("instructions").GetString()));
        var speakCriteria = speak.GetProperty("criteria").EnumerateObject().ToArray();
        Assert.Equal(new[] { "o0", "o1" }, speakCriteria.Select(c => c.Name));
        Assert.Equal(new[] { "yes", "no" }, speakCriteria.Select(c => c.Value.GetString()));

        Assert.Equal(3, sent[1].Value.GetProperty("criteria").EnumerateObject().Count());

        var upset = sent[2].Value;
        Assert.Equal("noul", upset.GetProperty("type").GetString());
        Assert.Equal("Laya is upset with Bob", upset.GetProperty("instructions").GetString());
        Assert.False(upset.TryGetProperty("criteria", out _));

        var rate = sent[3].Value;
        Assert.Equal("score", rate.GetProperty("type").GetString());
        Assert.Equal(new[] { "very low", "low", "medium", "high", "very high" },
            rate.GetProperty("criteria").EnumerateArray().Select(e => e.GetString()));

        // Answers come back in the questions' order, matched by id and typed.
        Assert.Equal(new[] { "speak", "which", "upset", "rate" }, answers.Select(a => a.Id));
        Assert.Equal(new[] { 0.9, 0.1 }, answers[0].Probabilities!);
        Assert.Null(answers[0].Score);
        Assert.Null(answers[0].YesNo);
        Assert.Equal(new[] { 0.2, 0.7, 0.1 }, answers[1].Probabilities!);
        Assert.Equal(0.87, answers[2].YesNo!.Value, 10);
        Assert.Null(answers[2].Probabilities);
        Assert.Equal(3.0, answers[3].Score!.Value, 10); // score 2 of 0..4 mapped onto 1..5
        Assert.Null(answers[3].YesNo);
    }

    [Fact]
    public void Ask_MissingIdInResponse_IsAbsentFromTheResult()
    {
        var stub = StubHandler.Json(AnswersBody(
            ("a", NoulAnswerBody(0.8)),
            ("c", ScoreAnswerBody(1.0)),
            ("zzz", NoulAnswerBody(0.5)))); // never asked: ignored
        using var client = Client(stub);

        Question[] questions =
        {
            new YesNoQuestion("a", "p1"),
            new ChoiceQuestion("b", new[] { "x", "y" }),
            new ScoreQuestion("c", 0, 4),
        };

        var answers = client.Ask("state", questions);

        Assert.Equal(new[] { "a", "c" }, answers.Select(x => x.Id));
        Assert.Equal(0.8, answers[0].YesNo!.Value, 10);
        Assert.Equal(1.0, answers[1].Score!.Value, 10);
        Assert.Single(stub.Requests); // one request even with a gap in the response
    }

    [Fact]
    public void Ask_NonObjectAnswerForAnId_IsAbsent()
    {
        var stub = StubHandler.Json(AnswersBody(("a", "5")));
        using var client = Client(stub);

        Assert.Empty(client.Ask("state", new Question[] { new YesNoQuestion("a", "p") }));
    }

    [Fact]
    public void Ask_PresentAnswerWithoutItsField_IsAbsent()
    {
        // A malformed answer is skipped like a missing id, so one bad answer never sinks the
        // batch (review nit: per-question gap fill, not a batch-wide failure).
        var stub = StubHandler.Json(AnswersBody(("ch", "{\"type\":\"choice\",\"choice\":\"o0\"}")));
        using var client = Client(stub);

        Assert.Empty(client.Ask("state", new Question[] { new ChoiceQuestion("ch", new[] { "a", "b" }) }));
    }

    [Fact]
    public void Ask_DuplicateIds_Throw_NoRequest()
    {
        var stub = StubHandler.Json(AnswersBody(("dup", NoulAnswerBody(0.5))));
        using var client = Client(stub);
        Question[] questions = { new YesNoQuestion("dup", "a"), new ScoreQuestion("dup", 0, 1) };

        Assert.Throws<ArgumentException>(() => client.Ask("state", questions));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public void Ask_EmptyQuestions_ReturnEmptyWithoutARequest()
    {
        var stub = StubHandler.Json("{}");
        using var client = Client(stub);

        Assert.Empty(client.Ask("state", Array.Empty<Question>()));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public void Ask_ChoiceAbove100Options_ThrowsBeforeAnyRequest()
    {
        var stub = StubHandler.Json("{}");
        using var client = Client(stub);
        var options = Enumerable.Range(0, 101).Select(i => "opt" + i).ToArray();
        Question[] questions = { new YesNoQuestion("ok", "p"), new ChoiceQuestion("big", options) };

        Assert.Throws<ArgumentException>(() => client.Ask("state", questions));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public void Ask_BudgetTokenCancelsAnInFlightRequest()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        bool sawCancellation = false;
        var stub = new StubHandler(token =>
        {
            try
            {
                Task.Delay(TimeSpan.FromSeconds(30), token).Wait();
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                sawCancellation = true;
                token.ThrowIfCancellationRequested();
            }
            return Respond(HttpStatusCode.OK, AnswersBody(("a", NoulAnswerBody(0.5))));
        });
        using var client = new LayaDecisionClient(stub, new LayaOptions { Timeout = TimeSpan.FromSeconds(30) }, budget.Token);

        var watch = Stopwatch.StartNew();
        Assert.ThrowsAny<OperationCanceledException>(
            () => client.Ask("state", new Question[] { new YesNoQuestion("a", "p") }));
        watch.Stop();

        Assert.True(sawCancellation, "handler's token was not cancelled");
        Assert.True(watch.ElapsedMilliseconds < 3000, $"call took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ResilientOverLaya_FillsAnIdTheServerSkipped()
    {
        var stub = StubHandler.Json(AnswersBody(
            ("speak", ChoiceAnswerBody(("o0", 0.6), ("o1", 0.4))),
            ("rate", ScoreAnswerBody(4.0))));
        using var laya = Client(stub);
        var resilient = new ResilientDecisionClient(laya);

        Question[] questions =
        {
            new ChoiceQuestion("speak", new[] { "chat", "walk" }),
            new YesNoQuestion("upset", "Laya is upset"),
            new ScoreQuestion("rate", 0, 4),
        };

        var answers = resilient.Ask("state", questions);

        Assert.Equal(new[] { "speak", "upset", "rate" }, answers.Select(a => a.Id));
        Assert.Equal(new[] { 0.6, 0.4 }, answers[0].Probabilities!);
        Assert.Equal(0.5, answers[1].YesNo!.Value, 10); // filled by the wrapper
        Assert.Equal(4.0, answers[2].Score!.Value, 10);
        Assert.Single(stub.Requests);
        Assert.Equal(1, resilient.Fallbacks);
    }

    [Fact]
    public void ResilientOverLaya_BatchFailureFallsBackForAllQuestions()
    {
        using var laya = Client(new StubHandler(_ => Respond(HttpStatusCode.ServiceUnavailable, "{\"detail\":\"busy\"}")));
        var resilient = new ResilientDecisionClient(laya);

        var answers = resilient.Ask("state", new Question[]
        {
            new YesNoQuestion("a", "p"),
            new ChoiceQuestion("b", new[] { "x", "y" }),
        });

        Assert.Equal(new[] { "a", "b" }, answers.Select(x => x.Id));
        Assert.Equal(0.5, answers[0].YesNo!.Value, 10);
        Assert.Equal(new[] { 0.5, 0.5 }, answers[1].Probabilities!);
        Assert.Equal(2, resilient.Fallbacks);
    }

    // ---- helpers ----

    /// <summary>A plain inner (no batch): counts calls, optionally throws instead of answering.</summary>
    private sealed class SingleOnlyClient : IDecisionClient
    {
        private readonly bool _throwing;
        public SingleOnlyClient(bool throwing) => _throwing = throwing;

        public int ChooseCalls, ScoreCalls, YesNoCalls;
        public int TotalCalls => ChooseCalls + ScoreCalls + YesNoCalls;

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            ChooseCalls++;
            if (_throwing)
                throw new LayaException("inner is down");
            return options.Select(_ => 0.9).ToArray();
        }

        public double Score(string context, double min, double max)
        {
            ScoreCalls++;
            if (_throwing)
                throw new LayaException("inner is down");
            return max;
        }

        public double YesNo(string context, string proposition)
        {
            YesNoCalls++;
            if (_throwing)
                throw new LayaException("inner is down");
            return 1.0;
        }
    }

    /// <summary>A batch-capable inner: counts both kinds of call, answers through a responder.</summary>
    private sealed class BatchInner : IDecisionClient, IBatchDecisionClient
    {
        private readonly Func<IReadOnlyList<Question>, IReadOnlyList<Answer>> _respond;

        public BatchInner(Func<IReadOnlyList<Question>, IReadOnlyList<Answer>> respond) => _respond = respond;

        public int BatchCalls;
        public int SingleCalls;

        public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
        {
            Interlocked.Increment(ref BatchCalls);
            return _respond(questions);
        }

        public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        {
            Interlocked.Increment(ref SingleCalls);
            return options.Select(_ => 0.0).ToArray();
        }

        public double Score(string context, double min, double max)
        {
            Interlocked.Increment(ref SingleCalls);
            return min;
        }

        public double YesNo(string context, string proposition)
        {
            Interlocked.Increment(ref SingleCalls);
            return 0.0;
        }
    }

    private static LayaDecisionClient Client(StubHandler stub, LayaOptions? options = null)
        => new(stub, options ?? new LayaOptions());

    private static string Num(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string ChoiceAnswerBody(params (string Label, double P)[] probs)
    {
        string p = string.Join(",", probs.Select(x => $"\"{x.Label}\":{Num(x.P)}"));
        string top = probs.OrderByDescending(x => x.P).First().Label;
        return $"{{\"type\":\"choice\",\"choice\":\"{top}\",\"probabilities\":{{{p}}},\"confidence\":0.4,\"answer_confidence\":0.7}}";
    }

    private static string NoulAnswerBody(double noul)
        => $"{{\"type\":\"noul\",\"noul\":{Num(noul)},\"confidence\":0.9,\"answer_confidence\":0.9}}";

    private static string ScoreAnswerBody(double score)
        => $"{{\"type\":\"score\",\"score\":{Num(score)},\"legend\":{{\"0\":\"very low\"}},\"probabilities\":{{\"0\":0.2}}}}";

    private static string AnswersBody(params (string Id, string Body)[] answers)
        => "{\"model\":\"laya-rl-agent\",\"answers\":{"
           + string.Join(",", answers.Select(a => $"\"{a.Id}\":{a.Body}"))
           + "}}";

    private static HttpResponseMessage Respond(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record Captured(HttpMethod Method, string Uri, string? Authorization, string? ContentType, string? Body);

    /// <summary>Records requests; supports the synchronous Send path HttpClient.Send uses.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, HttpResponseMessage> _respond;
        public List<Captured> Requests { get; } = new();

        public StubHandler(Func<CancellationToken, HttpResponseMessage> respond) => _respond = respond;

        public static StubHandler Json(string body) => new(_ => Respond(HttpStatusCode.OK, body));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null
                ? null
                : new StreamReader(request.Content.ReadAsStream(cancellationToken), Encoding.UTF8).ReadToEnd();
            lock (Requests)
            {
                Requests.Add(new Captured(request.Method, request.RequestUri!.ToString(),
                    request.Headers.Authorization?.ToString(),
                    request.Content?.Headers.ContentType?.ToString(), body));
            }
            return _respond(cancellationToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}
