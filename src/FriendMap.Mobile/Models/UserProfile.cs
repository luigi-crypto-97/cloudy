namespace FriendMap.Mobile.Models;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int? BirthYear { get; set; }
    public string Gender { get; set; } = "undisclosed";
    public bool IsFriend { get; set; }
    public string RelationshipStatus { get; set; } = "none";
    public int MutualFriendsCount { get; set; }
    public int FriendsCount { get; set; }
    public string PresenceState { get; set; } = "idle";
    public string StatusLabel { get; set; } = "Nessuna presenza live";
    public string? CurrentVenueName { get; set; }
    public string? CurrentVenueCategory { get; set; }
    public bool CanInviteToTable { get; set; }
}
