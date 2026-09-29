using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anvil;

public enum ConsentState { Granted, Withdrawn }
public enum PrivacyRequestKind { Export, Deletion, Anonymization }
public enum PrivacyRequestState { Pending, Processing, Completed, Rejected }
public enum ComplianceOperation { Read, Submit, Process }

public sealed class ConsentRecord : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = null!;
    public string SubjectId { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public ConsentState State { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? PolicyVersion { get; set; }
    public string? Source { get; set; }
}

public sealed class PrivacyRequest : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = null!;
    public string SubjectId { get; set; } = null!;
    public PrivacyRequestKind Kind { get; set; }
    public PrivacyRequestState State { get; set; } = PrivacyRequestState.Pending;
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class LegalHold : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = null!;
    public string SubjectId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAtUtc { get; set; }
    public bool IsActive => ReleasedAtUtc is null;
}

public sealed record ConsentQuery(string SubjectId, string? Purpose = null);
public sealed record PrivacyData(string Category, IReadOnlyDictionary<string, object?> Values);
public sealed record PrivacyExport(IReadOnlyCollection<PrivacyData> Data);
public sealed record PrivacyErasureResult(int AffectedRecords, bool Deferred = false);
public sealed record RetentionPolicy(string DataCategory, TimeSpan RetainFor, bool Enabled = true);
public sealed record RetentionCandidate(string DataCategory, string SubjectId, DateTimeOffset CreatedAtUtc);

public interface IConsentStore
{
    Task<ConsentRecord> RecordAsync(ConsentRecord consent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsentRecord>> ListAsync(ConsentQuery query, CancellationToken cancellationToken = default);
}

public interface IPrivacyRequestStore
{
    Task<PrivacyRequest> AddAsync(PrivacyRequest request, CancellationToken cancellationToken = default);
    Task<PrivacyRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrivacyRequest>> ListAsync(string? subjectId = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(PrivacyRequest request, CancellationToken cancellationToken = default);
}

public interface ILegalHoldStore
{
    Task<LegalHold> AddAsync(LegalHold hold, CancellationToken cancellationToken = default);
    Task<bool> HasActiveHoldAsync(string subjectId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPrivacyDataProvider
{
    string Name { get; }
    Task<IReadOnlyCollection<PrivacyData>> ExportAsync(string subjectId, CancellationToken cancellationToken = default);
}

public interface IPrivacyErasureHandler
{
    string Name { get; }
    Task<PrivacyErasureResult> EraseAsync(string subjectId, PrivacyRequestKind kind, CancellationToken cancellationToken = default);
}

public interface IComplianceAccessBoundary
{
    Task DemandAsync(string subjectId, ComplianceOperation operation, ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

public interface IRetentionPolicyProvider
{
    RetentionPolicy? Get(string dataCategory);
}

public interface IRetentionEvaluator
{
    Task<bool> ShouldRetainAsync(RetentionCandidate candidate, CancellationToken cancellationToken = default);
}

public static class AnvilComplianceModelBuilderExtensions
{
    public static void ConfigureAnvilConsent(this EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.Purpose });
    }

    public static void ConfigureAnvilPrivacyRequest(this EntityTypeBuilder<PrivacyRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.RequestedAtUtc });
    }

    public static void ConfigureAnvilLegalHold(this EntityTypeBuilder<LegalHold> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.ReleasedAtUtc });
    }

    public static IServiceCollection AddAnvilCompliance(this IServiceCollection services)
    {
        services.TryAddScoped<TenantContext>();
        services.AddScoped<IConsentStore, InMemoryConsentStore>();
        services.AddScoped<IPrivacyRequestStore, InMemoryPrivacyRequestStore>();
        services.AddScoped<ILegalHoldStore, InMemoryLegalHoldStore>();
        services.AddSingleton<InMemoryRetentionPolicyProvider>();
        services.AddSingleton<IRetentionPolicyProvider>(sp => sp.GetRequiredService<InMemoryRetentionPolicyProvider>());
        services.AddScoped<IComplianceAccessBoundary, ClaimsComplianceAccessBoundary>();
        services.AddScoped<IRetentionEvaluator, RetentionEvaluator>();
        services.AddScoped<PrivacyComplianceService>();
        return services;
    }
}
