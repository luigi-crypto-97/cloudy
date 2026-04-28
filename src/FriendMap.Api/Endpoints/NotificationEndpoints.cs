using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async (
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var items = await db.NotificationOutboxItems
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId && x.DeletedAtUtc == null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(80)
                .Select(x => new NotificationOutboxItemDto(
                    x.Id,
                    x.Title,
                    x.Body,
                    ResolveNotificationType(x.PayloadJson),
                    x.CreatedAtUtc,
                    x.IsRead,
                    x.DeepLink))
                .ToListAsync(ct);

            return Results.Ok(items);
        });

        group.MapGet("/unread-count", async (
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var count = await db.NotificationOutboxItems
                .AsNoTracking()
                .CountAsync(x => x.UserId == currentUserId && !x.IsRead && x.DeletedAtUtc == null, ct);

            return Results.Ok(new NotificationUnreadCountDto(count));
        });

        group.MapPost("/mark-read", async (
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
            var updated = await db.NotificationOutboxItems
                .Where(x => x.UserId == currentUserId && !x.IsRead && x.DeletedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsRead, true)
                    .SetProperty(x => x.ReadAtUtc, now)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);

            return Results.Ok(new { updated });
        });

        group.MapDelete("/{notificationId:guid}", async (
            Guid notificationId,
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
            var updated = await db.NotificationOutboxItems
                .Where(x => x.Id == notificationId && x.UserId == currentUserId && x.DeletedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DeletedAtUtc, now)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);

            return updated == 0 ? Results.NotFound() : Results.NoContent();
        });

        group.MapDelete("", DeleteAllAsync);
        group.MapDelete("/", DeleteAllAsync);

        group.MapPost("/device-tokens", async (
            RegisterDeviceTokenRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != request.UserId)
            {
                return Results.Forbid();
            }

            if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.DeviceToken))
            {
                return Results.BadRequest("userId and deviceToken are required.");
            }

            var platform = string.IsNullOrWhiteSpace(request.Platform)
                ? "ios"
                : request.Platform.Trim().ToLowerInvariant();

            var token = await db.NotificationDeviceTokens.FirstOrDefaultAsync(x =>
                x.UserId == request.UserId &&
                x.Platform == platform &&
                x.DeviceToken == request.DeviceToken, ct);

            if (token is null)
            {
                token = new NotificationDeviceToken
                {
                    UserId = request.UserId,
                    Platform = platform,
                    DeviceToken = request.DeviceToken.Trim()
                };
                db.NotificationDeviceTokens.Add(token);
            }

            token.IsActive = true;
            token.LastSeenAtUtc = DateTimeOffset.UtcNow;
            token.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { token.Id, token.UserId, token.Platform, token.IsActive });
        });

        group.MapPost("/test", async (
            QueueTestNotificationRequest request,
            NotificationOutboxService outbox,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(user);
            if (currentUserId == Guid.Empty || currentUserId != request.UserId)
            {
                return Results.Forbid();
            }

            await outbox.EnqueueAsync(
                request.UserId,
                string.IsNullOrWhiteSpace(request.Title) ? "FriendMap" : request.Title.Trim(),
                string.IsNullOrWhiteSpace(request.Body) ? "Notifica demo" : request.Body.Trim(),
                new { type = "test" },
                ct);

            return Results.Accepted();
        });

        return group;
    }

    private static async Task<IResult> DeleteAllAsync(
        AppDbContext db,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        var updated = await db.NotificationOutboxItems
            .Where(x => x.UserId == currentUserId && x.DeletedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DeletedAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        return Results.Ok(new { deleted = updated });
    }

    private static string ResolveNotificationType(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return "notification";
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("type", out var type) &&
                type.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return type.GetString() ?? "notification";
            }
        }
        catch
        {
            // Payload is best-effort metadata for the inbox.
        }

        return "notification";
    }
}
