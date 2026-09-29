using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace Anvil;

public static class AnvilProductionExtensions
{
    public static IServiceCollection AddAnvilProduction(
        this IServiceCollection services,
        Action<AnvilProductionOptions>? configure = null)
    {
        var options = new AnvilProductionOptions();
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrEmpty(options.DataProtectionKeysPath);
        ArgumentException.ThrowIfNullOrEmpty(options.LivenessPath);
        ArgumentException.ThrowIfNullOrEmpty(options.ReadinessPath);

        services.AddOptions<AnvilProductionOptions>().Configure(configure ?? (_ => { }));
        services.AddDataProtection().PersistKeysToFileSystem(
            new DirectoryInfo(Path.GetFullPath(options.DataProtectionKeysPath)));
        services.AddHealthChecks();
        return services;
    }

    public static IApplicationBuilder UseAnvilProduction(
        this IApplicationBuilder app,
        Action<AnvilProductionOptions>? configure = null)
    {
        var options = new AnvilProductionOptions();
        configure?.Invoke(options);
        var configured = app.ApplicationServices.GetService<IOptions<AnvilProductionOptions>>()?.Value;
        if (configured is not null && configure is null) options = configured;

        if (options.UseForwardedHeaders && options.TrustedProxyAddresses.Count > 0)
        {
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            foreach (var address in options.TrustedProxyAddresses)
            {
                if (IPAddress.TryParse(address, out var ip)) forwarded.KnownProxies.Add(ip);
            }
            app.UseForwardedHeaders(forwarded);
        }

        if (options.UseHsts) app.UseHsts();
        if (options.UseHttpsRedirection) app.UseHttpsRedirection();

        return app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'; form-action 'self'");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            await next(context);
        });
    }

    public static IEndpointRouteBuilder MapAnvilHealthChecks(
        this IEndpointRouteBuilder endpoints,
        Action<AnvilProductionOptions>? configure = null)
    {
        var options = new AnvilProductionOptions();
        configure?.Invoke(options);
        var configured = endpoints.ServiceProvider.GetService<IOptions<AnvilProductionOptions>>()?.Value;
        if (configured is not null && configure is null) options = configured;

        endpoints.MapHealthChecks(options.LivenessPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        });
        endpoints.MapHealthChecks(options.ReadinessPath);
        return endpoints;
    }
}
