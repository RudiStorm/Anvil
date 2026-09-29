namespace Anvil;

public sealed class AuditEntry : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ActorId { get; set; }
    public string TenantId { get; set; } = null!;
    public string Resource { get; set; } = null!;
    public string? ResourceId { get; set; }
    public string Operation { get; set; } = null!;
    public string? ChangedFields { get; set; }
    public string? TraceId { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed record AuditRequest(
    string Resource,
    string Operation,
    string? ResourceId = null,
    IReadOnlyDictionary<string, object?>? ChangedFields = null);

public interface IAuditWriter
{
    Task WriteAsync(AuditRequest request, CancellationToken cancellationToken = default);
}

public static class AuditEntryModelBuilderExtensions
{
    public static void ConfigureAnvilAudit(this Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.TenantId).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Resource).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Operation).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(200);
        builder.Property(entry => entry.ResourceId).HasMaxLength(200);
        builder.Property(entry => entry.TraceId).HasMaxLength(200);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(200);
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAtUtc });
    }
}
