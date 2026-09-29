using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class StreamingTests
{
    [Fact]
    public async Task Flush_uses_the_request_cancellation_token()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        await requestContext.FlushAsync();

        Assert.True(requestContext.IsClientConnected());
    }

    [Fact]
    public async Task Client_connection_state_follows_request_aborted()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var cancellation = new CancellationTokenSource();
        context.RequestAborted = cancellation.Token;
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        cancellation.Cancel();

        Assert.False(requestContext.IsClientConnected());
    }
}
