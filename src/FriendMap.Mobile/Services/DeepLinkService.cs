namespace FriendMap.Mobile.Services;

public static class DeepLinkService
{
    public static event EventHandler<DeepLinkArgs>? LinkReceived;

    public static void HandleUrl(Uri uri)
    {
        var (type, id) = ParseUri(uri);
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        LinkReceived?.Invoke(null, new DeepLinkArgs(type, id));
    }

    public static string BuildUniversalLink(Uri? backendBaseAddress, string type, Guid id)
    {
        var host = backendBaseAddress is null
            ? "https://api.iron-quote.it"
            : $"{backendBaseAddress.Scheme}://{backendBaseAddress.Host}";
        return $"{host.TrimEnd('/')}/l/{type.Trim().ToLowerInvariant()}/{id}";
    }

    private static (string? Type, string? Id) ParseUri(Uri uri)
    {
        if (uri.Scheme.Equals("cloudy", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("friendmap", StringComparison.OrdinalIgnoreCase))
        {
            var customSegments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (customSegments.Length >= 2)
            {
                return (customSegments[0], customSegments[1]);
            }
        }

        if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && segments[0].Equals("l", StringComparison.OrdinalIgnoreCase))
            {
                return (segments[1], segments[2]);
            }
        }

        return (null, null);
    }
}

public class DeepLinkArgs : EventArgs
{
    public string Type { get; }
    public string Id { get; }

    public DeepLinkArgs(string type, string id)
    {
        Type = type;
        Id = id;
    }
}
