using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anvil;

public interface ITenantOwned
{
    string TenantId { get; set; }
}

public sealed class TenantContext
{
    public string? TenantId { get; private set; }
    public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);

    public string RequireTenantId() =>
        TenantId ?? throw new InvalidOperationException("A tenant is required for this operation.");

    internal void Set(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("A tenant identifier is required.", nameof(tenantId));
        TenantId = tenantId.Trim();
    }
}

public static class TenantModelBuilderExtensions
{
    public static EntityTypeBuilder<TEntity> HasAnvilTenantFilter<TEntity>(
        this EntityTypeBuilder<TEntity> builder, TenantContext tenantContext)
        where TEntity : class, ITenantOwned
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        builder.Property(entity => entity.TenantId).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => entity.TenantId);
        builder.HasQueryFilter(entity => entity.TenantId == tenantContext.TenantId);
        return builder;
    }
}

public sealed class TenantSaveChangesInterceptor(TenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Validate(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Validate(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Validate(DbContext? context)
    {
        if (context is null)
            return;
        var tenantId = tenantContext.RequireTenantId();
        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.TenantId = tenantId;
            else if (!string.Equals(entry.Entity.TenantId, tenantId, StringComparison.Ordinal))
                throw new InvalidOperationException("An entity belongs to a different tenant.");
        }
    }
}
