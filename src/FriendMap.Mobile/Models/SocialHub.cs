namespace FriendMap.Mobile.Models;

public sealed class SocialHub
{
    public List<SocialConnection> Friends { get; set; } = new();
    public List<SocialConnection> IncomingRequests { get; set; } = new();
    public List<SocialConnection> OutgoingRequests { get; set; } = new();
    public List<SocialTableInvite> TableInvites { get; set; } = new();
}

public sealed class SocialConnection
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string RelationshipStatus { get; set; } = "none";
    public int MutualFriendsCount { get; set; }
    public string PresenceState { get; set; } = "idle";
    public string StatusLabel { get; set; } = "Nessuna presenza live";
    public string? CurrentVenueName { get; set; }
    public string? CurrentVenueCategory { get; set; }
}

public sealed class SocialTableInvite
{
    public Guid TableId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueCategory { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public string HostNickname { get; set; } = string.Empty;
    public string? HostDisplayName { get; set; }
    public string? HostAvatarUrl { get; set; }
}
