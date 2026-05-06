namespace FriendMap.Api.Models;

public class AppUser : BaseEntity
{
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AppleSubject { get; set; }
    public string? DiscoverablePhoneNormalized { get; set; }
    public string? DiscoverableEmailNormalized { get; set; }
    public string? Bio { get; set; }
    public int? BirthYear { get; set; }
    public string Gender { get; set; } = "undisclosed";
    public bool IsGhostModeEnabled { get; set; } = false;
    public bool SharePresenceWithFriends { get; set; } = true;
    public bool ShareIntentionsWithFriends { get; set; } = true;
    public string Status { get; set; } = "active";
    public ICollection<UserInterest> Interests { get; set; } = new List<UserInterest>();
}
