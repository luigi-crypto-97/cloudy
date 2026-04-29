namespace FriendMap.Api.Models;

public class DeepLinkToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public string LinkType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int MaxUses { get; set; } = 30;
    public int UseCount { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
