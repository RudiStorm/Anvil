using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace Anvil;

public static class AnvilStaticDirectoryExtensions
{
    public static IApplicationBuilder UseAnvilStaticDirectory(
        this IApplicationBuilder app,
        string directory,
        string requestPath = "/files")
    {
        var provider = new PhysicalFileProvider(Path.GetFullPath(directory));
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            RequestPath = requestPath
        });
    }
}
