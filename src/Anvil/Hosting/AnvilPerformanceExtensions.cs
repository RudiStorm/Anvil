using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilPerformanceExtensions
{
    public static IServiceCollection AddAnvilPerformance(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.AddRateLimiter(options => options.AddFixedWindowLimiter("anvil", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        }));
        return services;
    }

    public static IApplicationBuilder UseAnvilPerformance(this IApplicationBuilder app)
    {
        app.UseResponseCompression();
        app.UseRateLimiter();
        return app;
    }
}
