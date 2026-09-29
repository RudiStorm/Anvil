using System.Collections.Concurrent;

namespace Anvil;

public sealed class InMemoryAnvilBackgroundJobStore : IAnvilBackgroundJobStore
{
    private readonly ConcurrentQueue<AnvilBackgroundJob> jobs = new();

    public ValueTask EnqueueAsync(AnvilBackgroundJob job, CancellationToken cancellationToken = default)
    {
        jobs.Enqueue(job);
        return ValueTask.CompletedTask;
    }

    public ValueTask<AnvilBackgroundJob?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        jobs.TryDequeue(out var job);
        return ValueTask.FromResult(job);
    }

    public ValueTask CompleteAsync(string jobId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask FailAsync(AnvilBackgroundJob job, Exception error, CancellationToken cancellationToken = default)
    {
        jobs.Enqueue(job with { Attempts = job.Attempts + 1 });
        return ValueTask.CompletedTask;
    }

    public ValueTask ReplayAsync(string jobId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask CancelAsync(string jobId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
