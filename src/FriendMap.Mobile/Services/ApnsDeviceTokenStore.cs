using Microsoft.Maui.Storage;

namespace FriendMap.Mobile.Services;

public static class ApnsDeviceTokenStore
{
    private const string DeviceTokenKey = "apns_device_token";
    private const string RegistrationErrorKey = "apns_registration_error";

    public static event EventHandler<string>? TokenChanged;

    public static string CurrentToken => Preferences.Default.Get(DeviceTokenKey, string.Empty);

    public static void SaveToken(string token)
    {
        Preferences.Default.Set(DeviceTokenKey, token);
        TokenChanged?.Invoke(null, token);
    }

    public static void SaveRegistrationError(string error)
    {
        Preferences.Default.Set(RegistrationErrorKey, error);
    }
}
