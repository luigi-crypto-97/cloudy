namespace FriendMap.Api.Services;

public class FoursquareOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://places-api.foursquare.com/";
    public string SearchPath { get; set; } = "places/search";
    public string AuthorizationScheme { get; set; } = "Bearer";
    public string ApiVersion { get; set; } = "2025-02-05";
}
