namespace FriendMap.Api.Data;

public sealed class FeedOptions
{
    public bool ReentryEnabled { get; set; } = true;
    public int ReentryIntervalSeconds { get; set; } = 120;
    public int ReentryCooldownMinutes { get; set; } = 90;
    public int QuietHoursStartHour { get; set; } = 23;
    public int QuietHoursEndHour { get; set; } = 8;
    public int FlareRelayHourlyLimit { get; set; } = 9;
    public int FlareRelayPerFlareLimit { get; set; } = 6;
    public int FeedTake { get; set; } = 36;
}
