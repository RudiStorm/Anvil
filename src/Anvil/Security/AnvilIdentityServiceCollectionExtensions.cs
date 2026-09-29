using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Anvil;

public static class AnvilIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilIdentityContracts(this IServiceCollection services)
    {
        services.AddScoped<AnvilOAuthService>();
        services.AddSingleton<IAnvilPasswordResetService, InMemoryAnvilPasswordResetService>();
        services.TryAddSingleton<IAnvilDeviceSessionStore, InMemoryAnvilDeviceSessionStore>();
        return services;
    }

    public static IServiceCollection AddAnvilIdentity<TUser, TContext>(
        this IServiceCollection services,
        Action<IdentityOptions>? configureIdentity = null,
        Action<CookieAuthenticationOptions>? configureCookie = null)
        where TUser : IdentityUser
        where TContext : DbContext
    {
        services.AddOptions<AnvilIdentityOptions>();
        services.AddIdentityCore<TUser>(configureIdentity ?? (_ => { }))
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        if (configureCookie is not null)
            services.Configure(IdentityConstants.ApplicationScheme, configureCookie);

        services.AddAuthorization();
        services.AddAntiforgery();
        return services;
    }

    public static IServiceCollection AddAnvilIdentity<TContext>(
        this IServiceCollection services,
        Action<IdentityOptions>? configureIdentity = null,
        Action<CookieAuthenticationOptions>? configureCookie = null)
        where TContext : DbContext
    {
        return services.AddAnvilIdentity<ApplicationUser, TContext>(configureIdentity, configureCookie);
    }
}
