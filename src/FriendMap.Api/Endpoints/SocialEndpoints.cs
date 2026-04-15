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

            var alreadyJoined = await db.SocialTableParticipants
                .AnyAsync(x => x.SocialTableId == tableId && x.UserId == userId, ct);
            if (alreadyJoined) return Results.Conflict("User already joined this table.");

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

        return group;
    }
}
