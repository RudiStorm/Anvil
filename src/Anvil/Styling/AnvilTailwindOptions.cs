namespace Anvil;

public sealed class AnvilTailwindOptions
{
    public string Input { get; set; } = "styles.css";
    public string Output { get; set; } = "wwwroot/app.css";
    public IList<string> ContentGlobs { get; } = ["Components/**/*.razor", "wwwroot/**/*.html"];
}
