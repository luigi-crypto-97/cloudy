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
            var viewerUserId = CurrentUser.GetUserId(principal);
            var profile = await BuildUserProfileDtoAsync(db, viewerUserId, userId, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        group.MapGet("/me/profile", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var user = await db.Users
                .Include(x => x.Interests)
                .FirstOrDefaultAsync(x => x.Id == currentUserId, ct);

            if (user is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new EditableUserProfileDto(
                user.Id,
                user.Nickname,
                user.DisplayName,
                user.AvatarUrl,
                user.Bio,
                user.BirthYear,
                user.Gender,
                user.Interests
                    .Select(x => x.Tag)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()));
        }).RequireAuthorization();

        group.MapPut("/me/profile", async (
            UpdateMyProfileRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var user = await db.Users
                .Include(x => x.Interests)
                .FirstOrDefaultAsync(x => x.Id == currentUserId, ct);

            if (user is null)
            {
                return Results.NotFound();
            }

            user.DisplayName = NormalizeOptionalText(request.DisplayName, 60);
            user.AvatarUrl = NormalizeOptionalUrl(request.AvatarUrl);
            user.Bio = NormalizeOptionalText(request.Bio, 280);
            user.BirthYear = request.BirthYear is >= 1940 and <= 2012 ? request.BirthYear : null;
            user.Gender = NormalizeGender(request.Gender);
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;

            var normalizedInterests = (request.Interests ?? Array.Empty<string>())
                .Select(NormalizeInterestTag)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            db.UserInterests.RemoveRange(user.Interests);
            user.Interests.Clear();
            foreach (var interest in normalizedInterests)
            {
                user.Interests.Add(new UserInterest
                {
                    UserId = user.Id,
                    Tag = interest
                });
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new EditableUserProfileDto(
                user.Id,
                user.Nickname,
                user.DisplayName,
                user.AvatarUrl,
                user.Bio,
                user.BirthYear,
                user.Gender,
                normalizedInterests));
        }).RequireAuthorization();

        return group;
    }

    internal static async Task<UserProfileDto?> BuildUserProfileDtoAsync(
        AppDbContext db,
        Guid viewerUserId,
        Guid userId,
        CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(x => x.Interests)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var blocks = viewerUserId == Guid.Empty || viewerUserId == userId
            ? new List<UserBlock>()
            : await db.UserBlocks
                .AsNoTracking()
                .Where(x =>
                    (x.BlockerUserId == viewerUserId && x.BlockedUserId == userId) ||
                    (x.BlockerUserId == userId && x.BlockedUserId == viewerUserId))
                .ToListAsync(ct);

        var isBlockedByViewer = blocks.Any(x => x.BlockerUserId == viewerUserId && x.BlockedUserId == userId);
        var hasBlockedViewer = blocks.Any(x => x.BlockerUserId == userId && x.BlockedUserId == viewerUserId);
        var isInteractionBlocked = isBlockedByViewer || hasBlockedViewer;

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

        var isFriend = !isInteractionBlocked && viewerUserId != Guid.Empty && targetFriendIds.Contains(viewerUserId);
        var relationshipStatus = ResolveRelationshipStatus(viewerUserId, userId, directRelation, isBlockedByViewer, hasBlockedViewer);
        var mutualFriendsCount = viewerUserId == Guid.Empty || isInteractionBlocked
            ? 0
            : targetFriendIds.Intersect(viewerFriendIds).Where(x => x != userId && x != viewerUserId).Count();
        var friendsCount = targetFriendIds.Count;
        var canInviteToTable = viewerUserId != Guid.Empty && viewerUserId != userId && !isInteractionBlocked;
        var canMessageDirectly = isFriend && !isInteractionBlocked;
        var canEditProfile = viewerUserId == userId;

        Venue? activeCheckInVenue = null;
        Venue? activeHostedTableVenue = null;
        Venue? activeJoinedTableVenue = null;
        Venue? activeIntentionVenue = null;

        if (!isInteractionBlocked || canEditProfile)
        {
            activeCheckInVenue = await db.VenueCheckIns
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.ExpiresAtUtc >= now)
                .OrderByDescending(x => x.ExpiresAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    checkIn => checkIn.VenueId,
                    venue => venue.Id,
                    (checkIn, venue) => venue)
                .FirstOrDefaultAsync(ct);

            activeHostedTableVenue = await db.SocialTables
                .AsNoTracking()
                .Where(x => x.HostUserId == userId && x.Status == "open" && x.StartsAtUtc >= now)
                .OrderBy(x => x.StartsAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    table => table.VenueId,
                    venue => venue.Id,
                    (table, venue) => venue)
                .FirstOrDefaultAsync(ct);

            activeJoinedTableVenue = await db.SocialTableParticipants
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
                    (table, venue) => venue)
                .FirstOrDefaultAsync(ct);

            activeIntentionVenue = await db.VenueIntentions
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.EndsAtUtc >= now && x.StartsAtUtc <= now.AddHours(6))
                .OrderBy(x => x.StartsAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    intention => intention.VenueId,
                    venue => venue.Id,
                    (intention, venue) => venue)
                .FirstOrDefaultAsync(ct);
        }

        var (presenceState, statusLabel, currentVenueName, currentVenueCategory) = isInteractionBlocked && !canEditProfile
            ? ("hidden", "Profilo limitato", null, null)
            : ResolvePresence(activeCheckInVenue, activeHostedTableVenue, activeJoinedTableVenue, activeIntentionVenue);

        return new UserProfileDto(
            user.Id,
            user.Nickname,
            user.DisplayName,
            user.AvatarUrl,
            user.Bio,
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
            canInviteToTable,
            canMessageDirectly,
            canEditProfile,
            isBlockedByViewer,
            hasBlockedViewer,
            user.Interests
                .Select(x => x.Tag)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList());
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

    private static string ResolveRelationshipStatus(
        Guid viewerUserId,
        Guid targetUserId,
        FriendRelation? relation,
        bool isBlockedByViewer,
        bool hasBlockedViewer)
    {
        if (viewerUserId == Guid.Empty)
        {
            return "viewer_anonymous";
        }

        if (viewerUserId == targetUserId)
        {
            return "self";
        }

        if (isBlockedByViewer)
        {
            return "blocked_by_you";
        }

        if (hasBlockedViewer)
        {
            return "blocked_you";
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

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeOptionalUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.ToString() : null;
    }

    private static string NormalizeGender(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "male" or "female" or "non-binary" or "undisclosed" => normalized,
            _ => "undisclosed"
        };
    }

    private static string? NormalizeInterestTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 24)
        {
            trimmed = trimmed[..24];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
