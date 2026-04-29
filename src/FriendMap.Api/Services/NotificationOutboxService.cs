using System.Text.Json;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public class NotificationOutboxService
{
    private readonly AppDbContext _db;
    private readonly UniversalLinksOptions _links;
    private readonly SignedDeepLinkService _signedLinks;

    public NotificationOutboxService(
        AppDbContext db,
        IOptions<UniversalLinksOptions> links,
        SignedDeepLinkService signedLinks)
    {
        _db = db;
        _links = links.Value;
        _signedLinks = signedLinks;
    }

    public async Task EnqueueAsync(Guid userId, string title, string body, object? payload, CancellationToken ct = default, string? deepLink = null)
    {
        var item = new NotificationOutboxItem
        {
            UserId = userId,
            Title = title,
            Body = body,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            DeepLink = deepLink,
            Status = "pending",
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        };

        _db.NotificationOutboxItems.Add(item);
        await _db.SaveChangesAsync(ct);
    }

    public string BuildDeepLink(string type, Guid id)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_links.BaseUrl)
            ? "https://api.iron-quote.it"
            : _links.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/l/{type.Trim().ToLowerInvariant()}/{id}";
    }

    public Task<string> BuildSignedDeepLinkAsync(
        string type,
        Guid id,
        Guid? createdByUserId,
        TimeSpan? ttl = null,
        CancellationToken ct = default) =>
        _signedLinks.CreateAsync(type, id, createdByUserId, ttl, ct: ct);
}
