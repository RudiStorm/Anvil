namespace Anvil;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

public static class AnvilAuthorizationExtensions
{
    public static ValueTask<AnvilSessionEntry?> GetCurrentSessionAsync(
        this RequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetApplicationService<AnvilSessionManager>().GetAsync(cancellationToken);
    }

    public static async ValueTask<AnvilSessionEntry> RequireAuthenticatedAsync(
        this RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var session = await context.GetCurrentSessionAsync(cancellationToken);
        if (session is not null)
            return session;

        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var now = DateTimeOffset.UtcNow;
            return new AnvilSessionEntry(
                context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.HttpContext.User.Identity.Name ?? string.Empty,
                now,
                now,
                DateTimeOffset.MaxValue,
                new Dictionary<string, string>());
        }

        throw new AnvilUnauthorizedException();
    }

    public static async ValueTask<AnvilSessionEntry> RequireRoleAsync(
        this RequestContext context,
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        var session = await context.RequireAuthenticatedAsync(cancellationToken);
        return context.HttpContext.User.IsInRole(role)
            || (session.Values.TryGetValue("role", out var currentRole)
                && string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase))
            ? session
            : throw new AnvilForbiddenException();
    }

    public static async ValueTask<AnvilSessionEntry> RequirePermissionAsync(
        this RequestContext context, string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        var session = await context.RequireAuthenticatedAsync(cancellationToken);
        var authorization = context.HttpContext.RequestServices.GetService(typeof(IAuthorizationService)) as IAuthorizationService;
        if (authorization is null)
            throw new InvalidOperationException("Authorization services are not registered.");

        if (!context.HttpContext.User.HasClaim("permission", permission))
            throw new AnvilForbiddenException();
        return session;
    }
}
