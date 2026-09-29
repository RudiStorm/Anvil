using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Anvil;

public interface IAnvilBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default);
}

internal sealed class AnvilBackgroundTaskQueue : IAnvilBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> channel = Channel.CreateUnbounded<Func<CancellationToken, ValueTask>>();
    public ValueTask QueueAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default) => channel.Writer.WriteAsync(work, cancellationToken);
    public IAsyncEnumerable<Func<CancellationToken, ValueTask>> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class AnvilBackgroundWorker(AnvilBackgroundTaskQueue queue, Microsoft.Extensions.Options.IOptions<AnvilBackgroundOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var work in queue.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await work(stoppingToken);
                    break;
                }
                catch when (attempt < options.Value.MaxAttempts)
                {
                    await Task.Delay(options.Value.RetryDelay, stoppingToken);
                }
            }
        }
    }
}

public static class AnvilBackgroundJobServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilTransactionalOutbox<TContext>(this IServiceCollection services)
        where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        services.AddScoped<IAnvilTransactionalOutbox, AnvilEfTransactionalOutbox<TContext>>();
        return services;
    }

    public static IServiceCollection AddAnvilBackgroundJobs(this IServiceCollection services)
    {
        services.AddOptions<AnvilBackgroundOptions>();
        services.AddSingleton<AnvilJsonBackgroundJobStore>();
        services.AddSingleton<IAnvilBackgroundJobStore>(provider => provider.GetRequiredService<AnvilJsonBackgroundJobStore>());
        services.AddSingleton<IAnvilBackgroundJobInspector>(provider => provider.GetRequiredService<AnvilJsonBackgroundJobStore>());
        services.AddSingleton<AnvilBackgroundTaskQueue>();
        services.AddSingleton<IAnvilBackgroundTaskQueue>(provider => provider.GetRequiredService<AnvilBackgroundTaskQueue>());
        services.AddHostedService<AnvilBackgroundWorker>();
        return services;
    }

    public static IServiceCollection AddAnvilInMemoryBackgroundJobs(this IServiceCollection services)
    {
        services.AddOptions<AnvilBackgroundOptions>();
        services.AddSingleton<IAnvilBackgroundJobStore, InMemoryAnvilBackgroundJobStore>();
        services.AddSingleton<AnvilBackgroundTaskQueue>();
        services.AddSingleton<IAnvilBackgroundTaskQueue>(provider => provider.GetRequiredService<AnvilBackgroundTaskQueue>());
        services.AddHostedService<AnvilBackgroundWorker>();
        return services;
    }
}
