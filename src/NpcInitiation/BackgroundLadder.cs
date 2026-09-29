using System.Collections.Concurrent;
using NpcMemory;

namespace NpcInitiation;

/// <summary>
/// Runs an <see cref="InitiationLadder"/> on a single background worker so its decision-client
/// calls never block the game thread. The game thread only enqueues work (a tick's inputs, or a
/// player response) and drains finished results; it never touches the ladder itself. Diary lines
/// the ladder writes are collected and handed back, so the caller applies them to the real
/// diaries on its own thread. If the model is slow and more than <c>maxBacklog</c> ticks are
/// waiting, new ticks are dropped (and counted) rather than queued without limit.
/// </summary>
public sealed class BackgroundLadder
{
    /// <summary>One finished result: ladder events plus the diary lines they produced.</summary>
    public sealed record Result(IReadOnlyList<InitiationEvent> Events, IReadOnlyList<(string Npc, DiaryEntry Entry)> DiaryLines);

    private readonly InitiationLadder _ladder;
    private readonly int _maxBacklog;
    private readonly object _gate = new();
    private readonly ConcurrentQueue<Result> _results = new();
    private Task _chain = Task.CompletedTask;
    private int _backlog;
    private int _dropped;
    private volatile string _latestJson;

    public BackgroundLadder(InitiationLadder ladder, int maxBacklog = 6)
    {
        _ladder = ladder;
        _maxBacklog = Math.Max(1, maxBacklog);
        _latestJson = ladder.ToJson();
    }

    /// <summary>Ticks dropped because the worker was too far behind.</summary>
    public int Dropped => _dropped;

    /// <summary>Operations queued or running.</summary>
    public int Backlog => Volatile.Read(ref _backlog);

    /// <summary>The ladder state as of the last finished operation; safe to read from any thread
    /// (the save uses it, so saving never waits on the model).</summary>
    public string LatestJson => _latestJson;

    /// <summary>Queue one tick. Returns false (and counts a drop) when the backlog is full.</summary>
    public bool EnqueueTick(int absoluteTick, IReadOnlyList<InitiationInput> inputs)
    {
        if (Volatile.Read(ref _backlog) >= _maxBacklog)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }

        var copy = inputs.ToList();
        Enqueue(() =>
        {
            var lines = new List<(string, DiaryEntry)>();
            var scratch = new Dictionary<string, Diary>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<InitiationEvent> events = _ladder.Tick(absoluteTick, copy, npc =>
            {
                if (!scratch.TryGetValue(npc, out Diary? diary))
                    scratch[npc] = diary = new Diary();
                return diary;
            });
            foreach ((string npc, Diary diary) in scratch)
                foreach (DiaryEntry entry in diary.Entries)
                    lines.Add((npc, entry));
            return new Result(events, lines);
        });
        return true;
    }

    /// <summary>Queue a player response (always accepted: it is cheap and never calls the model).</summary>
    public void EnqueueResponse(string npc, int absoluteTick)
        => Enqueue(() =>
        {
            InitiationEvent? responded = _ladder.NoteResponded(npc, absoluteTick);
            return new Result(responded is null ? Array.Empty<InitiationEvent>() : new[] { responded },
                Array.Empty<(string, DiaryEntry)>());
        });

    /// <summary>Everything finished since the last drain, in order. Never blocks.</summary>
    public IReadOnlyList<Result> Drain()
    {
        var drained = new List<Result>();
        while (_results.TryDequeue(out Result? result))
            drained.Add(result);
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

    private void Enqueue(Func<Result> work)
    {
        Interlocked.Increment(ref _backlog);
        lock (_gate)
        {
            _chain = _chain.ContinueWith(_ =>
            {
                try
                {
                    Result result = work();
                    if (result.Events.Count > 0 || result.DiaryLines.Count > 0)
                        _results.Enqueue(result);
                    _latestJson = _ladder.ToJson();
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
