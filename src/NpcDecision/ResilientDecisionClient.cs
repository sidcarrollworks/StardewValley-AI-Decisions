namespace NpcDecision;

/// <summary>
/// Wraps a decision client so a slow or failing call falls back to a deterministic answer
/// (uniform choice, mid-scale score, 0.5 yes/no). With a timeout, the calling thread waits at
/// most that long per call, so callers must run this OFF the game thread (the mod plans on a
/// background task). The optional budget token caps a whole batch of calls: once it is
/// cancelled every remaining call falls back immediately without touching the inner client.
/// The inner client is expected to enforce and cancel its own timeout too (the Laya client does);
/// the wrapper's timeout is a backstop, and any exception is caught the same way.
/// <para>
/// Batching (docs/spec/laya.md, "Data model"): when the inner client implements
/// <see cref="IBatchDecisionClient"/> the whole batch is delegated as one call, sharing one
/// timeout/budget window, and a failure falls back for every question at once; ids the inner left
/// out are then filled with their per-question fallback. A plain inner is looped instead, with a
/// per-question call, timeout, budget check, and fallback. Every fallback answer counts once in
/// <see cref="Fallbacks"/>.
/// </para>
/// </summary>
public sealed class ResilientDecisionClient : IDecisionClient, IBatchDecisionClient
{
    private readonly IDecisionClient _inner;
    private readonly TimeSpan? _timeout;
    private readonly CancellationToken _budget;
    private readonly Func<bool>? _isDown;

    /// <param name="isDown">Optional health gate (docs/spec/laya.md, "Short-circuit when down"):
    /// when it returns true, every call falls back immediately without touching the inner client,
    /// so a dead server doesn't cost a full timeout per call.</param>
    public ResilientDecisionClient(IDecisionClient inner, TimeSpan? timeout = null, CancellationToken budget = default, Func<bool>? isDown = null)
    {
        _inner = inner;
        _timeout = timeout;
        _budget = budget;
        _isDown = isDown;
    }

    /// <summary>How many answers fell back so far (timeouts, failures, missing batch answers, or an exhausted budget).</summary>
    public int Fallbacks => _fallbacks;
    private int _fallbacks;

    /// <summary>Why the newest fallback happened, in plain words: the budget ran out, the model is
    /// marked down, no answer within the timeout, the inner client's error (its type and message,
    /// cut to <see cref="MaxReasonChars"/>), or a question the batch left out. Null until the first
    /// fallback. Read it right after a call on the same thread (the recorder does).</summary>
    public string? LastFallbackReason => Volatile.Read(ref _lastReason);
    private string? _lastReason;

    public const int MaxReasonChars = 200;

    /// <summary>"Type: message" of the innermost cause (a task's AggregateException unwrapped),
    /// cut to <see cref="MaxReasonChars"/>.</summary>
    public static string ReasonOf(Exception ex)
    {
        while (ex is AggregateException { InnerExceptions.Count: 1 } agg)
            ex = agg.InnerExceptions[0];
        string reason = $"{ex.GetType().Name}: {ex.Message}";
        return reason.Length > MaxReasonChars ? reason[..MaxReasonChars] : reason;
    }

    private void Because(string reason) => Volatile.Write(ref _lastReason, reason);

    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => Call(() => _inner.Choose(options, context), () => FakeDecisionClient.Uniform(options));

    public double Score(string context, double min, double max)
        => Call(() => _inner.Score(context, min, max), () => (min + max) / 2.0);

    public double YesNo(string context, string proposition)
        => Call(() => _inner.YesNo(context, proposition), () => 0.5);

    /// <summary>
    /// One answer per question, in the questions' order. Never throws on failure: gaps and errors
    /// become deterministic per-question fallbacks.
    /// </summary>
    public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
    {
        if (questions is null)
            throw new ArgumentNullException(nameof(questions));

        // An exhausted budget caps the whole batch: every question falls back, no inner call.
        if (_budget.IsCancellationRequested)
        {
            Because(BudgetReason);
            return AllFallbacks(questions);
        }

        if (_inner is IBatchDecisionClient batch)
        {
            // One call, one timeout/budget window for the whole batch.
            if (!TryCall(() => batch.Ask(state, questions), out IReadOnlyList<Answer> answers))
                return AllFallbacks(questions);

            // Rebuild in the questions' order; an id the inner skipped is filled per question.
            var byId = new Dictionary<string, Answer>();
            foreach (Answer answer in answers)
            {
                if (answer is not null)
                    byId[answer.Id] = answer;
            }

            var result = new List<Answer>(questions.Count);
            foreach (Question question in questions)
            {
                if (byId.TryGetValue(question.Id, out Answer? answer) && answer is not null)
                    result.Add(answer);
                else
                {
                    Because($"the model's answer left out question {question.Id}");
                    result.Add(FallbackAnswer(question));
                }
            }
            return result;
        }

        // A plain inner has no batch: loop, one call + timeout + budget + fallback per question.
        var looped = new List<Answer>(questions.Count);
        foreach (Question question in questions)
            looped.Add(AskOne(state, question));
        return looped;
    }

    /// <summary>One question through the wrapper's own single-question methods.</summary>
    private Answer AskOne(string state, Question question) => question switch
    {
        ChoiceQuestion choice => Answer.FromChoice(choice.Id, Choose(choice.Options, state)),
        ScoreQuestion score => Answer.FromScore(score.Id, Score(state, score.Min, score.Max)),
        YesNoQuestion yesNo => Answer.FromYesNo(yesNo.Id, YesNo(state, yesNo.Proposition)),
        _ => throw new ArgumentException($"Unknown question type {question.GetType().Name}.", nameof(question)),
    };

    private IReadOnlyList<Answer> AllFallbacks(IReadOnlyList<Question> questions)
    {
        var answers = new List<Answer>(questions.Count);
        foreach (Question question in questions)
            answers.Add(FallbackAnswer(question));
        return answers;
    }

    /// <summary>The deterministic fallback for one question; counts as one fallback answer.</summary>
    private Answer FallbackAnswer(Question question)
    {
        Interlocked.Increment(ref _fallbacks);
        return FallbackValue(question);
    }

    private static Answer FallbackValue(Question question) => question switch
    {
        ChoiceQuestion choice => Answer.FromChoice(choice.Id, FakeDecisionClient.Uniform(choice.Options)),
        ScoreQuestion score => Answer.FromScore(score.Id, (score.Min + score.Max) / 2.0),
        YesNoQuestion yesNo => Answer.FromYesNo(yesNo.Id, 0.5),
        _ => throw new ArgumentException($"Unknown question type {question.GetType().Name}.", nameof(question)),
    };

    private const string BudgetReason = "the time budget ran out";

    private T Call<T>(Func<T> call, Func<T> fallback)
        => TryCall(call, out T result) ? result : Fallback(fallback);

    /// <summary>Runs one call under the timeout and budget; false (instead of throwing) on any failure.</summary>
    private bool TryCall<T>(Func<T> call, out T result)
    {
        if (_budget.IsCancellationRequested)
        {
            Because(BudgetReason);
            result = default!;
            return false;
        }
        if (_isDown?.Invoke() ?? false)
        {
            Because("skipped: the model is marked down");
            result = default!;
            return false;
        }

        try
        {
            if (_timeout is null)
            {
                result = call();
                return true;
            }

            Task<T> task = Task.Run(call);
            // Wait for the call, the timeout, or the budget, whichever comes first.
            if (task.Wait((int)Math.Min(int.MaxValue, _timeout.Value.TotalMilliseconds), _budget))
            {
                result = task.Result;
                return true;
            }

            Because(_budget.IsCancellationRequested
                ? BudgetReason
                : $"no answer within {_timeout.Value.TotalMilliseconds:0} ms");
            result = default!;
            return false;
        }
        catch (Exception ex)
        {
            Because(_budget.IsCancellationRequested && ex is OperationCanceledException ? BudgetReason : ReasonOf(ex));
            result = default!;
            return false;
        }
    }

    private T Fallback<T>(Func<T> fallback)
    {
        Interlocked.Increment(ref _fallbacks);
        return fallback();
    }
}
