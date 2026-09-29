using System.Collections.Concurrent;

namespace Anvil;

public sealed class AnvilRequestMemoizer
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> values = new(StringComparer.Ordinal);

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);

        var fullKey = $"{typeof(T).AssemblyQualifiedName}:{key}";
        var lazy = values.GetOrAdd(
            fullKey,
            _ => new Lazy<Task<object?>>(
                async () => (object?)await factory(cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var result = await lazy.Value;
        return result is null ? default! : (T)result;
    }
}
