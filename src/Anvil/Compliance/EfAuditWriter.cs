using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

internal sealed class EfAuditWriter<TContext>(
    TContext dbContext,
    TenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor) : IAuditWriter
    where TContext : DbContext
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "secret", "credential", "access_token", "refresh_token"
    };

    public async Task WriteAsync(AuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequireTenantId();
        var httpContext = httpContextAccessor.HttpContext;
        var actorId = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext?.User.FindFirstValue("sub");
        var correlationId = httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault();

        dbContext.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = tenantId,
            ActorId = actorId,
            Resource = RequireValue(request.Resource, nameof(request.Resource)),
            ResourceId = request.ResourceId,
            Operation = RequireValue(request.Operation, nameof(request.Operation)),
            ChangedFields = SerializeSafeChanges(request.ChangedFields),
            TraceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier,
            CorrelationId = correlationId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? SerializeSafeChanges(IReadOnlyDictionary<string, object?>? changes)
    {
        if (changes is null)
            return null;
        var safe = changes
            .Where(pair => !SensitiveNames.Any(name => pair.Key.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return JsonSerializer.Serialize(safe);
    }

    private static string RequireValue(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value;
}

public static class AnvilAuditServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilAudit<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditWriter, EfAuditWriter<TContext>>();
        return services;
    }
}
