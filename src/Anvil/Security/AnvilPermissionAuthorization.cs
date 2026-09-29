using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilPermissionAuthorization
{
    public static IServiceCollection AddAnvilAuthorization(
        this IServiceCollection services,
        Action<AuthorizationOptions>? configure = null)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AnvilAuthorizationPolicies.Authenticated,
                policy => policy.RequireAuthenticatedUser());
            configure?.Invoke(options);
        });
        return services;
    }

    public static AuthorizationPolicyBuilder RequirePermission(
        this AuthorizationPolicyBuilder policy, string permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        return policy.RequireClaim("permission", permission);
    }

    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        return builder.RequireAuthorization(policy => policy.RequirePermission(permission));
    }

    public static TBuilder RequireAnvilAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(AnvilAuthorizationPolicies.Authenticated);
}

public static class AnvilAuthorizationPolicies
{
    public const string Authenticated = "anvil.authenticated";
}
