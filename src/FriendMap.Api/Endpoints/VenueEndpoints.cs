using FriendMap.Api.Contracts;
using FriendMap.Api.Services;
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
            ClaimsPrincipal user,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var viewerUserId = CurrentUser.GetUserId(user);
            var markers = await service.GetVenueMarkersAsync(minLat, minLng, maxLat, maxLng, viewerUserId, ct);
            return Results.Ok(markers);
        });

        group.MapGet("/{venueId:guid}", async (
            Guid venueId,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            var details = await service.GetVenueDetailsAsync(venueId, ct);
            return details is null ? Results.NotFound() : Results.Ok(details);
        });

        group.MapPost("/{venueId:guid}/rebuild-snapshot", async (
            Guid venueId,
            AffluenceAggregationService service,
            CancellationToken ct) =>
        {
            await service.RebuildVenueSnapshotAsync(venueId, ct);
            return Results.Accepted($"/api/venues/{venueId}");
        });

        return group;
    }
}
