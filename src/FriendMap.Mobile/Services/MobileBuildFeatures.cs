namespace FriendMap.Mobile.Services;

public static class MobileBuildFeatures
{
#if FRIENDMAP_APNS_ENABLED
    public const bool PushNotificationsEnabled = true;
#else
    public const bool PushNotificationsEnabled = false;
#endif
}
