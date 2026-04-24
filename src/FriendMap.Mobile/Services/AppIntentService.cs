namespace FriendMap.Mobile.Services;

public sealed class AppIntentService
{
    private readonly object _gate = new();
    private AppIntent? _pendingIntent;

    public void QueueProfile(Guid userId)
    {
        Queue(new AppIntent(AppIntentType.Profile, userId));
    }

    public void QueueDirectMessage(Guid userId)
    {
        Queue(new AppIntent(AppIntentType.DirectMessage, userId));
    }

    public void QueueTable(Guid tableId)
    {
        Queue(new AppIntent(AppIntentType.Table, tableId));
    }

    public AppIntent? Consume()
    {
        lock (_gate)
        {
            var intent = _pendingIntent;
            _pendingIntent = null;
            return intent;
        }
    }

    private void Queue(AppIntent intent)
    {
        lock (_gate)
        {
            _pendingIntent = intent;
        }
    }
}

public sealed record AppIntent(AppIntentType Type, Guid EntityId);

public enum AppIntentType
{
    Profile,
    DirectMessage,
    Table
}
