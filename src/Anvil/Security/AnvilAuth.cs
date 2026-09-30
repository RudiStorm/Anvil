using System.Security.Claims;

namespace Anvil;

/// <summary>
/// Provides convenient access to the authenticated principal for the current request.
/// </summary>
public sealed class AnvilAuth(RequestContext request)
{
    private ClaimsPrincipal Principal => request.HttpContext.User;

    public ClaimsPrincipal User => Principal;

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public string? UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal.FindFirstValue("sub");

    public string? Username => Principal.Identity?.Name
        ?? Principal.FindFirstValue(ClaimTypes.Name)
        ?? Principal.FindFirstValue("preferred_username");

    public string? Email => Principal.FindFirstValue(ClaimTypes.Email)
        ?? Principal.FindFirstValue("email");

    public IEnumerable<Claim> Claims => Principal.Claims;

    public bool HasRole(string role)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        return Principal.IsInRole(role);
    }

    public bool HasPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        return Principal.HasClaim("permission", permission);
    }

    public ClaimsPrincipal RequireAuthenticated()
    {
        return IsAuthenticated ? Principal : throw new AnvilUnauthorizedException();
    }

    public ClaimsPrincipal RequireRole(string role)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        RequireAuthenticated();
        return HasRole(role) ? Principal : throw new AnvilForbiddenException();
    }

    public ClaimsPrincipal RequirePermission(string permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        RequireAuthenticated();
        return HasPermission(permission) ? Principal : throw new AnvilForbiddenException();
    }
}
