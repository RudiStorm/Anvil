using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Anvil;

public static class AnvilErrorHandlingExtensions
{
    public static IApplicationBuilder UseAnvilErrors(this IApplicationBuilder app)
        => app.UseAnvilErrors(new AnvilErrorOptions());

    public static IApplicationBuilder UseAnvilErrors(this IApplicationBuilder app, AnvilErrorOptions options)
    {
        app.UseExceptionHandler(exceptionApp =>
            exceptionApp.Run(context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var statusCode = exception switch
                {
                    AnvilUnauthorizedException => StatusCodes.Status401Unauthorized,
                    AnvilForbiddenException => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status500InternalServerError
                };
                return WriteErrorAsync(context, statusCode, options);
            }));

        app.UseStatusCodePages(statusContext =>
        {
            var context = statusContext.HttpContext;
            return context.Response.HasStarted || context.Response.ContentLength is not null
                ? Task.CompletedTask
                : WriteErrorAsync(context, context.Response.StatusCode, options);
        });

        return app;
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, AnvilErrorOptions options)
    {
        context.Response.StatusCode = statusCode;
        var title = statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not found",
            _ => "An unexpected error occurred"
        };

        if (context.Request.GetTypedHeaders().Accept.Any(header =>
                header.MediaType.HasValue
                && header.MediaType.Value.Equals("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            var html = options.HtmlRenderer?.Invoke(statusCode)
                ?? $"<!doctype html><html><head><title>{title}</title></head><body><h1>{title}</h1><p>Request could not be completed.</p></body></html>";
            return context.Response.WriteAsync(html, context.RequestAborted);
        }

        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}"
        }, context.RequestAborted);
    }
}
