using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilRoutingServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilRouting(
        this IServiceCollection services,
        Action<AnvilRoutingOptions>? configure = null)
    {
        services.AddOptions<AnvilRoutingOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
