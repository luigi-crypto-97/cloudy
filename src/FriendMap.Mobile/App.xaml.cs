using FriendMap.Mobile.Pages;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Respect system dark mode by default
        UserAppTheme = AppTheme.Unspecified;

        MainPage = new AppShell();

        DeepLinkService.LinkReceived += OnDeepLinkReceived;
    }

    private async void OnDeepLinkReceived(object? sender, DeepLinkArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//main/social");
            switch (e.Type)
            {
                case "venue":
                    await Shell.Current.GoToAsync("//main/map");
                    MessagingCenter.Send(this, "DeepLinkVenue", e.Id);
                    break;
                case "chat":
                    await Shell.Current.GoToAsync($"{nameof(SocialChatPage)}?userId={Uri.EscapeDataString(e.Id)}");
                    break;
                case "table":
                    await Shell.Current.GoToAsync($"{nameof(SocialTablePage)}?tableId={Uri.EscapeDataString(e.Id)}");
                    break;
            }
        }
        catch { /* ignore */ }
    }
}
