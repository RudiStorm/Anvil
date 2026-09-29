using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task Cookie_helpers_use_secure_defaults()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        requestContext.SetCookie("test", "value");

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("test=value", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signed_cookie_round_trips_and_rejects_tampering()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        requestContext.SetSignedCookie("signed", "value");
        context.Request.Headers.Cookie = GetLatestCookie(context);

        Assert.Equal("value", requestContext.GetSignedCookie("signed"));

        context.Request.Headers.Cookie = "signed=invalid";
        Assert.Null(requestContext.GetSignedCookie("signed"));
    }

    [Fact]
    public async Task Encrypted_json_cookie_round_trips()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        requestContext.SetJsonCookie("preferences", new Preferences("dark"));
        context.Request.Headers.Cookie = GetLatestCookie(context);

        var preferences = requestContext.GetJsonCookie<Preferences>("preferences");

        Assert.Equal("dark", preferences?.Theme);
    }

    [Fact]
    public async Task Session_can_start_and_be_loaded_from_the_cookie()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();

        var firstContext = new DefaultHttpContext();
        var firstScope = provider.CreateAsyncScope();
        await using (firstScope)
        {
            var requestContext = firstScope.ServiceProvider.GetRequiredService<RequestContext>();
            requestContext.Initialize(firstContext);
            var manager = firstScope.ServiceProvider.GetRequiredService<AnvilSessionManager>();

            await manager.StartAsync(new Dictionary<string, string> { ["user"] = "42" });
        }

        var cookieHeader = GetLatestCookie(firstContext);
        var secondContext = new DefaultHttpContext();
        secondContext.Request.Headers.Cookie = cookieHeader;
        await using var secondScope = provider.CreateAsyncScope();
        var secondRequestContext = secondScope.ServiceProvider.GetRequiredService<RequestContext>();
        secondRequestContext.Initialize(secondContext);
        var secondManager = secondScope.ServiceProvider.GetRequiredService<AnvilSessionManager>();

        var session = await secondManager.GetAsync();

        Assert.NotNull(session);
        Assert.Equal("42", session.Values["user"]);
    }

    [Fact]
    public async Task Session_rotation_replaces_the_old_token()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext();
        await using var scope = provider.CreateAsyncScope();
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);
        var manager = scope.ServiceProvider.GetRequiredService<AnvilSessionManager>();

        var first = await manager.StartAsync();
        var firstCookie = GetLatestCookie(context);
        context.Request.Headers.Cookie = firstCookie;
        var second = await manager.RotateAsync();

        Assert.NotNull(second);
        Assert.NotEqual(first.TokenHash, second!.TokenHash);
    }

    private static string GetLatestCookie(HttpContext context)
    {
        var cookies = context.Response.Headers.SetCookie.ToArray();
        Assert.NotEmpty(cookies);
        var latest = cookies[^1] ?? throw new InvalidOperationException("No cookie was emitted.");
        return latest.Split(';')[0];
    }

    private sealed record Preferences(string Theme);
}
