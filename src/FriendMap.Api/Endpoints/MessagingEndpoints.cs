using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class MessagingEndpoints
{
    public static RouteGroupBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messages").WithTags("Messages").RequireAuthorization();
        group.MapGet("/threads", GetThreadsAsync);
        group.MapGet("/threads/{otherUserId:guid}", GetThreadAsync);
        group.MapPost("/threads/{otherUserId:guid}", PostThreadMessageAsync);
        return group;
    }

    private static async Task<IResult> GetThreadsAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var blockedUserIds = await GetBlockedUserIdsAsync(db, currentUserId, ct);
        var threads = await db.DirectMessageThreads
            .AsNoTracking()
            .Where(x => x.UserLowId == currentUserId || x.UserHighId == currentUserId)
            .OrderByDescending(x => x.LastMessageAtUtc)
            .Take(40)
            .ToListAsync(ct);

        threads = threads
            .Where(x => !blockedUserIds.Contains(x.UserLowId == currentUserId ? x.UserHighId : x.UserLowId))
            .ToList();

        var threadIds = threads.Select(x => x.Id).ToList();
        var lastMessages = threadIds.Count == 0
            ? new Dictionary<Guid, DirectMessage>()
            : await db.DirectMessages
                .AsNoTracking()
                .Where(x => threadIds.Contains(x.ThreadId))
                .GroupBy(x => x.ThreadId)
                .Select(g => g.OrderByDescending(x => x.CreatedAtUtc).First())
                .ToDictionaryAsync(x => x.ThreadId, ct);

        var otherUserIds = threads
            .Select(x => x.UserLowId == currentUserId ? x.UserHighId : x.UserLowId)
            .Distinct()
            .ToList();

        var users = otherUserIds.Count == 0
            ? new Dictionary<Guid, AppUser>()
            : await db.Users
                .AsNoTracking()
                .Where(x => otherUserIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        var result = threads
            .Where(x => users.ContainsKey(x.UserLowId == currentUserId ? x.UserHighId : x.UserLowId))
            .Select(thread =>
            {
                var otherUserId = thread.UserLowId == currentUserId ? thread.UserHighId : thread.UserLowId;
                var user = users[otherUserId];
                lastMessages.TryGetValue(thread.Id, out var lastMessage);
                return new DirectMessageThreadSummaryDto(
                    user.Id,
                    user.Nickname,
                    user.DisplayName,
                    user.AvatarUrl,
                    BuildLastMessagePreview(lastMessage?.Body),
                    thread.LastMessageAtUtc);
            })
            .ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> GetThreadAsync(
        Guid otherUserId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var validation = await ValidateMessagingAccessAsync(db, currentUserId, otherUserId, ct);
        if (validation is not null)
        {
            return validation;
        }

        var otherProfile = await BuildDirectMessagePeerAsync(db, currentUserId, otherUserId, ct);
        if (otherProfile is null)
        {
            return Results.NotFound();
        }

        var (lowId, highId) = NormalizePair(currentUserId, otherUserId);
        var thread = await db.DirectMessageThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserLowId == lowId && x.UserHighId == highId, ct);

        var messages = thread is null
            ? new List<DirectMessageDto>()
            : await db.DirectMessages
                .AsNoTracking()
                .Where(x => x.ThreadId == thread.Id)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(200)
                .Join(
                    db.Users.AsNoTracking(),
                    message => message.SenderUserId,
                    user => user.Id,
                    (message, user) => new DirectMessageDto(
                        message.Id,
                        message.SenderUserId,
                        user.Nickname,
                        user.DisplayName,
                        user.AvatarUrl,
                        message.Body,
                        message.CreatedAtUtc,
                        message.SenderUserId == currentUserId))
                .ToListAsync(ct);

        return Results.Ok(new DirectMessageThreadDto(otherProfile, messages));
    }

    private static async Task<IResult> PostThreadMessageAsync(
        Guid otherUserId,
        SendDirectMessageRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Results.BadRequest("Messaggio vuoto.");
        }

        var validation = await ValidateMessagingAccessAsync(db, currentUserId, otherUserId, ct);
        if (validation is not null)
        {
            return validation;
        }

        var (lowId, highId) = NormalizePair(currentUserId, otherUserId);
        var thread = await db.DirectMessageThreads
            .FirstOrDefaultAsync(x => x.UserLowId == lowId && x.UserHighId == highId, ct);

        if (thread is null)
        {
            thread = new DirectMessageThread
            {
                UserLowId = lowId,
                UserHighId = highId
            };
            db.DirectMessageThreads.Add(thread);
        }

        thread.LastMessageAtUtc = DateTimeOffset.UtcNow;
        thread.UpdatedAtUtc = DateTimeOffset.UtcNow;

        db.DirectMessages.Add(new DirectMessage
        {
            ThreadId = thread.Id,
            SenderUserId = currentUserId,
            Body = request.Body.Trim()
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(new SocialActionResultDto("sent", "Messaggio inviato."));
    }

    private static async Task<IResult?> ValidateMessagingAccessAsync(AppDbContext db, Guid currentUserId, Guid otherUserId, CancellationToken ct)
    {
        if (otherUserId == Guid.Empty || otherUserId == currentUserId)
        {
            return Results.BadRequest("Seleziona un altro utente.");
        }

        var exists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == otherUserId, ct);
        if (!exists)
        {
            return Results.NotFound("Utente non trovato.");
        }

        var isBlocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
            (x.BlockerUserId == currentUserId && x.BlockedUserId == otherUserId) ||
            (x.BlockerUserId == otherUserId && x.BlockedUserId == currentUserId), ct);
        if (isBlocked)
        {
            return Results.Conflict("Messaggistica non disponibile per questo utente.");
        }

        var areFriends = await db.FriendRelations.AsNoTracking().AnyAsync(x =>
            x.Status == "accepted" &&
            ((x.RequesterId == currentUserId && x.AddresseeId == otherUserId) ||
             (x.RequesterId == otherUserId && x.AddresseeId == currentUserId)), ct);
        if (!areFriends)
        {
            return Results.Conflict("La chat 1:1 è disponibile solo tra amici.");
        }

        return null;
    }

    private static async Task<HashSet<Guid>> GetBlockedUserIdsAsync(AppDbContext db, Guid currentUserId, CancellationToken ct)
    {
        var blocked = await db.UserBlocks
            .AsNoTracking()
            .Where(x => x.BlockerUserId == currentUserId || x.BlockedUserId == currentUserId)
            .Select(x => x.BlockerUserId == currentUserId ? x.BlockedUserId : x.BlockerUserId)
            .ToListAsync(ct);
        return blocked.ToHashSet();
    }

    private static async Task<DirectMessagePeerDto?> BuildDirectMessagePeerAsync(AppDbContext db, Guid viewerUserId, Guid otherUserId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == otherUserId, ct);
        if (user is null)
        {
            return null;
        }

        var blocks = await db.UserBlocks
            .AsNoTracking()
            .Where(x =>
                (x.BlockerUserId == viewerUserId && x.BlockedUserId == otherUserId) ||
                (x.BlockerUserId == otherUserId && x.BlockedUserId == viewerUserId))
            .ToListAsync(ct);

        return new DirectMessagePeerDto(
            user.Id,
            user.Nickname,
            user.DisplayName,
            user.AvatarUrl,
            blocks.Any(x => x.BlockerUserId == viewerUserId),
            blocks.Any(x => x.BlockerUserId == otherUserId));
    }

    private static (Guid LowId, Guid HighId) NormalizePair(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }

    private static string BuildLastMessagePreview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Nessun messaggio";
        }

        var trimmed = body.Trim();
        return trimmed.Length <= 72 ? trimmed : $"{trimmed[..72]}...";
    }
}
