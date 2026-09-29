namespace Anvil;

public interface IAnvilSessionStore
{
    ValueTask<AnvilSessionEntry?> GetAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync(
        AnvilSessionEntry entry,
        CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);
}
