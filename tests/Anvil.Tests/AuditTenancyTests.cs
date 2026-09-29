using System.Security.Claims;
using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class AuditTenancyTests
{
    [Fact]
    public async Task Ef_audit_writer_persists_context_and_omits_sensitive_fields()
    {
        var tenant = new TenantContext();
        tenant.Set("tenant-a");
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-1";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var writer = new EfAuditWriter<TestDbContext>(db, tenant, accessor);

        await writer.WriteAsync(new AuditRequest("customer", "update", "customer-1",
            new Dictionary<string, object?> { ["name"] = "Ada", ["password"] = "do-not-store" }));

        var entry = await db.AuditEntries.SingleAsync();
        Assert.Equal("tenant-a", entry.TenantId);
        Assert.Equal("user-1", entry.ActorId);
        Assert.Equal("corr-1", entry.CorrelationId);
        Assert.Contains("name", entry.ChangedFields);
        Assert.DoesNotContain("password", entry.ChangedFields);
        Assert.DoesNotContain("do-not-store", entry.ChangedFields);
    }

    [Fact]
    public async Task Tenant_interceptor_assigns_tenant_on_create_and_rejects_cross_tenant_updates()
    {
        var tenant = new TenantContext();
        tenant.Set("tenant-a");
        var interceptor = new TenantSaveChangesInterceptor(tenant);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new TestDbContext(options);
        var item = new TenantItem { Name = "owned by a" };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        Assert.Equal("tenant-a", item.TenantId);

        item.TenantId = "tenant-b";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
        public DbSet<TenantItem> Items => Set<TenantItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditEntry>().ConfigureAnvilAudit();
        }
    }

    private sealed class TenantItem : ITenantOwned
    {
        public int Id { get; set; }
        public string TenantId { get; set; } = null!;
        public string Name { get; set; } = null!;
    }
}
