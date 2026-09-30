using System.Security.Claims;
using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class AnvilAuthTests
{
    [Fact]
    public async Task Auth_reads_identity_claims_roles_and_permissions()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "jordan"),
                new Claim(ClaimTypes.Email, "jordan@example.test"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("permission", "projects.read")
            ], "test"))
        };
        var request = scope.ServiceProvider.GetRequiredService<RequestContext>();
        request.Initialize(context);
        var auth = scope.ServiceProvider.GetRequiredService<AnvilAuth>();

        Assert.True(auth.IsAuthenticated);
        Assert.Same(context.User, auth.User);
        Assert.Equal("user-1", auth.UserId);
        Assert.Equal("jordan", auth.Username);
        Assert.Equal("jordan@example.test", auth.Email);
        Assert.True(auth.HasRole("Admin"));
        Assert.True(auth.HasPermission("projects.read"));
        Assert.False(auth.HasPermission("projects.write"));
    }

    [Fact]
    public async Task Auth_guards_require_authentication_and_authorization()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = scope.ServiceProvider.GetRequiredService<RequestContext>();
        request.Initialize(new DefaultHttpContext());
        var auth = scope.ServiceProvider.GetRequiredService<AnvilAuth>();

        Assert.Throws<AnvilUnauthorizedException>(() => auth.RequireAuthenticated());
        Assert.Throws<AnvilUnauthorizedException>(() => auth.RequireRole("Admin"));

        var authenticatedContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        request.Initialize(authenticatedContext);

        Assert.Throws<AnvilForbiddenException>(() => auth.RequireRole("Admin"));
        Assert.Throws<AnvilForbiddenException>(() => auth.RequirePermission("projects.read"));
    }

    [Fact]
    public async Task Auth_is_scoped_to_the_current_request()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();

        var firstAuth = firstScope.ServiceProvider.GetRequiredService<AnvilAuth>();
        var secondAuth = secondScope.ServiceProvider.GetRequiredService<AnvilAuth>();

        Assert.NotSame(firstAuth, secondAuth);
    }
}
