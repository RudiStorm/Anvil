using Microsoft.AspNetCore.Builder;

namespace Anvil;

public static class AnvilObservabilityApplicationBuilderExtensions
{
    public static IApplicationBuilder UseAnvilObservability(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var correlationId = AnvilTelemetry.GetOrCreateCorrelationId(context);
            using var activity = AnvilTelemetry.ActivitySource.StartActivity("anvil.request", System.Diagnostics.ActivityKind.Server);
            activity?.SetTag("anvil.correlation_id", correlationId);
            await next(context);
        });
    }
}
