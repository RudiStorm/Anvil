using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilExportServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilStaticExport(this IServiceCollection services)
    {
        services.AddHttpClient<AnvilStaticExporter>();
        return services;
    }
}
