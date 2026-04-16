using Microsoft.Maui.ApplicationModel;

namespace FriendMap.Mobile.Services;

public partial class DevicePermissionService : IDevicePermissionService
{
    public async Task RequestMapAndPushPermissionsAsync()
    {
        await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

#if FRIENDMAP_APNS_ENABLED
        await RequestPushNotificationsAsync();
#endif
    }

    private partial Task RequestPushNotificationsAsync();
}
