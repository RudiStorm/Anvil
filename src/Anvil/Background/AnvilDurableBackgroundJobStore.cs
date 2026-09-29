using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Anvil;

/// <summary>
/// A small durable adapter for development and single-process deployments.
/// Applications requiring multi-node claims should replace this with an EF or Wolverine adapter.
/// </summary>
public sealed class AnvilJsonBackgroundJobStore(IOptions<AnvilBackgroundOptions> options)
    : IAnvilBackgroundJobStore, IAnvilBackgroundJobInspector
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask EnqueueAsync(AnvilBackgroundJob job, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var jobs = await ReadAsync(cancellationToken);
            jobs.RemoveAll(item => item.Job.Id == job.Id);
            jobs.Add(new AnvilBackgroundJobInfo(job, AnvilBackgroundJobStatus.Pending, DateTimeOffset.UtcNow));
            await WriteAsync(jobs, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<AnvilBackgroundJob?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var jobs = await ReadAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var leaseExpired = options.Value.LeaseTimeout > TimeSpan.Zero
                ? jobs.Where(item => item.Status == AnvilBackgroundJobStatus.Running && item.UpdatedAt + options.Value.LeaseTimeout <= now)
                    .Select(item => jobs.IndexOf(item)).ToArray()
                : [];
            foreach (var index in leaseExpired)
                jobs[index] = AddHistory(jobs[index] with { Status = AnvilBackgroundJobStatus.Pending, UpdatedAt = now }, AnvilBackgroundJobStatus.Pending, null);
            var item = jobs.Where(item => item.Status == AnvilBackgroundJobStatus.Pending && item.Job.AvailableAt <= now)
                .OrderBy(item => item.Job.AvailableAt).FirstOrDefault();
            if (item is null) return null;
            jobs[jobs.IndexOf(item)] = AddHistory(item with { Status = AnvilBackgroundJobStatus.Running, UpdatedAt = DateTimeOffset.UtcNow }, AnvilBackgroundJobStatus.Running, null);
            await WriteAsync(jobs, cancellationToken);
            return item.Job;
        }
        finally { gate.Release(); }
    }

    public ValueTask CompleteAsync(string jobId, CancellationToken cancellationToken = default) =>
        UpdateAsync(jobId, AnvilBackgroundJobStatus.Completed, null, cancellationToken);

    public async ValueTask FailAsync(AnvilBackgroundJob job, Exception error, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var jobs = await ReadAsync(cancellationToken);
            var index = jobs.FindIndex(item => item.Job.Id == job.Id);
            if (index < 0) return;
            var deadLetter = job.Attempts + 1 >= options.Value.MaxAttempts;
            var delay = options.Value.ExponentialBackoff
                ? TimeSpan.FromTicks(options.Value.RetryDelay.Ticks * Math.Min(1L << Math.Min(job.Attempts, 20), 1L << 20))
                : options.Value.RetryDelay;
            var retry = job with { Attempts = job.Attempts + 1, AvailableAt = DateTimeOffset.UtcNow + delay };
            jobs[index] = AddHistory(new AnvilBackgroundJobInfo(retry,
                deadLetter ? AnvilBackgroundJobStatus.DeadLetter : AnvilBackgroundJobStatus.Pending,
                DateTimeOffset.UtcNow, error.Message) with { History = jobs[index].History },
                deadLetter ? AnvilBackgroundJobStatus.DeadLetter : AnvilBackgroundJobStatus.Pending, error.Message);
            await WriteAsync(jobs, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask ReplayAsync(string jobId, CancellationToken cancellationToken = default) =>
        await UpdateJobAsync(jobId, item => item with
        {
            Job = item.Job with { Attempts = 0, AvailableAt = DateTimeOffset.UtcNow },
            Status = AnvilBackgroundJobStatus.Pending,
            Error = null
        }, AnvilBackgroundJobStatus.Pending, cancellationToken);

    public async ValueTask CancelAsync(string jobId, CancellationToken cancellationToken = default) =>
        await UpdateJobAsync(jobId, item => item with { Status = AnvilBackgroundJobStatus.Cancelled }, AnvilBackgroundJobStatus.Cancelled, cancellationToken);

    public async ValueTask<IReadOnlyList<AnvilBackgroundJobHistory>> HistoryAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return (await ReadAsync(cancellationToken)).FirstOrDefault(item => item.Job.Id == jobId)?.History ?? []; }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<AnvilBackgroundJobInfo>> ListAsync(
        AnvilBackgroundJobStatus? status = null, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return (await ReadAsync(cancellationToken)).Where(item => status is null || item.Status == status).ToArray(); }
        finally { gate.Release(); }
    }

    private async ValueTask UpdateAsync(string id, AnvilBackgroundJobStatus status, string? error, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var jobs = await ReadAsync(cancellationToken);
            var index = jobs.FindIndex(item => item.Job.Id == id);
            if (index >= 0)
            {
                jobs[index] = AddHistory(jobs[index] with { Status = status, UpdatedAt = DateTimeOffset.UtcNow, Error = error }, status, error);
                await WriteAsync(jobs, cancellationToken);
            }
        }
        finally { gate.Release(); }
    }

    private async ValueTask UpdateJobAsync(string id, Func<AnvilBackgroundJobInfo, AnvilBackgroundJobInfo> update,
        AnvilBackgroundJobStatus status, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var jobs = await ReadAsync(cancellationToken);
            var index = jobs.FindIndex(item => item.Job.Id == id);
            if (index >= 0)
            {
                jobs[index] = AddHistory(update(jobs[index]) with { UpdatedAt = DateTimeOffset.UtcNow }, status, jobs[index].Error);
                await WriteAsync(jobs, cancellationToken);
            }
        }
        finally { gate.Release(); }
    }

    private static AnvilBackgroundJobInfo AddHistory(AnvilBackgroundJobInfo item, AnvilBackgroundJobStatus status, string? error) =>
        item with { History = [.. item.History, new AnvilBackgroundJobHistory(item.Job.Id, status, DateTimeOffset.UtcNow, error)] };

    private async Task<List<AnvilBackgroundJobInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var path = options.Value.StorePath;
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<AnvilBackgroundJobInfo>>(stream, jsonOptions, cancellationToken) ?? [];
    }

    private async Task WriteAsync(List<AnvilBackgroundJobInfo> jobs, CancellationToken cancellationToken)
    {
        var path = options.Value.StorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, jobs, jsonOptions, cancellationToken);
        File.Move(temporary, path, true);
    }
}
