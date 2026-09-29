using Anvil;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class IdentityTests
{
    [Fact]
    public void Identity_registration_uses_standard_identity_services()
    {
        var services = new ServiceCollection();

        services.AddAnvilIdentity<TestIdentityDbContext>();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(UserManager<ApplicationUser>));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuthenticationService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAntiforgery));
    }

    [Fact]
    public async Task Identity_endpoints_start_without_inferred_service_bodies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<TestIdentityDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        builder.Services.AddAnvilIdentityContracts();
        builder.Services.AddAnvilIdentity<TestIdentityDbContext>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAnvilIdentityEndpoints();

        await app.StartAsync();
        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Permission_policy_requires_the_permission_claim()
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddAnvilAuthorization()
            .BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequirePermission(AnvilPermissions.CustomersView)
            .Build();

        var permitted = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", AnvilPermissions.CustomersView)], "test"));
        var denied = new ClaimsPrincipal(new ClaimsIdentity([], "test"));

        Assert.True((await authorization.AuthorizeAsync(permitted, null, policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(denied, null, policy)).Succeeded);
    }

    [Fact]
    public async Task OAuth_state_is_issued_and_validated_once()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        services.AddAnvilIdentityContracts();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);
        var oauth = scope.ServiceProvider.GetRequiredService<AnvilOAuthService>();
        var options = new AnvilOAuthOptions
        {
            ProviderName = "test",
            AuthorizationEndpoint = "https://id.example/authorize",
            ClientId = "client",
            RedirectUri = "https://app.example/callback"
        };

        var url = oauth.Begin(options, "/account");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(url).Query);
        context.Request.Headers.Cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];

        Assert.Equal("/account", oauth.ValidateCallback(query["state"]!));
        context.Request.Headers.Cookie = string.Empty;
        Assert.Null(oauth.ValidateCallback(query["state"]!));
    }

    [Fact]
    public async Task Password_reset_tokens_expire_and_are_single_use()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var service = new InMemoryAnvilPasswordResetService(clock);
        var reset = await service.IssueAsync("user-1", TimeSpan.FromMinutes(5));

        Assert.Equal("user-1", await service.ConsumeAsync(reset.Token));
        Assert.Null(await service.ConsumeAsync(reset.Token));

        var expired = await service.IssueAsync("user-2", TimeSpan.FromMinutes(5));
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Null(await service.ConsumeAsync(expired.Token));
    }

    [Fact]
    public void Totp_matches_the_rfc_6238_sha1_vector()
    {
        Assert.Equal("287082", AnvilTotp.ComputeCode("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", 59));
    }

    [Fact]
    public async Task Device_store_lists_and_revokes_only_the_requested_user_session()
    {
        var store = new InMemoryAnvilDeviceSessionStore();
        var now = DateTimeOffset.UtcNow;
        await store.AddAsync(new AnvilDeviceSession("one", "user-1", "Browser", now, now));
        await store.AddAsync(new AnvilDeviceSession("two", "user-1", "Phone", now, now));
        await store.AddAsync(new AnvilDeviceSession("other", "user-2", "Browser", now, now));

        await store.RevokeAsync("user-1", "two");

        Assert.Equal(["one"], (await store.ListAsync("user-1")).Select(session => session.Id));
        Assert.Single(await store.ListAsync("user-2"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset current = current;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current = current.Add(amount);
    }

    private sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
        : AnvilIdentityDbContext<ApplicationUser>(options);
}
