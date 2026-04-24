using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _viewModel;

    public NotificationsPage(NotificationsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
        UpdateBadge();
    }

    private void UpdateBadge()
    {
        var count = _viewModel.Items.Count;
        TabBarBadgeHelper.SetBadge(this, count);
    }

    private void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not Models.NotificationItem item) return;
        if (!string.IsNullOrWhiteSpace(item.DeepLink) && Uri.TryCreate(item.DeepLink, UriKind.Absolute, out var uri))
        {
            Services.DeepLinkService.HandleUrl(uri);
        }
    }
}
