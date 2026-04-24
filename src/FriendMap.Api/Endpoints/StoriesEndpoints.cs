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
        group.MapPost("/", CreateStoryAsync);
        group.MapDelete("/{id:guid}", DeleteStoryAsync);
        return group;
    }

    private static async Task<IResult> GetStoriesAsync(
        ClaimsPrincipal user,
        AppDbContext db,
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
            .Where(x => x.ExpiresAtUtc > now && friendIds.Contains(x.UserId))
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
                    story.CreatedAtUtc,
                    story.ExpiresAtUtc
                })
            .ToListAsync(ct);

        return Results.Ok(stories);
    }

    private static async Task<IResult> CreateStoryAsync(
        CreateStoryRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
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
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24)
        };

        db.UserStories.Add(story);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { story.Id, story.ExpiresAtUtc });
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
}

public record CreateStoryRequest(string MediaUrl, string? Caption);
