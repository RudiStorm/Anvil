using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Anvil;

public interface ITenantResolver
{
    ValueTask<string?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

public sealed class ClaimsTenantResolver(IHostEnvironment environment, IOptions<TenantOptions> options) : ITenantResolver
{
    public ValueTask<string?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var tenant = httpContext.User.FindFirst("tenant_id")?.Value
            ?? httpContext.User.FindFirst("tenant")?.Value;
        if (tenant is null && options.Value.AllowDevelopmentHeader && environment.IsDevelopment())
            tenant = httpContext.Request.Headers[options.Value.HeaderName].FirstOrDefault();
        return ValueTask.FromResult(tenant);
    }
}

public sealed class TenantOptions
{
    public bool Required { get; set; } = true;
    public bool AllowDevelopmentHeader { get; set; }
    public string HeaderName { get; set; } = "X-Tenant-Id";
}

internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext, ITenantResolver resolver, IOptions<TenantOptions> options)
    {
        var tenantId = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        if (tenantId is not null)
            tenantContext.Set(tenantId);
        else if (options.Value.Required)
            throw new BadHttpRequestException("A tenant could not be resolved.");
        await next(httpContext);
    }
}

public static class AnvilTenancyExtensions
{
    public static IServiceCollection AddAnvilTenancy(
        this IServiceCollection services, Action<TenantOptions>? configure = null)
    {
        services.AddOptions<TenantOptions>();
        if (configure is not null)
            services.Configure(configure);
        services.AddScoped<TenantContext>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddScoped<ITenantResolver, ClaimsTenantResolver>();
        return services;
    }

    public static IApplicationBuilder UseAnvilTenancy(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
