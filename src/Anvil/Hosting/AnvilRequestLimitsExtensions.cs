using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Anvil;

public static class AnvilRequestLimitsExtensions
{
    public static IApplicationBuilder UseAnvilBodyLimit(this IApplicationBuilder app, long maximumBytes)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        return app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > maximumBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            await next(context);
        });
    }
}
