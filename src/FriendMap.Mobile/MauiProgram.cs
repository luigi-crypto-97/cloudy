using FriendMap.Mobile.Pages;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps();

        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<IDevicePermissionService, DevicePermissionService>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainMapViewModel>();
        builder.Services.AddTransient<MainMapPage>();

        return builder.Build();
    }
}
