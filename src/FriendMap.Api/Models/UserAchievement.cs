namespace FriendMap.Api.Models;

public class UserAchievement : BaseEntity
{
    public Guid UserId { get; set; }
    public string BadgeCode { get; set; } = string.Empty;
    public DateTimeOffset EarnedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
