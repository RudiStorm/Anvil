using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public static class AnvilLinkExtensions
{
    public static string Link(
        this RequestContext context,
        string routeTemplate,
        object? routeValues = null,
        object? queryValues = null,
        string? fragment = null)
    {
        return AnvilLink.Build(routeTemplate, routeValues, queryValues, fragment);
    }

    public static string? RouteLink(
        this RequestContext context,
        string routeName,
        object? routeValues = null,
        object? queryValues = null,
        string? fragment = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(routeName);

        var generator = context.HttpContext.RequestServices.GetRequiredService<LinkGenerator>();
        var path = generator.GetPathByName(context.HttpContext, routeName, routeValues);
        return path is null ? null : AnvilLink.Build(path, queryValues: queryValues, fragment: fragment);
    }

    public static bool IsCurrent(this RequestContext context, string path)
    {
        return AnvilLink.IsCurrent(context.HttpContext, path);
    }

    public static string FormAction(
        this RequestContext context,
        string routeTemplate,
        object? routeValues = null,
        object? queryValues = null,
        string? fragment = null)
    {
        return context.Link(routeTemplate, routeValues, queryValues, fragment);
    }

    public static string AbsoluteLink(this RequestContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<AnvilRoutingOptions>>()
            .Value;
        var baseUrl = options.BaseUrl ?? new Uri(
            $"{context.Request.Scheme}://{context.Request.Host.Value}");
        if (!baseUrl.IsAbsoluteUri || baseUrl.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Anvil routing BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        return new Uri(baseUrl, path.StartsWith('/') ? path[1..] : path).ToString();
    }
}
