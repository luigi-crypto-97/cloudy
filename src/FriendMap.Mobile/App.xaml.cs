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
            await Shell.Current.GoToAsync("//main");
            switch (e.Type)
            {
                case "venue":
                    MessagingCenter.Send(this, "DeepLinkVenue", e.Id);
                    break;
                case "chat":
                    MessagingCenter.Send(this, "DeepLinkChat", e.Id);
                    break;
                case "table":
                    MessagingCenter.Send(this, "DeepLinkTable", e.Id);
                    break;
            }
        }
        catch { /* ignore */ }
    }
}
