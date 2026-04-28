using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FriendMap.Api.Data;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public class MediaStorageService
{
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
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.Bucket) ||
            string.IsNullOrWhiteSpace(_options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_options.SecretAccessKey))
        {
            throw new InvalidOperationException("MediaStorage S3 non configurato: endpoint, bucket, access key e secret sono obbligatori.");
        }

        Amazon.AWSConfigsS3.UseSignatureVersion4 = true;
        var config = BuildS3Config();

        using var client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
            config);

        await using var stream = file.OpenReadStream();
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true
        }, ct);

        return _options.UsePrivateBucket ? key : BuildPublicUrl(key);
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
        Amazon.AWSConfigsS3.UseSignatureVersion4 = true;
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
            BuildS3Config());

        return client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(Math.Clamp(_options.SignedUrlMinutes, 1, 120))
        });
    }

    private AmazonS3Config BuildS3Config()
    {
        EnsureS3Configured();
        return new AmazonS3Config
        {
            ServiceURL = _options.Endpoint.TrimEnd('/'),
            AuthenticationRegion = _options.Region,
            ForcePathStyle = _options.ForcePathStyle,
            DisableS3ExpressSessionAuth = true
        };
    }

    private void EnsureS3Configured()
    {
        if (!IsS3Provider())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.Bucket) ||
            string.IsNullOrWhiteSpace(_options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_options.SecretAccessKey))
        {
            throw new InvalidOperationException("MediaStorage S3 non configurato: endpoint, bucket, access key e secret sono obbligatori.");
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
}
