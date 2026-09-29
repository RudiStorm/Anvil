using Microsoft.AspNetCore.Components;

namespace Anvil.Razor;

public static class AnvilMarkup
{
    public static MarkupString Trusted(string html) => new(html ?? string.Empty);
}
