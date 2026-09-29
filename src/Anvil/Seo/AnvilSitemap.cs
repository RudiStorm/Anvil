using System.Security;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public sealed record AnvilSitemapEntry(
    string Location,
    DateTimeOffset? LastModified = null,
    string? ChangeFrequency = null,
    decimal? Priority = null);

public static class AnvilSitemapExtensions
{
    public static RouteHandlerBuilder MapAnvilSitemap(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, Task<IReadOnlyList<AnvilSitemapEntry>>> entries)
    {
        return endpoints.MapGet(pattern, async (RequestContext context) =>
        {
            var xml = await BuildXmlAsync(entries(context));
            return Results.Content(xml, "application/xml; charset=utf-8");
        });
    }

    private static async Task<string> BuildXmlAsync(Task<IReadOnlyList<AnvilSitemapEntry>> entriesTask)
    {
        var entries = await entriesTask;
        var builder = new System.Text.StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var entry in entries)
        {
            builder.Append("<url><loc>").Append(SecurityElement.Escape(entry.Location)).Append("</loc>");
            if (entry.LastModified is { } modified) builder.Append("<lastmod>").Append(modified.ToString("O", CultureInfo.InvariantCulture)).Append("</lastmod>");
            if (entry.ChangeFrequency is not null) builder.Append("<changefreq>").Append(SecurityElement.Escape(entry.ChangeFrequency)).Append("</changefreq>");
            if (entry.Priority is { } priority) builder.Append("<priority>").Append(priority.ToString("0.0", CultureInfo.InvariantCulture)).Append("</priority>");
            builder.Append("</url>");
        }
        return builder.Append("</urlset>").ToString();
    }
}
