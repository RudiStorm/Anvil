namespace Anvil;

public sealed class AnvilSessionOptions
{
    public string CookieName { get; set; } = "anvil.session";

    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(8);

    public TimeSpan AbsoluteTimeout { get; set; } = TimeSpan.FromDays(30);
}
