namespace FriendMap.Mobile.Models;

public class VenueMarker
{
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int PeopleEstimate { get; set; }
    public string DensityLevel { get; set; } = "unknown";
    public int BubbleIntensity { get; set; }
    public bool DemographicDataAvailable { get; set; }
    public int ActiveCheckIns { get; set; }
    public int ActiveIntentions { get; set; }
    public int OpenTables { get; set; }
    public List<PresencePreview> PresencePreview { get; set; } = new();
}

public class PresencePreview
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
