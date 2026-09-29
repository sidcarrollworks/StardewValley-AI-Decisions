namespace NpcDecision;

/// <summary>
/// Wraps a decision client so a slow or failing call falls back to a deterministic answer
/// (uniform choice, mid-scale score, 0.5 yes/no) instead of stalling the sim thread. The
/// timeout is optional; the inner client is also expected to raise on its own timeout or
/// failure, which is caught the same way.
/// </summary>
public sealed class ResilientDecisionClient : IDecisionClient
{
    private readonly IDecisionClient _inner;
    private readonly TimeSpan? _timeout;

    public ResilientDecisionClient(IDecisionClient inner, TimeSpan? timeout = null)
    {
        _inner = inner;
        _timeout = timeout;
    }

    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => Call(() => _inner.Choose(options, context), () => FakeDecisionClient.Uniform(options));

    public double Score(string context, double min, double max)
        => Call(() => _inner.Score(context, min, max), () => (min + max) / 2.0);

    public double YesNo(string context, string proposition)
        => Call(() => _inner.YesNo(context, proposition), () => 0.5);

    private T Call<T>(Func<T> call, Func<T> fallback)
    {
        try
        {
            if (_timeout is null)
                return call();

            Task<T> task = Task.Run(call);
            return task.Wait(_timeout.Value) ? task.Result : fallback();
        }
        catch
        {
            return fallback();
        }
    }
}
