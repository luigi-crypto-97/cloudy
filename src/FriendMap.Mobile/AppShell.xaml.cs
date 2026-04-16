using FriendMap.Mobile.Pages;

namespace FriendMap.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(MainMapPage), typeof(MainMapPage));
    }
}
