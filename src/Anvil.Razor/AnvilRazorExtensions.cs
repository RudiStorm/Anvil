using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Razor;

public static class AnvilRazorExtensions
{
    public static IServiceCollection AddAnvilRazor(this IServiceCollection services)
    {
        return services.AddAnvil();
    }
}
