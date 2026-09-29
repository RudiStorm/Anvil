using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilTelemetry
{
    public const string SourceName = "Anvil";
    public const string MeterName = "Anvil";
    public const string CorrelationHeader = "X-Correlation-ID";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> BackgroundJobsEnqueued = Meter.CreateCounter<long>("anvil.background.jobs.enqueued");
    public static readonly Counter<long> BackgroundJobsCompleted = Meter.CreateCounter<long>("anvil.background.jobs.completed");
    public static readonly Counter<long> BackgroundJobsFailed = Meter.CreateCounter<long>("anvil.background.jobs.failed");

    public static string GetOrCreateCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[CorrelationHeader].FirstOrDefault();
        var correlationId = supplied is { Length: > 0 and <= 128 } && supplied.All(IsSafeCorrelationCharacter)
            ? supplied
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[CorrelationHeader] = correlationId;
        return correlationId;
    }

    public static Activity? StartBackgroundJob(string name, AnvilBackgroundJob job)
    {
        var activity = ActivitySource.StartActivity($"anvil.job {name}", ActivityKind.Consumer);
        activity?.SetTag("anvil.job.id", job.Id);
        activity?.SetTag("anvil.job.name", job.Name);
        activity?.SetTag("anvil.job.attempt", job.Attempts);
        activity?.SetTag("anvil.tenant.id", job.TenantId);
        activity?.SetTag("anvil.actor.id", job.ActorId);
        return activity;
    }

    private static bool IsSafeCorrelationCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '-' or '_' or '.';
}

public static class AnvilObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilObservability(this IServiceCollection services) => services;
}
