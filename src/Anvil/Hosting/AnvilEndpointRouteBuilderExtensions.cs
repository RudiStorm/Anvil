using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapAnvilGet(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, Task<IResult>> handler,
        string? name = null)
    {
        var route = endpoints.MapGet(pattern, handler);
        endpoints.RegisterEndpoint("GET", pattern, name, feature: InferFeature(pattern));
        return name is null ? route : route.WithName(name);
    }

    public static RouteHandlerBuilder MapAnvilPost<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<IResult>> handler,
        string? name = null)
    {
        var route = endpoints.MapPost(pattern, CreateValidatedHandler(handler));
        endpoints.RegisterEndpoint("POST", pattern, name, typeof(TRequest), feature: InferFeature(pattern));
        return name is null ? route : route.WithName(name);
    }

    public static RouteHandlerBuilder MapAnvilFormPost<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<IResult>> handler)
    {
        var route = endpoints.MapPost(pattern, async (RequestContext context, [FromForm] TRequest request) =>
        {
            var errors = context.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(
                    RequestValidationExtensions.ToProblemErrors(errors));
            }

            return await handler(context, request);
        });
        endpoints.RegisterEndpoint("POST", pattern, requestType: typeof(TRequest));
        return route;
    }

    public static RouteHandlerBuilder MapAnvilPut<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<IResult>> handler)
    {
        var route = endpoints.MapPut(pattern, CreateValidatedHandler(handler));
        endpoints.RegisterEndpoint("PUT", pattern, requestType: typeof(TRequest));
        return route;
    }

    public static RouteHandlerBuilder MapAnvilPatch<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<IResult>> handler)
    {
        var route = endpoints.MapPatch(pattern, CreateValidatedHandler(handler));
        endpoints.RegisterEndpoint("PATCH", pattern, requestType: typeof(TRequest));
        return route;
    }

    public static RouteHandlerBuilder MapAnvilDelete(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, Task<IResult>> handler)
    {
        var route = endpoints.MapDelete(pattern, handler);
        endpoints.RegisterEndpoint("DELETE", pattern);
        return route;
    }

    public static RouteHandlerBuilder MapAnvilProcedure<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<TResponse>> handler,
        string? name = null,
        bool requireAuthentication = false)
    {
        var route = endpoints.MapPost(pattern, async (RequestContext context, TRequest request) =>
        {
            if (requireAuthentication)
            {
                await context.RequireAuthenticatedAsync(context.RequestAborted);
            }
            var errors = context.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(
                    RequestValidationExtensions.ToProblemErrors(errors));
            }

            return Results.Ok(await handler(context, request));
        });
        endpoints.RegisterEndpoint("POST", pattern, name, typeof(TRequest), typeof(TResponse), requireAuthentication);
        return name is null ? route : route.WithName(name);
    }

    public static RouteHandlerBuilder MapAnvilShard<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<string>> render,
        bool requireAuthentication = true)
    {
        return endpoints.MapPost(pattern, async (RequestContext context, TRequest request) =>
        {
            if (requireAuthentication)
            {
                await context.RequireAuthenticatedAsync(context.RequestAborted);
            }

            var errors = context.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(
                    RequestValidationExtensions.ToProblemErrors(errors));
            }

            var html = await render(context, request);
            return Results.Content(html, "text/html; charset=utf-8");
        });
    }

    public static RouteHandlerBuilder MapAnvilShard<TRequest, TComponent>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<RequestContext, TRequest, Task<object?>> parameters,
        bool requireAuthentication = true)
        where TComponent : IComponent
    {
        return endpoints.MapPost(pattern, async (
            RequestContext context,
            TRequest request,
            AnvilFragmentRenderer renderer) =>
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
    }

    private static Func<RequestContext, TRequest, Task<IResult>> CreateValidatedHandler<TRequest>(
        Func<RequestContext, TRequest, Task<IResult>> handler)
    {
        return async (context, request) =>
        {
            var errors = context.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(
                    RequestValidationExtensions.ToProblemErrors(errors));
            }

            return await handler(context, request);
        };
    }

    private static string? InferFeature(string pattern)
    {
        var segment = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return segment is null or "api" ? null : segment;
    }
}
