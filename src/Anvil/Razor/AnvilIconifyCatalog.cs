using System.Text.Json;

namespace Anvil.Razor;

public sealed class AnvilIconifyCatalog
{
    private readonly Dictionary<string, string> icons = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, string svg) => icons[name] = svg;
    public bool TryGet(string name, out string? svg) => icons.TryGetValue(name, out svg);

    public static AnvilIconifyCatalog Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var catalog = new AnvilIconifyCatalog();
        if (document.RootElement.TryGetProperty("icons", out var entries))
            foreach (var entry in entries.EnumerateObject())
                if (entry.Value.TryGetProperty("body", out var body)) catalog.Register(entry.Name, body.GetString() ?? string.Empty);
        return catalog;
    }
}
