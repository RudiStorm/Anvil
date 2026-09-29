using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Anvil;

public static class AnvilSessionServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilSessions(
        this IServiceCollection services,
        Action<AnvilSessionOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddOptions<AnvilSessionOptions>();
        services.TryAddSingleton<IAnvilSessionStore, InMemoryAnvilSessionStore>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AnvilSessionManager>();
        return services;
    }
}
