using System.Net;
using System.Text;
using Anvil;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil.Tests;

public sealed class OperationsTests
{
    [Fact]
    public async Task Local_storage_enforces_size_and_owner_boundary()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-storage", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalAnvilStorage(Options.Create(new AnvilStorageOptions { RootPath = root, MaxBytes = 3 }));
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync("tenant", "x.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes("four"))));
            var file = await storage.SaveAsync("tenant", "x.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes("ok")));
            await using var ownedRead = await storage.OpenReadAsync("tenant", file.Id);
            Assert.NotNull(ownedRead);
            Assert.Null(await storage.OpenReadAsync("other", file.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Notification_store_paginates_and_limits_page_size()
    {
        var store = new InMemoryAnvilNotificationStore();
        await store.AddAsync("u", "one", "body"); await store.AddAsync("u", "two", "body");
        Assert.Single(await store.ListAsync("u", 2, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListAsync("u", 1, 101));
    }

    [Fact]
    public async Task File_notification_store_survives_a_new_store_and_enforces_owner()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-notifications", Guid.NewGuid().ToString("N"));
        try
        {
            var options = Options.Create(new AnvilStorageOptions { RootPath = root });
            var first = new FileAnvilNotificationStore(options);
            var created = await first.AddAsync("tenant-a/user-1", "title", "body");
            var second = new FileAnvilNotificationStore(options);
            Assert.Single(await second.ListAsync("tenant-a/user-1"));
            Assert.Empty(await second.ListAsync("tenant-b/user-1"));
            await second.MarkReadAsync("tenant-a/user-1", created.Id);
            Assert.True((await second.ListAsync("tenant-a/user-1"))[0].Read);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task File_transfer_store_recovers_status_after_restart()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-transfers", Guid.NewGuid().ToString("N"));
        try
        {
            var options = Options.Create(new AnvilStorageOptions { RootPath = root });
            var first = new FileAnvilTransferStore(options);
            var transfer = await first.StartAsync("tenant-a", "json");
            await first.CompleteAsync(transfer.Id, "result.json");
            var second = new FileAnvilTransferStore(options);
            Assert.Equal(AnvilTransferStatus.Completed, (await second.GetAsync("tenant-a", transfer.Id))!.Status);
            Assert.Null(await second.GetAsync("tenant-b", transfer.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Webhook_signatures_are_tamper_evident()
    {
        var signature = AnvilWebhookSigner.Sign("{}", "secret");
        Assert.True(AnvilWebhookSigner.Verify("{}", signature, "secret"));
        Assert.False(AnvilWebhookSigner.Verify("{x}", signature, "secret"));
    }

    [Fact]
    public async Task Cache_keys_are_namespaced_by_tenant()
    {
        var services = new ServiceCollection().AddAnvilCaching(o => o.Namespace = "test");
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<AnvilCache>();
        await cache.SetAsync("a", "key", [1], new DistributedCacheEntryOptions());
        Assert.Equal([1], await cache.GetAsync("a", "key"));
        Assert.Null(await cache.GetAsync("b", "key"));
    }

    [Fact]
    public async Task Typed_cache_round_trips_values_through_the_file_provider()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection()
                .AddAnvilCaching(options => options.Namespace = "typed")
                .Configure<AnvilStorageOptions>(options => options.RootPath = root);
            await using var provider = services.BuildServiceProvider();
            var cache = provider.GetRequiredService<AnvilCache>();
            await cache.SetAsync("public", "value", new CacheValue("hello", 3), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });

            Assert.Equal(new CacheValue("hello", 3), await cache.GetAsync<CacheValue>("public", "value"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed record CacheValue(string Text, int Count);
}
