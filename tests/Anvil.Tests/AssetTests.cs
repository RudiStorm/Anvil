using Anvil;

namespace Anvil.Tests;

public sealed class AssetTests
{
    [Fact]
    public async Task Asset_manifest_hashes_files_and_resolves_urls()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-assets", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "app.css"), "body{}");
        try
        {
            var manifest = await AnvilAssetManifest.BuildAsync(root, output);
            var url = manifest.Url("app.css");
            Assert.StartsWith("/app.", url);
            Assert.EndsWith(".css", url);
            Assert.True(File.Exists(Path.Combine(output, "anvil-assets.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
