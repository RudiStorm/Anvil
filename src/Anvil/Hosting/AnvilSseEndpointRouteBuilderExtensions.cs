using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilSseEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapAnvilSse(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, IAsyncEnumerable<AnvilSseEvent>> stream)
    {
        return endpoints.MapAnvilSse(pattern, (context, _) => stream(context));
    }

    public static RouteHandlerBuilder MapAnvilSse(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, string?, IAsyncEnumerable<AnvilSseEvent>> stream)
    {
        return endpoints.MapGet(pattern, async (RequestContext context) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            await foreach (var item in stream(context, context.LastEventId).WithCancellation(context.RequestAborted))
            {
                await context.Response.WriteAsync(item.Format(), context.RequestAborted);
                await context.FlushAsync();
            }
        });
    }
}
