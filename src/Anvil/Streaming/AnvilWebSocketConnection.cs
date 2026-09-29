using System.Net.WebSockets;
using System.Text;

namespace Anvil;

public sealed class AnvilWebSocketConnection(WebSocket socket, AnvilWebSocketOptions options)
{
    public WebSocket Socket => socket;

    public async ValueTask<string?> ReceiveTextAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[options.ReceiveBufferSize];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text)
                throw new InvalidOperationException("Only text WebSocket messages are supported by this helper.");
            if (message.Length + result.Count > options.MaxMessageBytes)
                throw new InvalidOperationException("The WebSocket message exceeds the configured size limit.");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.ToArray());
    }

    public Task SendTextAsync(string value, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > options.MaxMessageBytes)
            throw new InvalidOperationException("The WebSocket message exceeds the configured size limit.");
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public Task CloseAsync(
        WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        return socket.CloseAsync(status, description, cancellationToken);
    }
}
