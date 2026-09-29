using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class ContextOverrideTests
{
    [Fact]
    public async Task Context_values_can_be_temporarily_overridden()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var context = scope.ServiceProvider.GetRequiredService<RequestContext>();
        context.Initialize(httpContext);
        context.SetValue(new RequestState("outer"));

        using (context.PushValue(new RequestState("inner")))
        {
            Assert.True(context.TryGetValue<RequestState>(out var inner));
            Assert.Equal("inner", inner?.Value);
        }

        Assert.True(context.TryGetValue<RequestState>(out var outer));
        Assert.Equal("outer", outer?.Value);
    }

    private sealed record RequestState(string Value);
}
