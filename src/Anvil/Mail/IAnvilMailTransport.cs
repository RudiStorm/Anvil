namespace Anvil;

public interface IAnvilMailTransport
{
    ValueTask SendAsync(AnvilMailMessage message, CancellationToken cancellationToken = default);
}
