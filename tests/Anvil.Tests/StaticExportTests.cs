using Anvil;
using System.Net;
using System.Net.Http.Headers;

namespace Anvil.Tests;

public sealed class StaticExportTests
{
    [Fact]
    public async Task Export_creates_clean_deployable_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-export", Guid.NewGuid().ToString("N"));
        var sourceAssets = Path.Combine(root, "source-assets");
        var output = Path.Combine(root, "site");
        Directory.CreateDirectory(sourceAssets);
        Directory.CreateDirectory(Path.Combine(output, "old"));
        await File.WriteAllTextAsync(Path.Combine(output, "old", "stale.html"), "stale");
        await File.WriteAllTextAsync(Path.Combine(sourceAssets, "app.css"), "body{}");

        try
        {
            using var client = new HttpClient(new StaticResponseHandler
            {
                ["/"] = "<link href=\"/app.css\"><h1>Home</h1>",
                ["/docs/getting-started"] = "<h1>Docs</h1>"
            }) { BaseAddress = new Uri("https://example.test") };
            var exporter = new AnvilStaticExporter(client);

            await exporter.ExportAsync(
                ["/", "/docs/getting-started?preview=true"],
                new AnvilStaticExportOptions
                {
                    BaseUrl = "https://example.test",
                    OutputDirectory = output,
                    AssetDirectory = sourceAssets
                });

            Assert.Equal("<link href=\"/app.css\"><h1>Home</h1>", await File.ReadAllTextAsync(Path.Combine(output, "index.html")));
            Assert.Equal("<h1>Docs</h1>", await File.ReadAllTextAsync(Path.Combine(output, "docs", "getting-started", "index.html")));
            Assert.True(File.Exists(Path.Combine(output, "assets", "app.css")));
            Assert.False(File.Exists(Path.Combine(output, "old", "stale.html")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Export_rejects_path_traversal()
    {
        using var client = new HttpClient(new StaticResponseHandler());
        var exporter = new AnvilStaticExporter(client);
        var output = Path.Combine(Path.GetTempPath(), "anvil-export", Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportAsync(
            ["/../secret"],
            new AnvilStaticExportOptions { BaseUrl = "https://example.test", OutputDirectory = output }));
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> responses = new(StringComparer.OrdinalIgnoreCase);

        public string this[string path]
        {
            init => responses[path] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "/";
            if (!responses.TryGetValue(path, out var content))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("text/html") }
                }
            });
        }
    }
}
