namespace FriendMap.Mobile.Models;

public class AuthSession
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public AuthUser User { get; set; } = new();
}

public class AuthUser
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
