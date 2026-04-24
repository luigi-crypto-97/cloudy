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
    public string? DiscoverablePhone { get; set; }
    public string? DiscoverableEmail { get; set; }
    public string? Bio { get; set; }
    public int? BirthYear { get; set; }
    public string Gender { get; set; } = "undisclosed";
    public List<string> Interests { get; set; } = new();
}

public sealed class UserSearchResult
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string RelationshipStatus { get; set; } = "none";
    public bool IsBlockedByViewer { get; set; }
    public bool HasBlockedViewer { get; set; }
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

public sealed class ContactMatchResult
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string RelationshipStatus { get; set; } = "none";
    public string MatchSource { get; set; } = "phone";
    public string? CurrentVenueName { get; set; }
    public string? CurrentVenueCategory { get; set; }
    public string StatusLabel { get; set; } = "Presente su Cloudy";
}

public sealed class UserRecap
{
    public string Period { get; set; } = "month";
    public DateTimeOffset RangeStartUtc { get; set; }
    public DateTimeOffset RangeEndUtc { get; set; }
    public int TotalCheckIns { get; set; }
    public int UniqueVenues { get; set; }
    public int HostedTables { get; set; }
    public int JoinedTables { get; set; }
    public int NightsOutEstimate { get; set; }
    public List<VenueRecapItem> TopVenues { get; set; } = new();
    public List<FriendRecapItem> TopPeople { get; set; } = new();
}

public sealed class VenueRecapItem
{
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Visits { get; set; }
}

public sealed class FriendRecapItem
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int SharedMoments { get; set; }
}
