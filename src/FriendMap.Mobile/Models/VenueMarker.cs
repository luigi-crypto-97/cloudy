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
}
