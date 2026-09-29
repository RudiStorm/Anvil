namespace Anvil.Razor;

public sealed class AnvilIconCatalog
{
    private readonly Dictionary<string, string> paths = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(path);
        paths[name] = path;
    }

    public bool TryGet(string name, out string? path) => paths.TryGetValue(name, out path);
}
