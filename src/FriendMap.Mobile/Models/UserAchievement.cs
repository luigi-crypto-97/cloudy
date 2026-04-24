namespace FriendMap.Mobile.Models;

public class UserAchievement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BadgeCode { get; set; } = string.Empty;
    public DateTimeOffset EarnedAtUtc { get; set; }
}
