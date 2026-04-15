using System.Text.Json;
using FriendMap.Api.Data;
using FriendMap.Api.Models;

namespace FriendMap.Api.Services;

public class NotificationOutboxService
{
    private readonly AppDbContext _db;

    public NotificationOutboxService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(Guid userId, string title, string body, object? payload, CancellationToken ct)
    {
        var item = new NotificationOutboxItem
        {
            UserId = userId,
            Title = title,
            Body = body,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            Status = "pending",
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        };

        _db.NotificationOutboxItems.Add(item);
        await _db.SaveChangesAsync(ct);
    }
}
