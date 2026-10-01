using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>The loopback server: serves the page and the state, read-only, loopback only.</summary>
public sealed class MindsServerTests
{
    private static MindsServer Started(out RingLog<DecisionCall> calls)
    {
        calls = new RingLog<DecisionCall>(10);
        var server = new MindsServer(calls, MindsSnapshot.Idle(1, "Fake"));
        Assert.True(server.TryStart(0, out string? error), error);
        return server;
    }

    private static HttpClient Client() => new() { Timeout = TimeSpan.FromSeconds(5) };

    [Fact]
    public async Task ServesTheViewerPage()
    {
        using MindsServer server = Started(out _);
        using HttpClient http = Client();

        HttpResponseMessage response = await http.GetAsync($"http://127.0.0.1:{server.Port}/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType!.ToString());
        string page = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>NPC Minds</title>", page);
        Assert.Contains("state.json", page);
    }

    [Fact]
    public async Task ServesTheLatestSnapshotAndCalls()
    {
        using MindsServer server = Started(out RingLog<DecisionCall> calls);
        server.Publish(MindsSnapshot.Idle(42, "Laya") with { Time = "2:00 pm" });
        calls.Add(seq => new DecisionCall(seq, DateTime.UtcNow, "ladder", "yesno", "Sam", "should Sam wave?",
            new[] { new CallAnswer("yes", double.NaN) }, 31.5, false, "ctx"));
        using HttpClient http = Client();

        string json = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/state.json?t=1");

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal(42, doc.RootElement.GetProperty("snapshot").GetProperty("seq").GetInt64());
        Assert.Equal("2:00 pm", doc.RootElement.GetProperty("snapshot").GetProperty("time").GetString());
        JsonElement call = doc.RootElement.GetProperty("calls")[0];
        Assert.Equal("Sam", call.GetProperty("npc").GetString());
        Assert.Equal("NaN", call.GetProperty("answers")[0].GetProperty("value").GetString()); // NaN must not break the JSON
    }

    [Fact]
    public void OnlyGetIsServed()
    {
        using MindsServer server = Started(out _);
        Assert.Equal(405, server.Route("POST /state.json HTTP/1.1\r\nHost: 127.0.0.1").Status);
        Assert.Equal(404, server.Route("GET /save HTTP/1.1\r\nHost: 127.0.0.1").Status);
        Assert.Equal(400, server.Route("garbage").Status);
        Assert.Equal(200, server.Route("GET /health HTTP/1.1").Status);
    }

    [Fact]
    public void AForeignHostIsRefused()
    {
        using MindsServer server = Started(out _);
        Assert.Equal(403, server.Route("GET /state.json HTTP/1.1\r\nHost: evil.example:8765").Status);
        Assert.Equal(200, server.Route("GET /state.json HTTP/1.1\r\nHost: localhost:8765").Status);
    }

    [Theory]
    [InlineData("127.0.0.1:8765", true)]
    [InlineData("localhost", true)]
    [InlineData("[::1]:8765", true)]
    [InlineData("127.0.0.1.evil.example", false)]
    [InlineData("example.com", false)]
    public void LoopbackHosts(string host, bool expected)
        => Assert.Equal(expected, MindsServer.IsLoopbackHost(host));

    [Fact]
    public void ABusyPortIsReportedNotThrown()
    {
        var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        try
        {
            int port = ((IPEndPoint)blocker.LocalEndpoint).Port;
            using var server = new MindsServer(new RingLog<DecisionCall>(1), MindsSnapshot.Idle(1, "Fake"));
            Assert.False(server.TryStart(port, out string? error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.False(server.Running);
        }
        finally
        {
            blocker.Stop();
        }
    }

    [Fact]
    public async Task AClientThatSendsNothingDoesNotStopTheServer()
    {
        using MindsServer server = Started(out _);
        using (var idle = new TcpClient())
        {
            await idle.ConnectAsync(IPAddress.Loopback, server.Port);
            // connect and hang up without a request
        }
        using HttpClient http = Client();
        Assert.Equal("ok", await http.GetStringAsync($"http://127.0.0.1:{server.Port}/health"));
    }

    [Fact]
    public void ThePageIsEmbedded()
    {
        string page = Encoding.UTF8.GetString(MindsServer.ViewerPage());
        Assert.Contains("NPC Minds", page);
        Assert.DoesNotContain("missing from NpcMinds.dll", page);
    }

    private static readonly byte[] FakePng = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

    private static MindsServer WithPortraits()
    {
        var server = new MindsServer(new RingLog<DecisionCall>(10), MindsSnapshot.Idle(1, "Fake"));
        server.PublishPortraits(new Dictionary<string, byte[]> { ["Abigail"] = FakePng, ["../evil"] = FakePng, ["Empty"] = Array.Empty<byte>() });
        return server;
    }

    [Fact]
    public void ServesAPublishedPortraitAsPng()
    {
        using MindsServer server = WithPortraits();
        (int status, string type, byte[] body) = server.Route("GET /portrait/Abigail.png HTTP/1.1\r\nHost: 127.0.0.1");
        Assert.Equal(200, status);
        Assert.Equal("image/png", type);
        Assert.Equal(FakePng, body);
        // Names match case-insensitively, like the game's own NPC names in the snapshot.
        Assert.Equal(200, server.Route("GET /portrait/abigail.png HTTP/1.1").Status);
    }

    [Fact]
    public void UnknownOrUnsafePortraitNamesAre404()
    {
        using MindsServer server = WithPortraits();
        Assert.Equal(404, server.Route("GET /portrait/Shane.png HTTP/1.1").Status);       // not published
        Assert.Equal(404, server.Route("GET /portrait/../evil.png HTTP/1.1").Status);     // never a path
        Assert.Equal(404, server.Route("GET /portrait/Empty.png HTTP/1.1").Status);       // empty bytes dropped
        Assert.Equal(404, server.Route("GET /portrait/Abigail.gif HTTP/1.1").Status);     // png only
        Assert.False(MindsServer.IsPortraitName("../evil"));
        Assert.True(MindsServer.IsPortraitName("Mister_Qi"));
    }
}
