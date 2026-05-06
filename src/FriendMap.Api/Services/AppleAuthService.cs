using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FriendMap.Api.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FriendMap.Api.Services;

public sealed record AppleIdentity(string Subject, string? Email);

public sealed class AppleAuthException : Exception
{
    public AppleAuthException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed class AppleAuthService
{
    private const string CacheKey = "apple-jwks";
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly AppleAuthOptions _options;

    public AppleAuthService(HttpClient http, IMemoryCache cache, IOptions<AppleAuthOptions> options)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<AppleIdentity> ValidateIdentityTokenAsync(string identityToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identityToken))
        {
            throw new AppleAuthException("identityToken Apple mancante.");
        }

        var signingKeys = await GetSigningKeysAsync(ct);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            NameClaimType = JwtRegisteredClaimNames.Sub
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(identityToken, parameters, out _);
            var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new AppleAuthException("Token Apple valido ma senza subject.");
            }

            var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            return new AppleIdentity(subject, string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant());
        }
        catch (AppleAuthException)
        {
            throw;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            throw new AppleAuthException("Token Apple non valido o non destinato a questa app.", ex);
        }
    }

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<SecurityKey>? cached) && cached is not null)
        {
            return cached;
        }

        using var response = await _http.GetAsync(_options.JwksUrl, ct);
        response.EnsureSuccessStatusCode();
        var jwksJson = await response.Content.ReadAsStringAsync(ct);
        var keys = new JsonWebKeySet(jwksJson).Keys.Cast<SecurityKey>().ToArray();
        _cache.Set(CacheKey, keys, TimeSpan.FromHours(12));
        return keys;
    }
}
