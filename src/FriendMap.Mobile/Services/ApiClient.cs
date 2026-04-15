using System.Net.Http.Json;
using FriendMap.Mobile.Models;

namespace FriendMap.Mobile.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:8080/")
    };

    public async Task<List<VenueMarker>> GetVenueMarkersAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<VenueMarker>>(
            "api/venues/map?minLat=44.0&minLng=8.0&maxLat=46.0&maxLng=10.0");

        return result ?? new List<VenueMarker>();
    }
}
