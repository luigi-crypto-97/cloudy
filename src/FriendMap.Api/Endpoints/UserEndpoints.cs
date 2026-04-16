using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/{userId:guid}", async (
            Guid userId,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
            if (user is null)
            {
                return Results.NotFound();
            }

            var viewerUserId = CurrentUser.GetUserId(principal);
            var now = DateTimeOffset.UtcNow;

            var relations = await db.FriendRelations
                .AsNoTracking()
                .Where(x =>
                    x.RequesterId == userId ||
                    x.AddresseeId == userId ||
                    (viewerUserId != Guid.Empty && (x.RequesterId == viewerUserId || x.AddresseeId == viewerUserId)))
                .ToListAsync(ct);

            var targetFriendIds = relations
                .Where(x => x.Status == "accepted" && (x.RequesterId == userId || x.AddresseeId == userId))
                .Select(x => x.RequesterId == userId ? x.AddresseeId : x.RequesterId)
                .Distinct()
                .ToHashSet();

            var viewerFriendIds = viewerUserId == Guid.Empty
                ? new HashSet<Guid>()
                : relations
                    .Where(x => x.Status == "accepted" && (x.RequesterId == viewerUserId || x.AddresseeId == viewerUserId))
                    .Select(x => x.RequesterId == viewerUserId ? x.AddresseeId : x.RequesterId)
                    .Distinct()
                    .ToHashSet();

            var directRelation = viewerUserId == Guid.Empty || viewerUserId == userId
                ? null
                : relations.FirstOrDefault(x =>
                    (x.RequesterId == viewerUserId && x.AddresseeId == userId) ||
                    (x.RequesterId == userId && x.AddresseeId == viewerUserId));

            var isFriend = viewerUserId != Guid.Empty && targetFriendIds.Contains(viewerUserId);
            var relationshipStatus = ResolveRelationshipStatus(viewerUserId, userId, directRelation);
            var mutualFriendsCount = viewerUserId == Guid.Empty
                ? 0
                : targetFriendIds.Intersect(viewerFriendIds).Where(x => x != userId && x != viewerUserId).Count();
            var friendsCount = targetFriendIds.Count;
            var canInviteToTable = viewerUserId != Guid.Empty && viewerUserId != userId;

            var activeCheckIn = await db.VenueCheckIns
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.ExpiresAtUtc >= now)
                .OrderByDescending(x => x.ExpiresAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    checkIn => checkIn.VenueId,
                    venue => venue.Id,
                    (checkIn, venue) => new { Venue = venue })
                .FirstOrDefaultAsync(ct);

            var activeHostedTable = await db.SocialTables
                .AsNoTracking()
                .Where(x => x.HostUserId == userId && x.Status == "open" && x.StartsAtUtc >= now)
                .OrderBy(x => x.StartsAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    table => table.VenueId,
                    venue => venue.Id,
                    (table, venue) => new { Venue = venue })
                .FirstOrDefaultAsync(ct);

            var activeJoinedTable = await db.SocialTableParticipants
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Status == "accepted")
                .Join(
                    db.SocialTables.AsNoTracking().Where(x => x.Status == "open" && x.StartsAtUtc >= now),
                    participant => participant.SocialTableId,
                    table => table.Id,
                    (participant, table) => table)
                .OrderBy(x => x.StartsAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    table => table.VenueId,
                    venue => venue.Id,
                    (table, venue) => new { Venue = venue })
                .FirstOrDefaultAsync(ct);

            var activeIntention = await db.VenueIntentions
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.EndsAtUtc >= now && x.StartsAtUtc <= now.AddHours(6))
                .OrderBy(x => x.StartsAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    intention => intention.VenueId,
                    venue => venue.Id,
                    (intention, venue) => new { Venue = venue })
                .FirstOrDefaultAsync(ct);

            var (presenceState, statusLabel, currentVenueName, currentVenueCategory) = ResolvePresence(
                activeCheckIn?.Venue,
                activeHostedTable?.Venue,
                activeJoinedTable?.Venue,
                activeIntention?.Venue);

            return Results.Ok(new UserProfileDto(
                user.Id,
                user.Nickname,
                user.DisplayName,
                user.AvatarUrl,
                user.BirthYear,
                user.Gender,
                isFriend,
                relationshipStatus,
                mutualFriendsCount,
                friendsCount,
                presenceState,
                statusLabel,
                currentVenueName,
                currentVenueCategory,
                canInviteToTable));
        });

        return group;
    }

    private static (string PresenceState, string StatusLabel, string? VenueName, string? VenueCategory) ResolvePresence(
        Venue? checkInVenue,
        Venue? hostedTableVenue,
        Venue? joinedTableVenue,
        Venue? intentionVenue)
    {
        if (checkInVenue is not null)
        {
            return ("checked_in", $"Ora a {checkInVenue.Name}", checkInVenue.Name, checkInVenue.Category);
        }

        if (hostedTableVenue is not null)
        {
            return ("hosting_table", $"Tavolo aperto a {hostedTableVenue.Name}", hostedTableVenue.Name, hostedTableVenue.Category);
        }

        if (joinedTableVenue is not null)
        {
            return ("joined_table", $"Dentro un tavolo a {joinedTableVenue.Name}", joinedTableVenue.Name, joinedTableVenue.Category);
        }

        if (intentionVenue is not null)
        {
            return ("planning", $"Piano per {intentionVenue.Name}", intentionVenue.Name, intentionVenue.Category);
        }

        return ("idle", "Nessuna presenza live", null, null);
    }

    private static string ResolveRelationshipStatus(Guid viewerUserId, Guid targetUserId, FriendRelation? relation)
    {
        if (viewerUserId == Guid.Empty)
        {
            return "viewer_anonymous";
        }

        if (viewerUserId == targetUserId)
        {
            return "self";
        }

        if (relation is null)
        {
            return "none";
        }

        if (relation.Status.Equals("accepted", StringComparison.OrdinalIgnoreCase))
        {
            return "friend";
        }

        if (relation.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
        {
            return relation.RequesterId == viewerUserId ? "pending_sent" : "pending_received";
        }

        return relation.Status;
    }
}
