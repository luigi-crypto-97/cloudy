namespace FriendMap.Api.Data;

public class ApnsOptions
{
    public bool Enabled { get; set; }
    public bool UseSandbox { get; set; } = true;
    public string TeamId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string BundleId { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;

    public string Host => UseSandbox
        ? "https://api.sandbox.push.apple.com"
        : "https://api.push.apple.com";
}
