namespace Anvil;

public sealed class AnvilFontsourceOptions
{
    public string Family { get; set; } = string.Empty;
    public IReadOnlyList<int> Weights { get; set; } = [400, 700];
    public string? PackageName { get; set; }
}
