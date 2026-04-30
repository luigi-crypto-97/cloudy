using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class VenueEndpoints
{
    public static RouteGroupBuilder MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/venues").WithTags("Venues");

        group.MapGet("/map", async (
            double minLat,
            double minLng,
            double maxLat,
            double maxLng,
            string? q,
            string? category,
            bool? openNow,
            double? centerLat,
            double? centerLng,
            double? maxDistanceKm,
            ClaimsPrincipal user,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var viewerUserId = CurrentUser.GetUserId(user);
            var markers = await service.GetVenueMarkersAsync(
                minLat,
                minLng,
                maxLat,
                maxLng,
                viewerUserId,
                q,
                category,
                openNow == true,
                centerLat,
                centerLng,
                maxDistanceKm,
                ct);
            return Results.Ok(markers);
        });

        group.MapGet("/map-layer", async (
            double minLat,
            double minLng,
            double maxLat,
            double maxLng,
            string? q,
            string? category,
            bool? openNow,
            double? centerLat,
            double? centerLng,
            double? maxDistanceKm,
            ClaimsPrincipal user,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var viewerUserId = CurrentUser.GetUserId(user);
            var layer = await service.GetVenueMapLayerAsync(
                minLat,
                minLng,
                maxLat,
                maxLng,
                viewerUserId,
                q,
                category,
                openNow == true,
                centerLat,
                centerLng,
                maxDistanceKm,
                ct);
            return Results.Ok(layer);
        });

        group.MapGet("/{venueId:guid}", async (
            Guid venueId,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var details = await service.GetVenueDetailsAsync(venueId, ct);
            return details is null ? Results.NotFound() : Results.Ok(details);
        });

        group.MapGet("/{venueId:guid}/marker", async (
            Guid venueId,
            ClaimsPrincipal user,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var viewerUserId = CurrentUser.GetUserId(user);
            var marker = await service.GetVenueMarkerByIdAsync(venueId, viewerUserId, ct);
            return marker is null ? Results.NotFound() : Results.Ok(marker);
        });

        group.MapGet("/{venueId:guid}/rating", async (
            Guid venueId,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var viewerUserId = CurrentUser.GetUserId(user);
            if (!await db.Venues.AsNoTracking().AnyAsync(x => x.Id == venueId, ct))
            {
                return Results.NotFound();
            }

            return Results.Ok(await BuildRatingSummaryAsync(db, venueId, viewerUserId, ct));
        });

        group.MapPost("/{venueId:guid}/rating", async (
            Guid venueId,
            RateVenueRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (request.Stars is < 1 or > 5)
            {
                return Results.BadRequest("Scegli da 1 a 5 stelle.");
            }

            if (!await db.Venues.AsNoTracking().AnyAsync(x => x.Id == venueId, ct))
            {
                return Results.NotFound();
            }

            var now = DateTimeOffset.UtcNow;
            var isVerified = await HasVerifiedVenueSignalAsync(db, venueId, userId, now, ct);
            var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (comment?.Length > 280)
            {
                comment = comment[..280];
            }

            var rating = await db.VenueRatings.FirstOrDefaultAsync(x => x.VenueId == venueId && x.UserId == userId, ct);
            if (rating is null)
            {
                rating = new VenueRating
                {
                    VenueId = venueId,
                    UserId = userId
                };
                db.VenueRatings.Add(rating);
            }

            rating.Stars = request.Stars;
            rating.Comment = comment;
            rating.IsVerifiedVisit = isVerified;
            rating.IsFlagged = false;
            rating.FlaggedAtUtc = null;
            rating.FlagReason = null;
            rating.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            return Results.Ok(await BuildRatingSummaryAsync(db, venueId, userId, ct));
        }).RequireAuthorization();

        group.MapPost("/{venueId:guid}/ratings/{ratingId:guid}/report", async (
            Guid venueId,
            Guid ratingId,
            ReportVenueRatingRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var reporterUserId = CurrentUser.GetUserId(user);
            if (reporterUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var rating = await db.VenueRatings.FirstOrDefaultAsync(x => x.Id == ratingId && x.VenueId == venueId, ct);
            if (rating is null)
            {
                return Results.NotFound();
            }

            if (rating.UserId == reporterUserId)
            {
                return Results.BadRequest("Non puoi segnalare una tua valutazione.");
            }

            rating.IsFlagged = true;
            rating.FlaggedAtUtc = DateTimeOffset.UtcNow;
            rating.FlagReason = string.IsNullOrWhiteSpace(request.ReasonCode) ? "fake_venue_rating" : request.ReasonCode.Trim();
            db.ModerationReports.Add(new ModerationReport
            {
                ReporterUserId = reporterUserId,
                ReportedUserId = rating.UserId,
                ReportedVenueId = venueId,
                ReasonCode = rating.FlagReason,
                Details = string.IsNullOrWhiteSpace(request.Details) ? "Valutazione locale segnalata come non veritiera." : request.Details.Trim()
            });
            await db.SaveChangesAsync(ct);

            return Results.Ok(new SocialActionResultDto("reported", "Valutazione segnalata. Se confermata, comporta perdita punti."));
        }).RequireAuthorization();

        group.MapPost("/{venueId:guid}/intentions", async (
            Guid venueId,
            CreateIntentionRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(user);
            if (userId != request.UserId)
                return Results.Forbid();

            var intention = new VenueIntention
            {
                UserId = request.UserId,
                VenueId = venueId,
                StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = request.EndsAtUtc,
                Note = request.Note,
                VisibilityLevel = "friends_or_aggregate"
            };

            db.VenueIntentions.Add(intention);
            await db.SaveChangesAsync(ct);
            await service.RebuildVenueSnapshotAsync(venueId, ct);

            return Results.Created($"/api/venues/{venueId}/intentions/{intention.Id}", intention);
        });

        group.MapPost("/{venueId:guid}/checkins", async (
            Guid venueId,
            CreateCheckInRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(user);
            if (userId != request.UserId)
                return Results.Forbid();

            var checkIn = new VenueCheckIn
            {
                UserId = request.UserId,
                VenueId = venueId,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(request.TtlMinutes),
                IsManual = true,
                VisibilityLevel = "friends_or_aggregate"
            };

            db.VenueCheckIns.Add(checkIn);
            await db.SaveChangesAsync(ct);
            await service.RebuildVenueSnapshotAsync(venueId, ct);

            return Results.Created($"/api/venues/{venueId}/checkins/{checkIn.Id}", checkIn);
        });

        group.MapDelete("/{venueId:guid}/checkins/current", async (
            Guid venueId,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(user);
            if (userId == Guid.Empty)
                return Results.Forbid();

            var checkIn = await db.VenueCheckIns
                .FirstOrDefaultAsync(c => c.UserId == userId && c.VenueId == venueId && c.ExpiresAtUtc > DateTimeOffset.UtcNow, ct);

            if (checkIn is null)
                return Results.NotFound();

            db.VenueCheckIns.Remove(checkIn);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        group.MapPost("/{venueId:guid}/tables", async (
            Guid venueId,
            CreateSocialTableRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(user);
            if (userId != request.HostUserId)
                return Results.Forbid();

            var table = new SocialTable
            {
                HostUserId = request.HostUserId,
                VenueId = venueId,
                Title = request.Title,
                Description = request.Description,
                StartsAtUtc = request.StartsAtUtc,
                Capacity = request.Capacity,
                JoinPolicy = request.JoinPolicy
            };

            db.SocialTables.Add(table);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/venues/{venueId}/tables/{table.Id}", table);
        });

        return group;
    }

    private static async Task<VenueRatingSummaryDto> BuildRatingSummaryAsync(
        AppDbContext db,
        Guid venueId,
        Guid viewerUserId,
        CancellationToken ct)
    {
        var ratings = await db.VenueRatings
            .AsNoTracking()
            .Where(x => x.VenueId == venueId && !x.IsFlagged)
            .ToListAsync(ct);
        var myRating = viewerUserId == Guid.Empty
            ? null
            : ratings.FirstOrDefault(x => x.UserId == viewerUserId);

        return new VenueRatingSummaryDto(
            venueId,
            ratings.Count == 0 ? 0 : Math.Round(ratings.Average(x => x.Stars), 1),
            ratings.Count,
            myRating?.Stars,
            myRating?.Id,
            myRating?.IsVerifiedVisit == true,
            myRating?.IsVerifiedVisit == true && myRating?.IsFlagged != true);
    }

    private static async Task<bool> HasVerifiedVenueSignalAsync(
        AppDbContext db,
        Guid venueId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var since = now.AddDays(-14);
        return await db.VenueCheckIns.AsNoTracking().AnyAsync(x => x.VenueId == venueId && x.UserId == userId && x.CreatedAtUtc >= since, ct)
            || await db.VenueIntentions.AsNoTracking().AnyAsync(x => x.VenueId == venueId && x.UserId == userId && x.CreatedAtUtc >= since, ct)
            || await db.UserStories.AsNoTracking().AnyAsync(x => x.VenueId == venueId && x.UserId == userId && x.CreatedAtUtc >= since, ct)
            || await db.SocialTables.AsNoTracking().AnyAsync(x => x.VenueId == venueId && x.HostUserId == userId && x.CreatedAtUtc >= since, ct)
            || await db.SocialTableParticipants.AsNoTracking()
                .Join(db.SocialTables.AsNoTracking(), p => p.SocialTableId, t => t.Id, (p, t) => new { Participant = p, Table = t })
                .AnyAsync(x => x.Table.VenueId == venueId && x.Participant.UserId == userId && x.Participant.Status == "accepted" && x.Participant.CreatedAtUtc >= since, ct);
    }
}
