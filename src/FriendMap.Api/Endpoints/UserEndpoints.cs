using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

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
                user.DiscoverablePhoneNormalized,
                user.DiscoverableEmailNormalized,
                user.Bio,
                user.BirthYear,
                user.Gender,
                user.Interests
                    .Select(x => x.Tag)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()));
        }).RequireAuthorization();

        async Task<IResult> UpdateMyProfileAsync(
            UpdateMyProfileRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct)
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

            await db.SaveChangesAsync(ct);

            var previousInterests = await db.UserInterests
                .Where(x => x.UserId == user.Id)
                .ToListAsync(ct);
            db.UserInterests.RemoveRange(previousInterests);

            if (normalizedInterests.Count > 0)
            {
                db.UserInterests.AddRange(normalizedInterests.Select(interest => new UserInterest
                {
                    UserId = user.Id,
                    Tag = interest
                }));
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new EditableUserProfileDto(
                user.Id,
                user.Nickname,
                user.DisplayName,
                user.AvatarUrl,
                user.DiscoverablePhoneNormalized,
                user.DiscoverableEmailNormalized,
                user.Bio,
                user.BirthYear,
                user.Gender,
                normalizedInterests));
        }

        group.MapPut("/me/profile", UpdateMyProfileAsync).RequireAuthorization();
        group.MapPost("/me/profile", UpdateMyProfileAsync).RequireAuthorization();

        group.MapPut("/me/discovery-identity", async (
            UpdateDiscoveryIdentityRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            if (user is null)
            {
                return Results.NotFound();
            }

            user.DiscoverablePhoneNormalized = NormalizePhone(request.PhoneNumber);
            user.DiscoverableEmailNormalized = NormalizeEmail(request.Email);
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                phoneNumber = user.DiscoverablePhoneNormalized,
                email = user.DiscoverableEmailNormalized
            });
        }).RequireAuthorization();

        group.MapPost("/contacts/match", async (
            MatchContactsRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var normalizedPhones = (request.Phones ?? Array.Empty<string>())
                .Select(NormalizePhone)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToList();

            var normalizedEmails = (request.Emails ?? Array.Empty<string>())
                .Select(NormalizeEmail)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToList();

            if (normalizedPhones.Count == 0 && normalizedEmails.Count == 0)
            {
                return Results.Ok(Array.Empty<ContactMatchDto>());
            }

            var blocks = await db.UserBlocks
                .AsNoTracking()
                .Where(x => x.BlockerUserId == currentUserId || x.BlockedUserId == currentUserId)
                .ToListAsync(ct);

            var blockedUserIds = blocks
                .Select(x => x.BlockerUserId == currentUserId ? x.BlockedUserId : x.BlockerUserId)
                .ToHashSet();

            var matchedUsers = await db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id != currentUserId &&
                    !blockedUserIds.Contains(x.Id) &&
                    ((x.DiscoverablePhoneNormalized != null && normalizedPhones.Contains(x.DiscoverablePhoneNormalized)) ||
                     (x.DiscoverableEmailNormalized != null && normalizedEmails.Contains(x.DiscoverableEmailNormalized))))
                .OrderBy(x => x.DisplayName ?? x.Nickname)
                .Take(100)
                .ToListAsync(ct);

            if (matchedUsers.Count == 0)
            {
                return Results.Ok(Array.Empty<ContactMatchDto>());
            }

            var matchedUserIds = matchedUsers.Select(x => x.Id).ToList();
            var relations = await db.FriendRelations
                .AsNoTracking()
                .Where(x =>
                    (x.RequesterId == currentUserId && matchedUserIds.Contains(x.AddresseeId)) ||
                    (x.AddresseeId == currentUserId && matchedUserIds.Contains(x.RequesterId)))
                .ToListAsync(ct);

            var presenceByUser = await LoadPresenceByUserAsync(db, matchedUserIds, ct);

            var matches = matchedUsers.Select(user =>
            {
                var relation = relations.FirstOrDefault(x =>
                    (x.RequesterId == currentUserId && x.AddresseeId == user.Id) ||
                    (x.RequesterId == user.Id && x.AddresseeId == currentUserId));

                var matchSource = user.DiscoverablePhoneNormalized is not null && normalizedPhones.Contains(user.DiscoverablePhoneNormalized)
                    ? "phone"
                    : "email";

                var snapshot = presenceByUser.GetValueOrDefault(user.Id) ?? new PresenceSnapshot("idle", "Nessuna presenza live", null, null);

                return new ContactMatchDto(
                    user.Id,
                    user.Nickname,
                    user.DisplayName,
                    user.AvatarUrl,
                    ResolveRelationshipStatus(currentUserId, user.Id, relation, false, false),
                    matchSource,
                    snapshot.CurrentVenueName,
                    snapshot.CurrentVenueCategory,
                    snapshot.StatusLabel);
            }).ToList();

            return Results.Ok(matches);
        }).RequireAuthorization();

        group.MapGet("/search", async (
            string q,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var query = q?.Trim();
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Results.Ok(Array.Empty<UserSearchResultDto>());
            }

            var blocks = await db.UserBlocks
                .AsNoTracking()
                .Where(x => x.BlockerUserId == currentUserId || x.BlockedUserId == currentUserId)
                .ToListAsync(ct);

            var blockedUserIds = blocks
                .Select(x => x.BlockerUserId == currentUserId ? x.BlockedUserId : x.BlockerUserId)
                .ToHashSet();

            var users = await db.Users
                .AsNoTracking()
                .Include(x => x.Interests)
                .Where(x =>
                    x.Id != currentUserId &&
                    !blockedUserIds.Contains(x.Id) &&
                    (EF.Functions.ILike(x.Nickname, $"%{query}%") ||
                     (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, $"%{query}%"))))
                .OrderBy(x => x.DisplayName ?? x.Nickname)
                .Take(20)
                .ToListAsync(ct);

            var relations = await db.FriendRelations
                .AsNoTracking()
                .Where(x =>
                    (x.RequesterId == currentUserId && users.Select(u => u.Id).Contains(x.AddresseeId)) ||
                    (x.AddresseeId == currentUserId && users.Select(u => u.Id).Contains(x.RequesterId)))
                .ToListAsync(ct);

            var results = users.Select(user =>
            {
                var relation = relations.FirstOrDefault(x =>
                    (x.RequesterId == currentUserId && x.AddresseeId == user.Id) ||
                    (x.RequesterId == user.Id && x.AddresseeId == currentUserId));

                return new UserSearchResultDto(
                    user.Id,
                    user.Nickname,
                    user.DisplayName,
                    user.AvatarUrl,
                    ResolveRelationshipStatus(currentUserId, user.Id, relation, false, false),
                    false,
                    false,
                    user.Interests.Select(x => x.Tag).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
            }).ToList();

            return Results.Ok(results);
        }).RequireAuthorization();

        group.MapDelete("/me", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            if (user is null)
            {
                return Results.NotFound();
            }

            // Hard delete cascades (configure in DbContext or handle manually)
            db.Users.Remove(user);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { deleted = true, message = "Account eliminato." });
        }).RequireAuthorization();

        group.MapPost("/me/avatar", async (
            HttpRequest request,
            ClaimsPrincipal principal,
            MediaStorageService mediaStorage,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
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
                return Results.BadRequest(new { message = "File avatar mancante." });
            }

            const long maxBytes = 5 * 1024 * 1024;
            if (file.Length > maxBytes)
            {
                return Results.BadRequest(new { message = "Avatar troppo grande. Massimo 5MB." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(extension))
            {
                return Results.BadRequest(new { message = "Formato non supportato. Usa jpg, png o webp." });
            }

            var user = await db.Users
                .Include(x => x.Interests)
                .FirstOrDefaultAsync(x => x.Id == currentUserId, ct);

            if (user is null)
            {
                return Results.NotFound();
            }

            try
            {
                user.AvatarUrl = await mediaStorage.UploadAsync(file, "uploads/avatars", currentUserId, request, ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
            catch (Amazon.S3.AmazonS3Exception ex)
            {
                return Results.Problem($"Storage media non configurato correttamente: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
            }

            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new EditableUserProfileDto(
                user.Id,
                user.Nickname,
                user.DisplayName,
                user.AvatarUrl,
                user.DiscoverablePhoneNormalized,
                user.DiscoverableEmailNormalized,
                user.Bio,
                user.BirthYear,
                user.Gender,
                user.Interests.Select(x => x.Tag).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()));
        }).RequireAuthorization();

        group.MapGet("/me/recap", async (
            string? period,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var normalizedPeriod = string.Equals(period, "year", StringComparison.OrdinalIgnoreCase) ? "year" : "month";
            var rangeEnd = DateTimeOffset.UtcNow;
            var rangeStart = normalizedPeriod == "year"
                ? rangeEnd.AddDays(-365)
                : rangeEnd.AddDays(-30);

            var checkIns = await db.VenueCheckIns
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId && x.CreatedAtUtc >= rangeStart && x.CreatedAtUtc <= rangeEnd)
                .ToListAsync(ct);

            var hostedTables = await db.SocialTables
                .AsNoTracking()
                .Where(x => x.HostUserId == currentUserId && x.CreatedAtUtc >= rangeStart && x.CreatedAtUtc <= rangeEnd)
                .ToListAsync(ct);

            var joinedTables = await db.SocialTableParticipants
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId && x.Status == "accepted" && x.CreatedAtUtc >= rangeStart && x.CreatedAtUtc <= rangeEnd)
                .ToListAsync(ct);

            var venueIds = checkIns.Select(x => x.VenueId).Distinct().ToList();
            var venueMap = venueIds.Count == 0
                ? new Dictionary<Guid, Venue>()
                : await db.Venues
                    .AsNoTracking()
                    .Where(x => venueIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

            var topVenues = checkIns
                .GroupBy(x => x.VenueId)
                .OrderByDescending(x => x.Count())
                .Take(5)
                .Select(group =>
                {
                    venueMap.TryGetValue(group.Key, out var venue);
                    return new VenueRecapItemDto(
                        group.Key,
                        venue?.Name ?? "Locale",
                        venue?.Category ?? "venue",
                        group.Count());
                })
                .ToList();

            var sharedFriendIds = await db.FriendRelations
                .AsNoTracking()
                .Where(x => x.Status == "accepted" && (x.RequesterId == currentUserId || x.AddresseeId == currentUserId))
                .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
                .ToListAsync(ct);

            var topPeople = sharedFriendIds.Count == 0
                ? new List<FriendRecapItemDto>()
                : await db.Users
                    .AsNoTracking()
                    .Where(x => sharedFriendIds.Contains(x.Id))
                    .OrderBy(x => x.DisplayName ?? x.Nickname)
                    .Take(5)
                    .Select(x => new FriendRecapItemDto(
                        x.Id,
                        x.Nickname,
                        x.DisplayName,
                        x.AvatarUrl,
                        0))
                    .ToListAsync(ct);

            var recap = new UserRecapDto(
                normalizedPeriod,
                rangeStart,
                rangeEnd,
                checkIns.Count,
                checkIns.Select(x => x.VenueId).Distinct().Count(),
                hostedTables.Count,
                joinedTables.Count,
                checkIns
                    .Select(x => x.CreatedAtUtc.ToOffset(TimeSpan.Zero).Date)
                    .Distinct()
                    .Count(),
                topVenues,
                topPeople);

            return Results.Ok(recap);
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

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var digits = Regex.Replace(trimmed, "[^0-9]", string.Empty);
        if (digits.Length < 8)
        {
            return null;
        }

        return trimmed.StartsWith('+')
            ? $"+{digits}"
            : digits;
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("@", StringComparison.Ordinal) ? normalized : null;
    }

    private sealed record PresenceSnapshot(string PresenceState, string StatusLabel, string? CurrentVenueName, string? CurrentVenueCategory);

    private static async Task<Dictionary<Guid, PresenceSnapshot>> LoadPresenceByUserAsync(AppDbContext db, List<Guid> userIds, CancellationToken ct)
    {
        var snapshots = userIds.ToDictionary(x => x, _ => new PresenceSnapshot("idle", "Nessuna presenza live", null, null));
        if (userIds.Count == 0)
        {
            return snapshots;
        }

        var now = DateTimeOffset.UtcNow;

        var checkIns = await db.VenueCheckIns
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.ExpiresAtUtc >= now)
            .Join(
                db.Venues.AsNoTracking(),
                checkIn => checkIn.VenueId,
                venue => venue.Id,
                (checkIn, venue) => new { checkIn.UserId, venue.Name, venue.Category, checkIn.ExpiresAtUtc })
            .ToListAsync(ct);

        foreach (var item in checkIns
                     .OrderByDescending(x => x.ExpiresAtUtc)
                     .GroupBy(x => x.UserId)
                     .Select(x => x.First()))
        {
            snapshots[item.UserId] = new PresenceSnapshot("checked_in", $"Ora a {item.Name}", item.Name, item.Category);
        }

        return snapshots;
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
