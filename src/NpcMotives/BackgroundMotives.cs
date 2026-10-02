using System.Collections.Concurrent;

namespace NpcMotives;

/// <summary>
/// Runs a <see cref="MotivesRunner"/> on a single background worker, the way
/// <c>BackgroundLadder</c> runs the ladder, so its model calls never block the game thread
/// (AGENTS.md rule 5). The game thread only enqueues work (a tick's copied inputs, or a talk with
/// the player) and drains finished events; it never touches the runner itself. When the model is
/// slow and more than <c>maxBacklog</c> ticks are waiting, new ticks are dropped and counted.
/// </summary>
public sealed class BackgroundMotives
{
    private readonly MotivesRunner _runner;
    private readonly int _maxBacklog;
    private readonly object _gate = new();
    private readonly ConcurrentQueue<IReadOnlyList<MotiveEvent>> _results = new();
    private Task _chain = Task.CompletedTask;
    private int _backlog;
    private int _dropped;
    private volatile string _latestJson;
    private volatile IReadOnlyDictionary<string, MotiveDecision> _latestDecisions;
    private volatile IReadOnlyList<MotiveNpcState> _latestStates;

    public BackgroundMotives(MotivesRunner runner, int maxBacklog = 6)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _maxBacklog = Math.Max(1, maxBacklog);
        _latestJson = runner.ToJson();
        _latestDecisions = runner.LatestDecisions();
        _latestStates = runner.States();
    }

    /// <summary>Ticks dropped because the worker was too far behind.</summary>
    public int Dropped => _dropped;

    /// <summary>Operations queued or running.</summary>
    public int Backlog => Volatile.Read(ref _backlog);

    /// <summary>The runner's state as of the last finished operation; safe from any thread (the
    /// save uses it, so saving never waits on the model).</summary>
    public string LatestJson => _latestJson;

    /// <summary>Each NPC's latest weighed decision, for the viewer; safe from any thread.</summary>
    public IReadOnlyDictionary<string, MotiveDecision> LatestDecisions => _latestDecisions;

    /// <summary>Each NPC's pacing state, for the viewer; safe from any thread.</summary>
    public IReadOnlyList<MotiveNpcState> LatestStates => _latestStates;

    /// <summary>Queue one tick. Returns false (and counts a drop) when the backlog is full.</summary>
    public bool EnqueueTick(int absoluteTick, IReadOnlyList<MotiveInputs> inputs)
    {
        if (Volatile.Read(ref _backlog) >= _maxBacklog)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }
        var copy = inputs.ToList();
        Enqueue(() => _runner.Tick(absoluteTick, copy));
        return true;
    }

    /// <summary>Queue a talk with the player (always accepted: cheap, never calls the model).</summary>
    public void EnqueueTalked(string npc, int absoluteTick)
        => Enqueue(() => _runner.NoteTalked(npc, absoluteTick));

    /// <summary>Everything finished since the last drain, in order. Never blocks.</summary>
    public IReadOnlyList<MotiveEvent> Drain()
    {
        var drained = new List<MotiveEvent>();
        while (_results.TryDequeue(out IReadOnlyList<MotiveEvent>? events))
            drained.AddRange(events);
        return drained;
    }

    /// <summary>Wait for queued work (tests and shutdown only; never call on the game thread).</summary>
    public bool WaitIdle(TimeSpan timeout)
    {
        Task chain;
        lock (_gate)
            chain = _chain;
        return chain.Wait(timeout);
    }

    private void Enqueue(Func<IReadOnlyList<MotiveEvent>> work)
    {
        Interlocked.Increment(ref _backlog);
        lock (_gate)
        {
            _chain = _chain.ContinueWith(_ =>
            {
                try
                {
                    IReadOnlyList<MotiveEvent> events = work();
                    if (events.Count > 0)
                        _results.Enqueue(events);
                    _latestJson = _runner.ToJson();
                    _latestDecisions = _runner.LatestDecisions();
                    _latestStates = _runner.States();
                }
                catch
                {
                    // A failed tick is skipped; the decision client already falls back on its own errors.
                }
                finally
                {
                    Interlocked.Decrement(ref _backlog);
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }
}
