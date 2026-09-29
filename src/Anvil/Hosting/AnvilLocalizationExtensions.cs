using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilLocalizationExtensions
{
    public static IServiceCollection AddAnvilLocalization(
        this IServiceCollection services,
        params string[] supportedCultures)
    {
        var cultures = supportedCultures.Length == 0 ? ["en-US"] : supportedCultures;
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(cultures[0]);
            options.AddSupportedCultures(cultures);
            options.AddSupportedUICultures(cultures);
        });
        return services;
    }

    public static IApplicationBuilder UseAnvilLocalization(this IApplicationBuilder app)
    {
        return app.UseRequestLocalization();
    }
}
