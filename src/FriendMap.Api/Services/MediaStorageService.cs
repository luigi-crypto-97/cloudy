using Amazon.S3;
using FriendMap.Api.Data;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FriendMap.Api.Services;

public class MediaStorageService
{
    private static readonly ConcurrentDictionary<string, string> DiscoveredRegionsByEndpoint = new(StringComparer.OrdinalIgnoreCase);
    private readonly MediaStorageOptions _options;
    private readonly IWebHostEnvironment _env;

    public MediaStorageService(IOptions<MediaStorageOptions> options, IWebHostEnvironment env)
    {
        _options = options.Value;
        _env = env;
    }

    public async Task<string> UploadAsync(
        IFormFile file,
        string folder,
        Guid ownerUserId,
        HttpRequest request,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeName = BuildSafeFileName(file.FileName, extension);
        var key = $"{NormalizeFolder(folder)}/{ownerUserId:N}/{DateTimeOffset.UtcNow:yyyy/MM}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}-{safeName}{extension}";

        if (string.Equals(_options.Provider, "s3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_options.Provider, "supabase", StringComparison.OrdinalIgnoreCase))
        {
            return await UploadToS3Async(file, key, ct);
        }

        return await UploadToLocalAsync(file, key, request, ct);
    }

    private async Task<string> UploadToLocalAsync(IFormFile file, string key, HttpRequest request, CancellationToken ct)
    {
        var rootPath = Path.IsPathRooted(_options.LocalRootPath)
            ? _options.LocalRootPath
            : Path.Combine(_env.ContentRootPath, _options.LocalRootPath);
        var physicalPath = Path.Combine(rootPath, key.Replace('/', Path.DirectorySeparatorChar));
        var targetFolder = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrWhiteSpace(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var baseUri = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUri}/{key}";
    }

    private async Task<string> UploadToS3Async(IFormFile file, string key, CancellationToken ct)
    {
        EnsureS3Configured();

        var endpoint = BuildEndpointUri();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, ct);
            bytes = memory.ToArray();
        }

        var payloadHash = ToHex(SHA256.HashData(bytes));
        var canonicalUri = BuildS3CanonicalUri(endpoint, key);
        var url = $"{endpoint.Scheme}://{endpoint.Authority}{canonicalUri}";

        var result = await SendS3PutAsync(url, canonicalUri, contentType, payloadHash, bytes, GetEffectiveRegion(endpoint), ct);
        if (!result.Success)
        {
            var advertisedRegion = ExtractS3Region(result.Detail);
            if (!string.IsNullOrWhiteSpace(advertisedRegion) &&
                !string.Equals(advertisedRegion, GetEffectiveRegion(endpoint), StringComparison.OrdinalIgnoreCase))
            {
                DiscoveredRegionsByEndpoint[endpoint.Authority] = advertisedRegion;
                result = await SendS3PutAsync(url, canonicalUri, contentType, payloadHash, bytes, advertisedRegion, ct);
            }
        }

        if (!result.Success)
        {
            throw new AmazonS3Exception($"S3 upload failed ({result.StatusCode} {result.ReasonPhrase}): {result.Detail}");
        }

        return _options.UsePrivateBucket ? key : BuildPublicUrl(key);
    }

    private async Task<S3PutResult> SendS3PutAsync(
        string url,
        string canonicalUri,
        string contentType,
        string payloadHash,
        byte[] bytes,
        string region,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var canonicalHeaders =
            $"content-type:{contentType}\n" +
            $"host:{new Uri(url).Authority}\n" +
            $"x-amz-content-sha256:{payloadHash}\n" +
            $"x-amz-date:{amzDate}\n";
        const string signedHeaders = "content-type;host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = string.Join('\n', new[]
        {
            "PUT",
            canonicalUri,
            "",
            canonicalHeaders,
            signedHeaders,
            payloadHash
        });
        var credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        var signature = SignString(dateStamp, credentialScope, amzDate, canonicalRequest, region);

        using var http = new HttpClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        using var message = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = content
        };
        message.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        message.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        message.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={_options.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");

        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            return new S3PutResult(false, (int)response.StatusCode, response.ReasonPhrase ?? "", detail);
        }

        return new S3PutResult(true, (int)response.StatusCode, response.ReasonPhrase ?? "", "");
    }

    public string? ResolveUrl(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return storedValue;
        }

        var key = TryExtractStorageKey(storedValue.Trim());
        if (key is null)
        {
            return storedValue;
        }

        if (!IsS3Provider())
        {
            if (Uri.TryCreate(storedValue, UriKind.Absolute, out _))
            {
                return storedValue;
            }

            var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
                ? ""
                : _options.PublicBaseUrl.TrimEnd('/');
            return string.IsNullOrWhiteSpace(baseUrl) ? storedValue : $"{baseUrl}/{key}";
        }

        if (!_options.UsePrivateBucket && !string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";
        }

        EnsureS3Configured();
        return $"/api/media/{EncodePath(key)}";
    }

    public async Task<MediaDownloadResult?> DownloadAsync(string key, CancellationToken ct)
    {
        key = key.TrimStart('/');
        if (!LooksLikeStorageKey(key))
        {
            return null;
        }

        if (!IsS3Provider())
        {
            var rootPath = Path.IsPathRooted(_options.LocalRootPath)
                ? _options.LocalRootPath
                : Path.Combine(_env.ContentRootPath, _options.LocalRootPath);
            var physicalPath = Path.Combine(rootPath, key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(physicalPath))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(physicalPath, ct);
            return new MediaDownloadResult(bytes, GuessContentType(key));
        }

        EnsureS3Configured();
        var endpoint = BuildEndpointUri();
        var result = await DownloadFromSignedUrlAsync(BuildPreSignedGetUrl(key), ct);
        if (!result.Success)
        {
            var advertisedRegion = ExtractS3Region(result.Detail);
            if (!string.IsNullOrWhiteSpace(advertisedRegion) &&
                !string.Equals(advertisedRegion, GetEffectiveRegion(endpoint), StringComparison.OrdinalIgnoreCase))
            {
                DiscoveredRegionsByEndpoint[endpoint.Authority] = advertisedRegion;
                result = await DownloadFromSignedUrlAsync(BuildPreSignedGetUrl(key), ct);
            }
        }

        return result.Success ? new MediaDownloadResult(result.Bytes, result.ContentType) : null;
    }

    private static async Task<S3GetResult> DownloadFromSignedUrlAsync(string url, CancellationToken ct)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new S3GetResult(false, Array.Empty<byte>(), response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", Encoding.UTF8.GetString(bytes));
        }

        return new S3GetResult(true, bytes, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", "");
    }

    private Uri BuildEndpointUri()
    {
        if (!Uri.TryCreate(_options.Endpoint.TrimEnd('/'), UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("MediaStorage S3 non configurato: endpoint non valido.");
        }

        return endpoint;
    }

    private void EnsureS3Configured()
    {
        if (!IsS3Provider())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.Bucket) ||
            string.IsNullOrWhiteSpace(_options.Region) ||
            string.IsNullOrWhiteSpace(_options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_options.SecretAccessKey))
        {
            throw new InvalidOperationException("MediaStorage S3 non configurato: endpoint, bucket, region, access key e secret sono obbligatori.");
        }
    }

    private string BuildPublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";
        }

        return $"{_options.Endpoint.TrimEnd('/')}/{_options.Bucket}/{key}";
    }

    private bool IsS3Provider()
    {
        return string.Equals(_options.Provider, "s3", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(_options.Provider, "supabase", StringComparison.OrdinalIgnoreCase);
    }

    private string? TryExtractStorageKey(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return LooksLikeStorageKey(value) ? value.TrimStart('/') : null;
        }

        var path = uri.AbsolutePath.TrimStart('/');
        var publicPrefix = $"storage/v1/object/public/{_options.Bucket}/";
        if (!string.IsNullOrWhiteSpace(_options.Bucket) &&
            path.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(path[publicPrefix.Length..]);
        }

        var s3Prefix = $"{_options.Bucket}/";
        if (!string.IsNullOrWhiteSpace(_options.Bucket) &&
            path.StartsWith(s3Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(path[s3Prefix.Length..]);
        }

        return null;
    }

    private static bool LooksLikeStorageKey(string value)
    {
        return value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildPreSignedGetUrl(string key)
    {
        var endpoint = BuildEndpointUri();
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var expires = Math.Clamp(_options.SignedUrlMinutes, 1, 120) * 60;
        var region = GetEffectiveRegion(endpoint);
        var credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        var canonicalUri = BuildS3CanonicalUri(endpoint, key);
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{_options.AccessKeyId}/{credentialScope}",
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = expires.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };
        var canonicalQuery = BuildCanonicalQueryString(query);
        var canonicalHeaders = $"host:{endpoint.Authority}\n";
        var canonicalRequest = string.Join('\n', new[]
        {
            "GET",
            canonicalUri,
            canonicalQuery,
            canonicalHeaders,
            "host",
            "UNSIGNED-PAYLOAD"
        });
        var signature = SignString(dateStamp, credentialScope, amzDate, canonicalRequest, region);
        return $"{endpoint.Scheme}://{endpoint.Authority}{canonicalUri}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    private string SignString(string dateStamp, string credentialScope, string amzDate, string canonicalRequest, string region)
    {
        var stringToSign = string.Join('\n', new[]
        {
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))
        });
        var signingKey = GetSignatureKey(_options.SecretAccessKey, dateStamp, region, "s3");
        return ToHex(HmacSha256(signingKey, stringToSign));
    }

    private string GetEffectiveRegion(Uri endpoint)
    {
        return DiscoveredRegionsByEndpoint.TryGetValue(endpoint.Authority, out var discovered)
            ? discovered
            : _options.Region;
    }

    private static string? ExtractS3Region(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var match = Regex.Match(detail, @"<Region>(?<region>[^<]+)</Region>", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["region"].Value.Trim() : null;
    }

    private static string GuessContentType(string key)
    {
        return Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".m4v" => "video/x-m4v",
            ".webm" => "video/webm",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private string BuildS3CanonicalUri(Uri endpoint, string key)
    {
        var basePath = endpoint.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(basePath) || basePath == "/")
        {
            basePath = "";
        }

        if (_options.ForcePathStyle)
        {
            return $"{basePath}/{S3UriEncode(_options.Bucket)}/{EncodePath(key)}";
        }

        return $"{basePath}/{EncodePath(key)}";
    }

    private static string BuildCanonicalQueryString(SortedDictionary<string, string> query)
    {
        return string.Join("&", query.Select(pair => $"{S3UriEncode(pair.Key)}={S3UriEncode(pair.Value)}"));
    }

    private static string EncodePath(string value)
    {
        return string.Join("/", value.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(S3UriEncode));
    }

    private static string S3UriEncode(string value)
    {
        var encoded = Uri.EscapeDataString(value);
        return encoded
            .Replace("%7E", "~", StringComparison.OrdinalIgnoreCase)
            .Replace("+", "%20", StringComparison.Ordinal);
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{key}"), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeFolder(string folder)
    {
        return folder
            .Trim()
            .Trim('/', '\\')
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    private static string BuildSafeFileName(string fileName, string extension)
    {
        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var safeName = rawName
            .Replace(' ', '-')
            .ToLowerInvariant();
        safeName = string.Concat(safeName.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
        return string.IsNullOrWhiteSpace(safeName) ? "file" : safeName;
    }

    private readonly record struct S3PutResult(bool Success, int StatusCode, string ReasonPhrase, string Detail);
    private readonly record struct S3GetResult(bool Success, byte[] Bytes, string ContentType, string Detail);
}

public sealed record MediaDownloadResult(byte[] Bytes, string ContentType);
