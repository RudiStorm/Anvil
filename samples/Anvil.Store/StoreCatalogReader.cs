using Anvil;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Anvil.Store;

public sealed record StoreCatalogItem(
    int Id,
    string Slug,
    string Name,
    PackageType PackageType,
    string Summary,
    string License,
    string SupportedAnvilVersions,
    string Tags);

public sealed class StoreCatalogReader(StoreDbContext db, AnvilCache cache)
{
    public Task<IReadOnlyList<StoreCatalogItem>> GetPublishedAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        var key = $"catalog:{packageType.ToString().ToLowerInvariant()}";
        return cache.GetOrCreateAsync(
            "public",
            key,
            async token => (IReadOnlyList<StoreCatalogItem>)await db.Listings
                .AsNoTracking()
                .Where(x => x.Status == ListingStatus.Published && x.PackageType == packageType)
                .OrderByDescending(x => x.Id)
                .Select(x => new StoreCatalogItem(x.Id, x.Slug, x.Name, x.PackageType, x.Summary, x.License, x.SupportedAnvilVersions, x.Tags))
                .ToListAsync(token),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken);
    }
}
