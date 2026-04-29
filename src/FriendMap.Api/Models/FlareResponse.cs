namespace FriendMap.Api.Models;

public class FlareResponse : BaseEntity
{
    public Guid FlareSignalId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
}
