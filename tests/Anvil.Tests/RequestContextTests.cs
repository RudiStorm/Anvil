using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace Anvil.Tests;

public sealed class RequestContextTests
{
    [Fact]
    public async Task AddAnvil_registers_request_context_per_scope()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<RequestContext>();

        Assert.Throws<InvalidOperationException>(() => context.HttpContext);
    }

    [Fact]
    public async Task Request_context_exposes_the_current_http_context()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext();
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();

        var middleware = new RequestContextProbeMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, requestContext);

        Assert.Same(httpContext, requestContext.HttpContext);
    }

    [Fact]
    public async Task Request_context_reads_json_using_the_request_cancellation_token()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"name\":\"Anvil\"}"));
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(httpContext);

        var value = await requestContext.ReadJsonAsync<TestPayload>();

        Assert.Equal("Anvil", value?.Name);
    }

    [Fact]
    public async Task Request_context_writes_json()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(httpContext);

        await requestContext.WriteJsonAsync(new TestPayload("Anvil"));

        httpContext.Response.Body.Position = 0;
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.Equal("{\"name\":\"Anvil\"}", body);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);
    }

    private sealed class RequestContextProbeMiddleware(RequestDelegate next)
    {
        public Task InvokeAsync(HttpContext httpContext, RequestContext requestContext)
        {
            requestContext.Initialize(httpContext);
            return next(httpContext);
        }
    }

    private sealed record TestPayload(string Name);
}
