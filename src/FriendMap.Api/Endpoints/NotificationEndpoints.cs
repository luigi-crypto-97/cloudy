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
}
