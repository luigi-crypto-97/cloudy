using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Globalization;
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

    public Task<List<VenueMarker>> GetVenueMarkersAsync()
    {
        return GetVenueMarkersAsync(MapViewport.MilanDefault);
    }

    public async Task<List<VenueMarker>> GetVenueMarkersAsync(MapViewport viewport)
    {
        var layer = await GetMapLayerAsync(viewport);
        return layer.Markers;
    }

    public async Task<VenueMapLayer> GetMapLayerAsync(MapViewport viewport)
    {
        return await GetMapLayerAsync(viewport, null, null, false, null);
    }

    public async Task<VenueMapLayer> GetMapLayerAsync(
        MapViewport viewport,
        string? query,
        string? category,
        bool openNowOnly,
        double? maxDistanceKm)
    {
        var queryViewport = viewport.Normalize();
        var parameters = new List<string>
        {
            $"minLat={queryViewport.MinLatitude.ToString(CultureInfo.InvariantCulture)}",
            $"minLng={queryViewport.MinLongitude.ToString(CultureInfo.InvariantCulture)}",
            $"maxLat={queryViewport.MaxLatitude.ToString(CultureInfo.InvariantCulture)}",
            $"maxLng={queryViewport.MaxLongitude.ToString(CultureInfo.InvariantCulture)}"
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            parameters.Add($"q={Uri.EscapeDataString(query.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            parameters.Add($"category={Uri.EscapeDataString(category.Trim())}");
        }

        if (openNowOnly)
        {
            parameters.Add("openNow=true");
        }

        if (maxDistanceKm is > 0)
        {
            parameters.Add($"centerLat={queryViewport.CenterLatitude.ToString(CultureInfo.InvariantCulture)}");
            parameters.Add($"centerLng={queryViewport.CenterLongitude.ToString(CultureInfo.InvariantCulture)}");
            parameters.Add($"maxDistanceKm={maxDistanceKm.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        var response = await _httpClient.GetAsync(
            $"api/venues/map-layer?{string.Join("&", parameters)}");
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<VenueMapLayer>()
            ?? new VenueMapLayer();
    }

    public async Task<UserProfile> GetUserProfileAsync(Guid userId)
    {
        var response = await _httpClient.GetAsync($"api/users/{userId}");
        await EnsureSuccessAsync(response);
        var profile = await response.Content.ReadFromJsonAsync<UserProfile>();
        return profile ?? throw new InvalidOperationException("Profilo utente non disponibile.");
    }

    public async Task<EditableUserProfile> GetMyProfileAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/users/me/profile");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<EditableUserProfile>()
            ?? throw new InvalidOperationException("Profilo personale non disponibile.");
    }

    public async Task<EditableUserProfile> UpdateMyProfileAsync(
        string? displayName,
        string? avatarUrl,
        string? bio,
        int? birthYear,
        string? gender,
        IEnumerable<string> interests)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/profile",
            new UpdateMyProfileRequestDto(displayName, avatarUrl, bio, birthYear, gender, interests.ToList()));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<EditableUserProfile>()
            ?? throw new InvalidOperationException("Profilo aggiornato non disponibile.");
    }

    public async Task<List<DirectMessageThreadSummary>> GetDirectMessageInboxAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/messages/threads");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<DirectMessageThreadSummary>>() ?? new List<DirectMessageThreadSummary>();
    }

    public async Task<DirectMessageThread> GetDirectMessageThreadAsync(Guid otherUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync($"api/messages/threads/{otherUserId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<DirectMessageThread>() ?? new DirectMessageThread();
    }

    public async Task SendDirectMessageAsync(Guid otherUserId, string body)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            $"api/messages/threads/{otherUserId}",
            new SendDirectMessageRequestDto(body));
        await EnsureSuccessAsync(response);
    }

    public async Task BlockUserAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/safety/blocks/{targetUserId}", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task UnblockUserAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.DeleteAsync($"api/safety/blocks/{targetUserId}");
        await EnsureSuccessAsync(response);
    }

    public async Task ReportUserAsync(Guid reportedUserId, string reasonCode, string? details)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/safety/reports/user",
            new CreateUserReportRequestDto(reportedUserId, reasonCode, details));
        await EnsureSuccessAsync(response);
    }

    public async Task ReportTableAsync(Guid tableId, string reasonCode, string? details)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/safety/reports/table",
            new CreateTableReportRequestDto(tableId, reasonCode, details));
        await EnsureSuccessAsync(response);
    }

    public async Task<SocialHub> GetSocialHubAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/social/hub");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SocialHub>() ?? new SocialHub();
    }

    public async Task<SocialMeState> GetSocialMeStateAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/social/me/state");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SocialMeState>() ?? new SocialMeState();
    }

    public async Task UpdatePrivacySettingsAsync(bool? ghostMode, bool? sharePresence, bool? shareIntentions)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/me/privacy",
            new UpdatePrivacySettingsRequest(ghostMode, sharePresence, shareIntentions));
        await EnsureSuccessAsync(response);
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

        await EnsureSuccessAsync(response);
    }

    public async Task CreateIntentionAsync(Guid userId, Guid venueId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, string? note)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/intentions",
            new CreateIntentionRequest(userId, venueId, startsAtUtc, endsAtUtc, note));

        await EnsureSuccessAsync(response);
    }

    public async Task CreateSocialTableAsync(Guid hostUserId, Guid venueId, string title, string? description, DateTimeOffset startsAtUtc, int capacity, string joinPolicy)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/tables",
            new CreateSocialTableRequest(hostUserId, venueId, title, description, startsAtUtc, capacity, joinPolicy));

        await EnsureSuccessAsync(response);
    }

    public async Task SendFriendRequestAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/friends/{targetUserId}/request", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task AcceptFriendRequestAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/friends/{targetUserId}/accept", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task RejectFriendRequestAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/friends/{targetUserId}/reject", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task InviteUserToHostedTableAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/tables/mine/invite",
            new InviteToHostedTableRequest(targetUserId));
        await EnsureSuccessAsync(response);
    }

    public async Task AcceptTableInviteAsync(Guid tableId)
    {
        var userId = await GetCurrentUserIdAsync();
        var response = await _httpClient.PostAsync($"api/social/tables/{tableId}/join?userId={userId}", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task ExitActiveCheckInAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync("api/social/check-ins/exit", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task<List<SocialTableSummary>> GetMyTablesAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/social/tables/mine");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<SocialTableSummary>>() ?? new List<SocialTableSummary>();
    }

    public async Task<SocialTableThread> GetTableThreadAsync(Guid tableId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync($"api/social/tables/{tableId}/thread");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SocialTableThread>() ?? new SocialTableThread();
    }

    public async Task ApproveTableRequestAsync(Guid tableId, Guid userId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/tables/{tableId}/participants/{userId}/approve", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task RejectTableRequestAsync(Guid tableId, Guid userId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/tables/{tableId}/participants/{userId}/reject", content: null);
        await EnsureSuccessAsync(response);
    }

    public async Task SendTableMessageAsync(Guid tableId, string body)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/social/tables/{tableId}/messages", new SendSocialTableMessageRequest(body));
        await EnsureSuccessAsync(response);
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

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(detail))
        {
            throw new InvalidOperationException(detail.Trim().Trim('"'));
        }

        response.EnsureSuccessStatusCode();
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
            TaskCanceledException => $"Timeout verso {_httpClient.BaseAddress}. Controlla rete, backend e avvio LAN.",
            HttpRequestException => $"Backend non raggiungibile su {_httpClient.BaseAddress}. Sul Mac usa ./scripts/run-api-lan.sh.",
            InvalidOperationException => ex.Message,
            _ => ex.Message
        };
    }

    private record DevLoginRequest(string Nickname, string? DisplayName);

    private record CreateCheckInRequest(Guid UserId, Guid VenueId, int TtlMinutes);

    private record CreateIntentionRequest(Guid UserId, Guid VenueId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string? Note);

    private record CreateSocialTableRequest(Guid HostUserId, Guid VenueId, string Title, string? Description, DateTimeOffset StartsAtUtc, int Capacity, string JoinPolicy);

    private record InviteToHostedTableRequest(Guid TargetUserId);

    private record UpdatePrivacySettingsRequest(bool? IsGhostModeEnabled, bool? SharePresenceWithFriends, bool? ShareIntentionsWithFriends);

    private record SendSocialTableMessageRequest(string Body);

    private record UpdateMyProfileRequestDto(string? DisplayName, string? AvatarUrl, string? Bio, int? BirthYear, string? Gender, List<string> Interests);

    private record SendDirectMessageRequestDto(string Body);

    private record CreateUserReportRequestDto(Guid ReportedUserId, string ReasonCode, string? Details);

    private record CreateTableReportRequestDto(Guid SocialTableId, string ReasonCode, string? Details);

    private record RegisterDeviceTokenRequest(Guid UserId, string Platform, string DeviceToken);

    public sealed record ServiceHealthResponse(string Status, DateTimeOffset Utc);
}
