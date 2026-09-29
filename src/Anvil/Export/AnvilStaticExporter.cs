namespace Anvil;

public sealed class AnvilStaticExportOptions
{
    public required string BaseUrl { get; init; }
    public required string OutputDirectory { get; init; }
    public string? AssetDirectory { get; init; }
    public bool ClearOutputDirectory { get; init; } = true;
}

public sealed class AnvilStaticExporter(HttpClient client)
{
    public async Task ExportAsync(
        IEnumerable<string> paths,
        AnvilStaticExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri(options.BaseUrl, UriKind.Absolute);
        if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("BaseUrl must use HTTP or HTTPS.", nameof(options));

        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        if (options.ClearOutputDirectory && Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, recursive: true);
        Directory.CreateDirectory(outputDirectory);

        var renderedPages = new List<(string Path, string Html)>();
        foreach (var path in paths)
        {
            var normalized = NormalizePath(path);
            using var response = await client.GetAsync(new Uri(baseUri, normalized), cancellationToken);
            response.EnsureSuccessStatusCode();
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Route is not a static HTML route: {path}");

            var output = string.IsNullOrEmpty(normalized)
                ? Path.Combine(outputDirectory, "index.html")
                : Path.Combine(outputDirectory, normalized.Replace('/', Path.DirectorySeparatorChar), "index.html");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            await File.WriteAllTextAsync(output, html, cancellationToken);
            renderedPages.Add((normalized, html));
        }

        if (options.AssetDirectory is not null && Directory.Exists(options.AssetDirectory))
        {
            CopyDirectory(options.AssetDirectory, Path.Combine(outputDirectory, "assets"));
            CopyReferencedAssets(renderedPages, options.AssetDirectory, outputDirectory);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        if (!Uri.TryCreate(path, UriKind.Relative, out var uri) || uri.IsAbsoluteUri)
            throw new ArgumentException($"Export path must be relative: {path}", nameof(path));

        var normalized = Uri.UnescapeDataString(path.Split(['?', '#'], 2)[0]).Trim('/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            throw new ArgumentException($"Export path cannot contain traversal segments: {path}", nameof(path));

        return normalized;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void CopyReferencedAssets(
        IEnumerable<(string Path, string Html)> pages,
        string assetDirectory,
        string outputDirectory)
    {
        var references = pages
            .SelectMany(page => System.Text.RegularExpressions.Regex.Matches(
                page.Html,
                "(?:src|href)=[\\\"']([^\\\"'#?]+)[\\\"']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(match => match.Groups[1].Value)
            .Where(reference => reference.StartsWith("/", StringComparison.Ordinal))
            .Select(reference => reference.TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in references)
        {
            var relative = reference.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                ? reference["assets/".Length..]
                : reference;
            var source = Path.GetFullPath(Path.Combine(assetDirectory, relative));
            var root = Path.GetFullPath(assetDirectory) + Path.DirectorySeparatorChar;
            if (!source.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
                continue;

            var destination = Path.Combine(outputDirectory, "assets", relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }
}
