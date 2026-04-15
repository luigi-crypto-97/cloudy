using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Endpoints;

public static class SocialEndpoints
{
    public static RouteGroupBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/social").WithTags("Social");

        group.MapPost("/intentions", async (
            CreateIntentionRequest request,
            AppDbContext db,
            AffluenceAggregationService aggregationService,
            IOptions<PrivacyOptions> privacy,
            CancellationToken ct) =>
        {
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
            CancellationToken ct) =>
        {
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
            CancellationToken ct) =>
        {
            var entity = new SocialTable
            {
                HostUserId = request.HostUserId,
                VenueId = request.VenueId,
                Title = request.Title,
                Description = request.Description,
                StartsAtUtc = request.StartsAtUtc,
                Capacity = request.Capacity,
                JoinPolicy = request.JoinPolicy
            };

            db.SocialTables.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/social/tables/{entity.Id}", entity);
        });

        group.MapPost("/tables/{tableId:guid}/join", async (
            Guid tableId,
            Guid userId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var table = await db.SocialTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
            if (table is null) return Results.NotFound();

            var participant = new SocialTableParticipant
            {
                SocialTableId = tableId,
                UserId = userId,
                Status = table.JoinPolicy == "auto" ? "accepted" : "requested"
            };

            db.SocialTableParticipants.Add(participant);
            await db.SaveChangesAsync(ct);

            return Results.Ok(participant);
        });

        return group;
    }
}
