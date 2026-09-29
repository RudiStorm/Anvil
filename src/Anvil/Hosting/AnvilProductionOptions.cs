namespace Anvil;

public sealed class AnvilProductionOptions
{
    public string DataProtectionKeysPath { get; set; } = "data-protection-keys";
    public string LivenessPath { get; set; } = "/health/live";
    public string ReadinessPath { get; set; } = "/health/ready";
    public bool UseHttpsRedirection { get; set; } = true;
    public bool UseHsts { get; set; } = true;
    public bool UseForwardedHeaders { get; set; } = true;
    public IList<string> TrustedProxyAddresses { get; } = [];
}
