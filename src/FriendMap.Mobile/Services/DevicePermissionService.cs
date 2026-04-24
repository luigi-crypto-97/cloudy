using Microsoft.Maui.ApplicationModel;

namespace FriendMap.Mobile.Services;

public partial class DevicePermissionService : IDevicePermissionService
{
    public async Task RequestMapAndPushPermissionsAsync()
    {
        await RequestLocationAsync();

#if FRIENDMAP_APNS_ENABLED
        await RequestPushNotificationsPermissionAsync();
#endif
    }

    public Task<PermissionStatus> GetLocationStatusAsync()
    {
        return Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    }

    public Task<PermissionStatus> RequestLocationAsync()
    {
        return Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    }

    public Task<PermissionStatus> GetContactsStatusAsync()
    {
        return Permissions.CheckStatusAsync<Permissions.ContactsRead>();
    }

    public Task<PermissionStatus> RequestContactsAsync()
    {
        return Permissions.RequestAsync<Permissions.ContactsRead>();
    }

    public partial Task<bool> GetPushNotificationsEnabledAsync();

    public partial Task<bool> RequestPushNotificationsPermissionAsync();

    public void OpenAppSettings()
    {
        AppInfo.ShowSettingsUI();
    }
}
