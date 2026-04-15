namespace FriendMap.Api.Data;

public class PrivacyOptions
{
    public int MinimumAggregationK { get; set; } = 20;
    public int CheckInTtlMinutes { get; set; } = 180;
    public int IntentionTtlHours { get; set; } = 24;
    public int PresenceBucketMinutes { get; set; } = 15;
}
