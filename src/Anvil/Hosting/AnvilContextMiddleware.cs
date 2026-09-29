using Microsoft.AspNetCore.Http;

namespace Anvil;

internal sealed class AnvilContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext httpContext, RequestContext requestContext)
    {
        requestContext.Initialize(httpContext);
        return next(httpContext);
    }
}
