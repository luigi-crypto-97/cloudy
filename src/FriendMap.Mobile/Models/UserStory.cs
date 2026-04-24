namespace FriendMap.Mobile.Models;

public class UserStory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? MediaUrl { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
