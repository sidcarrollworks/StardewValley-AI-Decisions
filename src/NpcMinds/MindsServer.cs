using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NpcMinds;

/// <summary>
/// A tiny read-only HTTP server on the loopback interface for the NPC Minds viewer
/// (docs/spec/debug-tools.md, "Live viewer"). It serves the viewer page at <c>/</c>, the
/// latest snapshot plus recent model calls at <c>/state.json</c>, and each villager's portrait at
/// <c>/portrait/&lt;Name&gt;.png</c> (PNG bytes the mod made from the player's own game content and
/// handed over with <see cref="PublishPortraits"/>); nothing else, and only GET.
/// <para>
/// It runs on its own background thread and never touches the game: the game thread publishes
/// an immutable <see cref="MindsSnapshot"/> by swapping one reference, and serialization happens
/// here, on request. A raw <see cref="TcpListener"/> bound to 127.0.0.1 is used instead of
/// HttpListener so no URL reservation or admin rights are needed on Windows. Requests whose Host
/// header is not a loopback name are refused, so a web page can't read the snapshot through a
/// rebound DNS name. Every failure is caught: a broken request only fails that request.
/// </para>
/// </summary>
public sealed class MindsServer : IDisposable
{
    public const string PageResource = "NpcMinds.viewer.html";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = false,
    };

    private readonly RingLog<DecisionCall> _calls;
    private readonly byte[] _page;
    private volatile MindsSnapshot _snapshot;
    private volatile IReadOnlyDictionary<string, byte[]> _portraits =
        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    private TcpListener? _listener;
    private Thread? _thread;
    private volatile bool _stopping;

    public MindsServer(RingLog<DecisionCall> calls, MindsSnapshot initial, byte[]? page = null)
    {
        _calls = calls ?? throw new ArgumentNullException(nameof(calls));
        _snapshot = initial ?? throw new ArgumentNullException(nameof(initial));
        _page = page ?? ViewerPage();
    }

    /// <summary>The port actually bound (useful with port 0 in tests); 0 until started.</summary>
    public int Port { get; private set; }

    public bool Running => _listener is not null && !_stopping;

    /// <summary>The snapshot the next request will see. Called from the game thread.</summary>
    public void Publish(MindsSnapshot snapshot)
    {
        if (snapshot is not null)
            _snapshot = snapshot;
    }

    public MindsSnapshot Current => _snapshot;

    /// <summary>Replaces the portrait PNGs (villager name -> bytes). Called from the game thread
    /// after a save loads; the dictionary is copied, so the caller may reuse its own.</summary>
    public void PublishPortraits(IReadOnlyDictionary<string, byte[]> portraits)
    {
        if (portraits is null)
            return;
        _portraits = new Dictionary<string, byte[]>(
            portraits.Where(kv => IsPortraitName(kv.Key) && kv.Value is { Length: > 0 })
                     .ToDictionary(kv => kv.Key, kv => kv.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Portrait names are plain villager names: letters, digits and underscores only, so a
    /// request path can never reach anything else.</summary>
    public static bool IsPortraitName(string name)
        => !string.IsNullOrEmpty(name) && name.Length <= 64 && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>Binds 127.0.0.1:<paramref name="port"/> and starts serving. False (with the reason)
    /// if the port can't be bound, for example because another program already uses it.</summary>
    public bool TryStart(int port, out string? error)
    {
        error = null;
        if (_listener is not null)
            return true;
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _listener = null;
            return false;
        }

        _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "NpcMinds viewer" };
        _thread.Start();
        return true;
    }

    public void Dispose()
    {
        _stopping = true;
        try
        {
            _listener?.Stop();
        }
        catch
        {
            // already stopped
        }
        _listener = null;
    }

    /// <summary>The JSON body of <c>/state.json</c>: the snapshot plus the newest model calls.</summary>
    public string StateJson()
        => JsonSerializer.Serialize(new StateBody(_snapshot, _calls.Newest()), Json);

    private sealed record StateBody(MindsSnapshot Snapshot, IReadOnlyList<DecisionCall> Calls);

    /// <summary>The viewer page embedded in this assembly.</summary>
    public static byte[] ViewerPage()
    {
        using Stream? stream = typeof(MindsServer).Assembly.GetManifestResourceStream(PageResource);
        if (stream is null)
            return Encoding.UTF8.GetBytes("<!doctype html><title>NPC Minds</title><p>The viewer page is missing from NpcMinds.dll.</p>");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private void AcceptLoop()
    {
        while (!_stopping)
        {
            TcpClient client;
            try
            {
                TcpListener? listener = _listener;
                if (listener is null)
                    return;
                client = listener.AcceptTcpClient();
            }
            catch
            {
                if (_stopping)
                    return;
                continue; // a failed accept: keep listening
            }
            ThreadPool.QueueUserWorkItem(_ => Handle(client));
        }
    }

    private void Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 2000;
                client.SendTimeout = 2000;
                NetworkStream stream = client.GetStream();
                string? head = ReadHead(stream);
                if (head is null)
                    return;
                (int status, string type, byte[] body) = Route(head);
                Write(stream, status, type, body);
            }
            catch
            {
                // one bad request never affects the server or the game
            }
        }
    }

    /// <summary>Decides the response for a request head (request line and headers). Public for tests.</summary>
    public (int Status, string ContentType, byte[] Body) Route(string head)
    {
        string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
        string[] request = lines[0].Split(' ');
        if (request.Length < 2)
            return Text(400, "bad request");
        if (!string.Equals(request[0], "GET", StringComparison.Ordinal))
            return Text(405, "the viewer is read-only: GET only");

        string? host = lines.Skip(1)
            .Where(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Substring(5).Trim())
            .FirstOrDefault();
        if (host is not null && !IsLoopbackHost(host))
            return Text(403, "loopback only");

        string path = request[1];
        int query = path.IndexOf('?');
        if (query >= 0)
            path = path.Substring(0, query);

        try
        {
            if (path.StartsWith("/portrait/", StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.Ordinal))
            {
                string name = path.Substring("/portrait/".Length, path.Length - "/portrait/".Length - ".png".Length);
                return IsPortraitName(name) && _portraits.TryGetValue(name, out byte[]? png)
                    ? (200, "image/png", png)
                    : Text(404, "no portrait");
            }
            return path switch
            {
                "/" or "/index.html" => (200, "text/html; charset=utf-8", _page),
                "/state.json" => (200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(StateJson())),
                "/health" => Text(200, "ok"),
                _ => Text(404, "not found"),
            };
        }
        catch (Exception ex)
        {
            return Text(500, "snapshot failed: " + ex.GetType().Name);
        }
    }

    /// <summary>127.0.0.1, localhost or [::1], with or without a port.</summary>
    public static bool IsLoopbackHost(string host)
    {
        string name = host.Trim();
        if (name.StartsWith("[", StringComparison.Ordinal))
        {
            int close = name.IndexOf(']');
            name = close > 0 ? name.Substring(1, close - 1) : name;
        }
        else
        {
            int colon = name.IndexOf(':');
            if (colon >= 0)
                name = name.Substring(0, colon);
        }
        return name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name == "127.0.0.1"
            || name == "::1";
    }

    private static (int, string, byte[]) Text(int status, string message)
        => (status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(message));

    /// <summary>Reads up to the blank line that ends the headers (8 KB at most; the body, if any,
    /// is ignored because only GET is served).</summary>
    private static string? ReadHead(NetworkStream stream)
    {
        var buffer = new byte[8192];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = stream.Read(buffer, length, buffer.Length - length);
            if (read <= 0)
                break;
            length += read;
            string sofar = Encoding.ASCII.GetString(buffer, 0, length);
            int end = sofar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end >= 0)
                return sofar.Substring(0, end);
        }
        return length == 0 ? null : Encoding.ASCII.GetString(buffer, 0, length);
    }

    private static void Write(NetworkStream stream, int status, string type, byte[] body)
    {
        string reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            _ => "Internal Server Error",
        };
        string header = $"HTTP/1.1 {status} {reason}\r\n" +
                        $"Content-Type: {type}\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Cache-Control: no-store\r\n" +
                        "X-Content-Type-Options: nosniff\r\n" +
                        "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }
}
