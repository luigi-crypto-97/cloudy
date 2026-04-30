using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class FeedEndpoints
{
    public static RouteGroupBuilder MapFeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feed")
            .WithTags("Feed")
            .RequireAuthorization()
            .RequireRateLimiting("social");

        group.MapGet("/", GetFeedAsync);
        group.MapPost("/fatigue", UpdateFatigueAsync);
        group.MapPost("/links", CreateSignedLinkAsync);

        return group;
    }

    private static async Task<IResult> GetFeedAsync(
        double? latitude,
        double? longitude,
        AppDbContext db,
        AffluenceAggregationService affluence,
        SignedDeepLinkService links,
        IOptions<FeedOptions> feedOptions,
        MediaStorageService mediaStorage,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty) return Results.Forbid();

        var centerLat = latitude is >= -90 and <= 90 ? latitude.Value : 45.4642;
        var centerLng = longitude is >= -180 and <= 180 ? longitude.Value : 9.1900;
        const double delta = 0.065;
        var venues = await affluence.GetVenueMarkersAsync(
            centerLat - delta,
            centerLng - delta,
            centerLat + delta,
            centerLng + delta,
            currentUserId,
            query: null,
            category: null,
            openNowOnly: false,
            centerLat: centerLat,
            centerLng: centerLng,
            maxDistanceKm: 16,
            ct);

        var now = DateTimeOffset.UtcNow;
        var friendIds = await db.FriendRelations
            .AsNoTracking()
            .Where(x => x.Status == "accepted" && (x.RequesterId == currentUserId || x.AddresseeId == currentUserId))
            .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
            .ToListAsync(ct);

        var flares = await LoadFlaresAsync(db, mediaStorage, currentUserId, friendIds, now, ct);
        var tables = await LoadTablesAsync(db, currentUserId, now, ct);
        var fatigue = await db.FeedCardFatigues
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .ToListAsync(ct);
        var fatigueMap = fatigue.ToDictionary(x => x.CardKey, x => x);

        var items = new List<FeedServerItemDto>();
        // Keep feed refresh read-only and cheap: signed/share links are minted by
        // POST /api/feed/links only when the user actually shares or opens a card.

        foreach (var venue in venues)
        {
            var socialProof = venue.ActiveCheckIns * 18 + venue.ActiveIntentions * 14 + venue.OpenTables * 10 + venue.PresencePreview.Count() * 12;
            var heat = venue.PartyPulse.EnergyScore * 0.9 + venue.PartyPulse.ArrivalsLast15 * 8;
            var actionable = venue.OpenTables > 0 ? 18 : 8;
            var cardKey = $"hotspot-{venue.VenueId:D}";
            var score = socialProof * 0.30 + heat * 0.25 + actionable * 0.15 - FatiguePenalty(fatigueMap, cardKey);
            if (score < 10 && venue.PeopleEstimate == 0 && venue.PartyPulse.EnergyScore < 35)
            {
                continue;
            }

            items.Add(new FeedServerItemDto(
                cardKey,
                "hotspotVenue",
                Math.Round(score, 2),
                now,
                venue.VenueId,
                venue.VenueId,
                venue.Name,
                $"{venue.PartyPulse.EnergyScore}% energia · dato aggregato",
                "Dato aggregato, nessuna posizione precisa.",
                "server_feed:venue_heat",
                links.BuildUnsignedFallback("venue", venue.VenueId),
                null));

            if (venue.IntentRadar.GoingOut + venue.IntentRadar.AlmostThere >= 3)
            {
                var forecastKey = $"arrival-{venue.VenueId:D}";
                items.Add(new FeedServerItemDto(
                    forecastKey,
                    "arrivalForecast",
                    Math.Round(score + 18 - FatiguePenalty(fatigueMap, forecastKey), 2),
                    now,
                    venue.VenueId,
                    venue.VenueId,
                    $"{venue.IntentRadar.GoingOut + venue.IntentRadar.AlmostThere} amici stanno convergendo",
                    $"Tra poco movimento a {venue.Name}",
                    "Mostrato in modalita fuzzy in base alle impostazioni privacy.",
                    "server_feed:intent_radar",
                    links.BuildUnsignedFallback("venue", venue.VenueId),
                    null));
            }
        }

        foreach (var table in tables)
        {
            var cardKey = $"table-{table.TableId:D}";
            var fill = table.Capacity <= 0 ? 0 : (double)table.AcceptedCount / table.Capacity;
            items.Add(new FeedServerItemDto(
                cardKey,
                "joinableTable",
                Math.Round(55 + fill * 35 - FatiguePenalty(fatigueMap, cardKey), 2),
                table.StartsAtUtc,
                null,
                table.TableId,
                table.Title,
                $"{table.AcceptedCount}/{table.Capacity} posti · {table.VenueName}",
                "Mostrato perche il tavolo e visibile a te.",
                "server_feed:tables",
                links.BuildUnsignedFallback("table", table.TableId),
                null));
        }

        foreach (var flare in flares)
        {
            var minutesLeft = Math.Max(1, (int)Math.Ceiling((flare.ExpiresAtUtc - now).TotalMinutes));
            var cardKey = $"flare-{flare.FlareId:D}";
            items.Add(new FeedServerItemDto(
                cardKey,
                "flareChain",
                Math.Round(65 + Math.Max(0, 15 - minutesLeft) * 2 + flare.ResponseCount * 4 - FatiguePenalty(fatigueMap, cardKey), 2),
                flare.CreatedAtUtc,
                null,
                flare.FlareId,
                flare.DisplayName ?? flare.Nickname,
                flare.Message,
                "Flare mostrato in zona, senza coordinate utente precise.",
                "server_feed:flares",
                links.BuildUnsignedFallback("flare", flare.FlareId),
                null));
        }

        var ranked = items
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(feedOptions.Value.FeedTake, 12, 80))
            .ToList();

        return Results.Ok(new FeedResponseDto(
            ranked,
            venues,
            flares,
            tables,
            fatigue.Select(x => new FeedCardFatigueDto(x.CardKey, x.SeenCount, x.DismissedCount, x.LastSeenAtUtc, x.LastDismissedAtUtc)),
            now));
    }

    private static async Task<IResult> UpdateFatigueAsync(
        FeedFatigueUpdateRequest request,
        AppDbContext db,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty) return Results.Forbid();

        var cardKey = request.CardKey.Trim();
        if (cardKey.Length is < 3 or > 160)
        {
            return Results.BadRequest("CardKey non valida.");
        }

        var now = DateTimeOffset.UtcNow;
        var row = await db.FeedCardFatigues.FirstOrDefaultAsync(x => x.UserId == currentUserId && x.CardKey == cardKey, ct);
        if (row is null)
        {
            row = new FeedCardFatigue { UserId = currentUserId, CardKey = cardKey };
            db.FeedCardFatigues.Add(row);
        }

        row.SeenCount++;
        row.LastSeenAtUtc = now;
        if (request.Dismissed)
        {
            row.DismissedCount++;
            row.LastDismissedAtUtc = now;
        }
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new FeedCardFatigueDto(row.CardKey, row.SeenCount, row.DismissedCount, row.LastSeenAtUtc, row.LastDismissedAtUtc));
    }

    private static async Task<IResult> CreateSignedLinkAsync(
        SignedDeepLinkRequest request,
        SignedDeepLinkService links,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty) return Results.Forbid();
        if (request.TargetId == Guid.Empty) return Results.BadRequest("TargetId richiesto.");

        var ttlMinutes = Math.Clamp(request.ExpiresInMinutes ?? 240, 5, 10_080);
        var url = await links.CreateAsync(
            request.Type,
            request.TargetId,
            currentUserId,
            TimeSpan.FromMinutes(ttlMinutes),
            Math.Clamp(request.MaxUses ?? 30, 1, 500),
            ct);

        return Results.Ok(new SignedDeepLinkDto(url, DateTimeOffset.UtcNow.AddMinutes(ttlMinutes)));
    }

    private static double FatiguePenalty(Dictionary<string, FeedCardFatigue> fatigue, string key)
    {
        if (!fatigue.TryGetValue(key, out var row)) return 0;
        return Math.Min(35, row.SeenCount * 2.5 + row.DismissedCount * 12);
    }

    private static async Task<List<FlareDto>> LoadFlaresAsync(
        AppDbContext db,
        MediaStorageService mediaStorage,
        Guid currentUserId,
        List<Guid> friendIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var rows = await db.FlareSignals
            .AsNoTracking()
            .Where(x => x.ExpiresAtUtc > now && (x.UserId == currentUserId || friendIds.Contains(x.UserId)))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .Join(db.Users.AsNoTracking(), flare => flare.UserId, u => u.Id, (flare, u) => new
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

        var ids = rows.Select(x => x.Id).ToList();
        var counts = ids.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.FlareResponses
                .AsNoTracking()
                .Where(x => ids.Contains(x.FlareSignalId))
                .GroupBy(x => x.FlareSignalId)
                .Select(x => new { FlareId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.FlareId, x => x.Count, ct);

        return rows.Select(x => new FlareDto(
            x.Id,
            x.UserId,
            x.Nickname,
            x.DisplayName,
            mediaStorage.ResolveUrl(x.AvatarUrl),
            x.Latitude,
            x.Longitude,
            x.Message,
            counts.GetValueOrDefault(x.Id),
            x.CreatedAtUtc,
            x.ExpiresAtUtc)).ToList();
    }

    private static async Task<List<SocialTableSummaryDto>> LoadTablesAsync(AppDbContext db, Guid currentUserId, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await db.SocialTableParticipants
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId && (x.Status == "accepted" || x.Status == "invited"))
            .Join(db.SocialTables.AsNoTracking().Where(x => x.Status == "open" && x.StartsAtUtc >= now),
                p => p.SocialTableId,
                t => t.Id,
                (p, t) => new { Participant = p, Table = t })
            .Join(db.Venues.AsNoTracking(),
                x => x.Table.VenueId,
                v => v.Id,
                (x, v) => new { x.Participant, x.Table, Venue = v })
            .OrderBy(x => x.Table.StartsAtUtc)
            .Take(30)
            .ToListAsync(ct);

        var tableIds = rows.Select(x => x.Table.Id).ToList();
        var participants = tableIds.Count == 0
            ? new List<SocialTableParticipant>()
            : await db.SocialTableParticipants.AsNoTracking().Where(x => tableIds.Contains(x.SocialTableId)).ToListAsync(ct);

        return rows.Select(x =>
        {
            var tableParticipants = participants.Where(p => p.SocialTableId == x.Table.Id).ToList();
            return new SocialTableSummaryDto(
                x.Table.Id,
                x.Table.Title,
                x.Table.Description,
                x.Table.StartsAtUtc,
                x.Venue.Name,
                x.Venue.Category,
                x.Table.JoinPolicy,
                x.Table.HostUserId == currentUserId,
                x.Participant.Status,
                x.Table.Capacity,
                tableParticipants.Count(p => p.Status == "requested"),
                tableParticipants.Count(p => p.Status == "accepted"),
                tableParticipants.Count(p => p.Status == "invited"));
        }).ToList();
    }
}
