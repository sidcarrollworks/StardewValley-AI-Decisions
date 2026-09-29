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
    private readonly CancellationTokenSource _budget = new();
    private Task<IntentPlan> _task = null!;
    private volatile bool _budgetHitBeforeDone;
    private bool _taken;

    private IntentPlanJob()
    {
    }

    /// <summary>Start planning now. Returns immediately.</summary>
    public static IntentPlanJob Start(Func<CancellationToken, IntentPlan> work, TimeSpan budget)
    {
        var job = new IntentPlanJob();
        CancellationToken token = job._budget.Token;
        job._budget.CancelAfter(budget);
        job._task = Task.Run(() =>
        {
            try
            {
                return work(token);
            }
            finally
            {
                // Recorded when the work ends, so a plan collected long after it finished is not
                // reported as cut short just because the budget time has since passed.
                job._budgetHitBeforeDone = token.IsCancellationRequested;
            }
        });
        return job;
    }

    public bool IsCompleted => _task.IsCompleted;

    /// <summary>True when the budget ran out before the work finished (the plan may then contain
    /// fallback decisions). False while the work is still running.</summary>
    public bool BudgetExhausted => _task.IsCompleted && _budgetHitBeforeDone;

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

    /// <summary>Cancels the budget (so a still-running plan finishes on fallbacks) and releases it.</summary>
    public void Dispose()
    {
        _budget.Cancel();
        _budget.Dispose();
    }
}
