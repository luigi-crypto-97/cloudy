namespace FriendMap.Api.Services;

public class OverpassOptions
{
    public string BaseUrl { get; set; } = "https://overpass-api.de/api/";
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    public int TimeoutSeconds { get; set; } = 25;
}
