using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class DiscoveryEndpoints
{
    public static RouteGroupBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/discovery").WithTags("Discovery").RequireAuthorization();
        group.MapGet("/nearby", GetNearbyAsync);
        return group;
    }

    private static async Task<IResult> GetNearbyAsync(
        double lat,
        double lon,
        int radiusMeters,
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (radiusMeters <= 0 || radiusMeters > 50000)
        {
            return Results.BadRequest("radiusMeters must be between 1 and 50000.");
        }

        var searchPoint = new Point(lon, lat) { SRID = 4326 };

        var nearbyVenueIds = await db.Venues
            .AsNoTracking()
            .Where(v => v.Location != null && v.Location.Distance(searchPoint) <= radiusMeters)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;

        var checkInUserIds = await db.VenueCheckIns
            .AsNoTracking()
            .Where(c => nearbyVenueIds.Contains(c.VenueId) && c.ExpiresAtUtc > now)
            .Select(c => c.UserId)
            .Distinct()
            .ToListAsync(ct);

        var intentionUserIds = await db.VenueIntentions
            .AsNoTracking()
            .Where(i => nearbyVenueIds.Contains(i.VenueId) && i.StartsAtUtc <= now && i.EndsAtUtc >= now)
            .Select(i => i.UserId)
            .Distinct()
            .ToListAsync(ct);

        var candidateUserIds = checkInUserIds
            .Union(intentionUserIds)
            .Where(id => id != currentUserId)
            .ToList();

        var blockedUserIds = await db.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockerUserId == currentUserId || b.BlockedUserId == currentUserId)
            .Select(b => b.BlockerUserId == currentUserId ? b.BlockedUserId : b.BlockerUserId)
            .ToListAsync(ct);

        candidateUserIds = candidateUserIds
            .Where(id => !blockedUserIds.Contains(id))
            .ToList();

        if (candidateUserIds.Count == 0)
        {
            return Results.Ok(new List<object>());
        }

        var users = await db.Users
            .AsNoTracking()
            .Where(u => candidateUserIds.Contains(u.Id) && !u.IsGhostModeEnabled)
            .ToListAsync(ct);

        var friendIds = await db.FriendRelations
            .AsNoTracking()
            .Where(f => f.Status == "accepted" && (f.RequesterId == currentUserId || f.AddresseeId == currentUserId))
            .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
            .ToListAsync(ct);

        var checkIns = await db.VenueCheckIns
            .AsNoTracking()
            .Where(c => candidateUserIds.Contains(c.UserId) && c.ExpiresAtUtc > now && nearbyVenueIds.Contains(c.VenueId))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        var intentions = await db.VenueIntentions
            .AsNoTracking()
            .Where(i => candidateUserIds.Contains(i.UserId) && i.StartsAtUtc <= now && i.EndsAtUtc >= now && nearbyVenueIds.Contains(i.VenueId))
            .OrderByDescending(i => i.StartsAtUtc)
            .ToListAsync(ct);

        var venueNames = await db.Venues
            .AsNoTracking()
            .Where(v => nearbyVenueIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, ct);

        var latestCheckInByUser = checkIns
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        var latestIntentionByUser = intentions
            .GroupBy(i => i.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = users.Select(u =>
        {
            Guid? venueId = latestCheckInByUser.TryGetValue(u.Id, out var c)
                ? c.VenueId
                : (latestIntentionByUser.TryGetValue(u.Id, out var i) ? i.VenueId : null);

            var venueName = venueId.HasValue && venueNames.TryGetValue(venueId.Value, out var name)
                ? name
                : null;

            bool isFriend = friendIds.Contains(u.Id);
            string nickname = isFriend ? u.Nickname : MaskNickname(u.Nickname);

            return new
            {
                u.Id,
                Nickname = nickname,
                u.DisplayName,
                u.AvatarUrl,
                CurrentVenueName = venueName
            };
        }).ToList();

        return Results.Ok(result);
    }

    private static string MaskNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname) || nickname.Length <= 2)
        {
            return "**";
        }
        return nickname[0] + new string('*', nickname.Length - 1);
    }
}
