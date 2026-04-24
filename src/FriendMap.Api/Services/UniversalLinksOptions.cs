namespace FriendMap.Api.Services;

public sealed class UniversalLinksOptions
{
    public string BaseUrl { get; set; } = "https://api.iron-quote.it";
    public List<string> IosAppIds { get; set; } = new();
}
