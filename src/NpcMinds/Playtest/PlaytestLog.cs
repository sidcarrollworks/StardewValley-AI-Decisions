using System.Collections.Concurrent;

namespace NpcMinds.Playtest;

/// <summary>
/// The per-save, per-day JSON-lines playtest log (docs/spec/debug-tools.md, "Playtest log").
/// One file per in-game day under <c>playtest/&lt;save&gt;/&lt;year&gt;-&lt;season&gt;-&lt;day&gt;.jsonl</c>.
/// The game thread appends records to an in-memory buffer; the buffer is written (flushed) at
/// the 6:00 tick and on Saving, never during a tick. Model-call records arrive from the worker
/// thread through <see cref="QueueFromWorker"/> and are drained on the game thread, so nothing
/// here ever touches a file off the game thread (AGENTS.md rule 5).
///
/// Reads only (rule 1): every method is an append; nothing is ever read back into a decision.
/// A failed write logs once at Warn through the injected callback and never throws into the
/// game loop.
/// </summary>
public sealed class PlaytestLog : IDisposable
{
    private readonly string _root;             // <moddir>/playtest/<save>
    private bool _enabled;                     // starts as the setting; turns off after a failed write
    private readonly Action<string> _warn;     // the mod's Monitor.Log(..., LogLevel.Warn)
    private readonly List<PlaytestRecord> _pending = new();
    private readonly ConcurrentQueue<PlaytestRecord> _workerQueue = new();

    private string? _currentPath;
    private StreamWriter? _writer;
    private bool _failedOnce;

    public PlaytestLog(string rootDirectory, bool enabled, Action<string> warn)
    {
        _root = rootDirectory;
        _enabled = enabled;
        _warn = warn ?? (_ => { });
    }

    /// <summary>The setting (ModConfig.PlaytestLog). When false, every method is a cheap no-op
    /// and nothing is ever written.</summary>
    public bool Enabled => _enabled;

    /// <summary>Records queued from the worker but not yet drained (viewer/debug only).</summary>
    public int PendingFromWorker => _workerQueue.Count;

    /// <summary>
    /// Switch to the given in-game day's file, flushing and closing the previous one. Call on
    /// the 6:00 tick (already the new date) and at DayStarted after a load; same-day calls are
    /// no-ops.
    /// </summary>
    public void OpenDay(int year, string season, int day)
    {
        if (!_enabled)
            return;
        string path = Path.Combine(_root, $"{year}-{season}-{day}.jsonl");
        if (path == _currentPath)
            return;
        Flush();
        _writer?.Dispose();
        _writer = null;
        _currentPath = path;
    }

    /// <summary>Buffer one record; written at the next flush. Game thread only.</summary>
    public void Append(PlaytestRecord record)
    {
        if (_enabled)
            _pending.Add(record);
    }

    /// <summary>
    /// Buffer one record from the worker thread (model calls). Thread-safe; the file is only
    /// touched when <see cref="Flush"/> runs on the game thread.
    /// </summary>
    public void QueueFromWorker(PlaytestRecord record)
    {
        if (_enabled)
            _workerQueue.Enqueue(record);
    }

    /// <summary>Write everything buffered so far to the current day file. Game thread only;
    /// never throws.</summary>
    public void Flush()
    {
        if (!_enabled || _currentPath is null)
            return;
        while (_workerQueue.TryDequeue(out PlaytestRecord? queued))
            _pending.Add(queued);
        if (_pending.Count == 0)
            return;
        try
        {
            EnsureWriter();
            foreach (PlaytestRecord record in _pending)
                _writer!.WriteLine(PlaytestRecords.ToLine(record));
            _writer!.Flush();
            _pending.Clear();
        }
        catch (Exception ex)
        {
            _pending.Clear();
            if (_failedOnce)
                return;
            _failedOnce = true;
            _warn($"Playtest log write failed (logging is disabled for the rest of this session): {ex.Message}");
            _enabled = false; // stop trying; the failure is recorded once and the game is unaffected
        }
    }

    public void Dispose()
    {
        if (!_enabled)
            return;
        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // dispose must not throw into the game loop either
        }
        _writer = null;
    }

    private void EnsureWriter()
    {
        if (_writer is not null)
            return;
        Directory.CreateDirectory(_root);
        _writer = new StreamWriter(_currentPath!, append: true) { AutoFlush = false };
    }
}
