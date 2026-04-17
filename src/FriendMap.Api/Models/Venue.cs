using NetTopologySuite.Geometries;

namespace FriendMap.Api.Models;

public class Venue : BaseEntity
{
    public string ExternalProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "bar";
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "IT";
    public string? PhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? HoursSummary { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public string? TagsCsv { get; set; }
    public Point? Location { get; set; }
    public bool IsClaimed { get; set; } = false;
    public string VisibilityStatus { get; set; } = "public";
}
