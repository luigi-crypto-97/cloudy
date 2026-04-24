namespace FriendMap.Mobile.Services;

public static class DeepLinkService
{
    public static event EventHandler<DeepLinkArgs>? LinkReceived;

    public static void HandleUrl(Uri uri)
    {
        if (uri.Scheme != "friendmap") return;
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) return;

        LinkReceived?.Invoke(null, new DeepLinkArgs(segments[0], segments[1]));
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
