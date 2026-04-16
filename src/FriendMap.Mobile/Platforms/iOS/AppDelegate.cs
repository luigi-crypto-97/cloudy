using Foundation;
using FriendMap.Mobile.Services;
using UIKit;

namespace FriendMap.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
    public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
    {
#if FRIENDMAP_APNS_ENABLED
        var token = deviceToken.ToString()
            .Trim('<', '>')
            .Replace(" ", string.Empty);

        if (!string.IsNullOrWhiteSpace(token))
        {
            ApnsDeviceTokenStore.SaveToken(token);
        }
#endif
    }

    [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
    public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
    {
#if FRIENDMAP_APNS_ENABLED
        ApnsDeviceTokenStore.SaveRegistrationError(error.LocalizedDescription);
#endif
    }
}
