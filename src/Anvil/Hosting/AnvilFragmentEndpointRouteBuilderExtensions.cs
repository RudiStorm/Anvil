using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilFragmentEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapAnvilFragmentGet<TComponent>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, Task<object?>>? parameters = null,
        Func<RequestContext, string?>? nonFragmentRedirect = null)
        where TComponent : IComponent
    {
        return MapFragment<TComponent>(
            endpoints,
            HttpMethods.Get,
            pattern,
            parameters,
            nonFragmentRedirect);
    }

    public static RouteHandlerBuilder MapAnvilFragmentPost<TComponent>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, Task<object?>>? parameters = null)
        where TComponent : IComponent
    {
        return MapFragment<TComponent>(
            endpoints,
            HttpMethods.Post,
            pattern,
            parameters,
            null);
    }

    public static RouteHandlerBuilder MapAnvilFragmentPost<TRequest, TComponent>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<object?>> parameters,
        bool requireAuthentication = false)
        where TComponent : IComponent
    {
        var route = endpoints.MapPost(
            pattern,
            async (RequestContext context, [FromForm] TRequest request, AnvilFragmentRenderer renderer) =>
            {
                if (requireAuthentication)
                    await context.RequireAuthenticatedAsync(context.RequestAborted);

                var errors = context.Validate(request);
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(
                        RequestValidationExtensions.ToProblemErrors(errors));
                }

                var values = await parameters(context, request);
                var html = await renderer.RenderAsync<TComponent>(
                    values,
                    context.RequestAborted);
                return Results.Content(html, "text/html; charset=utf-8");
            });

        endpoints.RegisterEndpoint(
            HttpMethods.Post,
            pattern,
            requestType: typeof(TRequest),
            requiresAuthentication: requireAuthentication,
            responseContentType: "text/html");

        return route.WithMetadata(new ProducesAttribute("text/html"));
    }

    private static RouteHandlerBuilder MapFragment<TComponent>(
        IEndpointRouteBuilder endpoints,
        string method,
        string pattern,
        Func<RequestContext, Task<object?>>? parameters,
        Func<RequestContext, string?>? nonFragmentRedirect)
        where TComponent : IComponent
    {
        var route = endpoints.MapMethods(
            pattern,
            [method],
            async (RequestContext context, AnvilFragmentRenderer renderer) =>
            {
                var isFragmentRequest = context.IsHtmxRequest()
                    || string.Equals(
                        context.Request.Headers["X-Anvil-Partial"].FirstOrDefault(),
                        "true",
                        StringComparison.OrdinalIgnoreCase);
                if (!isFragmentRequest && nonFragmentRedirect is not null)
                {
                    var location = nonFragmentRedirect(context);
                    return location is null ? Results.BadRequest() : Results.Redirect(location);
                }

                var values = parameters is null ? null : await parameters(context);
                var html = await renderer.RenderAsync<TComponent>(
                    values,
                    context.RequestAborted);
                return Results.Content(html, "text/html; charset=utf-8");
            });

        endpoints.RegisterEndpoint(
            method,
            pattern,
            responseContentType: "text/html");

        return route.WithMetadata(new ProducesAttribute("text/html"));
    }
}
