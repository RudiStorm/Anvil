using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anvil;

public static class AnvilMailServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilMail(this IServiceCollection services)
    {
        services.TryAddSingleton<IAnvilMailTransport, InMemoryAnvilMailTransport>();
        services.AddSingleton<AnvilMailer>();
        return services;
    }

    public static IServiceCollection AddAnvilFileMail(
        this IServiceCollection services,
        Action<AnvilMailOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IAnvilMailTransport, FileAnvilMailTransport>();
        services.AddSingleton<AnvilMailer>();
        return services;
    }

    public static IServiceCollection AddAnvilSmtpMail(
        this IServiceCollection services,
        Action<AnvilMailOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IAnvilMailTransport, SmtpAnvilMailTransport>();
        services.AddSingleton<AnvilMailer>();
        return services;
    }
}
