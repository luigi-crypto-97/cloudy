namespace FriendMap.Api.Models;

public class ModerationReport : BaseEntity
{
    public Guid ReporterUserId { get; set; }
    public Guid? ReportedUserId { get; set; }
    public Guid? ReportedVenueId { get; set; }
    public Guid? ReportedSocialTableId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = "open";
}
