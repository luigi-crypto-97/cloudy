namespace FriendMap.Mobile.Models;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
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
    public bool CanMessageDirectly { get; set; }
    public bool CanEditProfile { get; set; }
    public bool IsBlockedByViewer { get; set; }
    public bool HasBlockedViewer { get; set; }
    public List<string> Interests { get; set; } = new();
}

public sealed class EditableUserProfile
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int? BirthYear { get; set; }
    public string Gender { get; set; } = "undisclosed";
    public List<string> Interests { get; set; } = new();
}

public sealed class DirectMessageThreadSummary
{
    public Guid OtherUserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTimeOffset LastMessageAtUtc { get; set; }
}

public sealed class DirectMessagePeer
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsBlockedByViewer { get; set; }
    public bool HasBlockedViewer { get; set; }
}

public sealed class DirectMessageItem
{
    public Guid MessageId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }
    public bool IsMine { get; set; }
}

public sealed class DirectMessageThread
{
    public DirectMessagePeer OtherUser { get; set; } = new();
    public List<DirectMessageItem> Messages { get; set; } = new();
}
