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

        group.MapGet("/users", async (
            string? q,
            AppDbContext db,
            MediaStorageService mediaStorage,
            CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var query = db.Users.AsNoTracking().OrderBy(x => x.Nickname).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Nickname.ToLower().Contains(term) ||
                    (x.DisplayName != null && x.DisplayName.ToLower().Contains(term)) ||
                    (x.DiscoverableEmailNormalized != null && x.DiscoverableEmailNormalized.Contains(term)));
            }

            var users = await query.Take(300).ToListAsync(ct);
            var userIds = users.Select(x => x.Id).ToList();

            var friendCountMap = userIds.ToDictionary(id => id, _ => 0);
            var relations = userIds.Count == 0
                ? new List<FriendRelation>()
                : await db.FriendRelations
                    .AsNoTracking()
                    .Where(x => x.Status == "accepted" && (userIds.Contains(x.RequesterId) || userIds.Contains(x.AddresseeId)))
                    .ToListAsync(ct);
            foreach (var relation in relations)
            {
                if (friendCountMap.ContainsKey(relation.RequesterId)) friendCountMap[relation.RequesterId]++;
                if (friendCountMap.ContainsKey(relation.AddresseeId)) friendCountMap[relation.AddresseeId]++;
            }

            var checkInRows = userIds.Count == 0
                ? []
                : await db.VenueCheckIns
                    .AsNoTracking()
                    .Where(x => userIds.Contains(x.UserId) && x.ExpiresAtUtc >= now)
                    .Join(
                        db.Venues.AsNoTracking(),
                        checkIn => checkIn.VenueId,
                        venue => venue.Id,
                        (checkIn, venue) => new { CheckIn = checkIn, Venue = venue })
                    .ToListAsync(ct);
            var checkIns = checkInRows
                .Select(x => new AdminPresenceProjection(
                    x.CheckIn.UserId,
                    x.Venue.Name,
                    x.Venue.Category,
                    x.Venue.Location?.Y,
                    x.Venue.Location?.X,
                    x.CheckIn.CreatedAtUtc,
                    x.CheckIn.ExpiresAtUtc))
                .ToList();
            var checkInMap = checkIns
                .GroupBy(x => x.UserId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(i => i.ExpiresAtUtc).First());

            var intentionRows = userIds.Count == 0
                ? []
                : await db.VenueIntentions
                    .AsNoTracking()
                    .Where(x => userIds.Contains(x.UserId) && x.EndsAtUtc >= now && x.StartsAtUtc <= now.AddHours(6))
                    .Join(
                        db.Venues.AsNoTracking(),
                        intention => intention.VenueId,
                        venue => venue.Id,
                        (intention, venue) => new { Intention = intention, Venue = venue })
                    .ToListAsync(ct);
            var intentions = intentionRows
                .Select(x => new AdminPresenceProjection(
                    x.Intention.UserId,
                    x.Venue.Name,
                    x.Venue.Category,
                    x.Venue.Location?.Y,
                    x.Venue.Location?.X,
                    x.Intention.CreatedAtUtc,
                    x.Intention.StartsAtUtc))
                .ToList();
            var intentionMap = intentions
                .GroupBy(x => x.UserId)
                .ToDictionary(x => x.Key, x => x.OrderBy(i => i.ExpiresAtUtc).First());

            return Results.Ok(users.Select(appUser =>
            {
                if (appUser.IsGhostModeEnabled)
                {
                    return new AdminUserMonitorDto(
                        appUser.Id,
                        appUser.Nickname,
                        appUser.DisplayName,
                        mediaStorage.ResolveUrl(appUser.AvatarUrl),
                        appUser.Status,
                        friendCountMap.GetValueOrDefault(appUser.Id),
                        appUser.IsGhostModeEnabled,
                        appUser.SharePresenceWithFriends,
                        appUser.ShareIntentionsWithFriends,
                        "ghost",
                        null,
                        null,
                        null,
                        null,
                        appUser.UpdatedAtUtc ?? appUser.CreatedAtUtc,
                        "hidden_by_ghost_mode");
                }

                if (checkInMap.TryGetValue(appUser.Id, out var checkIn))
                {
                    return new AdminUserMonitorDto(
                        appUser.Id,
                        appUser.Nickname,
                        appUser.DisplayName,
                        mediaStorage.ResolveUrl(appUser.AvatarUrl),
                        appUser.Status,
                        friendCountMap.GetValueOrDefault(appUser.Id),
                        appUser.IsGhostModeEnabled,
                        appUser.SharePresenceWithFriends,
                        appUser.ShareIntentionsWithFriends,
                        "checked_in",
                        checkIn.VenueName,
                        checkIn.VenueCategory,
                        checkIn.Latitude,
                        checkIn.Longitude,
                        checkIn.SignalAtUtc,
                        "venue_level");
                }

                if (intentionMap.TryGetValue(appUser.Id, out var intention))
                {
                    return new AdminUserMonitorDto(
                        appUser.Id,
                        appUser.Nickname,
                        appUser.DisplayName,
                        mediaStorage.ResolveUrl(appUser.AvatarUrl),
                        appUser.Status,
                        friendCountMap.GetValueOrDefault(appUser.Id),
                        appUser.IsGhostModeEnabled,
                        appUser.SharePresenceWithFriends,
                        appUser.ShareIntentionsWithFriends,
                        "intention",
                        intention.VenueName,
                        intention.VenueCategory,
                        intention.Latitude,
                        intention.Longitude,
                        intention.SignalAtUtc,
                        "venue_level");
                }

                return new AdminUserMonitorDto(
                    appUser.Id,
                    appUser.Nickname,
                    appUser.DisplayName,
                    mediaStorage.ResolveUrl(appUser.AvatarUrl),
                    appUser.Status,
                    friendCountMap.GetValueOrDefault(appUser.Id),
                    appUser.IsGhostModeEnabled,
                    appUser.SharePresenceWithFriends,
                    appUser.ShareIntentionsWithFriends,
                    "idle",
                    null,
                    null,
                    null,
                    null,
                    appUser.UpdatedAtUtc ?? appUser.CreatedAtUtc,
                    "none");
            }));
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

        group.MapPost("/venues/import/foursquare/preview", async (
            VenueImportRequest request,
            FoursquareVenueImportService importer,
            CancellationToken ct) =>
        {
            try
            {
                var candidates = await importer.PreviewAsync(request, ct);
                return Results.Ok(candidates);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/venues/import/foursquare", async (
            VenueImportRequest request,
            FoursquareVenueImportService importer,
            CancellationToken ct) =>
        {
            try
            {
                var result = await importer.ImportAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/venues/import/osm/preview", async (
            VenueImportRequest request,
            OverpassVenueImportService importer,
            CancellationToken ct) =>
        {
            try
            {
                var candidates = await importer.PreviewAsync(request, ct);
                return Results.Ok(candidates);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/venues/import/osm", async (
            VenueImportRequest request,
            OverpassVenueImportService importer,
            CancellationToken ct) =>
        {
            try
            {
                var result = await importer.ImportAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
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
            venue.PhoneNumber,
            venue.WebsiteUrl,
            venue.HoursSummary,
            venue.CoverImageUrl,
            venue.Description,
            venue.TagsCsv,
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
        venue.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        venue.WebsiteUrl = string.IsNullOrWhiteSpace(request.WebsiteUrl) ? null : request.WebsiteUrl.Trim();
        venue.HoursSummary = string.IsNullOrWhiteSpace(request.HoursSummary) ? null : request.HoursSummary.Trim();
        venue.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
        venue.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        venue.TagsCsv = string.IsNullOrWhiteSpace(request.TagsCsv) ? null : request.TagsCsv.Trim();
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

    private sealed record AdminPresenceProjection(
        Guid UserId,
        string VenueName,
        string VenueCategory,
        double? Latitude,
        double? Longitude,
        DateTimeOffset SignalAtUtc,
        DateTimeOffset ExpiresAtUtc);
}
