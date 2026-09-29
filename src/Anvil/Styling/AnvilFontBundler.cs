using System.Text;

namespace Anvil;

public sealed class AnvilFontBundler
{
    public async Task<string> BundleAsync(string sourceDirectory, string outputDirectory, string family, IReadOnlyDictionary<int, string>? weights = null, CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(sourceDirectory, "*.woff2", SearchOption.TopDirectoryOnly).ToArray();
        if (files.Length == 0) throw new FileNotFoundException("No .woff2 font files were found.", sourceDirectory);
        Directory.CreateDirectory(outputDirectory);
        var css = new StringBuilder();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(outputDirectory, fileName), overwrite: true);
            var weight = weights?.FirstOrDefault(item => fileName.Contains(item.Value, StringComparison.OrdinalIgnoreCase)).Key ?? (fileName.Contains("bold", StringComparison.OrdinalIgnoreCase) ? 700 : 400);
            css.Append("@font-face{font-family:'").Append(family.Replace("'", "\\'"))
                .Append("';font-style:normal;font-weight:").Append(weight)
                .Append(";font-display:swap;src:url('").Append(fileName).Append("') format('woff2');}\n");
        }
        var output = Path.Combine(outputDirectory, "fonts.css");
        await File.WriteAllTextAsync(output, css.ToString(), cancellationToken);
        return output;
    }
}
