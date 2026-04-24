namespace FriendMap.Mobile.Models;

public class NearbyUser
{
    public Guid UserId { get; set; }
    public string? Nickname { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CurrentVenueName { get; set; }
}
