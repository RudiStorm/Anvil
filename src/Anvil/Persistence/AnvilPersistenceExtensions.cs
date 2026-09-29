using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public sealed class AnvilPersistenceOptions
{
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    public AnvilDatabaseProvider Provider { get; set; } = AnvilDatabaseProvider.Sqlite;

    public bool EnableDetailedErrors { get; set; }

    public bool EnableSensitiveDataLogging { get; set; }
}

public enum AnvilDatabaseProvider
{
    Sqlite,
    SqlServer,
    PostgreSql,
    MySql
}

public static class AnvilDatabaseProviderInfo
{
    public static string GetPackageId(this AnvilDatabaseProvider provider) => provider switch
    {
        AnvilDatabaseProvider.Sqlite => "Microsoft.EntityFrameworkCore.Sqlite",
        AnvilDatabaseProvider.SqlServer => "Microsoft.EntityFrameworkCore.SqlServer",
        AnvilDatabaseProvider.PostgreSql => "Npgsql.EntityFrameworkCore.PostgreSQL",
        AnvilDatabaseProvider.MySql => "Pomelo.EntityFrameworkCore.MySql",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

    public static string GetConfigurationMethod(this AnvilDatabaseProvider provider) => provider switch
    {
        AnvilDatabaseProvider.Sqlite => "UseSqlite",
        AnvilDatabaseProvider.SqlServer => "UseSqlServer",
        AnvilDatabaseProvider.PostgreSql => "UseNpgsql",
        AnvilDatabaseProvider.MySql => "UseMySql",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };
}

public static class AnvilPersistenceExtensions
{
    public static IServiceCollection AddAnvilSqlitePersistence<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder, string> configureProvider,
        Action<AnvilPersistenceOptions>? configure = null)
        where TContext : DbContext => AddProviderPersistence<TContext>(services, configuration, AnvilDatabaseProvider.Sqlite, configureProvider, configure);

    public static IServiceCollection AddAnvilSqlServerPersistence<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder, string> configureProvider,
        Action<AnvilPersistenceOptions>? configure = null)
        where TContext : DbContext => AddProviderPersistence<TContext>(services, configuration, AnvilDatabaseProvider.SqlServer, configureProvider, configure);

    public static IServiceCollection AddAnvilPostgreSqlPersistence<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder, string> configureProvider,
        Action<AnvilPersistenceOptions>? configure = null)
        where TContext : DbContext => AddProviderPersistence<TContext>(services, configuration, AnvilDatabaseProvider.PostgreSql, configureProvider, configure);

    public static IServiceCollection AddAnvilMySqlPersistence<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder, string> configureProvider,
        Action<AnvilPersistenceOptions>? configure = null)
        where TContext : DbContext => AddProviderPersistence<TContext>(services, configuration, AnvilDatabaseProvider.MySql, configureProvider, configure);

    public static IServiceCollection AddAnvilPersistence<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddDbContext<TContext>(configure);
        return services;
    }

    private static IServiceCollection AddProviderPersistence<TContext>(
        IServiceCollection services,
        IConfiguration configuration,
        AnvilDatabaseProvider provider,
        Action<DbContextOptionsBuilder, string> configureProvider,
        Action<AnvilPersistenceOptions>? configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configureProvider);
        return services.AddAnvilPersistence<TContext>(configuration, options =>
        {
            options.Provider = provider;
            configure?.Invoke(options);
            if (options.Provider != provider)
                throw new InvalidOperationException($"The {provider} persistence API cannot be configured for {options.Provider}.");
        }, configureProvider);
    }

    /// <summary>
    /// Registers an application-owned context using configuration and an explicit
    /// provider callback. The callback keeps provider packages out of Anvil.Core.
    /// </summary>
    public static IServiceCollection AddAnvilPersistence<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AnvilPersistenceOptions> configure,
        Action<DbContextOptionsBuilder, string> configureProvider)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configureProvider);

        var persistenceOptions = new AnvilPersistenceOptions();
        configure(persistenceOptions);
        var connectionString = configuration.GetConnectionString(persistenceOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{persistenceOptions.ConnectionStringName}' is required for {typeof(TContext).Name}.");
        }

        services.AddOptions<AnvilPersistenceOptions>()
            .Configure(options =>
            {
                options.ConnectionStringName = persistenceOptions.ConnectionStringName;
                options.Provider = persistenceOptions.Provider;
                options.EnableDetailedErrors = persistenceOptions.EnableDetailedErrors;
                options.EnableSensitiveDataLogging = persistenceOptions.EnableSensitiveDataLogging;
            });
        services.AddDbContext<TContext>((_, options) =>
        {
            configureProvider(options, connectionString);
            options.EnableDetailedErrors(persistenceOptions.EnableDetailedErrors);
            options.EnableSensitiveDataLogging(persistenceOptions.EnableSensitiveDataLogging);
        });
        return services;
    }

    public static IServiceCollection AddAnvilPersistence<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder, string> configure)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddDbContext<TContext>((_, options) => configure(options, connectionString));
        return services;
    }
}
