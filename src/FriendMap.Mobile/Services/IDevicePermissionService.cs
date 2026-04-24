namespace FriendMap.Mobile.Services;

public interface IDevicePermissionService
{
    Task RequestMapAndPushPermissionsAsync();
    Task<PermissionStatus> GetLocationStatusAsync();
    Task<PermissionStatus> RequestLocationAsync();
    Task<PermissionStatus> GetContactsStatusAsync();
    Task<PermissionStatus> RequestContactsAsync();
    Task<bool> GetPushNotificationsEnabledAsync();
    Task<bool> RequestPushNotificationsPermissionAsync();
    void OpenAppSettings();
}
