namespace Anvil;

public static class AnvilMailPreview
{
    public static string Render(AnvilMailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return $"<!doctype html><html><head><title>{System.Net.WebUtility.HtmlEncode(message.Subject)}</title></head><body>{message.Html}</body></html>";
    }
}
