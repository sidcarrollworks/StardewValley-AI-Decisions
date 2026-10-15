using System.Diagnostics;
using System.Text.Json;
using UnderGlass.Sim;
using UnderGlass.Sim.Lives;

// All access stays on the replay worker. Files are observer output, never decision inputs.
internal sealed class LiveRecordingWriter : IDisposable
{
    private readonly string path;
    private readonly string? control;
    private readonly Stopwatch publish = Stopwatch.StartNew(), clock = Stopwatch.StartNew();
    private double previous, rate;
    private bool paused;
    private int at;
    private string lastPhase = "";
    private readonly Stopwatch statusClock = Stopwatch.StartNew(), controlClock = Stopwatch.StartNew();
    private Func<string>? snapshot;
    public LiveRecordingWriter(string path, string? control)
    { this.path = path; this.control = control; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); }
    public void Observe(int minute, Func<string> read)
    {
        at = minute; snapshot = read;
        do
        {
            ReadControl();
            State(paused ? "paused" : "running");
            if (paused) { if (publish.ElapsedMilliseconds >= 1000) Flush(); Thread.Sleep(100); }
        } while (paused);
        if (rate > 0)
        {
            double delay = 1000 / rate - (clock.Elapsed.TotalMilliseconds - previous);
            if (delay > 0) Thread.Sleep((int)Math.Ceiling(delay));
        }
        previous = clock.Elapsed.TotalMilliseconds;
        if (minute == 0 || publish.ElapsedMilliseconds >= 1000) Flush();
    }
    private void ReadControl()
    {
        if (at != 0 && controlClock.ElapsedMilliseconds < 100) return;
        controlClock.Restart();
        if (control is null || !File.Exists(control)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(control));
            paused = doc.RootElement.GetProperty("paused").GetBoolean();
            double value = doc.RootElement.GetProperty("rate").GetDouble();
            if (double.IsFinite(value) && (value == 0 || value is >= 1 and <= 1440)) rate = value;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { }
    }
    private void Flush() { if (snapshot is not null) Write(path, snapshot()); publish.Restart(); }
    public void State(string phase)
    {
        if (phase == lastPhase && statusClock.ElapsedMilliseconds < 1000) return;
        Write(path + ".status.json", JsonSerializer.Serialize(new { at, phase, paused, rate }));
        lastPhase = phase; statusClock.Restart();
    }
    public void Finish(string json) { Write(path, json); State("complete"); }
    private static void Write(string target, string json)
    {
        File.WriteAllText(target + ".tmp", json);
        File.Move(target + ".tmp", target, overwrite: true);
    }
    public IReflectionMind Wrap(IReflectionMind inner) => new Reflection(inner, this);
    public ILivesDecisionMind Wrap(ILivesDecisionMind inner) => new Decisions(inner, this);
    private sealed class Reflection(IReflectionMind inner, LiveRecordingWriter owner) : IReflectionMind
    {
        public async Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            owner.Flush(); owner.State("reflection");
            try { return await inner.ReflectAsync(request, cancellationToken).ConfigureAwait(false); }
            finally { owner.State("running"); }
        }
    }
    private sealed class Decisions(ILivesDecisionMind inner, LiveRecordingWriter owner) : ILivesDecisionMind
    {
        public async Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken = default)
        {
            owner.Flush(); owner.State("evening-choice");
            try { return await inner.EvaluateAsync(request, cancellationToken).ConfigureAwait(false); }
            finally { owner.State("running"); }
        }
    }
    public void Dispose() { }
}
