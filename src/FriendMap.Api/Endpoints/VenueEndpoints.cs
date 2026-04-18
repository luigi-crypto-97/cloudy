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
}
