namespace Anvil;

public sealed record AnvilBackgroundJob(
    string Id,
    string Name,
    string Payload,
    int Attempts,
    DateTimeOffset AvailableAt)
{
    public string? TenantId { get; init; }
    public string? ActorId { get; init; }
}

public enum AnvilBackgroundJobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    DeadLetter,
    Cancelled
}

public sealed record AnvilBackgroundJobInfo(
    AnvilBackgroundJob Job,
    AnvilBackgroundJobStatus Status,
    DateTimeOffset UpdatedAt,
    string? Error = null)
{
    public IReadOnlyList<AnvilBackgroundJobHistory> History { get; init; } = [];
}

public sealed record AnvilBackgroundJobHistory(
    string JobId,
    AnvilBackgroundJobStatus Status,
    DateTimeOffset At,
    string? Error = null);

public interface IAnvilBackgroundJobStore
{
    ValueTask EnqueueAsync(AnvilBackgroundJob job, CancellationToken cancellationToken = default);
    ValueTask<AnvilBackgroundJob?> DequeueAsync(CancellationToken cancellationToken = default);
    ValueTask CompleteAsync(string jobId, CancellationToken cancellationToken = default);
    ValueTask FailAsync(AnvilBackgroundJob job, Exception error, CancellationToken cancellationToken = default);
    ValueTask ReplayAsync(string jobId, CancellationToken cancellationToken = default);
    ValueTask CancelAsync(string jobId, CancellationToken cancellationToken = default);
}

public interface IAnvilBackgroundJobInspector
{
    ValueTask<IReadOnlyList<AnvilBackgroundJobInfo>> ListAsync(
        AnvilBackgroundJobStatus? status = null,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AnvilBackgroundJobHistory>> HistoryAsync(
        string jobId, CancellationToken cancellationToken = default);
}
