namespace FriendMap.Api.Data;

public sealed class AppleAuthOptions
{
    public string Issuer { get; set; } = "https://appleid.apple.com";
    public string Audience { get; set; } = "it.luiginegri.FriendMapSeed";
    public string JwksUrl { get; set; } = "https://appleid.apple.com/auth/keys";
}
