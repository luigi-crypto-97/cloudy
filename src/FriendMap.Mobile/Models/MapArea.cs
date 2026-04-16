namespace FriendMap.Mobile.Models;

public sealed class MapArea
{
    public string AreaId { get; set; } = string.Empty;
    public string Label { get; set; } = "Area live";
    public double CentroidLatitude { get; set; }
    public double CentroidLongitude { get; set; }
    public int PeopleCount { get; set; }
    public string DensityLevel { get; set; } = "unknown";
    public int BubbleIntensity { get; set; }
    public int VenueCount { get; set; }
    public int ActiveCheckIns { get; set; }
    public int ActiveIntentions { get; set; }
    public int OpenTables { get; set; }
    public int PresenceCount { get; set; }
    public List<Guid> VenueIds { get; set; } = new();
    public List<MapCoordinate> Polygon { get; set; } = new();
    public List<PresencePreview> PresencePreview { get; set; } = new();

    public bool IsCluster => VenueCount > 1;
}

public sealed class MapCoordinate
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class VenueMapLayer
{
    public List<VenueMarker> Markers { get; set; } = new();
    public List<MapArea> Areas { get; set; } = new();
}
