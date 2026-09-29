using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Anvil;

public static class AnvilReadinessExtensions
{
    public const string ProductionFoundation = "production-foundation";
    public const string DevelopmentAdapter = "development-adapter";
    public const string ExampleOnly = "example-only";
    public static IServiceCollection AddAnvilReadiness(this IServiceCollection services, Action<HealthCheckBuilder>? configure = null)
    { var builder = services.AddHealthChecks(); configure?.Invoke(new HealthCheckBuilder(builder)); return services; }
    public static IEndpointConventionBuilder MapAnvilReadiness(this IEndpointRouteBuilder endpoints, string pattern = "/health/ready") => endpoints.MapHealthChecks(pattern, new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
}

public sealed class HealthCheckBuilder(IHealthChecksBuilder builder)
{
    public HealthCheckBuilder AddReadyCheck(string name, IHealthCheck check, string label = AnvilReadinessExtensions.ProductionFoundation) { builder.AddCheck(name, check, tags: ["ready", label]); return this; }
    public HealthCheckBuilder AddLiveCheck(string name, IHealthCheck check) { builder.AddCheck(name, check, tags: ["live"]); return this; }
}
