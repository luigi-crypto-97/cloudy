namespace FriendMap.Api.Data;

public class JwtOptions
{
    public string Issuer { get; set; } = "FriendMap.Dev";
    public string Audience { get; set; } = "FriendMap.Mobile";
    public string SigningKey { get; set; } = "friendmap-dev-signing-key-change-before-production-32chars";
    public int AccessTokenMinutes { get; set; } = 10080;
}
