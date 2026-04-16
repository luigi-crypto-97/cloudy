using System.Net.Http.Json;
using System.Net.Http.Headers;
using FriendMap.Mobile.Models;
using Microsoft.Maui.Devices;

namespace FriendMap.Mobile.Services;

public class ApiClient
{
    private const string AccessTokenKey = "friendmap_access_token";
    private const string UserIdKey = "friendmap_user_id";
    private const string NicknameKey = "friendmap_nickname";
    private const string ApiBaseUrlKey = "friendmap_api_base_url";
    private const string DefaultApiBaseUrl = "http://127.0.0.1:8080/";

    private HttpClient _httpClient;
    private string? _currentAccessToken;

    public Guid? CurrentUserId { get; private set; }

    public ApiClient()
    {
        _httpClient = CreateHttpClient(new Uri(GetConfiguredApiBaseUrl()));
    }

    public string GetConfiguredApiBaseUrl()
    {
        return Preferences.Default.Get(ApiBaseUrlKey, DefaultApiBaseUrl);
    }

    public void ConfigureApiBaseUrl(string apiBaseUrl)
    {
        var normalized = NormalizeApiBaseUrl(apiBaseUrl);
        Preferences.Default.Set(ApiBaseUrlKey, normalized);
        var nextBaseAddress = new Uri(normalized);
        if (_httpClient.BaseAddress == nextBaseAddress)
        {
            return;
        }

        var previousClient = _httpClient;
        _httpClient = CreateHttpClient(nextBaseAddress);
        previousClient.Dispose();
    }

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

    public async Task<Guid> GetCurrentUserIdAsync()
    {
        await EnsureAuthenticatedAsync();
        return CurrentUserId ?? throw new InvalidOperationException("Missing authenticated user.");
    }

    public async Task<List<VenueMarker>> GetVenueMarkersAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<VenueMarker>>(
            "api/venues/map?minLat=44.0&minLng=8.0&maxLat=46.0&maxLng=10.0");

        return result ?? new List<VenueMarker>();
    }

    public async Task<ServiceHealthResponse> GetHealthAsync()
    {
        var response = await _httpClient.GetAsync("health");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServiceHealthResponse>()
            ?? throw new InvalidOperationException("Health response vuota.");
    }

    public async Task<bool> CanReachBackendAsync()
    {
        try
        {
            await GetHealthAsync();
            return true;
        }
        catch
        {
            return false;
        }
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
#if !FRIENDMAP_APNS_ENABLED
        await Task.CompletedTask;
        return;
#else
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/notifications/device-tokens",
            new RegisterDeviceTokenRequest(userId, "ios", deviceToken));

        response.EnsureSuccessStatusCode();
#endif
    }

    public async Task RegisterStoredDeviceTokenAsync()
    {
#if !FRIENDMAP_APNS_ENABLED
        await Task.CompletedTask;
        return;
#else
        await EnsureAuthenticatedAsync();
        var deviceToken = ApnsDeviceTokenStore.CurrentToken;
        if (string.IsNullOrWhiteSpace(deviceToken) || CurrentUserId is not Guid userId)
        {
            return;
        }

        await RegisterDeviceTokenAsync(userId, deviceToken);
#endif
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
        _currentAccessToken = accessToken;
        CurrentUserId = userId;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private HttpClient CreateHttpClient(Uri baseAddress)
    {
        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(8)
        };

        if (!string.IsNullOrWhiteSpace(_currentAccessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _currentAccessToken);
        }

        return client;
    }

    private static string NormalizeApiBaseUrl(string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException("Inserisci un backend URL valido.");
        }

        var normalized = apiBaseUrl.Trim();
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"http://{normalized}";
        }

        if (!normalized.EndsWith('/'))
        {
            normalized = $"{normalized}/";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Backend URL non valido.");
        }

        return uri.ToString();
    }

    public string DescribeException(Exception ex)
    {
        if (DeviceInfo.Current.DeviceType == DeviceType.Physical &&
            _httpClient.BaseAddress is not null &&
            (_httpClient.BaseAddress.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             _httpClient.BaseAddress.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return "Su iPhone localhost punta al telefono. Usa l'IP del Mac come Backend URL.";
        }

        return ex switch
        {
            TaskCanceledException => $"Timeout verso {_httpClient.BaseAddress}. Controlla rete e backend.",
            HttpRequestException => $"Backend non raggiungibile su {_httpClient.BaseAddress}.",
            InvalidOperationException => ex.Message,
            _ => ex.Message
        };
    }

    private record DevLoginRequest(string Nickname, string? DisplayName);

    private record CreateCheckInRequest(Guid UserId, Guid VenueId, int TtlMinutes);

    private record CreateIntentionRequest(Guid UserId, Guid VenueId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string? Note);

    private record CreateSocialTableRequest(Guid HostUserId, Guid VenueId, string Title, string? Description, DateTimeOffset StartsAtUtc, int Capacity, string JoinPolicy);

    private record RegisterDeviceTokenRequest(Guid UserId, string Platform, string DeviceToken);

    public sealed record ServiceHealthResponse(string Status, DateTimeOffset Utc);
}
