using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilServiceCollectionExtensions
{
    public static IServiceCollection AddAnvil(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddDataProtection();
        services.AddOptions<AnvilCookieOptions>();
        services.AddRazorComponents();
        services.AddScoped<AnvilFragmentRenderer>();
        services.AddScoped<RequestContext>();
        services.AddScoped<AnvilAuth>();
        services.AddScoped<AnvilRequestMemoizer>();
        services.AddAnvilCaching();
        services.AddAnvilSessions();
        services.AddAnvilRouting();
        services.AddAnvilMail();
        services.AddAnvilObservability();
        return services;
    }
}
