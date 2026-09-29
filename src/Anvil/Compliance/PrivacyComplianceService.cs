namespace Anvil;

public sealed class PrivacyComplianceService(
    IPrivacyRequestStore requests,
    IComplianceAccessBoundary access,
    IEnumerable<IPrivacyDataProvider> providers,
    IEnumerable<IPrivacyErasureHandler> handlers,
    ILegalHoldStore holds)
{
    public async Task<PrivacyRequest> SubmitAsync(string subjectId, PrivacyRequestKind kind,
        System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        await access.DemandAsync(subjectId, ComplianceOperation.Submit, principal, cancellationToken);
        if (kind is PrivacyRequestKind.Deletion or PrivacyRequestKind.Anonymization
            && await holds.HasActiveHoldAsync(subjectId, cancellationToken))
            throw new InvalidOperationException("An active legal hold prevents this request from being processed.");
        return await requests.AddAsync(new PrivacyRequest { SubjectId = subjectId, Kind = kind }, cancellationToken);
    }

    public async Task<PrivacyExport> ExportAsync(string subjectId, System.Security.Claims.ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        await access.DemandAsync(subjectId, ComplianceOperation.Read, principal, cancellationToken);
        var data = (await Task.WhenAll(providers.Select(x => x.ExportAsync(subjectId, cancellationToken)))).SelectMany(x => x).ToArray();
        return new PrivacyExport(data);
    }

    public async Task<PrivacyErasureResult> ProcessAsync(PrivacyRequest request, System.Security.Claims.ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        await access.DemandAsync(request.SubjectId, ComplianceOperation.Process, principal, cancellationToken);
        if (request.State != PrivacyRequestState.Pending)
            throw new InvalidOperationException("Only pending privacy requests can be processed.");
        if (request.Kind is PrivacyRequestKind.Deletion or PrivacyRequestKind.Anonymization
            && await holds.HasActiveHoldAsync(request.SubjectId, cancellationToken))
            throw new InvalidOperationException("An active legal hold prevents this request from being processed.");
        request.State = PrivacyRequestState.Processing;
        await requests.UpdateAsync(request, cancellationToken);
        var results = await Task.WhenAll(handlers.Select(x => x.EraseAsync(request.SubjectId, request.Kind, cancellationToken)));
        request.State = PrivacyRequestState.Completed;
        request.CompletedAtUtc = DateTimeOffset.UtcNow;
        await requests.UpdateAsync(request, cancellationToken);
        return new PrivacyErasureResult(results.Sum(x => x.AffectedRecords), results.Any(x => x.Deferred));
    }
}
