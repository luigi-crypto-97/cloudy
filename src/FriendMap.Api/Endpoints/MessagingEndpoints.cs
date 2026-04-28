using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Hubs;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class MessagingEndpoints
{
    public static RouteGroupBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messages").WithTags("Messages").RequireAuthorization().RequireRateLimiting("social");
        group.MapGet("/threads", GetThreadsAsync);
        group.MapGet("/threads/{otherUserId:guid}", GetThreadAsync);
        group.MapPost("/threads/{otherUserId:guid}", PostThreadMessageAsync);
        group.MapPost("/files", UploadMessageFileAsync);
        group.MapGet("/groups", GetGroupChatsAsync);
        group.MapPost("/groups", CreateGroupChatAsync);
        group.MapGet("/groups/{chatId:guid}", GetGroupChatThreadAsync);
        group.MapPost("/groups/{chatId:guid}/messages", PostGroupChatMessageAsync);
        group.MapGet("/venues/{venueId:guid}/chat", GetVenueChatThreadAsync);
        group.MapPost("/venues/{venueId:guid}/chat/messages", PostVenueChatMessageAsync);
        return group;
    }

    private static async Task<IResult> UploadMessageFileAsync(
        HttpRequest request,
        ClaimsPrincipal principal,
        MediaStorageService mediaStorage,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Upload multipart/form-data richiesto." });
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { message = "Upload multipart/form-data non valido." });
        }

        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "File mancante." });
        }

        const long maxBytes = 25 * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return Results.BadRequest(new { message = "File troppo grande. Massimo 25MB." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".heic", ".pdf", ".txt", ".mov", ".mp4"
        };
        if (!allowed.Contains(extension))
        {
            return Results.BadRequest(new { message = "Formato file non supportato." });
        }

        try
        {
            var url = await mediaStorage.UploadAsync(file, "uploads/messages", currentUserId, request, ct);
            return Results.Ok(new UploadMediaResult(url));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Amazon.S3.AmazonS3Exception ex)
        {
            return Results.Problem($"Storage media non configurato correttamente: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
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

        var unreadCounts = threadIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.DirectMessages
                .AsNoTracking()
                .Where(x => threadIds.Contains(x.ThreadId) && x.SenderUserId != currentUserId && x.ReadAtUtc == null)
                .GroupBy(x => x.ThreadId)
                .Select(g => new { ThreadId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ThreadId, x => x.Count, ct);

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
                    thread.LastMessageAtUtc,
                    unreadCounts.TryGetValue(thread.Id, out var unreadCount) ? unreadCount : 0);
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
            .FirstOrDefaultAsync(x => x.UserLowId == lowId && x.UserHighId == highId, ct);

        if (thread is not null)
        {
            var now = DateTimeOffset.UtcNow;
            await db.DirectMessages
                .Where(x => x.ThreadId == thread.Id && x.SenderUserId != currentUserId && x.ReadAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ReadAtUtc, now)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);
        }

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
        IHubContext<ChatHub> hubContext,
        NotificationOutboxService outbox,
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

        var message = new DirectMessage
        {
            ThreadId = thread.Id,
            SenderUserId = currentUserId,
            Body = request.Body.Trim(),
            ReadAtUtc = DateTimeOffset.UtcNow
        };
        db.DirectMessages.Add(message);

        await db.SaveChangesAsync(ct);

        await outbox.EnqueueAsync(
            otherUserId,
            "Nuovo messaggio",
            "Hai ricevuto un nuovo messaggio su Cloudy.",
            new { type = "direct_message", otherUserId = currentUserId },
            ct,
            outbox.BuildDeepLink("chat", currentUserId));

        await hubContext.Clients.Group(thread.Id.ToString()).SendAsync("ReceiveMessage", new
        {
            ThreadId = thread.Id,
            SenderId = currentUserId,
            Body = request.Body.Trim(),
            SentAt = DateTimeOffset.UtcNow
        }, ct);

        var currentUser = await db.Users.AsNoTracking().FirstAsync(x => x.Id == currentUserId, ct);
        return Results.Ok(new DirectMessageDto(
            message.Id,
            message.SenderUserId,
            currentUser.Nickname,
            currentUser.DisplayName,
            currentUser.AvatarUrl,
            message.Body,
            message.CreatedAtUtc,
            true));
    }

    private static async Task<IResult> GetGroupChatsAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty) return Results.Forbid();

        var memberships = await db.GroupChatMembers
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .ToListAsync(ct);
        var chatIds = memberships.Select(x => x.GroupChatId).ToList();

        var chats = chatIds.Count == 0
            ? new List<GroupChat>()
            : await db.GroupChats.AsNoTracking()
                .Where(x => chatIds.Contains(x.Id) && !x.IsArchived)
                .OrderByDescending(x => x.LastMessageAtUtc)
                .Take(80)
                .ToListAsync(ct);

        var summaries = await BuildGroupChatSummariesAsync(db, chats, currentUserId, memberships, ct);
        return Results.Ok(summaries);
    }

    private static async Task<IResult> CreateGroupChatAsync(
        CreateGroupChatRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty) return Results.Forbid();

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80)
        {
            return Results.BadRequest("Titolo chat richiesto, massimo 80 caratteri.");
        }

        var targetIds = request.MemberUserIds
            .Where(x => x != Guid.Empty && x != currentUserId)
            .Distinct()
            .Take(30)
            .ToList();
        if (targetIds.Count == 0)
        {
            return Results.BadRequest("Seleziona almeno un amico.");
        }

        var friendIds = await db.FriendRelations.AsNoTracking()
            .Where(x => x.Status == "accepted" && (x.RequesterId == currentUserId || x.AddresseeId == currentUserId))
            .Select(x => x.RequesterId == currentUserId ? x.AddresseeId : x.RequesterId)
            .ToListAsync(ct);
        if (targetIds.Any(x => !friendIds.Contains(x)))
        {
            return Results.Conflict("Puoi aggiungere solo amici alla chat.");
        }

        var chat = new GroupChat
        {
            CreatedByUserId = currentUserId,
            Title = title,
            Kind = "group",
            LastMessageAtUtc = DateTimeOffset.UtcNow
        };
        db.GroupChats.Add(chat);
        db.GroupChatMembers.Add(new GroupChatMember { GroupChatId = chat.Id, UserId = currentUserId, Role = "owner", LastReadAtUtc = DateTimeOffset.UtcNow });
        foreach (var userId in targetIds)
        {
            db.GroupChatMembers.Add(new GroupChatMember { GroupChatId = chat.Id, UserId = userId });
        }
        await db.SaveChangesAsync(ct);

        var summary = (await BuildGroupChatSummariesAsync(db, new[] { chat }, currentUserId, null, ct)).Single();
        return Results.Created($"/api/messages/groups/{chat.Id}", summary);
    }

    private static async Task<IResult> GetGroupChatThreadAsync(
        Guid chatId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty) return Results.Forbid();

        var chat = await db.GroupChats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == chatId && !x.IsArchived, ct);
        if (chat is null) return Results.NotFound();
        if (!await EnsureGroupMembershipAsync(db, chat, currentUserId, ct)) return Results.Forbid();

        var now = DateTimeOffset.UtcNow;
        await db.GroupChatMembers
            .Where(x => x.GroupChatId == chatId && x.UserId == currentUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastReadAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        var summary = (await BuildGroupChatSummariesAsync(db, new[] { chat }, currentUserId, null, ct)).Single();
        var messages = await BuildGroupMessagesAsync(db, chatId, currentUserId, ct);
        return Results.Ok(new GroupChatThreadDto(summary, messages));
    }

    private static async Task<IResult> PostGroupChatMessageAsync(
        Guid chatId,
        SendGroupChatMessageRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty) return Results.Forbid();

        var chat = await db.GroupChats.FirstOrDefaultAsync(x => x.Id == chatId && !x.IsArchived, ct);
        if (chat is null) return Results.NotFound();
        if (!await EnsureGroupMembershipAsync(db, chat, currentUserId, ct)) return Results.Forbid();

        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            return Results.BadRequest("Messaggio richiesto, massimo 1000 caratteri.");
        }

        var now = DateTimeOffset.UtcNow;
        chat.LastMessageAtUtc = now;
        chat.UpdatedAtUtc = now;
        var message = new GroupChatMessage { GroupChatId = chat.Id, UserId = currentUserId, Body = body };
        db.GroupChatMessages.Add(message);
        await db.GroupChatMembers
            .Where(x => x.GroupChatId == chatId && x.UserId == currentUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastReadAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
        await db.SaveChangesAsync(ct);

        var author = await db.Users.AsNoTracking().FirstAsync(x => x.Id == currentUserId, ct);
        return Results.Ok(new GroupChatMessageDto(
            message.Id,
            currentUserId,
            author.Nickname,
            author.DisplayName,
            author.AvatarUrl,
            message.Body,
            message.CreatedAtUtc,
            true));
    }

    private static async Task<IResult> GetVenueChatThreadAsync(
        Guid venueId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty) return Results.Forbid();
        var chat = await GetOrCreateVenueChatAsync(db, venueId, currentUserId, ct);
        if (chat is null) return Results.NotFound("Locale non trovato.");
        return await GetGroupChatThreadAsync(chat.Id, principal, db, ct);
    }

    private static async Task<IResult> PostVenueChatMessageAsync(
        Guid venueId,
        SendGroupChatMessageRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(principal);
        if (currentUserId == Guid.Empty) return Results.Forbid();
        var chat = await GetOrCreateVenueChatAsync(db, venueId, currentUserId, ct);
        if (chat is null) return Results.NotFound("Locale non trovato.");
        return await PostGroupChatMessageAsync(chat.Id, request, principal, db, ct);
    }

    private static async Task<bool> EnsureGroupMembershipAsync(AppDbContext db, GroupChat chat, Guid userId, CancellationToken ct)
    {
        if (chat.Kind == "venue")
        {
            var membership = await db.GroupChatMembers
                .FirstOrDefaultAsync(x => x.GroupChatId == chat.Id && x.UserId == userId, ct);
            if (membership is null)
            {
                db.GroupChatMembers.Add(new GroupChatMember
                {
                    GroupChatId = chat.Id,
                    UserId = userId,
                    Role = "member",
                    LastReadAtUtc = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
            return true;
        }

        return await db.GroupChatMembers
            .AsNoTracking()
            .AnyAsync(x => x.GroupChatId == chat.Id && x.UserId == userId, ct);
    }

    private static async Task<GroupChat?> GetOrCreateVenueChatAsync(AppDbContext db, Guid venueId, Guid currentUserId, CancellationToken ct)
    {
        var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == venueId, ct);
        if (venue is null)
        {
            return null;
        }

        var chat = await db.GroupChats
            .FirstOrDefaultAsync(x => x.Kind == "venue" && x.VenueId == venueId && !x.IsArchived, ct);
        if (chat is null)
        {
            chat = new GroupChat
            {
                CreatedByUserId = currentUserId,
                VenueId = venueId,
                Title = $"Chat di {venue.Name}",
                Kind = "venue",
                LastMessageAtUtc = DateTimeOffset.UtcNow
            };
            db.GroupChats.Add(chat);
        }

        var hasMembership = await db.GroupChatMembers
            .AnyAsync(x => x.GroupChatId == chat.Id && x.UserId == currentUserId, ct);
        if (!hasMembership)
        {
            db.GroupChatMembers.Add(new GroupChatMember
            {
                GroupChatId = chat.Id,
                UserId = currentUserId,
                Role = "member",
                LastReadAtUtc = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return chat;
    }

    private static async Task<List<GroupChatSummaryDto>> BuildGroupChatSummariesAsync(
        AppDbContext db,
        IReadOnlyCollection<GroupChat> chats,
        Guid currentUserId,
        IReadOnlyCollection<GroupChatMember>? currentMemberships,
        CancellationToken ct)
    {
        if (chats.Count == 0)
        {
            return new List<GroupChatSummaryDto>();
        }

        var chatIds = chats.Select(x => x.Id).ToList();
        var venueIds = chats.Where(x => x.VenueId.HasValue).Select(x => x.VenueId!.Value).Distinct().ToList();
        var venues = venueIds.Count == 0
            ? new Dictionary<Guid, Venue>()
            : await db.Venues.AsNoTracking().Where(x => venueIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        var memberships = currentMemberships is not null
            ? currentMemberships.Where(x => chatIds.Contains(x.GroupChatId)).ToDictionary(x => x.GroupChatId)
            : await db.GroupChatMembers.AsNoTracking()
                .Where(x => chatIds.Contains(x.GroupChatId) && x.UserId == currentUserId)
                .ToDictionaryAsync(x => x.GroupChatId, ct);

        var memberCounts = await db.GroupChatMembers.AsNoTracking()
            .Where(x => chatIds.Contains(x.GroupChatId))
            .GroupBy(x => x.GroupChatId)
            .Select(g => new { ChatId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChatId, x => x.Count, ct);

        var lastMessages = await db.GroupChatMessages.AsNoTracking()
            .Where(x => chatIds.Contains(x.GroupChatId))
            .GroupBy(x => x.GroupChatId)
            .Select(g => g.OrderByDescending(x => x.CreatedAtUtc).First())
            .ToDictionaryAsync(x => x.GroupChatId, ct);

        var unreadRows = await db.GroupChatMessages.AsNoTracking()
            .Where(x => chatIds.Contains(x.GroupChatId) && x.UserId != currentUserId)
            .Select(x => new { x.GroupChatId, x.CreatedAtUtc })
            .ToListAsync(ct);

        return chats
            .Where(chat => memberships.ContainsKey(chat.Id))
            .Select(chat =>
            {
                memberships.TryGetValue(chat.Id, out var membership);
                lastMessages.TryGetValue(chat.Id, out var lastMessage);
                var lastReadAt = membership?.LastReadAtUtc ?? DateTimeOffset.MinValue;
                var unreadCount = unreadRows.Count(x => x.GroupChatId == chat.Id && x.CreatedAtUtc > lastReadAt);
                var venueName = chat.VenueId.HasValue && venues.TryGetValue(chat.VenueId.Value, out var venue)
                    ? venue.Name
                    : null;
                return new GroupChatSummaryDto(
                    chat.Id,
                    chat.Title,
                    chat.Kind,
                    chat.VenueId,
                    venueName,
                    memberCounts.TryGetValue(chat.Id, out var count) ? count : 0,
                    BuildLastMessagePreview(lastMessage?.Body),
                    chat.LastMessageAtUtc,
                    unreadCount);
            })
            .OrderByDescending(x => x.LastMessageAtUtc)
            .ToList();
    }

    private static async Task<List<GroupChatMessageDto>> BuildGroupMessagesAsync(
        AppDbContext db,
        Guid chatId,
        Guid currentUserId,
        CancellationToken ct)
    {
        return await db.GroupChatMessages
            .AsNoTracking()
            .Where(x => x.GroupChatId == chatId)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(300)
            .Join(
                db.Users.AsNoTracking(),
                message => message.UserId,
                user => user.Id,
                (message, user) => new GroupChatMessageDto(
                    message.Id,
                    message.UserId,
                    user.Nickname,
                    user.DisplayName,
                    user.AvatarUrl,
                    message.Body,
                    message.CreatedAtUtc,
                    message.UserId == currentUserId))
            .ToListAsync(ct);
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
