using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;

namespace Anvil;

public static class AnvilAssetHostingExtensions
{
    public static IApplicationBuilder UseAnvilAssets(this IApplicationBuilder app)
    {
        return app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                var name = context.File.Name;
                context.Context.Response.Headers.CacheControl = name.Contains('.', StringComparison.Ordinal)
                    && name.Split('.').Length >= 3
                    ? "public,max-age=31536000,immutable"
                    : "public,max-age=3600";
            }
        });
    }
}
