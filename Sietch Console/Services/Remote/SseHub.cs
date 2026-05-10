using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Sietch_Console.Services.Remote;

/// <summary>
/// Thread-safe hub that delivers Server-Sent Events frames to connected web clients (#149).
/// Each connected client gets its own unbounded <see cref="Channel{T}"/>.
/// </summary>
public sealed class SseHub
{
    private readonly ConcurrentDictionary<string, Channel<string>> _clients = new();

    /// <summary>Registers a new client and returns its unique subscriber ID.</summary>
    public string Subscribe()
    {
        var id = Guid.NewGuid().ToString("N");
        _clients[id] = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true });
        return id;
    }

    /// <summary>Removes the client and completes its channel.</summary>
    public void Unsubscribe(string id)
    {
        if (_clients.TryRemove(id, out var ch))
            ch.Writer.TryComplete();
    }

    /// <summary>Returns the reader for a subscribed client, or null if not found.</summary>
    public ChannelReader<string>? GetReader(string id) =>
        _clients.TryGetValue(id, out var ch) ? ch.Reader : null;

    /// <summary>Number of currently connected SSE clients.</summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// Broadcasts an SSE event frame to every connected client.
    /// Uses the wire format: <c>event: {type}\ndata: {json}\n\n</c>.
    /// </summary>
    public void Broadcast(string eventType, string jsonData)
    {
        var frame = $"event: {eventType}\ndata: {jsonData}\n\n";
        foreach (var (_, ch) in _clients)
            ch.Writer.TryWrite(frame);
    }
}
