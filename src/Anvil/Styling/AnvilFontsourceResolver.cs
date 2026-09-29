namespace Anvil;

public sealed class AnvilFontsourceResolver
{
    public string ResolvePackage(string projectDirectory, string packageName)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageName);
        var path = Path.Combine(projectDirectory, "node_modules", "@fontsource", packageName);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Fontsource package was not found: {packageName}");
        return path;
    }
}
