using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilStylingServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilTailwind(
        this IServiceCollection services,
        Action<AnvilTailwindOptions>? configure = null)
    {
        services.AddOptions<AnvilTailwindOptions>();
        if (configure is not null) services.Configure(configure);
        return services;
    }
}
