using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FriendMap.Api.Data;
using Xunit;

namespace FriendMap.Api.Tests;

public sealed class SocialFlowsTests : IClassFixture<ApiTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public SocialFlowsTests(ApiTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DevLogin_ThenMe_ReturnsAuthenticatedUser()
    {
        var token = await LoginAsync("giulia", "Giulia Test");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await _client.SendAsync(request);
        var user = await ReadJsonAsync<AuthUserDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("giulia", user.Nickname);
        Assert.NotEqual(Guid.Empty, user.UserId);
    }

    [Fact]
    public async Task ProfileUpdate_NormalizesInterests_AndRejectsAnonymous()
    {
        var anonymous = await _client.PutAsJsonAsync("/api/users/me/profile", new
        {
            DisplayName = "No Auth",
            Interests = new[] { "pub" }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var token = await LoginAsync("giulia", "Giulia Test");
        using var request = Authorized(HttpMethod.Put, "/api/users/me/profile", token.AccessToken);
        request.Content = JsonContent.Create(new
        {
            DisplayName = "  Giulia Viral  ",
            Bio = new string('x', 320),
            BirthYear = 1880,
            Gender = "female",
            Interests = new[]
            {
                " pub ", "PUB", "", "cocktail", "musica live", "casino", "tech", "startup",
                "aperitivo", "pizza", "sushi", "arte", "cinema", "running", "extra"
            }
        });

        using var response = await _client.SendAsync(request);
        var profile = await ReadJsonAsync<EditableProfileDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Giulia Viral", profile.DisplayName);
        Assert.Null(profile.BirthYear);
        Assert.Equal("female", profile.Gender);
        Assert.Equal(12, profile.Interests.Length);
        Assert.Contains("pub", profile.Interests);
        Assert.Single(profile.Interests, x => x.Equals("pub", StringComparison.OrdinalIgnoreCase));

        using var postAlias = Authorized(HttpMethod.Post, "/api/users/me/profile", token.AccessToken);
        postAlias.Content = JsonContent.Create(new
        {
            DisplayName = "Giulia Alias",
            Interests = new[] { "flare", "stories" }
        });
        using var postAliasResponse = await _client.SendAsync(postAlias);
        var aliasProfile = await ReadJsonAsync<EditableProfileDto>(postAliasResponse);
        Assert.Equal(HttpStatusCode.OK, postAliasResponse.StatusCode);
        Assert.Equal("Giulia Alias", aliasProfile.DisplayName);
        Assert.Contains("flare", aliasProfile.Interests);
    }

    [Fact]
    public async Task AvatarUpload_CoversSuccessMissingFileInvalidTypeAndTooLarge()
    {
        var token = await LoginAsync("giulia", "Giulia Test");

        using var success = Authorized(HttpMethod.Post, "/api/users/me/avatar", token.AccessToken);
        success.Content = Multipart("file", "avatar.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });
        using var successResponse = await _client.SendAsync(success);
        var profile = await ReadJsonAsync<EditableProfileDto>(successResponse);
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.EndsWith(".jpg", profile.AvatarUrl);

        using var missing = Authorized(HttpMethod.Post, "/api/users/me/avatar", token.AccessToken);
        missing.Content = new MultipartFormDataContent();
        using var missingResponse = await _client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);

        using var invalidType = Authorized(HttpMethod.Post, "/api/users/me/avatar", token.AccessToken);
        invalidType.Content = Multipart("file", "avatar.gif", "image/gif", new byte[] { 1, 2, 3 });
        using var invalidTypeResponse = await _client.SendAsync(invalidType);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTypeResponse.StatusCode);

        using var tooLarge = Authorized(HttpMethod.Post, "/api/users/me/avatar", token.AccessToken);
        tooLarge.Content = Multipart("file", "avatar.jpg", "image/jpeg", new byte[(5 * 1024 * 1024) + 1]);
        using var tooLargeResponse = await _client.SendAsync(tooLarge);
        Assert.Equal(HttpStatusCode.BadRequest, tooLargeResponse.StatusCode);
    }

    [Fact]
    public async Task Stories_CoverUploadCreateListAndNegativeCases()
    {
        var token = await LoginAsync("giulia", "Giulia Test");
        var friendToken = await LoginAsync("marco", "Marco Test");

        using var noAuth = new HttpRequestMessage(HttpMethod.Get, "/api/stories");
        using var noAuthResponse = await _client.SendAsync(noAuth);
        Assert.Equal(HttpStatusCode.Unauthorized, noAuthResponse.StatusCode);

        using var missingMedia = Authorized(HttpMethod.Post, "/api/stories", token.AccessToken);
        missingMedia.Content = JsonContent.Create(new { MediaUrl = "", Caption = "bad" });
        using var missingMediaResponse = await _client.SendAsync(missingMedia);
        Assert.Equal(HttpStatusCode.BadRequest, missingMediaResponse.StatusCode);

        using var upload = Authorized(HttpMethod.Post, "/api/stories/media", token.AccessToken);
        upload.Headers.Add("X-Forwarded-Host", "api.iron-quote.it");
        upload.Headers.Add("X-Forwarded-Proto", "https");
        upload.Content = Multipart("file", "story.jpg", "image/jpeg", new byte[] { 9, 8, 7, 6 });
        using var uploadResponse = await _client.SendAsync(upload);
        var uploadResult = await ReadJsonAsync<UploadResultDto>(uploadResponse);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.StartsWith("https://api.iron-quote.it/uploads/stories/", uploadResult.Url);

        using var videoUpload = Authorized(HttpMethod.Post, "/api/stories/media", token.AccessToken);
        videoUpload.Headers.Add("X-Forwarded-Host", "api.iron-quote.it");
        videoUpload.Headers.Add("X-Forwarded-Proto", "https");
        videoUpload.Content = Multipart("file", "story.mp4", "video/mp4", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 });
        using var videoUploadResponse = await _client.SendAsync(videoUpload);
        var videoUploadResult = await ReadJsonAsync<UploadResultDto>(videoUploadResponse);
        Assert.Equal(HttpStatusCode.OK, videoUploadResponse.StatusCode);
        Assert.EndsWith(".mp4", videoUploadResult.Url, StringComparison.OrdinalIgnoreCase);

        using var create = Authorized(HttpMethod.Post, "/api/stories", token.AccessToken);
        create.Content = JsonContent.Create(new { MediaUrl = uploadResult.Url, Caption = "Titolo\n\nCaption", VenueId = DevelopmentDataSeeder.BreraVenueId });
        using var createResponse = await _client.SendAsync(create);
        var story = await ReadJsonAsync<StoryDto>(createResponse);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal("giulia", story.Nickname);
        Assert.Equal(uploadResult.Url, story.MediaUrl);
        Assert.Equal(DevelopmentDataSeeder.BreraVenueId, story.VenueId);

        using var list = Authorized(HttpMethod.Get, "/api/stories", token.AccessToken);
        using var listResponse = await _client.SendAsync(list);
        var stories = await ReadJsonAsync<StoryDto[]>(listResponse);
        Assert.Contains(stories, x => x.Id == story.Id);

        using var like = Authorized(HttpMethod.Post, $"/api/stories/{story.Id}/like", token.AccessToken);
        using var likeResponse = await _client.SendAsync(like);
        var likeResult = await ReadJsonAsync<LikeDto>(likeResponse);
        Assert.True(likeResult.Liked);
        Assert.Equal(1, likeResult.LikeCount);

        using var comment = Authorized(HttpMethod.Post, $"/api/stories/{story.Id}/comments", token.AccessToken);
        comment.Content = JsonContent.Create(new { Body = "Ci sono!" });
        using var commentResponse = await _client.SendAsync(comment);
        var commentResult = await ReadJsonAsync<CommentDto>(commentResponse);
        Assert.Equal("Ci sono!", commentResult.Body);

        using var venueStories = Authorized(HttpMethod.Get, "/api/stories/venues", token.AccessToken);
        using var venueStoriesResponse = await _client.SendAsync(venueStories);
        var venueStoryResults = await ReadJsonAsync<VenueStoryDto[]>(venueStoriesResponse);
        Assert.Contains(venueStoryResults, x => x.Id == story.Id && x.VenueId == DevelopmentDataSeeder.BreraVenueId);

        using var friendCreate = Authorized(HttpMethod.Post, "/api/stories", friendToken.AccessToken);
        friendCreate.Content = JsonContent.Create(new { MediaUrl = "https://cdn.example.test/marco.jpg", Caption = "Story amico" });
        using var friendCreateResponse = await _client.SendAsync(friendCreate);
        var friendStory = await ReadJsonAsync<StoryDto>(friendCreateResponse);

        using var friendList = Authorized(HttpMethod.Get, "/api/stories", token.AccessToken);
        using var friendListResponse = await _client.SendAsync(friendList);
        var friendVisibleStories = await ReadJsonAsync<StoryDto[]>(friendListResponse);
        Assert.Contains(friendVisibleStories, x => x.Id == friendStory.Id && x.UserId == friendToken.User.UserId);

        using var share = Authorized(HttpMethod.Post, $"/api/stories/{story.Id}/share", token.AccessToken);
        share.Content = JsonContent.Create(new { TargetUserId = friendToken.User.UserId, Message = "Guarda questa" });
        using var shareResponse = await _client.SendAsync(share);
        var shareResult = await ReadJsonAsync<ActionDto>(shareResponse);
        Assert.Equal("shared", shareResult.Status);

        using var dm = Authorized(HttpMethod.Post, $"/api/messages/threads/{friendToken.User.UserId}", token.AccessToken);
        dm.Content = JsonContent.Create(new { Body = "Messaggio diretto" });
        using var dmResponse = await _client.SendAsync(dm);
        var dmResult = await ReadJsonAsync<DirectMessageDto>(dmResponse);
        Assert.Equal("Messaggio diretto", dmResult.Body);
    }

    [Fact]
    public async Task CheckInIntentionLiveLocationAndFlare_CoverPositiveNegativeAndEdgeCases()
    {
        var token = await LoginAsync("giulia", "Giulia Test");

        using var checkIn = Authorized(HttpMethod.Post, "/api/social/check-ins", token.AccessToken);
        checkIn.Content = JsonContent.Create(new
        {
            UserId = token.User.UserId,
            VenueId = DevelopmentDataSeeder.BreraVenueId,
            TtlMinutes = 0
        });
        using var checkInResponse = await _client.SendAsync(checkIn);
        Assert.Equal(HttpStatusCode.Created, checkInResponse.StatusCode);

        using var forged = Authorized(HttpMethod.Post, "/api/social/check-ins", token.AccessToken);
        forged.Content = JsonContent.Create(new
        {
            UserId = Guid.NewGuid(),
            VenueId = DevelopmentDataSeeder.BreraVenueId,
            TtlMinutes = 30
        });
        using var forgedResponse = await _client.SendAsync(forged);
        Assert.Equal(HttpStatusCode.Forbidden, forgedResponse.StatusCode);

        using var badLive = Authorized(HttpMethod.Post, "/api/social/live-location", token.AccessToken);
        badLive.Content = JsonContent.Create(new
        {
            UserId = token.User.UserId,
            Latitude = 140,
            Longitude = 9.18,
            AccuracyMeters = 20
        });
        using var badLiveResponse = await _client.SendAsync(badLive);
        Assert.Equal(HttpStatusCode.BadRequest, badLiveResponse.StatusCode);

        using var live = Authorized(HttpMethod.Post, "/api/social/live-location", token.AccessToken);
        live.Content = JsonContent.Create(new
        {
            UserId = token.User.UserId,
            Latitude = 45.4720,
            Longitude = 9.1876,
            AccuracyMeters = 20
        });
        using var liveResponse = await _client.SendAsync(live);
        var liveResult = await ReadJsonAsync<LiveLocationDto>(liveResponse);
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal("updated", liveResult.Status);
        Assert.Equal(DevelopmentDataSeeder.BreraVenueId, liveResult.VenueId);

        using var stopLive = Authorized(HttpMethod.Post, "/api/social/live-location/stop", token.AccessToken);
        using var stopLiveResponse = await _client.SendAsync(stopLive);
        var stopResult = await ReadJsonAsync<ActionDto>(stopLiveResponse);
        Assert.Equal(HttpStatusCode.OK, stopLiveResponse.StatusCode);
        Assert.Contains(stopResult.Status, new[] { "live_location_stopped", "noop" });

        using var flareEmpty = Authorized(HttpMethod.Post, "/api/social/flares", token.AccessToken);
        flareEmpty.Content = JsonContent.Create(new { Latitude = 45.47, Longitude = 9.18, Message = "" });
        using var flareEmptyResponse = await _client.SendAsync(flareEmpty);
        Assert.Equal(HttpStatusCode.BadRequest, flareEmptyResponse.StatusCode);

        using var flare = Authorized(HttpMethod.Post, "/api/social/flares", token.AccessToken);
        flare.Content = JsonContent.Create(new { Latitude = 45.47, Longitude = 9.18, Message = "Aperitivo ora?", DurationHours = 4 });
        using var flareResponse = await _client.SendAsync(flare);
        var flareResult = await ReadJsonAsync<FlareDto>(flareResponse);
        Assert.Equal(HttpStatusCode.OK, flareResponse.StatusCode);
        Assert.Equal("Aperitivo ora?", flareResult.Message);
        Assert.True(flareResult.ExpiresAtUtc > DateTimeOffset.UtcNow.AddHours(3));

        using var flareReply = Authorized(HttpMethod.Post, $"/api/social/flares/{flareResult.FlareId}/responses", token.AccessToken);
        flareReply.Content = JsonContent.Create(new { Body = "Io ci sono" });
        using var flareReplyResponse = await _client.SendAsync(flareReply);
        var flareReplyResult = await ReadJsonAsync<ActionDto>(flareReplyResponse);
        Assert.Equal("flare_response_sent", flareReplyResult.Status);

        using var table = Authorized(HttpMethod.Post, "/api/social/tables", token.AccessToken);
        table.Content = JsonContent.Create(new
        {
            HostUserId = token.User.UserId,
            VenueId = DevelopmentDataSeeder.BreraVenueId,
            Title = "Tavolo test",
            StartsAtUtc = DateTimeOffset.UtcNow.AddHours(2),
            Capacity = 2,
            JoinPolicy = "auto"
        });
        using var tableResponse = await _client.SendAsync(table);
        var tableResult = await ReadJsonAsync<TableDto>(tableResponse);
        Assert.Equal("Tavolo test", tableResult.Title);
        Assert.Equal(1, tableResult.AcceptedCount);
    }

    [Fact]
    public async Task PartyPulseIntentRadarAndVenueRatings_AreAggregatedAndPrivacySafe()
    {
        var token = await LoginAsync("giulia", "Giulia Test");
        var reporter = await LoginAsync("marco", "Marco Test");

        using var venueDetails = Authorized(HttpMethod.Get, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}", token.AccessToken);
        using var venueDetailsResponse = await _client.SendAsync(venueDetails);
        using var venueDetailsJson = await ReadJsonDocumentAsync(venueDetailsResponse);
        Assert.True(venueDetailsJson.RootElement.GetProperty("partyPulse").GetProperty("energyScore").GetInt32() > 0);
        Assert.NotEmpty(venueDetailsJson.RootElement.GetProperty("partyPulse").GetProperty("sparkline").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(venueDetailsJson.RootElement.GetProperty("intentRadar").GetProperty("privacyLevel").GetString()));

        using var invalidRating = Authorized(HttpMethod.Post, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}/rating", token.AccessToken);
        invalidRating.Content = JsonContent.Create(new { Stars = 6, Comment = "troppo" });
        using var invalidRatingResponse = await _client.SendAsync(invalidRating);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRatingResponse.StatusCode);

        using var checkIn = Authorized(HttpMethod.Post, "/api/social/check-ins", token.AccessToken);
        checkIn.Content = JsonContent.Create(new
        {
            UserId = token.User.UserId,
            VenueId = DevelopmentDataSeeder.BreraVenueId,
            TtlMinutes = 60
        });
        using var checkInResponse = await _client.SendAsync(checkIn);
        Assert.Equal(HttpStatusCode.Created, checkInResponse.StatusCode);

        using var rating = Authorized(HttpMethod.Post, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}/rating", token.AccessToken);
        rating.Content = JsonContent.Create(new { Stars = 5, Comment = "Serata reale, bel mood" });
        using var ratingResponse = await _client.SendAsync(rating);
        using var ratingJson = await ReadJsonDocumentAsync(ratingResponse);
        Assert.Equal(5, ratingJson.RootElement.GetProperty("myRating").GetInt32());
        Assert.True(ratingJson.RootElement.GetProperty("myRatingIsVerified").GetBoolean());
        Assert.True(ratingJson.RootElement.GetProperty("myRatingEarnsPoints").GetBoolean());
        var ratingId = ratingJson.RootElement.GetProperty("myRatingId").GetGuid();

        using var reviews = Authorized(HttpMethod.Get, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}/ratings", token.AccessToken);
        using var reviewsResponse = await _client.SendAsync(reviews);
        using var reviewsJson = await ReadJsonDocumentAsync(reviewsResponse);
        var review = Assert.Single(reviewsJson.RootElement.EnumerateArray());
        Assert.Equal(ratingId, review.GetProperty("ratingId").GetGuid());
        Assert.Equal("Serata reale, bel mood", review.GetProperty("comment").GetString());
        Assert.True(review.GetProperty("isVerifiedVisit").GetBoolean());
        Assert.True(review.GetProperty("isMine").GetBoolean());

        using var gamification = Authorized(HttpMethod.Get, "/api/gamification/me", token.AccessToken);
        using var gamificationResponse = await _client.SendAsync(gamification);
        using var gamificationJson = await ReadJsonDocumentAsync(gamificationResponse);
        Assert.Contains(
            gamificationJson.RootElement.GetProperty("weeklyMissions").EnumerateArray(),
            mission => mission.GetProperty("code").GetString() == "weekly_reviewer");

        using var reportOwn = Authorized(HttpMethod.Post, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}/ratings/{ratingId}/report", token.AccessToken);
        reportOwn.Content = JsonContent.Create(new { ReasonCode = "fake_venue_rating", Details = "self report" });
        using var reportOwnResponse = await _client.SendAsync(reportOwn);
        Assert.Equal(HttpStatusCode.BadRequest, reportOwnResponse.StatusCode);

        using var report = Authorized(HttpMethod.Post, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}/ratings/{ratingId}/report", reporter.AccessToken);
        report.Content = JsonContent.Create(new { ReasonCode = "fake_venue_rating", Details = "test moderation" });
        using var reportResponse = await _client.SendAsync(report);
        var reportResult = await ReadJsonAsync<ActionDto>(reportResponse);
        Assert.Equal("reported", reportResult.Status);

        using var afterReport = Authorized(HttpMethod.Get, $"/api/venues/{DevelopmentDataSeeder.BreraVenueId}/rating", token.AccessToken);
        using var afterReportResponse = await _client.SendAsync(afterReport);
        using var afterReportJson = await ReadJsonDocumentAsync(afterReportResponse);
        Assert.Equal(0, afterReportJson.RootElement.GetProperty("ratingCount").GetInt32());
    }

    private async Task<LoginDto> LoginAsync(string nickname, string displayName)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/dev-login", new { Nickname = nickname, DisplayName = displayName });
        return await ReadJsonAsync<LoginDto>(response);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static MultipartFormDataContent Multipart(string name, string fileName, string contentType, byte[] data)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, name, fileName);
        return content;
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize {typeof(T).Name}: {body}");
    }

    private static async Task<JsonDocument> ReadJsonDocumentAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private sealed record LoginDto(string AccessToken, DateTimeOffset ExpiresAtUtc, AuthUserDto User);
    private sealed record AuthUserDto(Guid UserId, string Nickname, string? DisplayName);
    private sealed record EditableProfileDto(
        Guid UserId,
        string Nickname,
        string? DisplayName,
        string? AvatarUrl,
        string? DiscoverablePhone,
        string? DiscoverableEmail,
        string? Bio,
        int? BirthYear,
        string Gender,
        string[] Interests);
    private sealed record UploadResultDto(string Url);
    private sealed record StoryDto(Guid Id, Guid UserId, string Nickname, string? MediaUrl, string? Caption, Guid? VenueId, int LikeCount, int CommentCount, bool HasLiked);
    private sealed record VenueStoryDto(Guid Id, Guid UserId, Guid VenueId, string VenueName, double Latitude, double Longitude);
    private sealed record LikeDto(Guid StoryId, bool Liked, int LikeCount);
    private sealed record CommentDto(Guid CommentId, Guid StoryId, string Body);
    private sealed record DirectMessageDto(Guid MessageId, string Body, bool IsMine);
    private sealed record LiveLocationDto(string Status, Guid? VenueId, string? VenueName, DateTimeOffset? ExpiresAtUtc, double? DistanceMeters);
    private sealed record ActionDto(string Status, string Message);
    private sealed record FlareDto(Guid FlareId, string Message, DateTimeOffset ExpiresAtUtc);
    private sealed record TableDto(Guid TableId, string Title, int Capacity, int AcceptedCount);
}
