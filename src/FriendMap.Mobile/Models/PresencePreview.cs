using System.Text.Json.Serialization;

namespace FriendMap.Mobile.Models;

public class PresencePreview
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}