namespace Anvil;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AnvilRouteAttribute(string template) : Attribute
{
    public string Template { get; } = string.IsNullOrWhiteSpace(template)
        ? throw new ArgumentException("A route template is required.", nameof(template))
        : template;

    public string? Name { get; init; }
}
