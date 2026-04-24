namespace FriendMap.Api.Contracts;

public record CreateFlareRequest(double Latitude, double Longitude, string Message);

public record SubmitVibeRequest(string VibeEmoji);