using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using FriendMap.Mobile.Models;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace FriendMap.Mobile.Services;

public class ApiClient
{
    private const string AccessTokenKey = "friendmap_access_token";
    private const string UserIdKey = "friendmap_user_id";
    private const string NicknameKey = "friendmap_nickname";
    private const string ApiBaseUrlKey = "friendmap_api_base_url";
    private const string DefaultApiBaseUrl = "http://127.0.0.1:8080/";
    private static readonly TimeSpan MapLayerCacheTtl = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SocialCacheTtl = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan VenueDetailsCacheTtl = TimeSpan.FromMinutes(3);
    private static readonly JsonSerializerOptions RelaxedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private HttpClient _httpClient;
    public Uri? BaseAddress => _httpClient.BaseAddress;
    private string? _currentAccessToken;
    private readonly Dictionary<string, CacheEntry<VenueMapLayer>> _mapLayerCache = new();
    private readonly Dictionary<string, CacheEntry<VenueDetails>> _venueDetailsCache = new();
    private readonly Dictionary<string, CacheEntry<UserProfile>> _userProfileCache = new();
    private readonly Dictionary<string, CacheEntry<EditableUserProfile>> _myProfileCache = new();
    private readonly Dictionary<string, CacheEntry<SocialHub>> _socialHubCache = new();
    private readonly Dictionary<string, CacheEntry<SocialMeState>> _socialMeStateCache = new();
    private readonly Dictionary<string, CacheEntry<List<SocialTableSummary>>> _myTablesCache = new();
    private readonly Dictionary<string, CacheEntry<List<DirectMessageThreadSummary>>> _messageInboxCache = new();

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
        ClearAllCaches();
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
        var cacheKey = BuildMapLayerCacheKey(queryViewport, query, category, openNowOnly, maxDistanceKm);
        if (TryGetCache(_mapLayerCache, cacheKey, out var cachedLayer))
        {
            return cachedLayer;
        }

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

        var layer = await response.Content.ReadFromJsonAsync<VenueMapLayer>()
            ?? new VenueMapLayer();
        SetCache(_mapLayerCache, cacheKey, layer, MapLayerCacheTtl);
        return layer;
    }

    public async Task<VenueDetails> GetVenueDetailsAsync(Guid venueId)
    {
        var cacheKey = venueId.ToString("N");
        if (TryGetCache(_venueDetailsCache, cacheKey, out var cachedDetails))
        {
            return cachedDetails;
        }

        var response = await _httpClient.GetAsync($"api/venues/{venueId}");
        await EnsureSuccessAsync(response);
        var details = await response.Content.ReadFromJsonAsync<VenueDetails>()
            ?? throw new InvalidOperationException("Scheda venue non disponibile.");
        SetCache(_venueDetailsCache, cacheKey, details, VenueDetailsCacheTtl);
        return details;
    }

    public async Task<UserProfile> GetUserProfileAsync(Guid userId)
    {
        var cacheKey = userId.ToString("N");
        if (TryGetCache(_userProfileCache, cacheKey, out var cachedProfile))
        {
            return cachedProfile;
        }

        var response = await _httpClient.GetAsync($"api/users/{userId}");
        await EnsureSuccessAsync(response);
        var profile = await response.Content.ReadFromJsonAsync<UserProfile>();
        if (profile is null)
        {
            throw new InvalidOperationException("Profilo utente non disponibile.");
        }

        SetCache(_userProfileCache, cacheKey, profile, ProfileCacheTtl);
        return profile;
    }

    public async Task<EditableUserProfile> GetMyProfileAsync()
    {
        await EnsureAuthenticatedAsync();
        var cacheKey = CurrentUserId?.ToString("N") ?? "me";
        if (TryGetCache(_myProfileCache, cacheKey, out var cachedProfile))
        {
            return cachedProfile;
        }

        var response = await _httpClient.GetAsync("api/users/me/profile");
        await EnsureSuccessAsync(response);
        var profile = await response.Content.ReadFromJsonAsync<EditableUserProfile>()
            ?? throw new InvalidOperationException("Profilo personale non disponibile.");
        SetCache(_myProfileCache, cacheKey, profile, ProfileCacheTtl);
        return profile;
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
        var profile = await response.Content.ReadFromJsonAsync<EditableUserProfile>()
            ?? throw new InvalidOperationException("Profilo aggiornato non disponibile.");
        InvalidateProfileCaches();
        return profile;
    }

    public async Task<EditableUserProfile> UploadMyAvatarAsync(FileResult file)
    {
        await EnsureAuthenticatedAsync();
        await using var stream = await file.OpenReadAsync();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(file.FileName));
        content.Add(fileContent, "file", file.FileName);

        var response = await _httpClient.PostAsync("api/users/me/avatar", content);
        await EnsureSuccessAsync(response);
        var profile = await response.Content.ReadFromJsonAsync<EditableUserProfile>()
            ?? throw new InvalidOperationException("Avatar aggiornato non disponibile.");
        InvalidateProfileCaches();
        return profile;
    }

    public async Task<List<UserSearchResult>> SearchUsersAsync(string query)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync($"api/users/search?q={Uri.EscapeDataString(query.Trim())}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UserSearchResult>>() ?? new List<UserSearchResult>();
    }

    public async Task<List<DirectMessageThreadSummary>> GetDirectMessageInboxAsync()
    {
        await EnsureAuthenticatedAsync();
        var cacheKey = CurrentUserId?.ToString("N") ?? "me";
        if (TryGetCache(_messageInboxCache, cacheKey, out var cachedInbox))
        {
            return cachedInbox;
        }

        var response = await _httpClient.GetAsync("api/messages/threads");
        await EnsureSuccessAsync(response);
        var inbox = await response.Content.ReadFromJsonAsync<List<DirectMessageThreadSummary>>() ?? new List<DirectMessageThreadSummary>();
        SetCache(_messageInboxCache, cacheKey, inbox, SocialCacheTtl);
        return inbox;
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
        ClearCache(_messageInboxCache);
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
        var cacheKey = CurrentUserId?.ToString("N") ?? "me";
        if (TryGetCache(_socialHubCache, cacheKey, out var cachedHub))
        {
            return cachedHub;
        }

        var response = await _httpClient.GetAsync("api/social/hub");
        await EnsureSuccessAsync(response);
        var hub = await response.Content.ReadFromJsonAsync<SocialHub>() ?? new SocialHub();
        SetCache(_socialHubCache, cacheKey, hub, SocialCacheTtl);
        return hub;
    }

    public async Task<SocialMeState> GetSocialMeStateAsync()
    {
        await EnsureAuthenticatedAsync();
        var cacheKey = CurrentUserId?.ToString("N") ?? "me";
        if (TryGetCache(_socialMeStateCache, cacheKey, out var cachedState))
        {
            return cachedState;
        }

        var response = await _httpClient.GetAsync("api/social/me/state");
        await EnsureSuccessAsync(response);
        var state = await response.Content.ReadFromJsonAsync<SocialMeState>() ?? new SocialMeState();
        SetCache(_socialMeStateCache, cacheKey, state, SocialCacheTtl);
        return state;
    }

    public async Task UpdatePrivacySettingsAsync(bool? ghostMode, bool? sharePresence, bool? shareIntentions)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/me/privacy",
            new UpdatePrivacySettingsRequest(ghostMode, sharePresence, shareIntentions));
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
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
            $"api/venues/{venueId}/checkins",
            new CreateCheckInRequest(userId, venueId, ttlMinutes));

        await EnsureSuccessAsync(response);
        InvalidateMapAndSocialCaches();
    }

    public async Task CreateIntentionAsync(Guid userId, Guid venueId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, string? note)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            $"api/venues/{venueId}/intentions",
            new CreateIntentionRequest(userId, venueId, startsAtUtc, endsAtUtc, note));

        await EnsureSuccessAsync(response);
        InvalidateMapAndSocialCaches();
    }

    public async Task CreateSocialTableAsync(Guid hostUserId, Guid venueId, string title, string? description, DateTimeOffset startsAtUtc, int capacity, string joinPolicy)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            $"api/venues/{venueId}/tables",
            new CreateSocialTableRequest(hostUserId, venueId, title, description, startsAtUtc, capacity, joinPolicy));

        await EnsureSuccessAsync(response);
        InvalidateMapAndSocialCaches();
    }

    public async Task SendFriendRequestAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/friends/{targetUserId}/request", content: null);
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
    }

    public async Task AcceptFriendRequestAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/friends/{targetUserId}/accept", content: null);
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
    }

    public async Task RejectFriendRequestAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/friends/{targetUserId}/reject", content: null);
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
    }

    public async Task InviteUserToHostedTableAsync(Guid targetUserId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync(
            "api/social/tables/mine/invite",
            new InviteToHostedTableRequest(targetUserId));
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
    }

    public async Task AcceptTableInviteAsync(Guid tableId)
    {
        var userId = await GetCurrentUserIdAsync();
        var response = await _httpClient.PostAsync($"api/social/tables/{tableId}/join?userId={userId}", content: null);
        await EnsureSuccessAsync(response);
        InvalidateMapAndSocialCaches();
    }

    public async Task ExitActiveCheckInAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync("api/social/check-ins/exit", content: null);
        await EnsureSuccessAsync(response);
        InvalidateMapAndSocialCaches();
    }

    public async Task<List<SocialTableSummary>> GetMyTablesAsync()
    {
        await EnsureAuthenticatedAsync();
        var cacheKey = CurrentUserId?.ToString("N") ?? "me";
        if (TryGetCache(_myTablesCache, cacheKey, out var cachedTables))
        {
            return cachedTables;
        }

        var response = await _httpClient.GetAsync("api/social/tables/mine");
        await EnsureSuccessAsync(response);
        var tables = await response.Content.ReadFromJsonAsync<List<SocialTableSummary>>() ?? new List<SocialTableSummary>();
        SetCache(_myTablesCache, cacheKey, tables, SocialCacheTtl);
        return tables;
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
        InvalidateSocialCaches();
    }

    public async Task RejectTableRequestAsync(Guid tableId, Guid userId)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync($"api/social/tables/{tableId}/participants/{userId}/reject", content: null);
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
    }

    public async Task SendTableMessageAsync(Guid tableId, string body)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/social/tables/{tableId}/messages", new SendSocialTableMessageRequest(body));
        await EnsureSuccessAsync(response);
        InvalidateSocialCaches();
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

    public async Task DeleteAccountAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.DeleteAsync("api/users/me");
        await EnsureSuccessAsync(response);
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(UserIdKey);
        SecureStorage.Remove(NicknameKey);
        _currentAccessToken = null;
        CurrentUserId = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<List<UserStory>> GetStoriesAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/stories");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UserStory>>(RelaxedJsonOptions) ?? new List<UserStory>();
    }

    public async Task PostStoryAsync(string caption)
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsJsonAsync("api/stories", new { caption });
        await EnsureSuccessAsync(response);
    }

    public async Task<List<NearbyUser>> GetNearbyUsersAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/discovery/nearby?radiusMeters=2000");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<NearbyUser>>(RelaxedJsonOptions) ?? new List<NearbyUser>();
    }

    public async Task<List<UserAchievement>> GetMyAchievementsAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/gamification/me");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UserAchievement>>(RelaxedJsonOptions) ?? new List<UserAchievement>();
    }

    public async Task CheckAchievementsAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.PostAsync("api/gamification/check", null);
        await EnsureSuccessAsync(response);
    }

    public async Task<List<NotificationItem>> GetNotificationsOutboxAsync()
    {
        await EnsureAuthenticatedAsync();
        var response = await _httpClient.GetAsync("api/notifications/outbox");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<NotificationItem>>(RelaxedJsonOptions) ?? new List<NotificationItem>();
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
        ClearAllCaches();
    }

    private static bool TryGetCache<T>(Dictionary<string, CacheEntry<T>> cache, string key, out T value)
    {
        lock (cache)
        {
            if (cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
                {
                    value = entry.Value;
                    return true;
                }

                cache.Remove(key);
            }
        }

        value = default!;
        return false;
    }

    private static void SetCache<T>(Dictionary<string, CacheEntry<T>> cache, string key, T value, TimeSpan ttl)
    {
        lock (cache)
        {
            cache[key] = new CacheEntry<T>(value, DateTimeOffset.UtcNow.Add(ttl));
        }
    }

    private static void ClearCache<T>(Dictionary<string, CacheEntry<T>> cache)
    {
        lock (cache)
        {
            cache.Clear();
        }
    }

    private static string BuildMapLayerCacheKey(
        MapViewport viewport,
        string? query,
        string? category,
        bool openNowOnly,
        double? maxDistanceKm)
    {
        static double Q(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim().ToLowerInvariant();
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim().ToLowerInvariant();
        var distance = maxDistanceKm is > 0 ? Math.Round(maxDistanceKm.Value, 1, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) : string.Empty;
        return string.Join('|',
            Q(viewport.MinLatitude).ToString(CultureInfo.InvariantCulture),
            Q(viewport.MinLongitude).ToString(CultureInfo.InvariantCulture),
            Q(viewport.MaxLatitude).ToString(CultureInfo.InvariantCulture),
            Q(viewport.MaxLongitude).ToString(CultureInfo.InvariantCulture),
            normalizedQuery,
            normalizedCategory,
            openNowOnly ? "open" : "all",
            distance);
    }

    private void InvalidateMapAndSocialCaches()
    {
        ClearCache(_mapLayerCache);
        InvalidateSocialCaches();
    }

    private void InvalidateSocialCaches()
    {
        ClearCache(_socialHubCache);
        ClearCache(_socialMeStateCache);
        ClearCache(_myTablesCache);
        ClearCache(_messageInboxCache);
    }

    private void InvalidateProfileCaches()
    {
        ClearCache(_myProfileCache);
        ClearCache(_userProfileCache);
        ClearCache(_mapLayerCache);
        InvalidateSocialCaches();
    }

    private void ClearAllCaches()
    {
        ClearCache(_mapLayerCache);
        ClearCache(_venueDetailsCache);
        ClearCache(_userProfileCache);
        ClearCache(_myProfileCache);
        InvalidateSocialCaches();
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

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
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

    private sealed record CacheEntry<T>(T Value, DateTimeOffset ExpiresAtUtc);

    public sealed record ServiceHealthResponse(string Status, DateTimeOffset Utc);
}
