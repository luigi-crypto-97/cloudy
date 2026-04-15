namespace FriendMap.Api.Models;

public class VenueAffluenceSnapshot : BaseEntity
{
    public Guid VenueId { get; set; }
    public DateTimeOffset BucketStartUtc { get; set; }
    public DateTimeOffset BucketEndUtc { get; set; }
    public int ActiveUsersEstimated { get; set; }
    public string DensityLevel { get; set; } = "low";
    public string? AggregatedAgeJson { get; set; }
    public string? AggregatedGenderJson { get; set; }
    public bool IsSuppressedForPrivacy { get; set; } = true;
}
