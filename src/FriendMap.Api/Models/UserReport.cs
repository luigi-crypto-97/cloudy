namespace FriendMap.Api.Models;

public class UserReport : BaseEntity
{
    public Guid ReporterUserId { get; set; }
    public Guid ReportedUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
}