namespace FriendMap.Api.Data;

public class NotificationDispatchOptions
{
    public bool DispatchEnabled { get; set; }
    public int DispatchIntervalSeconds { get; set; } = 30;
    public int MaxAttempts { get; set; } = 5;
}
