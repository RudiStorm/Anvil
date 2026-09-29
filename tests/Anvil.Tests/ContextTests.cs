using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class ContextTests
{
    [Fact]
    public async Task Request_context_resolves_services_and_scoped_values()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        services.AddSingleton(new ContextService("application"));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = CreateContext(scope.ServiceProvider);

        context.SetValue(new RequestValue("request"));

        Assert.Equal("application", context.GetApplicationService<ContextService>().Value);
        Assert.True(context.TryGetValue<RequestValue>(out var value));
        Assert.Equal("request", value?.Value);
    }

    [Fact]
    public async Task Memoization_deduplicates_concurrent_work_per_request()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = CreateContext(scope.ServiceProvider);
        var calls = 0;

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => context.MemoizeAsync("products", async _ =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(10);
                return 42;
            }).AsTask()));

        Assert.All(results, result => Assert.Equal(42, result));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Authentication_guard_rejects_requests_without_a_session()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = CreateContext(scope.ServiceProvider);

        await Assert.ThrowsAsync<AnvilUnauthorizedException>(async () =>
            await context.RequireAuthenticatedAsync());
    }

    private static RequestContext CreateContext(IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var context = services.GetRequiredService<RequestContext>();
        context.Initialize(httpContext);
        return context;
    }

    private sealed record ContextService(string Value);

    private sealed record RequestValue(string Value);
}
