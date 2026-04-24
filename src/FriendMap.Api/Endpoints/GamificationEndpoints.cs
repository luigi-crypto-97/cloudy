using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class GamificationEndpoints
{
    public static RouteGroupBuilder MapGamificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gamification").WithTags("Gamification").RequireAuthorization();
        group.MapGet("/me", GetMyAchievementsAsync);
        group.MapPost("/check", CheckAchievementsAsync);
        return group;
    }

    private static async Task<IResult> GetMyAchievementsAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var achievements = await db.UserAchievements
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .OrderByDescending(x => x.EarnedAtUtc)
            .Select(x => new { x.BadgeCode, x.EarnedAtUtc })
            .ToListAsync(ct);

        return Results.Ok(achievements);
    }

    private static async Task<IResult> CheckAchievementsAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var existingBadges = (await db.UserAchievements
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.BadgeCode)
            .ToListAsync(ct))
            .ToHashSet();

        var newAchievements = new List<UserAchievement>();

        var checkInCount = await db.VenueCheckIns
            .AsNoTracking()
            .CountAsync(x => x.UserId == currentUserId, ct);

        if (checkInCount >= 1 && !existingBadges.Contains("first_checkin"))
        {
            newAchievements.Add(new UserAchievement { UserId = currentUserId, BadgeCode = "first_checkin" });
        }
        if (checkInCount >= 10 && !existingBadges.Contains("checkin_10"))
        {
            newAchievements.Add(new UserAchievement { UserId = currentUserId, BadgeCode = "checkin_10" });
        }

        var hostCount = await db.SocialTables
            .AsNoTracking()
            .CountAsync(x => x.HostUserId == currentUserId, ct);

        if (hostCount >= 1 && !existingBadges.Contains("first_host"))
        {
            newAchievements.Add(new UserAchievement { UserId = currentUserId, BadgeCode = "first_host" });
        }
        if (hostCount >= 5 && !existingBadges.Contains("host_5"))
        {
            newAchievements.Add(new UserAchievement { UserId = currentUserId, BadgeCode = "host_5" });
        }

        var checkInDatesRaw = await db.VenueCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var distinctDates = checkInDatesRaw
            .Select(d => d.UtcDateTime.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        int streak = 0;
        var today = DateTimeOffset.UtcNow.Date;
        foreach (var date in distinctDates)
        {
            if (date == today.AddDays(-streak))
            {
                streak++;
            }
            else
            {
                break;
            }
        }

        if (streak >= 3 && !existingBadges.Contains("streak_3"))
        {
            newAchievements.Add(new UserAchievement { UserId = currentUserId, BadgeCode = "streak_3" });
        }

        if (newAchievements.Count > 0)
        {
            db.UserAchievements.AddRange(newAchievements);
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new { awarded = newAchievements.Select(x => x.BadgeCode) });
    }
}
