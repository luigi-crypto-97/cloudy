using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class StoriesEndpoints
{
    public static RouteGroupBuilder MapStoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stories").WithTags("Stories").RequireAuthorization().RequireRateLimiting("social");
        group.MapGet("/", GetStoriesAsync);
        group.MapGet("/archive", GetMyArchiveAsync);
        group.MapGet("/venues", GetVenueStoriesAsync);
        group.MapPost("/", CreateStoryAsync);
        group.MapPost("/media", UploadStoryMediaAsync);
        group.MapPost("/{id:guid}/like", ToggleLikeAsync);
        group.MapGet("/{id:guid}/comments", GetCommentsAsync);
        group.MapPost("/{id:guid}/comments", AddCommentAsync);
        group.MapPost("/{id:guid}/share", ShareStoryAsync);
        group.MapDelete("/{id:guid}", DeleteStoryAsync);
        return group;
    }

    private static async Task<IResult> GetStoriesAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var friendIds = await db.FriendRelations
            .AsNoTracking()
            .Where(x => x.Status == "accepted" && (x.RequesterId == currentUserId || x.AddresseeId == currentUserId))
            .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var stories = await db.UserStories
            .AsNoTracking()
            .Where(x => x.ExpiresAtUtc > now && (x.UserId == currentUserId || friendIds.Contains(x.UserId)))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Join(
                db.Users.AsNoTracking(),
                story => story.UserId,
                u => u.Id,
                (story, u) => new
                {
                    story.Id,
                    story.UserId,
                    u.Nickname,
                    u.DisplayName,
                    u.AvatarUrl,
                    story.MediaUrl,
                    story.Caption,
                    story.VenueId,
                    story.CreatedAtUtc,
                    story.ExpiresAtUtc
                })
            .ToListAsync(ct);

        var storyIds = stories.Select(x => x.Id).ToList();
        var likes = await LoadLikeCountsAsync(db, storyIds, ct);
        var comments = await LoadCommentCountsAsync(db, storyIds, ct);
        var myLikes = await LoadMyLikesAsync(db, storyIds, currentUserId, ct);

        return Results.Ok(stories.Select(story => new UserStoryDto(
            story.Id,
            story.UserId,
            story.Nickname,
            story.DisplayName,
            mediaStorage.ResolveUrl(story.AvatarUrl),
            mediaStorage.ResolveUrl(story.MediaUrl) ?? story.MediaUrl,
            story.Caption,
            story.VenueId,
            null,
            likes.GetValueOrDefault(story.Id),
            comments.GetValueOrDefault(story.Id),
            myLikes.Contains(story.Id),
            story.CreatedAtUtc,
            story.ExpiresAtUtc)));
    }

    private static async Task<IResult> GetMyArchiveAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var rows = await db.UserStories
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .GroupJoin(
                db.Venues.AsNoTracking(),
                story => story.VenueId,
                venue => venue.Id,
                (story, venues) => new { story, venue = venues.FirstOrDefault() })
            .Join(
                db.Users.AsNoTracking(),
                x => x.story.UserId,
                u => u.Id,
                (x, u) => new
                {
                    x.story.Id,
                    x.story.UserId,
                    u.Nickname,
                    u.DisplayName,
                    u.AvatarUrl,
                    x.story.MediaUrl,
                    x.story.Caption,
                    x.story.VenueId,
                    VenueName = x.venue == null ? null : x.venue.Name,
                    x.story.CreatedAtUtc,
                    x.story.ExpiresAtUtc
                })
            .ToListAsync(ct);

        var storyIds = rows.Select(x => x.Id).ToList();
        var likes = await LoadLikeCountsAsync(db, storyIds, ct);
        var comments = await LoadCommentCountsAsync(db, storyIds, ct);
        var myLikes = await LoadMyLikesAsync(db, storyIds, currentUserId, ct);

        return Results.Ok(rows.Select(story => new UserStoryDto(
            story.Id,
            story.UserId,
            story.Nickname,
            story.DisplayName,
            mediaStorage.ResolveUrl(story.AvatarUrl),
            mediaStorage.ResolveUrl(story.MediaUrl) ?? story.MediaUrl,
            story.Caption,
            story.VenueId,
            story.VenueName,
            likes.GetValueOrDefault(story.Id),
            comments.GetValueOrDefault(story.Id),
            myLikes.Contains(story.Id),
            story.CreatedAtUtc,
            story.ExpiresAtUtc)));
    }

    private static async Task<IResult> CreateStoryAsync(
        CreateStoryRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.MediaUrl))
        {
            return Results.BadRequest("MediaUrl is required.");
        }

        var story = new UserStory
        {
            UserId = currentUserId,
            MediaUrl = request.MediaUrl.Trim(),
            Caption = request.Caption?.Trim(),
            VenueId = request.VenueId,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24)
        };

        if (story.VenueId is Guid venueId)
        {
            var venueExists = await db.Venues.AsNoTracking().AnyAsync(x => x.Id == venueId, ct);
            if (!venueExists)
            {
                return Results.NotFound("Locale non trovato.");
            }
        }

        db.UserStories.Add(story);
        await db.SaveChangesAsync(ct);

        var currentUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
        var venueName = story.VenueId is null
            ? null
            : await db.Venues.AsNoTracking().Where(x => x.Id == story.VenueId).Select(x => x.Name).FirstOrDefaultAsync(ct);

        return Results.Ok(new UserStoryDto(
            story.Id,
            story.UserId,
            currentUser?.Nickname ?? "utente",
            currentUser?.DisplayName,
            mediaStorage.ResolveUrl(currentUser?.AvatarUrl),
            mediaStorage.ResolveUrl(story.MediaUrl) ?? story.MediaUrl,
            story.Caption,
            story.VenueId,
            venueName,
            0,
            0,
            false,
            story.CreatedAtUtc,
            story.ExpiresAtUtc));
    }

    private static async Task<IResult> GetVenueStoriesAsync(
        double? minLat,
        double? minLng,
        double? maxLat,
        double? maxLng,
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var friendIds = await GetFriendIdsAsync(db, currentUserId, ct);
        var now = DateTimeOffset.UtcNow;
        var query = db.UserStories
            .AsNoTracking()
            .Where(x => x.ExpiresAtUtc > now && x.VenueId != null && (x.UserId == currentUserId || friendIds.Contains(x.UserId)))
            .Join(
                db.Venues.AsNoTracking(),
                story => story.VenueId,
                venue => venue.Id,
                (story, venue) => new { story, venue })
            .Join(
                db.Users.AsNoTracking(),
                x => x.story.UserId,
                u => u.Id,
                (x, u) => new
                {
                    x.story.Id,
                    x.story.UserId,
                    u.Nickname,
                    u.DisplayName,
                    u.AvatarUrl,
                    x.story.MediaUrl,
                    x.story.Caption,
                    x.story.VenueId,
                    VenueName = x.venue.Name,
                    x.venue.Location,
                    x.story.CreatedAtUtc,
                    x.story.ExpiresAtUtc
                });

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(120)
            .ToListAsync(ct);

        if (minLat is not null && minLng is not null && maxLat is not null && maxLng is not null)
        {
            rows = rows
                .Where(x =>
                    x.Location is not null &&
                    x.Location.Y >= minLat.Value &&
                    x.Location.Y <= maxLat.Value &&
                    x.Location.X >= minLng.Value &&
                    x.Location.X <= maxLng.Value)
                .ToList();
        }

        var storyIds = rows.Select(x => x.Id).ToList();
        var likes = await LoadLikeCountsAsync(db, storyIds, ct);
        var comments = await LoadCommentCountsAsync(db, storyIds, ct);
        var myLikes = await LoadMyLikesAsync(db, storyIds, currentUserId, ct);

        return Results.Ok(rows.Select(row => new VenueStoryDto(
            row.Id,
            row.UserId,
            row.Nickname,
            row.DisplayName,
            mediaStorage.ResolveUrl(row.AvatarUrl),
            mediaStorage.ResolveUrl(row.MediaUrl) ?? row.MediaUrl,
            row.Caption,
            row.VenueId.GetValueOrDefault(),
            row.VenueName,
            row.Location?.Y ?? 0,
            row.Location?.X ?? 0,
            likes.GetValueOrDefault(row.Id),
            comments.GetValueOrDefault(row.Id),
            myLikes.Contains(row.Id),
            row.CreatedAtUtc,
            row.ExpiresAtUtc)));
    }

    private static async Task<IResult> UploadStoryMediaAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Upload multipart/form-data richiesto." });
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { message = "Upload multipart/form-data non valido." });
        }
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "File media mancante." });
        }

        const long maxBytes = 12 * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return Results.BadRequest(new { message = "Media troppo grande. Massimo 12MB." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".heic" };
        if (!allowed.Contains(extension))
        {
            return Results.BadRequest(new { message = "Formato non supportato. Usa jpg, png, webp o heic." });
        }

        try
        {
            var mediaUrl = await mediaStorage.UploadAsync(file, "uploads/stories", currentUserId, request, ct);
            return Results.Ok(new UploadMediaResult(mediaUrl));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Amazon.S3.AmazonS3Exception ex)
        {
            return Results.Problem($"Storage media non configurato correttamente: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> DeleteStoryAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var story = await db.UserStories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId, ct);
        if (story is null)
        {
            return Results.NotFound();
        }

        db.UserStories.Remove(story);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ToggleLikeAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (!await CanSeeStoryAsync(db, id, currentUserId, ct))
        {
            return Results.NotFound();
        }

        var existing = await db.UserStoryReactions
            .FirstOrDefaultAsync(x => x.UserStoryId == id && x.UserId == currentUserId && x.ReactionType == "like", ct);
        var liked = existing is null;
        if (existing is null)
        {
            db.UserStoryReactions.Add(new UserStoryReaction { UserStoryId = id, UserId = currentUserId });
        }
        else
        {
            db.UserStoryReactions.Remove(existing);
        }

        await db.SaveChangesAsync(ct);
        var likeCount = await db.UserStoryReactions.CountAsync(x => x.UserStoryId == id && x.ReactionType == "like", ct);
        return Results.Ok(new StoryLikeResultDto(id, liked, likeCount));
    }

    private static async Task<IResult> GetCommentsAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (!await CanSeeStoryAsync(db, id, currentUserId, ct))
        {
            return Results.NotFound();
        }

        var comments = await db.UserStoryComments
            .AsNoTracking()
            .Where(x => x.UserStoryId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(80)
            .Join(
                db.Users.AsNoTracking(),
                comment => comment.UserId,
                u => u.Id,
                (comment, u) => new StoryCommentDto(
                    comment.Id,
                    comment.UserStoryId,
                    comment.UserId,
                    u.Nickname,
                    u.DisplayName,
                    mediaStorage.ResolveUrl(u.AvatarUrl),
                    comment.Body,
                    comment.CreatedAtUtc,
                    comment.UserId == currentUserId))
            .ToListAsync(ct);

        return Results.Ok(comments);
    }

    private static async Task<IResult> AddCommentAsync(
        Guid id,
        AddStoryCommentRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 500)
        {
            return Results.BadRequest("Commento richiesto, massimo 500 caratteri.");
        }

        if (!await CanSeeStoryAsync(db, id, currentUserId, ct))
        {
            return Results.NotFound();
        }

        var comment = new UserStoryComment
        {
            UserStoryId = id,
            UserId = currentUserId,
            Body = body
        };
        db.UserStoryComments.Add(comment);
        await db.SaveChangesAsync(ct);

        var author = await db.Users.AsNoTracking().FirstAsync(x => x.Id == currentUserId, ct);
        return Results.Ok(new StoryCommentDto(
            comment.Id,
            comment.UserStoryId,
            comment.UserId,
            author.Nickname,
            author.DisplayName,
            mediaStorage.ResolveUrl(author.AvatarUrl),
            comment.Body,
            comment.CreatedAtUtc,
            true));
    }

    private static async Task<IResult> ShareStoryAsync(
        Guid id,
        ShareStoryRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (request.TargetUserId == Guid.Empty || request.TargetUserId == currentUserId)
        {
            return Results.BadRequest("Seleziona un amico.");
        }

        var story = await db.UserStories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ExpiresAtUtc > DateTimeOffset.UtcNow, ct);
        if (story is null || !await CanSeeStoryAsync(db, id, currentUserId, ct))
        {
            return Results.NotFound();
        }

        var areFriends = await db.FriendRelations.AsNoTracking().AnyAsync(x =>
            x.Status == "accepted" &&
            ((x.RequesterId == currentUserId && x.AddresseeId == request.TargetUserId) ||
             (x.RequesterId == request.TargetUserId && x.AddresseeId == currentUserId)), ct);
        if (!areFriends)
        {
            return Results.Conflict("Puoi inviare storie solo agli amici.");
        }

        var (lowId, highId) = NormalizePair(currentUserId, request.TargetUserId);
        var thread = await db.DirectMessageThreads.FirstOrDefaultAsync(x => x.UserLowId == lowId && x.UserHighId == highId, ct);
        if (thread is null)
        {
            thread = new DirectMessageThread { UserLowId = lowId, UserHighId = highId };
            db.DirectMessageThreads.Add(thread);
        }

        var storyUrl = mediaStorage.ResolveUrl(story.MediaUrl) ?? story.MediaUrl;
        var text = string.IsNullOrWhiteSpace(request.Message)
            ? $"[image] Storia Cloudy\n{storyUrl}"
            : $"{request.Message.Trim()}\n\n[image] Storia Cloudy\n{storyUrl}";
        thread.LastMessageAtUtc = DateTimeOffset.UtcNow;
        thread.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.DirectMessages.Add(new DirectMessage
        {
            ThreadId = thread.Id,
            SenderUserId = currentUserId,
            Body = text.Length <= 1000 ? text : text[..1000]
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new SocialActionResultDto("shared", "Storia inviata."));
    }

    private static async Task<List<Guid>> GetFriendIdsAsync(AppDbContext db, Guid currentUserId, CancellationToken ct)
    {
        return await db.FriendRelations
            .AsNoTracking()
            .Where(x => x.Status == "accepted" && (x.RequesterId == currentUserId || x.AddresseeId == currentUserId))
            .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
            .ToListAsync(ct);
    }

    private static async Task<bool> CanSeeStoryAsync(AppDbContext db, Guid storyId, Guid currentUserId, CancellationToken ct)
    {
        var story = await db.UserStories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == storyId && x.ExpiresAtUtc > DateTimeOffset.UtcNow, ct);
        if (story is null)
        {
            return false;
        }

        if (story.UserId == currentUserId)
        {
            return true;
        }

        return await db.FriendRelations.AsNoTracking().AnyAsync(x =>
            x.Status == "accepted" &&
            ((x.RequesterId == currentUserId && x.AddresseeId == story.UserId) ||
             (x.RequesterId == story.UserId && x.AddresseeId == currentUserId)), ct);
    }

    private static async Task<Dictionary<Guid, int>> LoadLikeCountsAsync(AppDbContext db, IReadOnlyCollection<Guid> storyIds, CancellationToken ct)
    {
        return storyIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.UserStoryReactions
                .AsNoTracking()
                .Where(x => storyIds.Contains(x.UserStoryId) && x.ReactionType == "like")
                .GroupBy(x => x.UserStoryId)
                .Select(x => new { StoryId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.StoryId, x => x.Count, ct);
    }

    private static async Task<Dictionary<Guid, int>> LoadCommentCountsAsync(AppDbContext db, IReadOnlyCollection<Guid> storyIds, CancellationToken ct)
    {
        return storyIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.UserStoryComments
                .AsNoTracking()
                .Where(x => storyIds.Contains(x.UserStoryId))
                .GroupBy(x => x.UserStoryId)
                .Select(x => new { StoryId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.StoryId, x => x.Count, ct);
    }

    private static async Task<HashSet<Guid>> LoadMyLikesAsync(AppDbContext db, IReadOnlyCollection<Guid> storyIds, Guid currentUserId, CancellationToken ct)
    {
        var ids = storyIds.Count == 0
            ? new List<Guid>()
            : await db.UserStoryReactions
                .AsNoTracking()
                .Where(x => storyIds.Contains(x.UserStoryId) && x.UserId == currentUserId && x.ReactionType == "like")
                .Select(x => x.UserStoryId)
                .ToListAsync(ct);
        return ids.ToHashSet();
    }

    private static (Guid LowId, Guid HighId) NormalizePair(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }
}

public record UserStoryDto(
    Guid Id,
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string MediaUrl,
    string? Caption,
    Guid? VenueId,
    string? VenueName,
    int LikeCount,
    int CommentCount,
    bool HasLiked,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public record VenueStoryDto(
    Guid Id,
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string MediaUrl,
    string? Caption,
    Guid VenueId,
    string VenueName,
    double Latitude,
    double Longitude,
    int LikeCount,
    int CommentCount,
    bool HasLiked,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public record StoryLikeResultDto(Guid StoryId, bool Liked, int LikeCount);
public record StoryCommentDto(Guid CommentId, Guid StoryId, Guid UserId, string Nickname, string? DisplayName, string? AvatarUrl, string Body, DateTimeOffset CreatedAtUtc, bool IsMine);
public record CreateStoryRequest(string MediaUrl, string? Caption, Guid? VenueId);
public record AddStoryCommentRequest(string? Body);
public record ShareStoryRequest(Guid TargetUserId, string? Message);
public record UploadMediaResult(string Url);
