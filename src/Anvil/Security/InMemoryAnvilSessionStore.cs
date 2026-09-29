using System.Collections.Concurrent;

namespace Anvil;

public sealed class InMemoryAnvilSessionStore : IAnvilSessionStore
{
    private readonly ConcurrentDictionary<string, AnvilSessionEntry> entries = new(StringComparer.Ordinal);

    public ValueTask<AnvilSessionEntry?> GetAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        entries.TryGetValue(tokenHash, out var entry);
        return ValueTask.FromResult(entry);
    }

    public ValueTask SetAsync(
        AnvilSessionEntry entry,
        CancellationToken cancellationToken = default)
    {
        entries[entry.TokenHash] = entry;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        entries.TryRemove(tokenHash, out _);
        return ValueTask.CompletedTask;
    }
}
