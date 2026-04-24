using Foundation;
using Microsoft.Maui.ApplicationModel;
using UIKit;
using UserNotifications;

namespace FriendMap.Mobile.Services;

public partial class DevicePermissionService
{
    public partial Task<bool> GetPushNotificationsEnabledAsync()
    {
        var completion = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
            {
                completion.TrySetResult(settings.AuthorizationStatus is UNAuthorizationStatus.Authorized or UNAuthorizationStatus.Provisional);
            });
        });

        return completion.Task;
    }

    public partial Task<bool> RequestPushNotificationsPermissionAsync()
    {
        var completion = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UNUserNotificationCenter.Current.RequestAuthorization(
                UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
                (approved, error) =>
                {
                    if (error is not null)
                    {
                        completion.TrySetException(new NSErrorException(error));
                        return;
                    }

                    if (approved)
                    {
                        UIApplication.SharedApplication.RegisterForRemoteNotifications();
                    }

                    completion.TrySetResult(approved);
                });
        });

        return completion.Task;
    }
}
