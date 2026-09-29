using System.Security.Claims;
using Anvil;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class ComplianceTests
{
    [Fact]
    public async Task Consent_store_is_tenant_scoped_and_filters_by_purpose()
    {
        var tenant = new TenantContext();
        tenant.Set("tenant-a");
        var store = new InMemoryConsentStore(tenant);

        await store.RecordAsync(new ConsentRecord { SubjectId = "user-1", Purpose = "marketing" });
        await store.RecordAsync(new ConsentRecord { SubjectId = "user-1", Purpose = "analytics" });

        var records = await store.ListAsync(new ConsentQuery("user-1", "marketing"));
        Assert.Single(records);
        Assert.Equal("tenant-a", records[0].TenantId);
    }

    [Fact]
    public async Task Retention_keeps_expired_data_when_a_legal_hold_is_active()
    {
        var tenant = new TenantContext();
        tenant.Set("tenant-a");
        var holds = new InMemoryLegalHoldStore(tenant);
        var policies = new InMemoryRetentionPolicyProvider();
        policies.Set(new RetentionPolicy("events", TimeSpan.FromDays(1)));
        await holds.AddAsync(new LegalHold { SubjectId = "user-1", Reason = "pending litigation" });
        var evaluator = new RetentionEvaluator(policies, holds,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        Assert.True(await evaluator.ShouldRetainAsync(new RetentionCandidate(
            "events", "user-1", DateTimeOffset.UtcNow.AddDays(-30))));
        Assert.False(await evaluator.ShouldRetainAsync(new RetentionCandidate(
            "events", "user-2", DateTimeOffset.UtcNow.AddDays(-30))));
    }

    [Fact]
    public async Task Access_boundary_allows_subject_and_compliance_manager_only()
    {
        var tenant = new TenantContext();
        tenant.Set("tenant-a");
        var boundary = new ClaimsComplianceAccessBoundary(tenant);
        var subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim("tenant_id", "tenant-a")], "test"));
        var other = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-2"), new Claim("tenant_id", "tenant-a")], "test"));
        var manager = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim("tenant_id", "tenant-a"),
            new Claim("permission", "compliance.manage")], "test"));

        await boundary.DemandAsync("user-1", ComplianceOperation.Read, subject);
        await boundary.DemandAsync("user-1", ComplianceOperation.Read, manager);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            boundary.DemandAsync("user-1", ComplianceOperation.Read, other));
    }

    [Fact]
    public async Task Privacy_service_exports_and_processes_registered_handlers()
    {
        var tenant = new TenantContext();
        tenant.Set("tenant-a");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));
        var requests = new InMemoryPrivacyRequestStore(tenant);
        var service = new PrivacyComplianceService(requests,
            new ClaimsComplianceAccessBoundary(tenant),
            [new TestProvider()], [new TestHandler()], new InMemoryLegalHoldStore(tenant));

        var export = await service.ExportAsync("user-1", principal);
        var request = await service.SubmitAsync("user-1", PrivacyRequestKind.Anonymization, principal);
        var manager = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim("permission", "compliance.manage")], "test"));
        var result = await service.ProcessAsync(request, manager);

        Assert.Single(export.Data);
        Assert.Equal(2, result.AffectedRecords);
        Assert.Equal(PrivacyRequestState.Completed, request.State);
    }

    [Fact]
    public void Compliance_registration_uses_scoped_application_boundaries()
    {
        using var provider = new ServiceCollection().AddAnvilCompliance().BuildServiceProvider();
        Assert.IsType<InMemoryConsentStore>(provider.GetRequiredService<IConsentStore>());
        Assert.IsType<ClaimsComplianceAccessBoundary>(provider.GetRequiredService<IComplianceAccessBoundary>());
    }

    private sealed class TestProvider : IPrivacyDataProvider
    {
        public string Name => "test";
        public Task<IReadOnlyCollection<PrivacyData>> ExportAsync(string subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<PrivacyData>>([
                new PrivacyData(Name, new Dictionary<string, object?> { ["subject"] = subjectId })]);
    }

    private sealed class TestHandler : IPrivacyErasureHandler
    {
        public string Name => "test";
        public Task<PrivacyErasureResult> EraseAsync(string subjectId, PrivacyRequestKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PrivacyErasureResult(2));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
