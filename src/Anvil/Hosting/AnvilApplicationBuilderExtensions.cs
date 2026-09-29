using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilApplicationBuilderExtensions
{
    public static IApplicationBuilder UseAnvil(this IApplicationBuilder app)
    {
        app.UseAnvilObservability();
        app.UseMiddleware<AnvilDevelopmentRefreshMiddleware>();
        app.UseWebSockets();
        app.UseAntiforgery();
        return app.UseMiddleware<AnvilContextMiddleware>();
    }

    public static RazorComponentsEndpointConventionBuilder MapAnvil<TApp>(
        this IEndpointRouteBuilder endpoints)
        where TApp : IComponent
    {
        return endpoints.MapRazorComponents<TApp>();
    }
}
