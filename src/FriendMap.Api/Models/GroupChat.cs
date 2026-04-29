namespace FriendMap.Api.Models;

public class GroupChat : BaseEntity
{
    public Guid CreatedByUserId { get; set; }
    public Guid? VenueId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Kind { get; set; } = "group";
    public bool IsArchived { get; set; }
    public DateTimeOffset LastMessageAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
