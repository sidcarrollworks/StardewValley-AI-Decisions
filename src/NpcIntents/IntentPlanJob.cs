namespace NpcIntents;

/// <summary>
/// Runs overnight planning on a background task so model calls never block the game thread,
/// including during the save. The work receives a budget token that is cancelled once
/// <c>budget</c> has elapsed; a <see cref="NpcDecision.ResilientDecisionClient"/> built on that
/// token answers every remaining call with its fallback, so the job finishes promptly even when
/// the model sidecar hangs. The game thread only ever polls <see cref="TryTake"/>.
/// </summary>
public sealed class IntentPlanJob : IDisposable
{
    private readonly Task<IntentPlan> _task;
    private readonly CancellationTokenSource _budget;
    private bool _taken;

    private IntentPlanJob(Task<IntentPlan> task, CancellationTokenSource budget)
    {
        _task = task;
        _budget = budget;
    }

    /// <summary>Start planning now. Returns immediately.</summary>
    public static IntentPlanJob Start(Func<CancellationToken, IntentPlan> work, TimeSpan budget)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(budget);
        CancellationToken token = cts.Token;
        Task<IntentPlan> task = Task.Run(() => work(token));
        return new IntentPlanJob(task, cts);
    }

    public bool IsCompleted => _task.IsCompleted;

    /// <summary>True once the budget ran out (the plan may then contain fallback decisions).</summary>
    public bool BudgetExhausted => _budget.IsCancellationRequested;

    /// <summary>
    /// Non-blocking. When the job has finished, hands over its plan once (an empty plan if the work
    /// threw, with the error) and returns true; otherwise returns false and the caller polls later.
    /// </summary>
    public bool TryTake(out IntentPlan plan, out Exception? error)
    {
        plan = new IntentPlan(Array.Empty<IntentCandidate>());
        error = null;
        if (_taken || !_task.IsCompleted)
            return false;

        _taken = true;
        if (_task.Status == TaskStatus.RanToCompletion)
            plan = _task.Result;
        else
            error = _task.Exception?.GetBaseException() ?? new OperationCanceledException("planning was cancelled");
        return true;
    }

    /// <summary>Stop waiting on the model: remaining calls fall back at once.</summary>
    public void Cancel() => _budget.Cancel();

    public void Dispose()
    {
        _budget.Cancel();
        _budget.Dispose();
    }
}
