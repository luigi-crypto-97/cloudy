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
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddSingleton<MainMapViewModel>();
        builder.Services.AddSingleton<MainMapPage>();

        return builder.Build();
    }
}
