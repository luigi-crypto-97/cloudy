using System.Text.Json.Serialization;

namespace FriendMap.Mobile.Models;

public class VenueMarker
{
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? HoursSummary { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsOpenNow { get; set; }
    public int PeopleEstimate { get; set; }
    public string DensityLevel { get; set; } = "unknown";
    public int BubbleIntensity { get; set; }
    public bool DemographicDataAvailable { get; set; }
    public int ActiveCheckIns { get; set; }
    public int ActiveIntentions { get; set; }
    public int OpenTables { get; set; }
    public List<PresencePreview> PresencePreview { get; set; } = new();
}

public class VenueDetails
{
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? HoursSummary { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public string DensityLevel { get; set; } = "unknown";
    public int PeopleEstimate { get; set; }
    public bool DemographicDataAvailable { get; set; }
    public object? AgeDistribution { get; set; }
    public object? GenderDistribution { get; set; }
    public List<IntentionWindow> IntentionWindows { get; set; } = new();
    public List<SocialTableSummary> UpcomingTables { get; set; } = new();
    public List<AffluenceTrendPoint> AffluenceTrends { get; set; } = new();
}

public class IntentionWindow
{
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int Count { get; set; }
}

public class AffluenceTrendPoint
{
    public DateTime BucketStartUtc { get; set; }
    public int PeopleEstimate { get; set; }
    public string DensityLevel { get; set; } = string.Empty;
    public int CheckInCount { get; set; }
    public int IntentionCount { get; set; }
    public int TableCount { get; set; }
}
