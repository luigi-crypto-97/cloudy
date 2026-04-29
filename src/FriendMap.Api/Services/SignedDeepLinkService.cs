using System.Security.Cryptography;
using System.Text;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public sealed class SignedDeepLinkService
{
    private readonly AppDbContext _db;
    private readonly UniversalLinksOptions _links;
    private readonly JwtOptions _jwt;

    public SignedDeepLinkService(
        AppDbContext db,
        IOptions<UniversalLinksOptions> links,
        IOptions<JwtOptions> jwt)
    {
        _db = db;
        _links = links.Value;
        _jwt = jwt.Value;
    }

    public async Task<string> CreateAsync(
        string type,
        Guid targetId,
        Guid? createdByUserId,
        TimeSpan? ttl = null,
        int maxUses = 30,
        CancellationToken ct = default)
    {
        var normalizedType = NormalizeType(type);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(Math.Clamp(_links.DefaultExpiryMinutes, 5, 10_080)));
        var reusableAfter = DateTimeOffset.UtcNow.AddMinutes(5);
        var reusable = await _db.DeepLinkTokens
            .AsNoTracking()
            .Where(x =>
                x.LinkType == normalizedType &&
                x.TargetId == targetId &&
                x.CreatedByUserId == createdByUserId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > reusableAfter &&
                x.UseCount < x.MaxUses)
            .OrderByDescending(x => x.ExpiresAtUtc)
            .FirstOrDefaultAsync(ct);
        if (reusable is not null)
        {
            var reusableExpiresUnix = reusable.ExpiresAtUtc.ToUnixTimeSeconds();
            var reusableSignature = Sign(normalizedType, targetId, reusableExpiresUnix, reusable.Token);
            var reusableBaseUrl = string.IsNullOrWhiteSpace(_links.BaseUrl)
                ? "https://api.iron-quote.it"
                : _links.BaseUrl.TrimEnd('/');
            return $"{reusableBaseUrl}/l/{normalizedType}/{targetId:D}?x={reusableExpiresUnix}&t={Uri.EscapeDataString(reusable.Token)}&s={Uri.EscapeDataString(reusableSignature)}";
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(24));

        _db.DeepLinkTokens.Add(new DeepLinkToken
        {
            Token = token,
            LinkType = normalizedType,
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            ExpiresAtUtc = expiresAt,
            MaxUses = Math.Clamp(maxUses, 1, 500)
        });
        await _db.SaveChangesAsync(ct);

        var expiresUnix = expiresAt.ToUnixTimeSeconds();
        var signature = Sign(normalizedType, targetId, expiresUnix, token);
        var baseUrl = string.IsNullOrWhiteSpace(_links.BaseUrl)
            ? "https://api.iron-quote.it"
            : _links.BaseUrl.TrimEnd('/');

        return $"{baseUrl}/l/{normalizedType}/{targetId:D}?x={expiresUnix}&t={Uri.EscapeDataString(token)}&s={Uri.EscapeDataString(signature)}";
    }

    public async Task<SignedDeepLinkValidation> ValidateAsync(
        string type,
        Guid targetId,
        long expiresUnix,
        string token,
        string signature,
        CancellationToken ct = default)
    {
        var normalizedType = NormalizeType(type);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signature))
        {
            return SignedDeepLinkValidation.Invalid;
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
        if (expiresAt <= now)
        {
            return SignedDeepLinkValidation.Expired;
        }

        var expected = Sign(normalizedType, targetId, expiresUnix, token);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature)))
        {
            return SignedDeepLinkValidation.Invalid;
        }

        var row = await _db.DeepLinkTokens.FirstOrDefaultAsync(x =>
            x.Token == token &&
            x.LinkType == normalizedType &&
            x.TargetId == targetId, ct);

        if (row is null || row.RevokedAtUtc is not null || row.ExpiresAtUtc <= now || row.UseCount >= row.MaxUses)
        {
            return SignedDeepLinkValidation.Invalid;
        }

        row.UseCount++;
        row.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return SignedDeepLinkValidation.Valid;
    }

    public string BuildUnsignedFallback(string type, Guid targetId)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_links.BaseUrl)
            ? "https://api.iron-quote.it"
            : _links.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/l/{NormalizeType(type)}/{targetId:D}";
    }

    private string Sign(string type, Guid targetId, long expiresUnix, string token)
    {
        var key = string.IsNullOrWhiteSpace(_links.SigningKey) ? _jwt.SigningKey : _links.SigningKey;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var input = $"{type}:{targetId:D}:{expiresUnix}:{token}";
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    private static string NormalizeType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        return normalized is "venue" or "table" or "flare" or "story-stack" or "chat"
            ? normalized
            : "open";
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public enum SignedDeepLinkValidation
{
    Valid,
    Expired,
    Invalid
}
