using FriendMap.Api.Services;

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

        return group;
    }
}
