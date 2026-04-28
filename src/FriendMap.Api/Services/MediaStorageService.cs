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

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint.TrimEnd('/'),
            AuthenticationRegion = _options.Region,
            ForcePathStyle = _options.ForcePathStyle
        };

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
            AutoCloseStream = false
        }, ct);

        return BuildPublicUrl(key);
    }

    private string BuildPublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";
        }

        return $"{_options.Endpoint.TrimEnd('/')}/{_options.Bucket}/{key}";
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
