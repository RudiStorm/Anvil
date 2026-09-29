using Microsoft.EntityFrameworkCore;

namespace Anvil;

/// <summary>Durable message stored in the application's EF transaction.</summary>
public sealed class AnvilOutboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string JobName { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }
    public string? ActorId { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}

public interface IAnvilTransactionalOutbox
{
    ValueTask<string> EnqueueAsync(string jobName, string payload, DateTimeOffset? availableAt = null,
        string? tenantId = null, string? actorId = null, CancellationToken cancellationToken = default);
    Task<int> DispatchAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Adds messages to the supplied DbContext. The caller's SaveChanges transaction
/// commits the business mutation and outbox row together.
/// </summary>
public sealed class AnvilEfTransactionalOutbox<TContext>(TContext db, IAnvilBackgroundJobStore jobs)
    : IAnvilTransactionalOutbox where TContext : DbContext
{
    public ValueTask<string> EnqueueAsync(string jobName, string payload, DateTimeOffset? availableAt = null,
        string? tenantId = null, string? actorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(payload);
        var message = new AnvilOutboxMessage
        {
            JobName = jobName,
            Payload = payload,
            AvailableAt = availableAt ?? DateTimeOffset.UtcNow,
            TenantId = tenantId,
            ActorId = actorId
        };
        db.Set<AnvilOutboxMessage>().Add(message);
        return ValueTask.FromResult(message.Id);
    }

    public async Task<int> DispatchAsync(CancellationToken cancellationToken = default)
    {
        var messages = await db.Set<AnvilOutboxMessage>()
            .Where(message => message.DispatchedAt == null && message.AvailableAt <= DateTimeOffset.UtcNow)
            .OrderBy(message => message.AvailableAt)
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            await jobs.EnqueueAsync(new AnvilBackgroundJob(message.Id, message.JobName, message.Payload, 0,
                message.AvailableAt) { TenantId = message.TenantId, ActorId = message.ActorId }, cancellationToken);
            message.DispatchedAt = DateTimeOffset.UtcNow;
        }
        if (messages.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
