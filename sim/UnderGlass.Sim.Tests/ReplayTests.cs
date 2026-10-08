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
        // The bytes themselves, as the viewer's own decoder reads them (varint low bits first, the
        // place with 128 for asleep, then x and y), so a change to Pack and Unpack together fails here.
        Assert.Equal(new byte[] { 0x00, 0x83, 0x07, 0x02, 0x01, 0x03, 0x08, 0x02, 0xAC, 0x02, 0xFF, 0xFF, 0x00 }, bytes.ToArray());
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
        Assert.Equal(r.Log.Count + r.MotiveLog.Count, beliefs + tellings + run.GetProperty("events").GetArrayLength());

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

        // Every person's character has one value for each trait the file names.
        int traits = run.GetProperty("traitNames").GetArrayLength();
        Assert.Equal(Enum.GetValues<Trait>().Length, traits);
        Assert.All(run.GetProperty("people").EnumerateArray(), p =>
        {
            Assert.Equal(traits, p.GetProperty("character").GetArrayLength());
            Assert.Equal(traits, p.GetProperty("characterEnd").GetArrayLength());
        });

        // Regard changes: between two people in "feelings", toward a kind of person in
        // "kindFeelings", and every moving change in one or the other.
        string[] personKinds = run.GetProperty("personKinds").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var feelings = run.GetProperty("feelings").EnumerateArray().Select(e => e.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
        var kindFeelings = run.GetProperty("kindFeelings").EnumerateArray().Select(e => e.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
        Assert.All(feelings, f => Assert.True(f[1] >= 0 && f[2] >= 0 && f[1] != f[2]));
        Assert.All(kindFeelings, f => Assert.InRange(f[2], 0, personKinds.Length - 1));
        Assert.Equal(r.Feelings.Count(f => f.Toward is not null && f.Change != 0), feelings.Length + kindFeelings.Length);
        Assert.Equal(r.Feelings.Count(f => f.Change != 0 && f.Toward is { } t && t.StartsWith("kind:")), kindFeelings.Length);
    }

    /// <summary>Each tie is stored at its minute: 23:59 of its day for a feud or a friendship,
    /// which are judged at night, and the moment of the act for a reconciliation.</summary>
    [Fact]
    public void TiesAreStoredAtTheirMinute()
    {
        const int days = 56;
        JsonElement run = Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = days }));
        SimResult r = new Simulation(1, feelings: DefaultTown.Feelings()).Run(days);
        string[] names = run.GetProperty("names").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var ties = run.GetProperty("ties").EnumerateArray().ToArray();
        Assert.Equal(r.Ties.Count, ties.Length);
        Assert.Contains(r.Ties, t => t.What == "reconciled");
        for (int k = 0; k < ties.Length; k++)
        {
            int tick = ties[k][0].GetInt32();
            var truth = r.Ties[k];
            Assert.Equal((truth.Day, truth.A, truth.B, truth.What), (Clock.Day(tick), names[ties[k][1].GetInt32()], names[ties[k][2].GetInt32()], ties[k][3].GetString()));
            if (truth.What == "reconciled")
                Assert.Contains(r.Log, l => l.StartsWith($"{tick} reconciled {truth.A} {truth.B} ", StringComparison.Ordinal));
            else
                Assert.Equal(Clock.MinutesPerDay - 1, Clock.OfDay(tick));
        }
    }

    /// <summary>With the gate only watching (DesireActs off), its lines stay out of the run's log
    /// but are recorded with the events, in time order, so the viewer can show them.</summary>
    [Fact]
    public void TheWatchingGatesLinesAreRecorded()
    {
        FeelingOptions watching = DefaultTown.Feelings();
        watching.DesireActs = false;
        JsonElement run = Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 2, Feelings = watching }));
        FeelingOptions again = DefaultTown.Feelings();
        again.DesireActs = false;
        SimResult r = new Simulation(1, feelings: again).Run(2);
        Assert.NotEmpty(r.MotiveLog);
        int[] ticks = run.GetProperty("events").EnumerateArray().Select(e => e[0].GetInt32()).ToArray();
        string[] texts = run.GetProperty("events").EnumerateArray().Select(e => e[1].GetString()!).ToArray();
        Assert.Equal(ticks.OrderBy(t => t), ticks);
        foreach (string line in r.MotiveLog)
            Assert.Contains(line[(line.IndexOf(' ') + 1)..], texts);
    }

    /// <summary>A setting that is not a finite number (a threshold of Infinity, to turn something
    /// off) is written as a string rather than failing after the whole run.</summary>
    [Fact]
    public void SettingsThatAreNotFiniteAreWritten()
    {
        FeelingOptions noFriends = DefaultTown.Feelings();
        noFriends.FriendAt = double.PositiveInfinity;
        JsonElement run = Parse(Replay.Json(new ReplayOptions { Seed = 1, Days = 1, Feelings = noFriends }));
        Assert.Equal("Infinity", run.GetProperty("settings").GetProperty("FriendAt").GetString());
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
