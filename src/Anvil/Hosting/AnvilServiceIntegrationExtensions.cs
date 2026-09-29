using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Anvil;

public static class AnvilServiceIntegrationExtensions
{
    public static IApplicationBuilder UseAnvilService(
        this IApplicationBuilder app,
        PathString path,
        RequestDelegate service)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(service);
        return app.Map(path, branch => branch.Run(service));
    }
}
