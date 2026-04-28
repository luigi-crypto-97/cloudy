namespace FriendMap.Api.Models;

public class FlareSignal : BaseEntity
{
    public Guid UserId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
