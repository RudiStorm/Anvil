using System.Security.Cryptography;
using System.Text.Json;

namespace Anvil;

public sealed class AnvilAssetManifest
{
    private readonly IReadOnlyDictionary<string, string> assets;

    private AnvilAssetManifest(IReadOnlyDictionary<string, string> assets)
    {
        this.assets = assets;
    }

    public string Url(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return assets.TryGetValue(path.Replace('\\', '/'), out var hashed)
            ? "/" + hashed.TrimStart('/')
            : throw new FileNotFoundException($"Asset was not found in the manifest: {path}");
    }

    public static async Task<AnvilAssetManifest> BuildAsync(
        string sourceDirectory,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(outputDirectory);
        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, source).Replace('\\', '/');
            var bytes = await File.ReadAllBytesAsync(source, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant();
            var extension = Path.GetExtension(relative);
            var name = Path.GetFileNameWithoutExtension(relative);
            var directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? string.Empty;
            var hashedName = string.IsNullOrEmpty(directory)
                ? $"{name}.{hash}{extension}"
                : $"{directory}/{name}.{hash}{extension}";
            var destination = Path.Combine(outputDirectory, hashedName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
            mappings[relative] = hashedName;
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "anvil-assets.json"),
            JsonSerializer.Serialize(mappings),
            cancellationToken);
        return new AnvilAssetManifest(mappings);
    }
}
