using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class SocialEndpoints
{
    public static RouteGroupBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/social").WithTags("Social").RequireAuthorization();

        group.MapGet("/hub", async (
            AppDbContext db,
            ClaimsPrincipal user,
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
                .Select(id => BuildConnectionDto(users[id], "friend", friendSet, currentUserId, presence, connectionFriendSets))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var incoming = incomingRequestIds
                .Where(users.ContainsKey)
                .Select(id => BuildConnectionDto(users[id], "pending_received", friendSet, currentUserId, presence, connectionFriendSets))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var outgoing = outgoingRequestIds
                .Where(users.ContainsKey)
                .Select(id => BuildConnectionDto(users[id], "pending_sent", friendSet, currentUserId, presence, connectionFriendSets))
                .OrderBy(x => x.DisplayName ?? x.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tableInvites = await db.SocialTableParticipants
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
                .Select(x => new SocialTableInviteDto(
                    x.Table.Id,
                    x.Table.Title,
                    x.Table.StartsAtUtc,
                    x.Venue.Name,
                    x.Venue.Category,
                    x.HostUser.Id,
                    x.HostUser.Nickname,
                    x.HostUser.DisplayName,
                    x.HostUser.AvatarUrl))
                .ToListAsync(ct);

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
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/social/tables/{entity.Id}", entity);
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
                Status = "invited"
            });

            await db.SaveChangesAsync(ct);

            await outbox.EnqueueAsync(
                request.TargetUserId,
                "Invito a un tavolo",
                $"Ti hanno invitato a {table.Title}.",
                new { type = "social_table_invite", tableId = table.Id, hostUserId = currentUserId },
                ct);

            return Results.Ok(new SocialActionResultDto("invited", "Invito tavolo inviato."));
        });

        group.MapGet("/tables/{tableId:guid}/thread", async (
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
                .Where(x => x.Status == "requested" || x.Status == "invited")
                .Where(x => users.ContainsKey(x.UserId))
                .Select(x => new SocialTableRequestDto(
                    x.UserId,
                    users[x.UserId].Nickname,
                    users[x.UserId].DisplayName,
                    users[x.UserId].AvatarUrl,
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
                        author?.AvatarUrl,
                        message.Body,
                        message.CreatedAtUtc,
                        message.UserId == currentUserId);
                }).ToList()));
        });

        group.MapPost("/tables/{tableId:guid}/participants/{targetUserId:guid}/approve", async (
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
                ct);

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
                ct);

            return Results.Ok(new SocialActionResultDto("removed", "Richiesta rimossa."));
        });

        group.MapPost("/tables/{tableId:guid}/messages", async (
            Guid tableId,
            SendSocialTableMessageRequest request,
            AppDbContext db,
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

            return Results.Ok(new SocialActionResultDto("sent", "Messaggio inviato."));
        });

        group.MapPost("/tables/{tableId:guid}/join", async (
            Guid tableId,
            Guid userId,
            AppDbContext db,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != userId)
            {
                return Results.Forbid();
            }

            var table = await db.SocialTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (table is null) return Results.NotFound();

            if (!await db.Users.AnyAsync(x => x.Id == userId, ct))
            {
                return Results.NotFound("User not found.");
            }

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                (x.BlockerUserId == userId && x.BlockedUserId == table.HostUserId) ||
                (x.BlockerUserId == table.HostUserId && x.BlockedUserId == userId), ct);
            if (blocked)
            {
                return Results.Conflict("Non puoi unirti a questo tavolo.");
            }

            var existingParticipant = await db.SocialTableParticipants
                .FirstOrDefaultAsync(x => x.SocialTableId == tableId && x.UserId == userId, ct);
            if (existingParticipant is not null)
            {
                if (existingParticipant.Status == "invited")
                {
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
                UserId = userId,
                Status = table.JoinPolicy == "auto" ? "accepted" : "requested"
            };

            db.SocialTableParticipants.Add(participant);
            await db.SaveChangesAsync(ct);

            if (table.HostUserId != userId)
            {
                await outbox.EnqueueAsync(
                    table.HostUserId,
                    "Nuova richiesta tavolo",
                    $"Qualcuno vuole unirsi a {table.Title}.",
                    new { type = "social_table_join", tableId, userId },
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

    private static SocialConnectionDto BuildConnectionDto(
        AppUser user,
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
            user.AvatarUrl,
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
