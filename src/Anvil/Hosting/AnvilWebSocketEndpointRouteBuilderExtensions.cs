using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilWebSocketEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapAnvilWebSocket(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, AnvilWebSocketConnection, Task> handler,
        AnvilWebSocketOptions? options = null)
    {
        options ??= new AnvilWebSocketOptions();
        return endpoints.MapGet(pattern, async (RequestContext context) =>
        {
            if (!context.HttpContext.WebSockets.IsWebSocketRequest)
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            if (options.RequireAuthentication)
            {
                await context.RequireAuthenticatedAsync(context.RequestAborted);
            }

            using var socket = await context.HttpContext.WebSockets.AcceptWebSocketAsync(
                new WebSocketAcceptContext { SubProtocol = options.SubProtocol });
            await handler(context, new AnvilWebSocketConnection(socket, options));
            return Results.Empty;
        });
    }
}
