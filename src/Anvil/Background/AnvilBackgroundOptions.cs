namespace Anvil;

public sealed class AnvilBackgroundOptions
{
    public string StorePath { get; set; } = ".anvil/jobs.json";
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool ExponentialBackoff { get; set; } = true;
}
