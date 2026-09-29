namespace Anvil;

public sealed class AnvilMailer(IAnvilMailTransport transport)
{
    public ValueTask SendAsync(AnvilMailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return transport.SendAsync(message, cancellationToken);
    }
}
