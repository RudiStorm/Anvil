namespace Anvil;

public sealed class AnvilMailOptions
{
    public string? FileDirectory { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? SmtpUserName { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
}
