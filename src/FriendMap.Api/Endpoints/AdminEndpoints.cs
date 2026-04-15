using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FriendMap.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapGet("/dashboard", async (VenueAnalyticsService analytics, CancellationToken ct) =>
        {
            var overview = await analytics.GetOverviewAsync(ct);
            return Results.Ok(overview);
        });

        group.MapGet("/moderation/queue", async (ModerationService moderation, CancellationToken ct) =>
        {
            var queue = await moderation.GetOpenQueueAsync(ct);
            return Results.Ok(queue);
        });

        group.MapPost("/moderation/{reportId:guid}/resolve", async (Guid reportId, ModerationService moderation, CancellationToken ct) =>
        {
            var ok = await moderation.ResolveAsync(reportId, ct);
            return ok ? Results.Ok() : Results.NotFound();
        });

        group.MapGet("/venues", async (string? q, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Venues.AsNoTracking().OrderBy(x => x.Name).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Name.ToLower().Contains(term) ||
                    x.City.ToLower().Contains(term) ||
                    x.Category.ToLower().Contains(term));
            }

            var venues = await query
                .Take(200)
                .ToListAsync(ct);

            return Results.Ok(venues.Select(ToAdminVenueDto));
        });

        group.MapPost("/venues", async (UpsertVenueRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var validation = ValidateVenueRequest(request);
            if (validation is not null) return Results.BadRequest(validation);

            var venue = new Venue();
            ApplyVenueRequest(venue, request);
            db.Venues.Add(venue);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/admin/venues/{venue.Id}", ToAdminVenueDto(venue));
        });

        group.MapPut("/venues/{venueId:guid}", async (Guid venueId, UpsertVenueRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var validation = ValidateVenueRequest(request);
            if (validation is not null) return Results.BadRequest(validation);

            var venue = await db.Venues.FirstOrDefaultAsync(x => x.Id == venueId, ct);
            if (venue is null) return Results.NotFound();

            ApplyVenueRequest(venue, request);
            venue.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(ToAdminVenueDto(venue));
        });

        group.MapDelete("/venues/{venueId:guid}", async (Guid venueId, AppDbContext db, CancellationToken ct) =>
        {
            var venue = await db.Venues.FirstOrDefaultAsync(x => x.Id == venueId, ct);
            if (venue is null) return Results.NotFound();

            db.Venues.Remove(venue);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return group;
    }

    private static AdminVenueDto ToAdminVenueDto(Venue venue)
    {
        return new AdminVenueDto(
            venue.Id,
            venue.ExternalProviderId,
            venue.Name,
            venue.Category,
            venue.AddressLine,
            venue.City,
            venue.CountryCode,
            venue.Location?.Y,
            venue.Location?.X,
            venue.IsClaimed,
            venue.VisibilityStatus);
    }

    private static void ApplyVenueRequest(Venue venue, UpsertVenueRequest request)
    {
        venue.ExternalProviderId = request.ExternalProviderId.Trim();
        venue.Name = request.Name.Trim();
        venue.Category = request.Category.Trim();
        venue.AddressLine = request.AddressLine.Trim();
        venue.City = request.City.Trim();
        venue.CountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? "IT" : request.CountryCode.Trim().ToUpperInvariant();
        venue.Location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
        venue.IsClaimed = request.IsClaimed;
        venue.VisibilityStatus = string.IsNullOrWhiteSpace(request.VisibilityStatus) ? "public" : request.VisibilityStatus.Trim();
    }

    private static string? ValidateVenueRequest(UpsertVenueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalProviderId)) return "externalProviderId is required.";
        if (string.IsNullOrWhiteSpace(request.Name)) return "name is required.";
        if (string.IsNullOrWhiteSpace(request.Category)) return "category is required.";
        if (string.IsNullOrWhiteSpace(request.AddressLine)) return "addressLine is required.";
        if (string.IsNullOrWhiteSpace(request.City)) return "city is required.";
        if (request.Latitude is < -90 or > 90) return "latitude must be between -90 and 90.";
        if (request.Longitude is < -180 or > 180) return "longitude must be between -180 and 180.";
        return null;
    }
}
