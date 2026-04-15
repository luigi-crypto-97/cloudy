using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FriendMap.Api.Data;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public class ApnsClient
{
    private readonly ApnsOptions _options;
    private readonly HttpClient _httpClient;
    private string? _cachedJwt;
    private DateTimeOffset _cachedJwtUntilUtc;

    public ApnsClient(IOptions<ApnsOptions> options)
    {
        _options = options.Value;
        _httpClient = new HttpClient
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };
    }

    public bool IsConfigured =>
        _options.Enabled &&
        !string.IsNullOrWhiteSpace(_options.TeamId) &&
        !string.IsNullOrWhiteSpace(_options.KeyId) &&
        !string.IsNullOrWhiteSpace(_options.BundleId) &&
        (!string.IsNullOrWhiteSpace(_options.PrivateKey) || !string.IsNullOrWhiteSpace(_options.PrivateKeyPath));

    public async Task SendAsync(string deviceToken, string title, string body, string? payloadJson, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("APNs is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Host}/3/device/{deviceToken}");
        request.Version = HttpVersion.Version20;
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", GetProviderToken());
        request.Headers.TryAddWithoutValidation("apns-topic", _options.BundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");

        var payload = new Dictionary<string, object?>
        {
            ["aps"] = new
            {
                alert = new { title, body },
                sound = "default"
            }
        };

        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            payload["data"] = JsonSerializer.Deserialize<object>(payloadJson);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"APNs rejected notification: {(int)response.StatusCode} {details}");
        }
    }

    private string GetProviderToken()
    {
        if (_cachedJwt is not null && DateTimeOffset.UtcNow < _cachedJwtUntilUtc)
        {
            return _cachedJwt;
        }

        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "ES256",
            kid = _options.KeyId
        }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = _options.TeamId,
            iat = issuedAt
        }));

        var signingInput = $"{header}.{payload}";
        var signature = Sign(signingInput);
        _cachedJwt = $"{signingInput}.{signature}";
        _cachedJwtUntilUtc = DateTimeOffset.UtcNow.AddMinutes(45);
        return _cachedJwt;
    }

    private string Sign(string signingInput)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(LoadPrivateKeyPem());
        var signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Base64UrlEncode(signature);
    }

    private string LoadPrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return _options.PrivateKey.Replace("\\n", "\n");
        }

        return File.ReadAllText(_options.PrivateKeyPath);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
