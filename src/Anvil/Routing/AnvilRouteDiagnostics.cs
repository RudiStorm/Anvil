using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilRouteDiagnostics
{
    public static IApplicationBuilder UseAnvilTrailingSlashNormalization(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value is { Length: > 1 } path
                && path.EndsWith('/')
                && HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.Redirect(path.TrimEnd('/') + context.Request.QueryString, permanent: false);
                return;
            }
            await next(context);
        });
    }

    public static void ValidateAnvilRoutes(this IEndpointRouteBuilder endpoints)
    {
        var duplicates = endpoints.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.DefaultIfEmpty("*")
                .Select(method => (method, Pattern: endpoint.RoutePattern.RawText ?? endpoint.RoutePattern.RawText ?? string.Empty))
                ?? [("*", endpoint.RoutePattern.RawText ?? string.Empty)])
            .GroupBy(route => (route.method, route.Pattern))
            .Where(group => group.Count() > 1)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate Anvil routes: {string.Join(", ", duplicates.Select(group => $"{group.Key.method} {group.Key.Pattern}"))}");
        }
    }
}
