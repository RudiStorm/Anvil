namespace Anvil;

public sealed class AnvilWebSocketOptions
{
    public bool RequireAuthentication { get; set; }
    public string? SubProtocol { get; set; }
    public int ReceiveBufferSize { get; set; } = 4 * 1024;
    public int MaxMessageBytes { get; set; } = 1024 * 1024;
}
