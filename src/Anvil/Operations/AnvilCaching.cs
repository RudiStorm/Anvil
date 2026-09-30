using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anvil;

public sealed class AnvilCacheOptions { public string Namespace { get; set; } = "anvil"; }

public sealed class AnvilCache(IDistributedCache cache, IOptions<AnvilCacheOptions> options)
{
    public Task<byte[]?> GetAsync(string tenantId, string key, CancellationToken cancellationToken = default) => cache.GetAsync(Key(tenantId, key), cancellationToken);
    public Task SetAsync(string tenantId, string key, byte[] value, DistributedCacheEntryOptions cacheOptions, CancellationToken cancellationToken = default) => cache.SetAsync(Key(tenantId, key), value, cacheOptions, cancellationToken);
    public Task RemoveAsync(string tenantId, string key, CancellationToken cancellationToken = default) => cache.RemoveAsync(Key(tenantId, key), cancellationToken);
    public async Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(scope, key, cancellationToken);
        return value is null ? default : JsonSerializer.Deserialize<T>(value, JsonOptions);
    }

    public Task SetAsync<T>(string scope, string key, T value, DistributedCacheEntryOptions cacheOptions, CancellationToken cancellationToken = default)
        => SetAsync(scope, key, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), cacheOptions, cancellationToken);

    public async Task<T> GetOrCreateAsync<T>(string scope, string key, Func<CancellationToken, Task<T>> factory, DistributedCacheEntryOptions cacheOptions, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(scope, key, cancellationToken);
        if (cached is not null)
            return cached;
        var value = await factory(cancellationToken);
        await SetAsync(scope, key, value, cacheOptions, cancellationToken);
        return value;
    }

    private string Key(string tenantId, string key) { ArgumentException.ThrowIfNullOrWhiteSpace(tenantId); ArgumentException.ThrowIfNullOrWhiteSpace(key); return $"{options.Value.Namespace}:{tenantId}:{key}"; }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>Small durable adapter for single-node deployments; replace with Redis or another IDistributedCache for a cluster.</summary>
public sealed class FileAnvilDistributedCache(IOptions<AnvilStorageOptions> storage) : IDistributedCache
{
    private string Root => Path.Combine(storage.Value.RootPath, "cache");
    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();
    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    { var path = Path.Combine(Root, Safe(key) + ".json"); if (!File.Exists(path)) return null; var entry = JsonSerializer.Deserialize<Entry>(await File.ReadAllTextAsync(path, cancellationToken)); if (entry is null || entry.ExpiresAt <= DateTimeOffset.UtcNow) { if (File.Exists(path)) File.Delete(path); return null; } return Convert.FromBase64String(entry.Value); }
    public void Refresh(string key) { }
    public Task RefreshAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) { var path = Path.Combine(Root, Safe(key) + ".json"); if (File.Exists(path)) File.Delete(path); }
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { Remove(key); return Task.CompletedTask; }
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).GetAwaiter().GetResult();
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    { Directory.CreateDirectory(Root); var expires = options.AbsoluteExpiration ?? DateTimeOffset.UtcNow.Add(options.AbsoluteExpirationRelativeToNow ?? options.SlidingExpiration ?? TimeSpan.FromHours(1)); var path = Path.Combine(Root, Safe(key) + ".json"); var temporary = path + ".tmp"; await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Entry(Convert.ToBase64String(value), expires)), cancellationToken); File.Move(temporary, path, true); }
    private static string Safe(string key) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
    private sealed record Entry(string Value, DateTimeOffset ExpiresAt);
}

public static class AnvilCachingServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilCaching(this IServiceCollection services, Action<AnvilCacheOptions>? configure = null)
    {
        services.AddOptions<AnvilCacheOptions>();
        services.AddOptions<AnvilStorageOptions>();
        if (configure is not null) services.Configure(configure);
        services.TryAddSingleton<IDistributedCache, FileAnvilDistributedCache>();
        services.TryAddSingleton<AnvilCache>();
        return services;
    }
}
