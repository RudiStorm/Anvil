using System.Collections.Concurrent;
using System.Security.Claims;

namespace Anvil;

public sealed class InMemoryConsentStore(TenantContext tenant) : IConsentStore
{
    private readonly ConcurrentDictionary<Guid, ConsentRecord> records = new();
    public Task<ConsentRecord> RecordAsync(ConsentRecord consent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        consent.TenantId = tenant.RequireTenantId();
        ArgumentException.ThrowIfNullOrWhiteSpace(consent.SubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(consent.Purpose);
        records[consent.Id] = consent;
        return Task.FromResult(consent);
    }

    public Task<IReadOnlyList<ConsentRecord>> ListAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenant.RequireTenantId();
        var result = records.Values.Where(x => x.TenantId == tenantId && x.SubjectId == query.SubjectId
            && (query.Purpose is null || x.Purpose == query.Purpose)).OrderByDescending(x => x.RecordedAtUtc).ToArray();
        return Task.FromResult<IReadOnlyList<ConsentRecord>>(result);
    }
}

public sealed class InMemoryPrivacyRequestStore(TenantContext tenant) : IPrivacyRequestStore
{
    private readonly ConcurrentDictionary<Guid, PrivacyRequest> records = new();
    public Task<PrivacyRequest> AddAsync(PrivacyRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.TenantId = tenant.RequireTenantId();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SubjectId);
        records[request.Id] = request;
        return Task.FromResult(request);
    }

    public Task<PrivacyRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records.TryGetValue(id, out var request);
        return Task.FromResult(request is not null && request.TenantId == tenant.RequireTenantId() ? request : null);
    }

    public Task<IReadOnlyList<PrivacyRequest>> ListAsync(string? subjectId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenant.RequireTenantId();
        var result = records.Values.Where(x => x.TenantId == tenantId && (subjectId is null || x.SubjectId == subjectId))
            .OrderByDescending(x => x.RequestedAtUtc).ToArray();
        return Task.FromResult<IReadOnlyList<PrivacyRequest>>(result);
    }

    public Task UpdateAsync(PrivacyRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.TenantId != tenant.RequireTenantId()) throw new InvalidOperationException("A request belongs to a different tenant.");
        if (!records.ContainsKey(request.Id)) throw new KeyNotFoundException("The privacy request was not found.");
        records[request.Id] = request;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryLegalHoldStore(TenantContext tenant) : ILegalHoldStore
{
    private readonly ConcurrentDictionary<Guid, LegalHold> holds = new();
    public Task<LegalHold> AddAsync(LegalHold hold, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        hold.TenantId = tenant.RequireTenantId();
        ArgumentException.ThrowIfNullOrWhiteSpace(hold.SubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hold.Reason);
        holds[hold.Id] = hold;
        return Task.FromResult(hold);
    }
    public Task<bool> HasActiveHoldAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenant.RequireTenantId();
        return Task.FromResult(holds.Values.Any(x => x.TenantId == tenantId && x.SubjectId == subjectId && x.IsActive));
    }
    public Task ReleaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (holds.TryGetValue(id, out var hold) && hold.TenantId == tenant.RequireTenantId()) hold.ReleasedAtUtc = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryRetentionPolicyProvider : IRetentionPolicyProvider
{
    private readonly ConcurrentDictionary<string, RetentionPolicy> policies = new(StringComparer.OrdinalIgnoreCase);
    public RetentionPolicy? Get(string dataCategory) => policies.TryGetValue(dataCategory, out var policy) ? policy : null;
    public void Set(RetentionPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.DataCategory);
        if (policy.RetainFor < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(policy));
        policies[policy.DataCategory] = policy;
    }
}

public sealed class ClaimsComplianceAccessBoundary(TenantContext tenant) : IComplianceAccessBoundary
{
    public Task DemandAsync(string subjectId, ComplianceOperation operation, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (principal.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException("Authentication is required.");
        var claimTenant = principal.FindFirst("tenant_id")?.Value ?? principal.FindFirst("tenant")?.Value;
        if (claimTenant is not null && claimTenant != tenant.RequireTenantId()) throw new UnauthorizedAccessException("The tenant boundary was not satisfied.");
        var ownSubject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        var administrator = principal.HasClaim("permission", "compliance.manage");
        if (operation == ComplianceOperation.Process && !administrator)
            throw new UnauthorizedAccessException("Compliance processing requires the compliance.manage permission.");
        if (!administrator && !string.Equals(ownSubject, subjectId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The compliance subject boundary was not satisfied.");
        return Task.CompletedTask;
    }
}

public sealed class RetentionEvaluator(IRetentionPolicyProvider policies, ILegalHoldStore holds, TimeProvider? clock = null) : IRetentionEvaluator
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    public async Task<bool> ShouldRetainAsync(RetentionCandidate candidate, CancellationToken cancellationToken = default)
    {
        if (await holds.HasActiveHoldAsync(candidate.SubjectId, cancellationToken)) return true;
        var policy = policies.Get(candidate.DataCategory);
        return policy is null || !policy.Enabled || candidate.CreatedAtUtc.Add(policy.RetainFor) > clock.GetUtcNow();
    }
}
