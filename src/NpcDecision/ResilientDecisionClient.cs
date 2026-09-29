namespace NpcDecision;

/// <summary>
/// Wraps a decision client so a slow or failing call falls back to a deterministic answer
/// (uniform choice, mid-scale score, 0.5 yes/no). With a timeout, the calling thread waits at
/// most that long per call, so callers must run this OFF the game thread (the mod plans on a
/// background task). The optional budget token caps a whole batch of calls: once it is
/// cancelled every remaining call falls back immediately without touching the inner client.
/// The inner client is expected to enforce and cancel its own timeout too (the Laya client does);
/// the wrapper's timeout is a backstop, and any exception is caught the same way.
/// </summary>
public sealed class ResilientDecisionClient : IDecisionClient
{
    private readonly IDecisionClient _inner;
    private readonly TimeSpan? _timeout;
    private readonly CancellationToken _budget;

    public ResilientDecisionClient(IDecisionClient inner, TimeSpan? timeout = null, CancellationToken budget = default)
    {
        _inner = inner;
        _timeout = timeout;
        _budget = budget;
    }

    /// <summary>How many calls fell back so far (timeouts, failures, or an exhausted budget).</summary>
    public int Fallbacks => _fallbacks;
    private int _fallbacks;

    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => Call(() => _inner.Choose(options, context), () => FakeDecisionClient.Uniform(options));

    public double Score(string context, double min, double max)
        => Call(() => _inner.Score(context, min, max), () => (min + max) / 2.0);

    public double YesNo(string context, string proposition)
        => Call(() => _inner.YesNo(context, proposition), () => 0.5);

    private T Call<T>(Func<T> call, Func<T> fallback)
    {
        if (_budget.IsCancellationRequested)
            return Fallback(fallback);

        try
        {
            if (_timeout is null)
                return call();

            Task<T> task = Task.Run(call);
            // Wait for the call, the timeout, or the budget, whichever comes first.
            return task.Wait((int)Math.Min(int.MaxValue, _timeout.Value.TotalMilliseconds), _budget)
                ? task.Result
                : Fallback(fallback);
        }
        catch
        {
            return Fallback(fallback);
        }
    }

    private T Fallback<T>(Func<T> fallback)
    {
        Interlocked.Increment(ref _fallbacks);
        return fallback();
    }
}
