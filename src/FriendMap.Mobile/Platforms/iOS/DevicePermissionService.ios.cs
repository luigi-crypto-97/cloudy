using Foundation;
using Microsoft.Maui.ApplicationModel;
using UIKit;
using UserNotifications;

namespace FriendMap.Mobile.Services;

public partial class DevicePermissionService
{
    private partial Task RequestPushNotificationsAsync()
    {
        var completion = new TaskCompletionSource();

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

                    completion.TrySetResult();
                });
        });

        return completion.Task;
    }
}
