using FriendMap.Mobile.Pages;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace FriendMap.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseSkiaSharp();

        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<IDevicePermissionService, DevicePermissionService>();

        // ViewModels
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<MainMapViewModel>();
        builder.Services.AddSingleton<SocialViewModel>();
        builder.Services.AddSingleton<NotificationsViewModel>();
        builder.Services.AddSingleton<InterestsViewModel>();
        builder.Services.AddSingleton<StoriesViewModel>();
        builder.Services.AddSingleton<DiscoveryViewModel>();
        builder.Services.AddSingleton<GamificationViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<MainMapPage>();
        builder.Services.AddTransient<SocialPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<EditProfilePage>();
        builder.Services.AddTransient<PrivacyPage>();
        builder.Services.AddTransient<TermsPage>();
        builder.Services.AddTransient<InterestsPage>();
        builder.Services.AddTransient<StoriesPage>();
        builder.Services.AddTransient<DiscoveryPage>();
        builder.Services.AddTransient<GamificationPage>();
        builder.Services.AddTransient<InvitePage>();

        builder.Services.AddSingleton<ChatHubService>();

        // App Center (Crashlytics + Analytics)
        // TODO: replace with your real App Secret from https://appcenter.ms
        const string appSecret = "ios=YOUR_IOS_APP_SECRET;android=YOUR_ANDROID_APP_SECRET";
        if (!appSecret.Contains("YOUR_"))
        {
            AppCenter.Start(appSecret, typeof(Analytics), typeof(Crashes));
        }

        return builder.Build();
    }
}
