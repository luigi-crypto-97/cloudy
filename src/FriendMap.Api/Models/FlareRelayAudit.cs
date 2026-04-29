namespace FriendMap.Api.Models;

public class FlareRelayAudit : BaseEntity
{
    public Guid FlareSignalId { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid TargetUserId { get; set; }
}
