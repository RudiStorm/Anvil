using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public sealed record AnvilNotification(
    string Id, string UserId, string Title, string Body, DateTimeOffset CreatedAt, bool Read = false);

public interface IAnvilNotificationStore
{
    Task<AnvilNotification> AddAsync(string userId, string title, string body, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnvilNotification>> ListAsync(string userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task MarkReadAsync(string userId, string notificationId, CancellationToken cancellationToken = default);
    Task RetainAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAnvilNotificationStore : IAnvilNotificationStore
{
    private readonly ConcurrentDictionary<string, AnvilNotification> notifications = new();

    public Task<AnvilNotification> AddAsync(string userId, string title, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var item = new AnvilNotification(Guid.NewGuid().ToString("N"), userId, title, body, DateTimeOffset.UtcNow);
        notifications[item.Id] = item;
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<AnvilNotification>> ListAsync(string userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        IReadOnlyList<AnvilNotification> result = notifications.Values.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(result);
    }

    public Task MarkReadAsync(string userId, string notificationId, CancellationToken cancellationToken = default)
    {
        if (notifications.TryGetValue(notificationId, out var item) && item.UserId == userId)
            notifications[notificationId] = item with { Read = true };
        return Task.CompletedTask;
    }

    public Task RetainAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        foreach (var item in notifications.Values.Where(x => x.CreatedAt < before)) notifications.TryRemove(item.Id, out _);
        return Task.CompletedTask;
    }
}

/// <summary>A durable file adapter whose records can be migrated to an EF entity without changing the contract.</summary>
public sealed class FileAnvilNotificationStore(IOptions<AnvilStorageOptions> options) : IAnvilNotificationStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string Root => Path.Combine(options.Value.RootPath, "notifications");

    public async Task<AnvilNotification> AddAsync(string userId, string title, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var item = new AnvilNotification(Guid.NewGuid().ToString("N"), userId, title, body ?? string.Empty, DateTimeOffset.UtcNow);
        await SaveAsync(item, cancellationToken);
        return item;
    }

    public async Task<IReadOnlyList<AnvilNotification>> ListAsync(string userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var items = await ReadAllAsync(cancellationToken);
        return items.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
    }

    public async Task MarkReadAsync(string userId, string notificationId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var item = await ReadAsync(notificationId, cancellationToken);
            if (item is not null && item.UserId == userId && !item.Read)
                await SaveAsync(item with { Read = true }, cancellationToken, lockHeld: true);
        }
        finally { gate.Release(); }
    }

    public async Task RetainAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var item in await ReadAllAsync(cancellationToken))
                if (item.CreatedAt < before) TryDelete(item.Id);
        }
        finally { gate.Release(); }
    }

    private async Task SaveAsync(AnvilNotification item, CancellationToken cancellationToken, bool lockHeld = false)
    {
        if (!lockHeld) await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Root);
            var path = Path.Combine(Root, item.Id + ".json");
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(item), cancellationToken);
            File.Move(temporary, path, true);
        }
        finally { if (!lockHeld) gate.Release(); }
    }

    private async Task<AnvilNotification?> ReadAsync(string id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Root, id + ".json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<AnvilNotification>(await File.ReadAllTextAsync(path, cancellationToken));
    }

    private async Task<IReadOnlyList<AnvilNotification>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Root)) return [];
        var result = new List<AnvilNotification>();
        foreach (var path in Directory.EnumerateFiles(Root, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = JsonSerializer.Deserialize<AnvilNotification>(await File.ReadAllTextAsync(path, cancellationToken));
            if (item is not null) result.Add(item);
        }
        return result;
    }

    private void TryDelete(string id)
    {
        var path = Path.Combine(Root, id + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}

public static class AnvilNotificationServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilNotifications(this IServiceCollection services, Action<AnvilStorageOptions>? configure = null)
    {
        services.AddOptions<AnvilStorageOptions>();
        if (configure is not null) services.Configure(configure);
        return services.AddSingleton<IAnvilNotificationStore, FileAnvilNotificationStore>();
    }
}
