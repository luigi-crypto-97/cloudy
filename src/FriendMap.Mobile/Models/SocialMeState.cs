namespace FriendMap.Mobile.Models;

public sealed class SocialMeState
{
    public bool IsGhostModeEnabled { get; set; }
    public bool SharePresenceWithFriends { get; set; } = true;
    public bool ShareIntentionsWithFriends { get; set; } = true;
    public Guid? ActiveCheckInVenueId { get; set; }
    public string? ActiveCheckInVenueName { get; set; }
    public Guid? ActiveIntentionVenueId { get; set; }
    public string? ActiveIntentionVenueName { get; set; }
}

public sealed class SocialTableSummary
{
    public Guid TableId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueCategory { get; set; } = string.Empty;
    public string JoinPolicy { get; set; } = "approval";
    public string JoinPolicyLabel => JoinPolicy == "auto" ? "Aperto" : "Su approvazione";
    public bool IsHost { get; set; }
    public string MembershipStatus { get; set; } = "none";
    public int Capacity { get; set; }
    public int RequestedCount { get; set; }
    public int AcceptedCount { get; set; }
    public int InvitedCount { get; set; }
}

public sealed class SocialTableRequest
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "requested";
}

public sealed class SocialTableMessage
{
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }
    public bool IsMine { get; set; }
}

public sealed class SocialTableThread
{
    public SocialTableSummary Table { get; set; } = new();
    public List<SocialTableRequest> Requests { get; set; } = new();
    public List<SocialTableMessage> Messages { get; set; } = new();
}
