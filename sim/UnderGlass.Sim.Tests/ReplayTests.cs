using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The run recorded for the viewer (Replay; sim/viewer): recording only reads, so the run is the
/// same run; the file is the same for the same seed; and what it says matches the run: where
/// everyone was at each tick, the acts, every log line, and regard at the end.
/// </summary>
public class ReplayTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void RecordingChangesNothing()
    {
        string json = Replay.Json(new ReplayOptions { Seed = 3, Days = 7 });
        string plain = Metrics.LogHash(new Simulation(3, feelings: DefaultTown.Feelings()).Run(7));
        Assert.Equal(plain, Parse(json).GetProperty("hash").GetString());
        Assert.Equal(json, Replay.Json(new ReplayOptions { Seed = 3, Days = 7 })); // the same file for the same seed
    }

    [Fact]
    public void PackedMovesRoundTrip()
    {
        var bytes = new List<byte>();
        Replay.Pack(bytes, 0, 3, 7, 2, true);
        Replay.Pack(bytes, 1, 3, 8, 2, false);
        Replay.Pack(bytes, 300, 127, 255, 0, true); // a long wait takes a two-byte varint
        Assert.Equal(new[] { (0, 3, 7, 2, true), (5, 3, 8, 2, false), (301 * 5, 127, 255, 0, true) }, Replay.Unpack(bytes.ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Replay.Pack(bytes, 1, 128, 0, 0, false));
    }

    /// <summary>Three days of seed 1: replaying each person's packed moves gives where the
    /// simulation put them at every tick, read live from a second run of the same seed.</summary>
    [Fact]
    public void TheMovesAreWhereEveryoneWas()
    {
        const int days = 3;
        JsonElement run = Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = days }));
        string[] names = run.GetProperty("names").EnumerateArray().Select(e => e.GetString()!).ToArray();
        string[] places = run.GetProperty("places").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray();
        var streams = run.GetProperty("moves").EnumerateArray().Select(e => Replay.Unpack(Convert.FromBase64String(e.GetString()!))).ToArray();
        Assert.Equal(names.Length, streams.Length);

        var cursor = new int[names.Length];
        int checkedTicks = 0;
        new Simulation(1, feelings: DefaultTown.Feelings()).Run(days, (m, s) =>
        {
            if (m % Clock.TickMinutes != 0)
                return;
            checkedTicks++;
            for (int i = 0; i < names.Length; i++)
            {
                var rows = streams[i];
                while (cursor[i] + 1 < rows.Count && rows[cursor[i] + 1].Tick <= m)
                    cursor[i]++;
                var row = rows[cursor[i]];
                var (place, at, asleep, _) = s.Where(names[i]);
                Assert.True(row.Tick <= m, $"{names[i]} has no row by minute {m}");
                Assert.Equal((place, at.X, at.Y, asleep), (places[row.Place], row.X, row.Y, row.Asleep));
            }
        });
        Assert.Equal(days * Clock.MinutesPerDay / Clock.TickMinutes, checkedTicks);
    }

    /// <summary>A week of seed 2 with a placed scandal: every act is there as the run had it,
    /// every log line is a belief, a telling or an event, regard on the last day is the run's
    /// regard at the end, and the gate's acts are flagged.</summary>
    [Fact]
    public void TheRecordMatchesTheRun()
    {
        const int days = 7;
        JsonElement run = Parse(Replay.Json(new ReplayOptions { Seed = 2, Days = days, Inject = true }));
        SimResult r = new Simulation(2, scheduled: new[] { Harness.ScandalFor(2, DefaultTown.Acts()) }, feelings: DefaultTown.Feelings()).Run(days);
        string[] names = run.GetProperty("names").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(r.Names, names);

        var acts = run.GetProperty("acts").EnumerateArray().ToArray();
        Assert.Equal(r.Acts.Count, acts.Length);
        string[] kinds = run.GetProperty("kinds").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray();
        for (int k = 0; k < acts.Length; k++)
        {
            int[] a = acts[k].EnumerateArray().Select(e => e.GetInt32()).ToArray();
            Act truth = r.Acts[k];
            Assert.Equal((truth.Id, truth.Tick, truth.Actor, truth.Kind), (a[0], a[1], names[a[2]], kinds[a[3]]));
            Assert.Equal(truth.Target, a[7] < 0 ? null : names[a[7]]);
            Assert.Equal(truth.Injected, (a[9] & 1) != 0);
            Assert.Equal(r.Pursued.Contains(truth.Id), (a[9] & 2) != 0);
        }
        Assert.Contains(acts, a => (a[9].GetInt32() & 1) != 0);

        int beliefs = run.GetProperty("beliefs").GetArrayLength(), tellings = run.GetProperty("tellings").GetArrayLength();
        Assert.Equal(r.Log.Count(l => l.Split(' ')[1] == "belief"), beliefs);
        Assert.Equal(r.Log.Count(l => l.Split(' ')[1] == "told"), tellings);
        Assert.Equal(r.Log.Count, beliefs + tellings + run.GetProperty("events").GetArrayLength());

        var regard = run.GetProperty("regard").EnumerateArray().ToArray();
        Assert.Equal(days, regard.Length);
        int[] lastDay = regard[^1].EnumerateArray().Select(e => e.GetInt32()).ToArray();
        int n = names.Length;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (i != j)
                    Assert.Equal((int)Math.Round(r.Regard[(names[i], names[j])] * 1000), lastDay[i * n + j]);
        Assert.Equal(days * 24, run.GetProperty("mood").GetArrayLength());
        Assert.Equal(r.Ties.Count, run.GetProperty("ties").GetArrayLength());
        Assert.Equal(r.LifeEvents.Count, run.GetProperty("life").GetArrayLength());
    }

    /// <summary>With feelings off there is no regard or mood to record, and the file says so
    /// with empty tables rather than failing.</summary>
    [Fact]
    public void FeelingsOffRecordsTheTownAlone()
    {
        JsonElement run = Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 1, Feelings = FeelingOptions.Off }));
        Assert.Equal(0, run.GetProperty("regard").GetArrayLength());
        Assert.Equal(0, run.GetProperty("mood").GetArrayLength());
        Assert.True(run.GetProperty("acts").GetArrayLength() > 0);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("stance").ValueKind);
    }
}
