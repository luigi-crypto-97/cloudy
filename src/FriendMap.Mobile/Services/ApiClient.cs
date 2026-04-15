using System.Net.Http.Json;
using System.Net.Http.Headers;
using FriendMap.Mobile.Models;

namespace FriendMap.Mobile.Services;

public class ApiClient
{
    private const string AccessTokenKey = "friendmap_access_token";
    private const string UserIdKey = "friendmap_user_id";
    private const string NicknameKey = "friendmap_nickname";

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:8080/")
    };

    public Guid? CurrentUserId { get; private set; }

    public async Task<AuthSession> DevLoginAsync(string nickname, string? displayName = null)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/dev-login",
            new DevLoginRequest(nickname, displayName));
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSession>()
            ?? throw new InvalidOperationException("Empty auth response.");

        await SecureStorage.Default.SetAsync(AccessTokenKey, session.AccessToken);
        await SecureStorage.Default.SetAsync(UserIdKey, session.User.UserId.ToString());
        await SecureStorage.Default.SetAsync(NicknameKey, session.User.Nickname);
        ApplySession(session.AccessToken, session.User.UserId);
        return session;
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var token = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var userIdValue = await SecureStorage.Default.GetAsync(UserIdKey);

        if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdValue, out var userId))
        {
            return false;
        }

        ApplySession(token, userId);
        return true;
    }

    public async Task<List<VenueMarker>> GetVenueMarkersAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<VenueMarker>>(
            "api/venues/map?minLat=44.0&minLng=8.0&maxLat=46.0&maxLng=10.0");

        return result ?? new List<VenueMarker>();
    }

    public async Task CheckInAsync(Guid userId, Guid venueId, int ttlMinutes = 180)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/check-ins",
            new CreateCheckInRequest(userId, venueId, ttlMinutes));

        response.EnsureSuccessStatusCode();
    }

    public async Task CreateIntentionAsync(Guid userId, Guid venueId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, string? note)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/intentions",
            new CreateIntentionRequest(userId, venueId, startsAtUtc, endsAtUtc, note));

        response.EnsureSuccessStatusCode();
    }

    public async Task CreateSocialTableAsync(Guid hostUserId, Guid venueId, string title, string? description, DateTimeOffset startsAtUtc, int capacity, string joinPolicy)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/tables",
            new CreateSocialTableRequest(hostUserId, venueId, title, description, startsAtUtc, capacity, joinPolicy));

        response.EnsureSuccessStatusCode();
    }

    public async Task RegisterDeviceTokenAsync(Guid userId, string deviceToken)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/notifications/device-tokens",
            new RegisterDeviceTokenRequest(userId, "ios", deviceToken));

        response.EnsureSuccessStatusCode();
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (CurrentUserId is not null)
        {
            return;
        }

        if (!await TryRestoreSessionAsync())
        {
            await DevLoginAsync("giulia", "Giulia Dev");
        }
    }

    private void ApplySession(string accessToken, Guid userId)
    {
        CurrentUserId = userId;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private record DevLoginRequest(string Nickname, string? DisplayName);

    private record CreateCheckInRequest(Guid UserId, Guid VenueId, int TtlMinutes);

    private record CreateIntentionRequest(Guid UserId, Guid VenueId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string? Note);

    private record CreateSocialTableRequest(Guid HostUserId, Guid VenueId, string Title, string? Description, DateTimeOffset StartsAtUtc, int Capacity, string JoinPolicy);

    private record RegisterDeviceTokenRequest(Guid UserId, string Platform, string DeviceToken);
}
