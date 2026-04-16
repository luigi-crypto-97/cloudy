using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class SafetyEndpoints
{
    public static RouteGroupBuilder MapSafetyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/safety").WithTags("Safety").RequireAuthorization();

        group.MapPost("/blocks/{targetUserId:guid}", async (
            Guid targetUserId,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (targetUserId == Guid.Empty || targetUserId == currentUserId)
            {
                return Results.BadRequest("Seleziona un altro utente.");
            }

            var exists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == targetUserId, ct);
            if (!exists)
            {
                return Results.NotFound("Utente non trovato.");
            }

            var alreadyBlocked = await db.UserBlocks
                .AnyAsync(x => x.BlockerUserId == currentUserId && x.BlockedUserId == targetUserId, ct);
            if (alreadyBlocked)
            {
                return Results.Ok(new SocialActionResultDto("blocked", "Utente già bloccato."));
            }

            db.UserBlocks.Add(new UserBlock
            {
                BlockerUserId = currentUserId,
                BlockedUserId = targetUserId
            });

            var relations = await db.FriendRelations
                .Where(x =>
                    (x.RequesterId == currentUserId && x.AddresseeId == targetUserId) ||
                    (x.RequesterId == targetUserId && x.AddresseeId == currentUserId))
                .ToListAsync(ct);
            if (relations.Any())
            {
                db.FriendRelations.RemoveRange(relations);
            }

            var threads = await db.DirectMessageThreads
                .Where(x =>
                    (x.UserLowId == currentUserId && x.UserHighId == targetUserId) ||
                    (x.UserLowId == targetUserId && x.UserHighId == currentUserId))
                .ToListAsync(ct);
            if (threads.Any())
            {
                db.DirectMessageThreads.RemoveRange(threads);
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new SocialActionResultDto("blocked", "Utente bloccato."));
        });

        group.MapDelete("/blocks/{targetUserId:guid}", async (
            Guid targetUserId,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var block = await db.UserBlocks
                .FirstOrDefaultAsync(x => x.BlockerUserId == currentUserId && x.BlockedUserId == targetUserId, ct);
            if (block is null)
            {
                return Results.NotFound();
            }

            db.UserBlocks.Remove(block);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new SocialActionResultDto("unblocked", "Utente sbloccato."));
        });

        group.MapPost("/reports/user", async (
            CreateUserReportRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (request.ReportedUserId == Guid.Empty || request.ReportedUserId == currentUserId)
            {
                return Results.BadRequest("Seleziona un altro utente.");
            }

            var exists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == request.ReportedUserId, ct);
            if (!exists)
            {
                return Results.NotFound("Utente non trovato.");
            }

            db.ModerationReports.Add(new ModerationReport
            {
                ReporterUserId = currentUserId,
                ReportedUserId = request.ReportedUserId,
                ReasonCode = NormalizeReasonCode(request.ReasonCode),
                Details = NormalizeDetails(request.Details)
            });

            await db.SaveChangesAsync(ct);
            return Results.Ok(new SocialActionResultDto("reported", "Segnalazione inviata al team di moderazione."));
        });

        group.MapPost("/reports/table", async (
            CreateTableReportRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentUserId = CurrentUser.GetUserId(principal);
            if (currentUserId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var exists = await db.SocialTables.AsNoTracking().AnyAsync(x => x.Id == request.SocialTableId, ct);
            if (!exists)
            {
                return Results.NotFound("Tavolo non trovato.");
            }

            db.ModerationReports.Add(new ModerationReport
            {
                ReporterUserId = currentUserId,
                ReportedSocialTableId = request.SocialTableId,
                ReasonCode = NormalizeReasonCode(request.ReasonCode),
                Details = NormalizeDetails(request.Details)
            });

            await db.SaveChangesAsync(ct);
            return Results.Ok(new SocialActionResultDto("reported", "Tavolo segnalato."));
        });

        return group;
    }

    private static string NormalizeReasonCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "other";
        }

        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed.Length <= 40 ? trimmed : trimmed[..40];
    }

    private static string? NormalizeDetails(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
