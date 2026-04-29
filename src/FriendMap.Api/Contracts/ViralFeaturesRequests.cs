namespace FriendMap.Api.Contracts;

public record CreateFlareRequest(double Latitude, double Longitude, string Message, int? DurationHours);

public record RespondToFlareRequest(string Body);

public record RelayFlareRequest(IEnumerable<Guid> TargetUserIds);

public record SubmitVibeRequest(string VibeEmoji);
