namespace Anvil;

public sealed record AnvilMailMessage(
    string From,
    string To,
    string Subject,
    string Html,
    string? Text = null)
{
    public IReadOnlyList<AnvilMailAttachment> Attachments { get; init; } = [];

    public string PlainText => Text ?? System.Text.RegularExpressions.Regex.Replace(Html, "<[^>]+>", string.Empty);
}
