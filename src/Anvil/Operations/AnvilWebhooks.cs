using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public sealed record AnvilWebhookDelivery(string Id, string OwnerId, Uri Endpoint, string Event, string Payload, int Attempts, DateTimeOffset? DeliveredAt, string? LastError);
public sealed class AnvilWebhookOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage", "webhooks");
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}

public static class AnvilWebhookSigner
{
    public static string Sign(string payload, string secret) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    public static bool Verify(string payload, string signature, string secret) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(payload, secret)), Encoding.UTF8.GetBytes(signature ?? ""));
}

public interface IAnvilWebhookStore
{
    Task<AnvilWebhookDelivery> AddAsync(string ownerId, Uri endpoint, string eventName, string payload, CancellationToken cancellationToken = default);
    Task<AnvilWebhookDelivery?> GetAsync(string ownerId, string id, CancellationToken cancellationToken = default);
    Task MarkAsync(string id, bool delivered, string? error, CancellationToken cancellationToken = default);
    Task ResetAsync(string ownerId, string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class InMemoryAnvilWebhookStore : IAnvilWebhookStore
{
    private readonly ConcurrentDictionary<string, AnvilWebhookDelivery> items = new();
    public Task<AnvilWebhookDelivery> AddAsync(string ownerId, Uri endpoint, string eventName, string payload, CancellationToken cancellationToken = default) { var item = new AnvilWebhookDelivery(Guid.NewGuid().ToString("N"), ownerId, endpoint, eventName, payload, 0, null, null); items[item.Id] = item; return Task.FromResult(item); }
    public Task<AnvilWebhookDelivery?> GetAsync(string ownerId, string id, CancellationToken cancellationToken = default) => Task.FromResult(items.TryGetValue(id, out var x) && x.OwnerId == ownerId ? x : null);
    public Task MarkAsync(string id, bool delivered, string? error, CancellationToken cancellationToken = default) { if (items.TryGetValue(id, out var x)) items[id] = x with { Attempts = x.Attempts + 1, DeliveredAt = delivered ? DateTimeOffset.UtcNow : null, LastError = error }; return Task.CompletedTask; }
    public Task ResetAsync(string ownerId, string id, CancellationToken cancellationToken = default) { if (items.TryGetValue(id, out var x) && x.OwnerId == ownerId) items[id] = x with { Attempts = 0, DeliveredAt = null, LastError = null }; return Task.CompletedTask; }
}

public sealed class FileAnvilWebhookStore(IOptions<AnvilWebhookOptions> options) : IAnvilWebhookStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string Root => options.Value.RootPath;
    public async Task<AnvilWebhookDelivery> AddAsync(string ownerId, Uri endpoint, string eventName, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("https" or "http")) throw new ArgumentException("A valid HTTP endpoint is required.", nameof(endpoint));
        var item = new AnvilWebhookDelivery(Guid.NewGuid().ToString("N"), ownerId, endpoint, eventName, payload, 0, null, null);
        await SaveAsync(item, cancellationToken); return item;
    }
    public async Task<AnvilWebhookDelivery?> GetAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    { var item = await ReadAsync(id, cancellationToken); return item?.OwnerId == ownerId ? item : null; }
    public async Task MarkAsync(string id, bool delivered, string? error, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken); try { var item = await ReadAsync(id, cancellationToken) ?? throw new KeyNotFoundException(id); await SaveAsync(item with { Attempts = item.Attempts + 1, DeliveredAt = delivered ? DateTimeOffset.UtcNow : item.DeliveredAt, LastError = error }, cancellationToken, true); } finally { gate.Release(); }
    }
    public async Task ResetAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    { await gate.WaitAsync(cancellationToken); try { var item = await GetAsync(ownerId, id, cancellationToken) ?? throw new KeyNotFoundException(id); await SaveAsync(item with { Attempts = 0, DeliveredAt = null, LastError = null }, cancellationToken, true); } finally { gate.Release(); } }
    private async Task<AnvilWebhookDelivery?> ReadAsync(string id, CancellationToken cancellationToken)
    { var path = Path.Combine(Root, id + ".json"); return File.Exists(path) ? JsonSerializer.Deserialize<AnvilWebhookDelivery>(await File.ReadAllTextAsync(path, cancellationToken)) : null; }
    private async Task SaveAsync(AnvilWebhookDelivery item, CancellationToken cancellationToken, bool lockHeld = false)
    { if (!lockHeld) await gate.WaitAsync(cancellationToken); try { Directory.CreateDirectory(Root); var path = Path.Combine(Root, item.Id + ".json"); var temporary = path + ".tmp"; await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(item), cancellationToken); File.Move(temporary, path, true); } finally { if (!lockHeld) gate.Release(); } }
}

public sealed class AnvilWebhookDispatcher(HttpClient client, IAnvilWebhookStore store, IOptions<AnvilWebhookOptions> options)
{
    public async Task<bool> DeliverAsync(string ownerId, string id, string secret, CancellationToken cancellationToken = default)
    {
        var delivery = await store.GetAsync(ownerId, id, cancellationToken) ?? throw new KeyNotFoundException(id);
        var maxAttempts = Math.Max(1, options.Value.MaxAttempts);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Endpoint) { Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Anvil-Event", delivery.Event); request.Headers.Add("X-Anvil-Signature", AnvilWebhookSigner.Sign(delivery.Payload, secret));
            try { using var response = await client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode(); await store.MarkAsync(id, true, null, cancellationToken); return true; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { await store.MarkAsync(id, false, ex.Message, cancellationToken); if (attempt + 1 < maxAttempts) await Task.Delay(options.Value.RetryDelay * (attempt + 1), cancellationToken); }
        }
        return false;
    }

    public async Task<bool> ReplayAsync(string ownerId, string id, string secret, CancellationToken cancellationToken = default)
    { await store.ResetAsync(ownerId, id, cancellationToken); return await DeliverAsync(ownerId, id, secret, cancellationToken); }
}

public static class AnvilWebhookServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilWebhooks(this IServiceCollection services, Action<AnvilWebhookOptions>? configure = null)
    { services.AddOptions<AnvilWebhookOptions>(); if (configure is not null) services.Configure(configure); return services.AddHttpClient<AnvilWebhookDispatcher>().Services.AddSingleton<IAnvilWebhookStore, FileAnvilWebhookStore>(); }
}
