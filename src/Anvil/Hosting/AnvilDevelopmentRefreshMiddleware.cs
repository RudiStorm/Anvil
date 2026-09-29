using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Anvil;

internal sealed class AnvilDevelopmentRefreshMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    private static readonly string InstanceId = Guid.NewGuid().ToString("N");

    public async Task InvokeAsync(HttpContext context)
    {
        if (environment.IsDevelopment() && context.Request.Path == "/__anvil/reload")
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { instance = InstanceId }, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
