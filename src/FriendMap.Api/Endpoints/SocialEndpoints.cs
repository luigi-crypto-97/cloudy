using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Hubs;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class SocialEndpoints
{
    public static RouteGroupBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/social").WithTags("Social").RequireAuthorization().RequireRateLimiting("social");

        group.MapGet("/hub", async (
            AppDbContext db,
            ClaimsPrincipal user,
            MediaStorageService mediaStorage,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var blockedUserIds = await db.UserBlocks
                .AsNoTracking()
                .Where(x => x.BlockerUserId == currentUserId || x.BlockedUserId == currentUserId)
                .Select(x => x.BlockerUserId == currentUserId ? x.BlockedUserId : x.BlockerUserId)
                .ToListAsync(ct);

            var relations = await db.FriendRelations
                .AsNoTracking()
                .Where(x => x.RequesterId == currentUserId || x.AddresseeId == currentUserId)
                .Where(x => !blockedUserIds.Contains(x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId))
                .ToListAsync(ct);

            var friendIds = relations
                .Where(x => x.Status == "accepted")
                .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
                .Distinct()
                .ToList();
            var incomingRequestIds = relations
                .Where(x => x.Status == "pending" && x.AddresseeId == currentUserId)
                .Select(x => x.RequesterId)
                .Distinct()
                .ToList();
            var outgoingRequestIds = relations
                .Where(x => x.Status == "pending" && x.RequesterId == currentUserId)
                .Select(x => x.AddresseeId)
                .Distinct()
                .ToList();

            var connectionIds = friendIds
                .Concat(incomingRequestIds)
                .Concat(outgoingRequestIds)
                .Distinct()
                .ToList();

            var users = connectionIds.Count == 0
                ? new Dictionary<Guid, AppUser>()
                : await db.Users
                    .AsNoTracking()
                    .Where(x => connectionIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

            var friendSet = friendIds.ToHashSet();
            var presence = await LoadPresenceByUserAsync(db, connectionIds, ct);

            var connectionFriendRelations = connectionIds.Count == 0
                ? new List<FriendRelation>()
                : await db.FriendRelations
                    .AsNoTracking()
                    .Where(x => (connectionIds.Contains(x.RequesterId) || connectionIds.Contains(x.AddresseeId)) && x.Status == "accepted")
                    .ToListAsync(ct);

            var connectionFriendSets = connectionIds.ToDictionary(
                id => id,
                id => connectionFriendRelations
                    .Where(x => x.RequesterId == id || x.AddresseeId == id)
                    .Select(x => x.RequesterId == id ? x.AddresseeId : x.RequesterId)
                    .ToHashSet());

            var friends = friendIds
                .Where(users.ContainsKey)
                .Select(id => BuildConnectionDto(users[id], mediaStorage, "friend", friendSet, currentUserId, presence, connectionFriendSets))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var incoming = incomingRequestIds
                .Where(users.ContainsKey)
                .Select(id => BuildConnectionDto(users[id], mediaStorage, "pending_received", friendSet, currentUserId, presence, connectionFriendSets))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var outgoing = outgoingRequestIds
                .Where(users.ContainsKey)
                .Select(id => BuildConnectionDto(users[id], mediaStorage, "pending_sent", friendSet, currentUserId, presence, connectionFriendSets))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tableInviteRows = await db.SocialTableParticipants
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId && x.Status == "invited")
                .Join(
                    db.SocialTables.AsNoTracking().Where(x => x.Status == "open"),
                    participant => participant.SocialTableId,
                    table => table.Id,
                    (participant, table) => table)
                .Join(
                    db.Venues.AsNoTracking(),
                    table => table.VenueId,
                    venue => venue.Id,
                    (table, venue) => new { Table = table, Venue = venue })
                .Join(
                    db.Users.AsNoTracking(),
                    tuple => tuple.Table.HostUserId,
                    host => host.Id,
                    (tuple, host) => new { tuple.Table, tuple.Venue, HostUser = host })
                .Where(x => !blockedUserIds.Contains(x.HostUser.Id))
                .OrderBy(x => x.Table.StartsAtUtc)
                .Select(x => new
                {
                    x.Table.Id,
                    x.Table.Title,
                    x.Table.StartsAtUtc,
                    VenueName = x.Venue.Name,
                    VenueCategory = x.Venue.Category,
                    HostUserId = x.HostUser.Id,
                    x.HostUser.Nickname,
                    x.HostUser.DisplayName,
                    x.HostUser.AvatarUrl
                })
                .ToListAsync(ct);
            var tableInvites = tableInviteRows
                .Select(x => new SocialTableInviteDto(
                    x.Id,
                    x.Title,
                    x.StartsAtUtc,
                    x.VenueName,
                    x.VenueCategory,
                    x.HostUserId,
                    x.Nickname,
                    x.DisplayName,
                    mediaStorage.ResolveUrl(x.AvatarUrl)))
                .ToList();

            return Results.Ok(new SocialHubDto(friends, incoming, outgoing, tableInvites));
        });

        group.MapGet("/me/state", async (
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var appUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            if (appUser is null)
            {
                return Results.NotFound();
            }

            var now = DateTimeOffset.UtcNow;
            var activeCheckIn = await db.VenueCheckIns
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId && x.ExpiresAtUtc >= now)
                .OrderByDescending(x => x.ExpiresAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    checkIn => checkIn.VenueId,
                    venue => venue.Id,
                    (checkIn, venue) => new { checkIn.VenueId, venue.Name })
                .FirstOrDefaultAsync(ct);

            var activeIntention = await db.VenueIntentions
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId && x.EndsAtUtc >= now && x.StartsAtUtc <= now.AddHours(6))
                .OrderBy(x => x.StartsAtUtc)
                .Join(
                    db.Venues.AsNoTracking(),
                    intention => intention.VenueId,
                    venue => venue.Id,
                    (intention, venue) => new { intention.VenueId, venue.Name })
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new SocialMeStateDto(
                appUser.IsGhostModeEnabled,
                appUser.SharePresenceWithFriends,
                appUser.ShareIntentionsWithFriends,
                activeCheckIn?.VenueId,
                activeCheckIn?.Name,
                activeIntention?.VenueId,
                activeIntention?.Name));
        });

        group.MapPost("/me/privacy", async (
            UpdatePrivacySettingsRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var appUser = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            if (appUser is null)
            {
                return Results.NotFound();
            }

            if (request.IsGhostModeEnabled is bool ghost)
            {
                appUser.IsGhostModeEnabled = ghost;
            }

            if (request.SharePresenceWithFriends is bool sharePresence)
            {
                appUser.SharePresenceWithFriends = sharePresence;
            }

            if (request.ShareIntentionsWithFriends is bool shareIntentions)
            {
                appUser.ShareIntentionsWithFriends = shareIntentions;
            }

            appUser.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("updated", "Impostazioni privacy aggiornate."));
        });

        group.MapPost("/intentions", async (
            CreateIntentionRequest request,
            AppDbContext db,
            AffluenceAggregationService aggregationService,
            IOptions<PrivacyOptions> privacy,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != request.UserId)
            {
                return Results.Forbid();
            }

            if (request.UserId == Guid.Empty || request.VenueId == Guid.Empty)
            {
                return Results.BadRequest("userId and venueId are required.");
            }

            if (request.EndsAtUtc <= request.StartsAtUtc)
            {
                return Results.BadRequest("endsAtUtc must be after startsAtUtc.");
            }

            var exists = await db.Users.AnyAsync(x => x.Id == request.UserId, ct)
                && await db.Venues.AnyAsync(x => x.Id == request.VenueId, ct);
            if (!exists) return Results.NotFound("User or venue not found.");

            var maxEnd = request.StartsAtUtc.AddHours(privacy.Value.IntentionTtlHours);
            var entity = new VenueIntention
            {
                UserId = request.UserId,
                VenueId = request.VenueId,
                StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = request.EndsAtUtc > maxEnd ? maxEnd : request.EndsAtUtc,
                Note = request.Note
            };

            db.VenueIntentions.Add(entity);
            await db.SaveChangesAsync(ct);
            await aggregationService.RebuildVenueSnapshotAsync(request.VenueId, ct);

            return Results.Created($"/api/social/intentions/{entity.Id}", entity);
        });

        group.MapPost("/check-ins", async (
            CreateCheckInRequest request,
            AppDbContext db,
            AffluenceAggregationService aggregationService,
            IOptions<PrivacyOptions> privacy,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != request.UserId)
            {
                return Results.Forbid();
            }

            if (request.UserId == Guid.Empty || request.VenueId == Guid.Empty)
            {
                return Results.BadRequest("userId and venueId are required.");
            }

            var exists = await db.Users.AnyAsync(x => x.Id == request.UserId, ct)
                && await db.Venues.AnyAsync(x => x.Id == request.VenueId, ct);
            if (!exists) return Results.NotFound("User or venue not found.");

            var ttl = request.TtlMinutes <= 0 ? privacy.Value.CheckInTtlMinutes : request.TtlMinutes;
            var entity = new VenueCheckIn
            {
                UserId = request.UserId,
                VenueId = request.VenueId,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(ttl),
                IsManual = true
            };

            db.VenueCheckIns.Add(entity);
            await db.SaveChangesAsync(ct);
            await aggregationService.RebuildVenueSnapshotAsync(request.VenueId, ct);

            return Results.Created($"/api/social/check-ins/{entity.Id}", entity);
        });

        group.MapPost("/check-ins/exit", async (
            AppDbContext db,
            AffluenceAggregationService aggregationService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var now = DateTimeOffset.UtcNow;
            var activeCheckIns = await db.VenueCheckIns
                .Where(x => x.UserId == currentUserId && x.ExpiresAtUtc >= now)
                .ToListAsync(ct);

            if (activeCheckIns.Count == 0)
            {
                return Results.Ok(new SocialActionResultDto("noop", "Nessun check-in attivo."));
            }

            var affectedVenueIds = activeCheckIns.Select(x => x.VenueId).Distinct().ToList();
            db.VenueCheckIns.RemoveRange(activeCheckIns);
            await db.SaveChangesAsync(ct);

            foreach (var venueId in affectedVenueIds)
            {
                await aggregationService.RebuildVenueSnapshotAsync(venueId, ct);
            }

            return Results.Ok(new SocialActionResultDto("checked_out", "Check-in terminato."));
        });

        group.MapPost("/live-location", async (
            UpdateLiveLocationRequest request,
            AppDbContext db,
            AffluenceAggregationService aggregationService,
            IOptions<PrivacyOptions> privacy,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != request.UserId)
            {
                return Results.Forbid();
            }

            if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            {
                return Results.BadRequest("Coordinate non valide.");
            }

            var appUser = await db.Users.FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            if (appUser is null)
            {
                return Results.NotFound();
            }

            if (appUser.IsGhostModeEnabled || !appUser.SharePresenceWithFriends)
            {
                var cleared = await RemoveAutomaticCheckInsAsync(db, aggregationService, currentUserId, ct);
                return Results.Ok(new LiveLocationUpdateResultDto(
                    cleared ? "disabled_cleared" : "disabled",
                    null,
                    null,
                    null,
                    null));
            }

            var point = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
            var radiusMeters = Math.Clamp((request.AccuracyMeters ?? 50d) * 2.5d, 80d, 250d);
            var nearest = await db.Venues
                .AsNoTracking()
                .Where(x =>
                    x.Location != null &&
                    (x.VisibilityStatus == "published" || x.VisibilityStatus == "public") &&
                    x.Location.Distance(point) <= radiusMeters)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    DistanceMeters = x.Location!.Distance(point)
                })
                .OrderBy(x => x.DistanceMeters)
                .FirstOrDefaultAsync(ct);

            if (nearest is null)
            {
                var cleared = await RemoveAutomaticCheckInsAsync(db, aggregationService, currentUserId, ct);
                return Results.Ok(new LiveLocationUpdateResultDto(
                    cleared ? "outside_venues_cleared" : "outside_venues",
                    null,
                    null,
                    null,
                    null));
            }

            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddMinutes(Math.Max(10, privacy.Value.CheckInTtlMinutes / 2));
            var affectedVenueIds = new HashSet<Guid> { nearest.Id };
            var automaticCheckIns = await db.VenueCheckIns
                .Where(x => x.UserId == currentUserId && !x.IsManual && x.ExpiresAtUtc >= now)
                .ToListAsync(ct);

            var current = automaticCheckIns.FirstOrDefault(x => x.VenueId == nearest.Id);
            foreach (var stale in automaticCheckIns.Where(x => x.VenueId != nearest.Id))
            {
                affectedVenueIds.Add(stale.VenueId);
                db.VenueCheckIns.Remove(stale);
            }

            if (current is null)
            {
                db.VenueCheckIns.Add(new VenueCheckIn
                {
                    UserId = currentUserId,
                    VenueId = nearest.Id,
                    ExpiresAtUtc = expiresAt,
                    IsManual = false,
                    VisibilityLevel = "friends_or_aggregate"
                });
            }
            else
            {
                current.ExpiresAtUtc = expiresAt;
            }

            await db.SaveChangesAsync(ct);
            foreach (var venueId in affectedVenueIds)
            {
                await aggregationService.RebuildVenueSnapshotAsync(venueId, ct);
            }

            return Results.Ok(new LiveLocationUpdateResultDto(
                "updated",
                nearest.Id,
                nearest.Name,
                expiresAt,
                nearest.DistanceMeters));
        });

        group.MapPost("/live-location/stop", async (
            AppDbContext db,
            AffluenceAggregationService aggregationService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var cleared = await RemoveAutomaticCheckInsAsync(db, aggregationService, currentUserId, ct);
            return Results.Ok(new SocialActionResultDto(
                cleared ? "live_location_stopped" : "noop",
                cleared ? "Posizione live disattivata." : "Posizione live gia disattivata."));
        });

        group.MapPost("/friends/{targetUserId:guid}/request", async (
            Guid targetUserId,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (targetUserId == Guid.Empty || targetUserId == currentUserId)
            {
                return Results.BadRequest("Seleziona un altro utente.");
            }

            var targetExists = await db.Users.AnyAsync(x => x.Id == targetUserId, ct);
            if (!targetExists)
            {
                return Results.NotFound("Utente non trovato.");
            }

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                (x.BlockerUserId == currentUserId && x.BlockedUserId == targetUserId) ||
                (x.BlockerUserId == targetUserId && x.BlockedUserId == currentUserId), ct);
            if (blocked)
            {
                return Results.Conflict("Relazione non disponibile con questo utente.");
            }

            var relation = await db.FriendRelations
                .FirstOrDefaultAsync(x =>
                    (x.RequesterId == currentUserId && x.AddresseeId == targetUserId) ||
                    (x.RequesterId == targetUserId && x.AddresseeId == currentUserId), ct);

            if (relation is not null)
            {
                return relation.Status switch
                {
                    "accepted" => Results.Conflict("Siete già amici."),
                    "pending" when relation.RequesterId == currentUserId => Results.Conflict("Richiesta già inviata."),
                    "pending" => Results.Conflict("Hai già una richiesta da accettare."),
                    _ => Results.Conflict("Relazione utente non modificabile.")
                };
            }

            relation = new FriendRelation
            {
                RequesterId = currentUserId,
                AddresseeId = targetUserId,
                Status = "pending"
            };

            db.FriendRelations.Add(relation);
            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                targetUserId,
                "Nuova richiesta di amicizia",
                "Qualcuno vuole aggiungerti su FriendMap.",
                new { type = "friend_request", requesterUserId = currentUserId },
                ct);

            return Results.Ok(new SocialActionResultDto("pending_sent", "Richiesta di amicizia inviata."));
        });

        group.MapPost("/friends/{targetUserId:guid}/accept", async (
            Guid targetUserId,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                (x.BlockerUserId == currentUserId && x.BlockedUserId == targetUserId) ||
                (x.BlockerUserId == targetUserId && x.BlockedUserId == currentUserId), ct);
            if (blocked)
            {
                return Results.Conflict("Relazione non disponibile con questo utente.");
            }

            var relation = await db.FriendRelations
                .FirstOrDefaultAsync(x =>
                    x.RequesterId == targetUserId &&
                    x.AddresseeId == currentUserId &&
                    x.Status == "pending", ct);

            if (relation is null)
            {
                return Results.NotFound("Nessuna richiesta da accettare.");
            }

            relation.Status = "accepted";
            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                targetUserId,
                "Richiesta accettata",
                "Ora siete amici su FriendMap.",
                new { type = "friend_accept", addresseeUserId = currentUserId },
                ct);

            return Results.Ok(new SocialActionResultDto("friend", "Amicizia confermata."));
        });

        group.MapPost("/friends/{targetUserId:guid}/reject", async (
            Guid targetUserId,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var relation = await db.FriendRelations
                .FirstOrDefaultAsync(x =>
                    x.Status == "pending" &&
                    ((x.RequesterId == targetUserId && x.AddresseeId == currentUserId) ||
                     (x.RequesterId == currentUserId && x.AddresseeId == targetUserId)), ct);

            if (relation is null)
            {
                return Results.NotFound("Nessuna richiesta pendente.");
            }

            db.FriendRelations.Remove(relation);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("removed", "Richiesta rimossa."));
        });

        group.MapPost("/tables", async (
            CreateSocialTableRequest request,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != request.HostUserId)
            {
                return Results.Forbid();
            }

            if (request.HostUserId == Guid.Empty || request.VenueId == Guid.Empty)
            {
                return Results.BadRequest("hostUserId and venueId are required.");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest("title is required.");
            }

            if (request.Capacity is < 2 or > 20)
            {
                return Results.BadRequest("capacity must be between 2 and 20.");
            }

            var exists = await db.Users.AnyAsync(x => x.Id == request.HostUserId, ct)
                && await db.Venues.AnyAsync(x => x.Id == request.VenueId, ct);
            if (!exists) return Results.NotFound("Host user or venue not found.");

            var entity = new SocialTable
            {
                HostUserId = request.HostUserId,
                VenueId = request.VenueId,
                Title = request.Title.Trim(),
                Description = request.Description,
                StartsAtUtc = request.StartsAtUtc,
                Capacity = request.Capacity,
                JoinPolicy = string.IsNullOrWhiteSpace(request.JoinPolicy) ? "approval" : request.JoinPolicy.Trim()
            };

            db.SocialTables.Add(entity);
            db.SocialTableParticipants.Add(new SocialTableParticipant
            {
                SocialTableId = entity.Id,
                UserId = currentUserId,
                Status = "accepted"
            });
            await db.SaveChangesAsync(ct);

            var venue = await db.Venues.AsNoTracking().FirstAsync(x => x.Id == entity.VenueId, ct);
            var nearbyUserIds = await db.VenueCheckIns
                .AsNoTracking()
                .Where(x => x.VenueId == entity.VenueId && x.UserId != currentUserId && x.ExpiresAtUtc >= DateTimeOffset.UtcNow)
                .Select(x => x.UserId)
                .Distinct()
                .Take(50)
                .ToListAsync(ct);

            foreach (var nearbyUserId in nearbyUserIds)
            {
                await outbox.EnqueueAsync(
                    nearbyUserId,
                    "Nuovo tavolo vicino",
                    $"È nato un tavolo da {venue.Name}: {entity.Title}.",
                    new { type = "social_table_nearby", tableId = entity.Id, venueId = venue.Id, hostUserId = currentUserId },
                    ct,
                    outbox.BuildDeepLink("table", entity.Id));
            }

            return Results.Created(
                $"/api/social/tables/{entity.Id}",
                BuildSocialTableSummaryDto(entity, venue, "host", new[] { new SocialTableParticipant { SocialTableId = entity.Id, UserId = currentUserId, Status = "accepted" } }));
        });

        group.MapGet("/tables/mine", async (
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var now = DateTimeOffset.UtcNow;
            var tables = await db.SocialTables
                .AsNoTracking()
                .Where(x => x.StartsAtUtc >= now && (
                    x.HostUserId == currentUserId ||
                    db.SocialTableParticipants.Any(p => p.SocialTableId == x.Id && p.UserId == currentUserId && (p.Status == "accepted" || p.Status == "requested" || p.Status == "invited"))))
                .OrderBy(x => x.StartsAtUtc)
                .ToListAsync(ct);

            var tableIds = tables.Select(x => x.Id).ToList();
            var venues = tableIds.Count == 0
                ? new Dictionary<Guid, Venue>()
                : await db.Venues.AsNoTracking().Where(x => tables.Select(t => t.VenueId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var participants = tableIds.Count == 0
                ? new List<SocialTableParticipant>()
                : await db.SocialTableParticipants.AsNoTracking().Where(x => tableIds.Contains(x.SocialTableId)).ToListAsync(ct);

            var result = tables
                .Select(table =>
                {
                    venues.TryGetValue(table.VenueId, out var venue);
                    var membership = table.HostUserId == currentUserId
                        ? "host"
                        : participants.FirstOrDefault(x => x.SocialTableId == table.Id && x.UserId == currentUserId)?.Status ?? "none";

                    return BuildSocialTableSummaryDto(table, venue, membership, participants);
                })
                .ToList();

            return Results.Ok(result);
        });

        group.MapPost("/tables/mine/invite", async (
            InviteToHostedTableRequest request,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (request.TargetUserId == Guid.Empty || request.TargetUserId == currentUserId)
            {
                return Results.BadRequest("Seleziona un altro utente.");
            }

            var targetExists = await db.Users.AnyAsync(x => x.Id == request.TargetUserId, ct);
            if (!targetExists)
            {
                return Results.NotFound("Utente non trovato.");
            }

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                (x.BlockerUserId == currentUserId && x.BlockedUserId == request.TargetUserId) ||
                (x.BlockerUserId == request.TargetUserId && x.BlockedUserId == currentUserId), ct);
            if (blocked)
            {
                return Results.Conflict("Invito non disponibile per questo utente.");
            }

            var now = DateTimeOffset.UtcNow;
            var table = await db.SocialTables
                .OrderBy(x => x.StartsAtUtc)
                .FirstOrDefaultAsync(x =>
                    x.HostUserId == currentUserId &&
                    x.Status == "open" &&
                    x.StartsAtUtc >= now, ct);

            if (table is null)
            {
                return Results.Conflict("Apri prima un tavolo sociale per invitare qualcuno.");
            }

            var existingParticipant = await db.SocialTableParticipants
                .FirstOrDefaultAsync(x => x.SocialTableId == table.Id && x.UserId == request.TargetUserId, ct);

            if (existingParticipant is not null)
            {
                return existingParticipant.Status switch
                {
                    "accepted" => Results.Conflict("Questa persona è già dentro il tuo tavolo."),
                    "requested" => Results.Conflict("Questa persona ha già chiesto di unirsi."),
                    "invited" => Results.Conflict("Invito già inviato."),
                    _ => Results.Conflict("Partecipazione già presente.")
                };
            }

            var acceptedCount = await db.SocialTableParticipants
                .CountAsync(x => x.SocialTableId == table.Id && x.Status == "accepted", ct);
            if (acceptedCount >= table.Capacity)
            {
                return Results.Conflict("Il tavolo è pieno.");
            }

            db.SocialTableParticipants.Add(new SocialTableParticipant
            {
                SocialTableId = table.Id,
                UserId = request.TargetUserId,
                Status = "accepted"
            });

            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                request.TargetUserId,
                "Invito a un tavolo",
                $"Sei dentro {table.Title}.",
                new { type = "social_table_invite_accepted", tableId = table.Id, hostUserId = currentUserId },
                ct,
                outbox.BuildDeepLink("table", table.Id));

            return Results.Ok(new SocialActionResultDto("accepted", "Amico aggiunto al tavolo."));
        });

        group.MapPost("/tables/{tableId:guid}/invite", async (
            Guid tableId,
            InviteToHostedTableRequest request,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (request.TargetUserId == Guid.Empty || request.TargetUserId == currentUserId)
            {
                return Results.BadRequest("Seleziona un altro utente.");
            }

            var table = await db.SocialTables
                .FirstOrDefaultAsync(x => x.Id == tableId && x.HostUserId == currentUserId && x.Status == "open", ct);
            if (table is null)
            {
                return Results.NotFound("Tavolo non trovato.");
            }

            var targetExists = await db.Users.AnyAsync(x => x.Id == request.TargetUserId, ct);
            if (!targetExists)
            {
                return Results.NotFound("Utente non trovato.");
            }

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                (x.BlockerUserId == currentUserId && x.BlockedUserId == request.TargetUserId) ||
                (x.BlockerUserId == request.TargetUserId && x.BlockedUserId == currentUserId), ct);
            if (blocked)
            {
                return Results.Conflict("Invito non disponibile per questo utente.");
            }

            var existingParticipant = await db.SocialTableParticipants
                .FirstOrDefaultAsync(x => x.SocialTableId == table.Id && x.UserId == request.TargetUserId, ct);

            if (existingParticipant is not null)
            {
                return existingParticipant.Status switch
                {
                    "accepted" => Results.Conflict("Questa persona è già dentro il tuo tavolo."),
                    "requested" => Results.Conflict("Questa persona ha già chiesto di unirsi."),
                    "invited" => Results.Conflict("Invito già inviato."),
                    _ => Results.Conflict("Partecipazione già presente.")
                };
            }

            var acceptedCount = await db.SocialTableParticipants
                .CountAsync(x => x.SocialTableId == table.Id && x.Status == "accepted", ct);
            if (acceptedCount >= table.Capacity)
            {
                return Results.Conflict("Il tavolo è pieno.");
            }

            db.SocialTableParticipants.Add(new SocialTableParticipant
            {
                SocialTableId = table.Id,
                UserId = request.TargetUserId,
                Status = "accepted"
            });

            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                request.TargetUserId,
                "Invito a un tavolo",
                $"Sei dentro {table.Title}.",
                new { type = "social_table_invite_accepted", tableId = table.Id, hostUserId = currentUserId },
                ct,
                outbox.BuildDeepLink("table", table.Id));

            return Results.Ok(new SocialActionResultDto("accepted", "Amico aggiunto al tavolo."));
        });

        group.MapGet("/tables/{tableId:guid}/thread", async (
            Guid tableId,
            AppDbContext db,
            ClaimsPrincipal user,
            MediaStorageService mediaStorage,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var table = await db.SocialTables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (table is null)
            {
                return Results.NotFound();
            }

            var participant = await db.SocialTableParticipants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SocialTableId == tableId && x.UserId == currentUserId, ct);
            var isHost = table.HostUserId == currentUserId;
            if (!isHost && participant is null)
            {
                return Results.Forbid();
            }

            var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == table.VenueId, ct);
            var participants = await db.SocialTableParticipants.AsNoTracking().Where(x => x.SocialTableId == tableId).ToListAsync(ct);
            var userIds = participants.Select(x => x.UserId).Append(table.HostUserId).Distinct().ToList();
            var users = userIds.Count == 0
                ? new Dictionary<Guid, AppUser>()
                : await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var requests = participants
                .Where(x => x.Status == "requested")
                .Where(x => users.ContainsKey(x.UserId))
                .Select(x => new SocialTableRequestDto(
                    x.UserId,
                    users[x.UserId].Nickname,
                    users[x.UserId].DisplayName,
                    mediaStorage.ResolveUrl(users[x.UserId].AvatarUrl),
                    x.Status))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var messages = await db.SocialTableMessages
                .AsNoTracking()
                .Where(x => x.SocialTableId == tableId)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(150)
                .ToListAsync(ct);

            var messageUserIds = messages.Select(x => x.UserId).Distinct().ToList();
            var messageUsers = messageUserIds.Count == 0
                ? users
                : await db.Users.AsNoTracking().Where(x => messageUserIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

            return Results.Ok(new SocialTableThreadDto(
                BuildSocialTableSummaryDto(table, venue, isHost ? "host" : participant?.Status ?? "none", participants),
                requests,
                messages.Select(message =>
                {
                    messageUsers.TryGetValue(message.UserId, out var author);
                    return new SocialTableMessageDto(
                        message.Id,
                        message.UserId,
                        author?.Nickname ?? "utente",
                        author?.DisplayName,
                        mediaStorage.ResolveUrl(author?.AvatarUrl),
                        message.Body,
                        message.CreatedAtUtc,
                        message.UserId == currentUserId);
                }).ToList()));
        });

        group.MapDelete("/tables/{tableId:guid}", async (
            Guid tableId,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var table = await db.SocialTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.HostUserId != currentUserId)
            {
                return Results.Forbid();
            }

            table.Status = "cancelled";
            table.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("table_cancelled", "Tavolo cancellato."));
        });

        group.MapPost("/tables/{tableId:guid}/participants/{targetUserId:guid}/approve", async (
            Guid tableId,
            Guid targetUserId,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            MediaStorageService mediaStorage,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            var table = await db.SocialTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (currentUserId == Guid.Empty || table is null || table.HostUserId != currentUserId)
            {
                return Results.Forbid();
            }

            var participant = await db.SocialTableParticipants
                .FirstOrDefaultAsync(x => x.SocialTableId == tableId && x.UserId == targetUserId, ct);
            if (participant is null)
            {
                return Results.NotFound();
            }

            var accepted = await db.SocialTableParticipants
                .CountAsync(x => x.SocialTableId == tableId && x.Status == "accepted", ct);
            if (accepted >= table.Capacity)
            {
                return Results.Conflict("Il tavolo è pieno.");
            }

            participant.Status = "accepted";
            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                targetUserId,
                "Ingresso approvato",
                $"Sei dentro {table.Title}.",
                new { type = "social_table_approved", tableId },
                ct,
                outbox.BuildDeepLink("table", tableId));

            return Results.Ok(new SocialActionResultDto("accepted", "Partecipante approvato."));
        });

        group.MapPost("/tables/{tableId:guid}/participants/{targetUserId:guid}/reject", async (
            Guid tableId,
            Guid targetUserId,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            var table = await db.SocialTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (currentUserId == Guid.Empty || table is null || table.HostUserId != currentUserId)
            {
                return Results.Forbid();
            }

            var participant = await db.SocialTableParticipants
                .FirstOrDefaultAsync(x => x.SocialTableId == tableId && x.UserId == targetUserId, ct);
            if (participant is null)
            {
                return Results.NotFound();
            }

            db.SocialTableParticipants.Remove(participant);
            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                targetUserId,
                "Ingresso non approvato",
                $"Non sei stato inserito in {table.Title}.",
                new { type = "social_table_rejected", tableId },
                ct,
                outbox.BuildDeepLink("table", tableId));

            return Results.Ok(new SocialActionResultDto("removed", "Richiesta rimossa."));
        });

        group.MapPost("/tables/{tableId:guid}/messages", async (
            Guid tableId,
            SendSocialTableMessageRequest request,
            AppDbContext db,
            IHubContext<ChatHub> hubContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest("Messaggio vuoto.");
            }

            var table = await db.SocialTables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (!await CanAccessTableThreadAsync(db, tableId, currentUserId, ct))
            {
                return Results.Forbid();
            }

            var message = new SocialTableMessage
            {
                SocialTableId = tableId,
                UserId = currentUserId,
                Body = request.Body.Trim()
            };

            db.SocialTableMessages.Add(message);
            await db.SaveChangesAsync(ct);

            await hubContext.Clients.Group(tableId.ToString()).SendAsync("ReceiveMessage", new
            {
                TableId = tableId,
                SenderId = currentUserId,
                Body = request.Body.Trim(),
                SentAt = DateTimeOffset.UtcNow
            }, ct);

            return Results.Ok(new SocialActionResultDto("sent", "Messaggio inviato."));
        });

        group.MapPost("/tables/{tableId:guid}/join", async (
            Guid tableId,
            Guid? userId,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            var joiningUserId = userId ?? currentUserId;
            if (currentUserId == Guid.Empty || currentUserId != joiningUserId)
            {
                return Results.Forbid();
            }

            var table = await db.SocialTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (table is null) return Results.NotFound();

            if (!await db.Users.AnyAsync(x => x.Id == joiningUserId, ct))
            {
                return Results.NotFound("User not found.");
            }

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                (x.BlockerUserId == joiningUserId && x.BlockedUserId == table.HostUserId) ||
                (x.BlockerUserId == table.HostUserId && x.BlockedUserId == joiningUserId), ct);
            if (blocked)
            {
                return Results.Conflict("Non puoi unirti a questo tavolo.");
            }

            var existingParticipant = await db.SocialTableParticipants
                .FirstOrDefaultAsync(x => x.SocialTableId == tableId && x.UserId == joiningUserId, ct);
            if (existingParticipant is not null)
            {
                if (existingParticipant.Status == "invited")
                {
                    var acceptedInvited = await db.SocialTableParticipants
                        .CountAsync(x => x.SocialTableId == tableId && x.Status == "accepted", ct);
                    if (acceptedInvited >= table.Capacity) return Results.Conflict("Table is full.");

                    existingParticipant.Status = "accepted";
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(existingParticipant);
                }

                return Results.Conflict("User already joined this table.");
            }

            var accepted = await db.SocialTableParticipants
                .CountAsync(x => x.SocialTableId == tableId && x.Status == "accepted", ct);
            if (accepted >= table.Capacity) return Results.Conflict("Table is full.");

            var participant = new SocialTableParticipant
            {
                SocialTableId = tableId,
                UserId = joiningUserId,
                Status = table.JoinPolicy == "auto" ? "accepted" : "requested"
            };

            db.SocialTableParticipants.Add(participant);
            await db.SaveChangesAsync(ct);

            if (table.HostUserId != joiningUserId)
            {
                await outbox.EnqueueAsync(
                    table.HostUserId,
                    "Nuova richiesta tavolo",
                    $"Qualcuno vuole unirsi a {table.Title}.",
                    new { type = "social_table_join", tableId, userId = joiningUserId },
                    ct);
            }

            return Results.Ok(participant);
        });

        group.MapPost("/users/{targetUserId:guid}/block", async (
            Guid targetUserId,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (targetUserId == Guid.Empty || targetUserId == currentUserId)
            {
                return Results.BadRequest("Non puoi bloccare te stesso.");
            }

            var targetExists = await db.Users.AnyAsync(x => x.Id == targetUserId, ct);
            if (!targetExists)
            {
                return Results.NotFound("Utente non trovato.");
            }

            var existingBlock = await db.UserBlocks
                .FirstOrDefaultAsync(x =>
                    (x.BlockerUserId == currentUserId && x.BlockedUserId == targetUserId) ||
                    (x.BlockerUserId == targetUserId && x.BlockedUserId == currentUserId), ct);

            if (existingBlock is not null)
            {
                return Results.Conflict("Relazione già bloccata.");
            }

            // Remove any friend relations
            var relations = await db.FriendRelations
                .Where(x =>
                    (x.RequesterId == currentUserId && x.AddresseeId == targetUserId) ||
                    (x.RequesterId == targetUserId && x.AddresseeId == currentUserId))
                .ToListAsync(ct);

            if (relations.Count > 0)
            {
                db.FriendRelations.RemoveRange(relations);
            }

            // Remove from any shared tables
            var sharedTables = await db.SocialTableParticipants
                .Where(x => x.UserId == currentUserId || x.UserId == targetUserId)
                .GroupBy(x => x.SocialTableId)
                .Where(g => g.Count() == 2)
                .Select(g => g.Key)
                .ToListAsync(ct);

            if (sharedTables.Count > 0)
            {
                var participantsToRemove = await db.SocialTableParticipants
                    .Where(x => sharedTables.Contains(x.SocialTableId) && x.UserId == currentUserId)
                    .ToListAsync(ct);

                db.SocialTableParticipants.RemoveRange(participantsToRemove);
            }

            var block = new UserBlock
            {
                BlockerUserId = currentUserId,
                BlockedUserId = targetUserId
            };

            db.UserBlocks.Add(block);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("blocked", "Utente bloccato."));
        });

        group.MapPost("/users/{targetUserId:guid}/unblock", async (
            Guid targetUserId,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var block = await db.UserBlocks
                .FirstOrDefaultAsync(x =>
                    x.BlockerUserId == currentUserId && x.BlockedUserId == targetUserId, ct);

            if (block is null)
            {
                return Results.NotFound("Blocco non trovato.");
            }

            db.UserBlocks.Remove(block);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("unblocked", "Utente sbloccato."));
        });

        // ==========================================
        // FEATURE VIRALI: FLARES E VIBE CHECK
        // ==========================================

        group.MapPost("/flares", async (
            CreateFlareRequest request,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            MediaStorageService mediaStorage,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty) return Results.Forbid();

            if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            {
                return Results.BadRequest("Coordinate flare non valide.");
            }

            var message = request.Message.Trim();
            if (string.IsNullOrWhiteSpace(message) || message.Length > 200)
            {
                return Results.BadRequest("Messaggio flare richiesto, massimo 200 caratteri.");
            }

            var durationHours = Math.Clamp(request.DurationHours ?? 1, 1, 4);

            var currentUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentUserId, ct);
            if (currentUser == null) return Results.NotFound();

            // Troviamo tutti gli amici fidati
            var friendIds = await db.FriendRelations
                .Where(x => (x.RequesterId == currentUserId || x.AddresseeId == currentUserId) && x.Status == "accepted")
                .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
                .ToListAsync(ct);

            // Il motore FOMO: Notifichiamo tutti gli amici istantaneamente
            foreach (var friendId in friendIds)
            {
                await outbox.EnqueueAsync(
                    friendId,
                    "🔥 Flare Lanciato!",
                    $"{currentUser.DisplayName ?? currentUser.Nickname} ha lanciato un segnale: '{message}'. Raggiungilo!",
                    new { type = "flare", senderId = currentUserId, lat = request.Latitude, lng = request.Longitude },
                    ct);
            }

            var flare = new FlareSignal
            {
                UserId = currentUserId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Message = message,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(durationHours)
            };
            db.FlareSignals.Add(flare);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new FlareDto(
                flare.Id,
                flare.UserId,
                currentUser.Nickname,
                currentUser.DisplayName,
                mediaStorage.ResolveUrl(currentUser.AvatarUrl),
                flare.Latitude,
                flare.Longitude,
                flare.Message,
                0,
                flare.CreatedAtUtc,
                flare.ExpiresAtUtc));
        });

        group.MapGet("/flares", async (
            AppDbContext db,
            ClaimsPrincipal user,
            MediaStorageService mediaStorage,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty) return Results.Forbid();

            var friendIds = await db.FriendRelations
                .AsNoTracking()
                .Where(x => (x.RequesterId == currentUserId || x.AddresseeId == currentUserId) && x.Status == "accepted")
                .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
                .ToListAsync(ct);

            var now = DateTimeOffset.UtcNow;
            var flares = await db.FlareSignals
                .AsNoTracking()
                .Where(x => x.ExpiresAtUtc > now && (x.UserId == currentUserId || friendIds.Contains(x.UserId)))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(80)
                .Join(
                    db.Users.AsNoTracking(),
                    flare => flare.UserId,
                    u => u.Id,
                    (flare, u) => new
                    {
                        flare.Id,
                        flare.UserId,
                        u.Nickname,
                        u.DisplayName,
                        u.AvatarUrl,
                        flare.Latitude,
                        flare.Longitude,
                        flare.Message,
                        flare.CreatedAtUtc,
                        flare.ExpiresAtUtc
                    })
                .ToListAsync(ct);

            var flareIds = flares.Select(x => x.Id).ToList();
            var responseCounts = flareIds.Count == 0
                ? new Dictionary<Guid, int>()
                : await db.FlareResponses
                    .AsNoTracking()
                    .Where(x => flareIds.Contains(x.FlareSignalId))
                    .GroupBy(x => x.FlareSignalId)
                    .Select(x => new { FlareId = x.Key, Count = x.Count() })
                    .ToDictionaryAsync(x => x.FlareId, x => x.Count, ct);

            return Results.Ok(flares.Select(flare => new FlareDto(
                flare.Id,
                flare.UserId,
                flare.Nickname,
                flare.DisplayName,
                mediaStorage.ResolveUrl(flare.AvatarUrl),
                flare.Latitude,
                flare.Longitude,
                flare.Message,
                responseCounts.GetValueOrDefault(flare.Id),
                flare.CreatedAtUtc,
                flare.ExpiresAtUtc)));
        });

        group.MapDelete("/flares/{flareId:guid}", async (
            Guid flareId,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty) return Results.Forbid();

            var flare = await db.FlareSignals.FirstOrDefaultAsync(x => x.Id == flareId, ct);
            if (flare is null) return Results.NotFound();
            if (flare.UserId != currentUserId) return Results.Forbid();

            flare.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            flare.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("flare_deleted", "Flare cancellato."));
        });

        group.MapPost("/flares/{flareId:guid}/responses", async (
            Guid flareId,
            RespondToFlareRequest request,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty) return Results.Forbid();

            var body = request.Body?.Trim();
            if (string.IsNullOrWhiteSpace(body) || body.Length > 200)
            {
                return Results.BadRequest("Risposta richiesta, massimo 200 caratteri.");
            }

            var flare = await db.FlareSignals.FirstOrDefaultAsync(x => x.Id == flareId && x.ExpiresAtUtc > DateTimeOffset.UtcNow, ct);
            if (flare is null) return Results.NotFound();

            var canRespond = flare.UserId == currentUserId || await db.FriendRelations.AsNoTracking().AnyAsync(x =>
                x.Status == "accepted" &&
                ((x.RequesterId == currentUserId && x.AddresseeId == flare.UserId) ||
                 (x.RequesterId == flare.UserId && x.AddresseeId == currentUserId)), ct);
            if (!canRespond)
            {
                return Results.Forbid();
            }

            db.FlareResponses.Add(new FlareResponse
            {
                FlareSignalId = flareId,
                UserId = currentUserId,
                Body = body
            });
            await db.SaveChangesAsync(ct);

            if (flare.UserId != currentUserId)
            {
                await outbox.EnqueueAsync(
                    flare.UserId,
                    "Risposta al flare",
                    body,
                    new { type = "flare_response", flareId, senderId = currentUserId },
                    ct);
            }

            return Results.Ok(new SocialActionResultDto("flare_response_sent", "Risposta inviata."));
        });

        group.MapPost("/venues/{venueId:guid}/vibe", async (
            Guid venueId,
            SubmitVibeRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty) return Results.Forbid();

            var venueExists = await db.Venues.AnyAsync(x => x.Id == venueId, ct);
            if (!venueExists) return Results.NotFound("Locale non trovato.");

            // Il Vibe check funziona in "fire and forget".
            // In futuro: Aggrega i Vibe in Redis per mostrarli anonimamente sulla mappa.
            
            return Results.Ok(new SocialActionResultDto("vibe_submitted", $"Hai votato il vibe del locale: {request.VibeEmoji}"));
        });

        return group;
    }

    private static async Task<bool> CanAccessTableThreadAsync(AppDbContext db, Guid tableId, Guid currentUserId, CancellationToken ct)
    {
        var participant = await db.SocialTableParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SocialTableId == tableId && x.UserId == currentUserId, ct);
        if (participant is not null)
        {
            return participant.Status is "accepted" or "requested" or "invited";
        }

        return await db.SocialTables.AsNoTracking().AnyAsync(x => x.Id == tableId && x.HostUserId == currentUserId, ct);
    }

    private static SocialTableSummaryDto BuildSocialTableSummaryDto(
        SocialTable table,
        Venue? venue,
        string membershipStatus,
        IReadOnlyCollection<SocialTableParticipant> participants)
    {
        var group = participants.Where(x => x.SocialTableId == table.Id).ToList();
        return new SocialTableSummaryDto(
            table.Id,
            table.Title,
            table.Description,
            table.StartsAtUtc,
            venue?.Name ?? "Venue",
            venue?.Category ?? "venue",
            table.JoinPolicy,
            membershipStatus == "host",
            membershipStatus,
            table.Capacity,
            group.Count(x => x.Status == "requested"),
            group.Count(x => x.Status == "accepted"),
            group.Count(x => x.Status == "invited"));
    }

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
                (checkIn, venue) => new { checkIn.UserId, Venue = venue, checkIn.ExpiresAtUtc })
            .OrderByDescending(x => x.ExpiresAtUtc)
            .ToListAsync(ct);

        foreach (var item in checkIns)
        {
            snapshots[item.UserId] = new PresenceSnapshot(
                "checked_in",
                $"Ora a {item.Venue.Name}",
                item.Venue.Name,
                item.Venue.Category);
        }

        var unresolvedTableUsers = userIds.Where(x => snapshots[x].PresenceState == "idle").ToList();
        if (unresolvedTableUsers.Count > 0)
        {
            var hostedTables = await db.SocialTables
                .AsNoTracking()
                .Where(x => unresolvedTableUsers.Contains(x.HostUserId) && x.Status == "open" && x.StartsAtUtc >= now)
                .Join(
                    db.Venues.AsNoTracking(),
                    table => table.VenueId,
                    venue => venue.Id,
                    (table, venue) => new { table.HostUserId, Venue = venue, table.StartsAtUtc })
                .OrderBy(x => x.StartsAtUtc)
                .ToListAsync(ct);

            foreach (var item in hostedTables)
            {
                snapshots[item.HostUserId] = new PresenceSnapshot(
                    "hosting_table",
                    $"Tavolo aperto a {item.Venue.Name}",
                    item.Venue.Name,
                    item.Venue.Category);
            }
        }

        unresolvedTableUsers = userIds.Where(x => snapshots[x].PresenceState == "idle").ToList();
        if (unresolvedTableUsers.Count > 0)
        {
            var joinedTables = await db.SocialTableParticipants
                .AsNoTracking()
                .Where(x => unresolvedTableUsers.Contains(x.UserId) && x.Status == "accepted")
                .Join(
                    db.SocialTables.AsNoTracking().Where(x => x.Status == "open" && x.StartsAtUtc >= now),
                    participant => participant.SocialTableId,
                    table => table.Id,
                    (participant, table) => new { participant.UserId, Table = table })
                .Join(
                    db.Venues.AsNoTracking(),
                    tuple => tuple.Table.VenueId,
                    venue => venue.Id,
                    (tuple, venue) => new { tuple.UserId, Venue = venue, tuple.Table.StartsAtUtc })
                .OrderBy(x => x.StartsAtUtc)
                .ToListAsync(ct);

            foreach (var item in joinedTables)
            {
                snapshots[item.UserId] = new PresenceSnapshot(
                    "joined_table",
                    $"Dentro un tavolo a {item.Venue.Name}",
                    item.Venue.Name,
                    item.Venue.Category);
            }
        }

        var unresolvedIntentions = userIds.Where(x => snapshots[x].PresenceState == "idle").ToList();
        if (unresolvedIntentions.Count > 0)
        {
            var intentions = await db.VenueIntentions
                .AsNoTracking()
                .Where(x => unresolvedIntentions.Contains(x.UserId) && x.EndsAtUtc >= now && x.StartsAtUtc <= now.AddHours(6))
                .Join(
                    db.Venues.AsNoTracking(),
                    intention => intention.VenueId,
                    venue => venue.Id,
                    (intention, venue) => new { intention.UserId, Venue = venue, intention.StartsAtUtc })
                .OrderBy(x => x.StartsAtUtc)
                .ToListAsync(ct);

            foreach (var item in intentions)
            {
                snapshots[item.UserId] = new PresenceSnapshot(
                    "planning",
                    $"Piano per {item.Venue.Name}",
                    item.Venue.Name,
                    item.Venue.Category);
            }
        }

        return snapshots;
    }

    private static async Task<bool> RemoveAutomaticCheckInsAsync(
        AppDbContext db,
        AffluenceAggregationService aggregationService,
        Guid userId,
        CancellationToken ct)
    {
        var active = await db.VenueCheckIns
            .Where(x => x.UserId == userId && !x.IsManual && x.ExpiresAtUtc >= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        if (active.Count == 0)
        {
            return false;
        }

        var affectedVenueIds = active.Select(x => x.VenueId).Distinct().ToList();
        db.VenueCheckIns.RemoveRange(active);
        await db.SaveChangesAsync(ct);

        foreach (var venueId in affectedVenueIds)
        {
            await aggregationService.RebuildVenueSnapshotAsync(venueId, ct);
        }

        return true;
    }

    private static SocialConnectionDto BuildConnectionDto(
        AppUser user,
        MediaStorageService mediaStorage,
        string relationshipStatus,
        HashSet<Guid> currentFriendIds,
        Guid currentUserId,
        Dictionary<Guid, PresenceSnapshot> presence,
        Dictionary<Guid, HashSet<Guid>> connectionFriendSets)
    {
        var mutualFriendsCount = connectionFriendSets.TryGetValue(user.Id, out var targetFriends)
            ? targetFriends.Intersect(currentFriendIds).Count(x => x != user.Id && x != currentUserId)
            : 0;
        presence.TryGetValue(user.Id, out var snapshot);
        snapshot ??= new PresenceSnapshot("idle", "Nessuna presenza live", null, null);

        return new SocialConnectionDto(
            user.Id,
            user.Nickname,
            user.DisplayName,
            mediaStorage.ResolveUrl(user.AvatarUrl),
            relationshipStatus,
            mutualFriendsCount,
            snapshot.PresenceState,
            snapshot.StatusLabel,
            snapshot.CurrentVenueName,
            snapshot.CurrentVenueCategory);
    }

    private sealed record PresenceSnapshot(
        string PresenceState,
        string StatusLabel,
        string? CurrentVenueName,
        string? CurrentVenueCategory);
}
