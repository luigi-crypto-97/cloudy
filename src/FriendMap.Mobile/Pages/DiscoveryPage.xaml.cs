using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class DiscoveryPage : ContentPage
{
    private readonly DiscoveryViewModel _vm;

    public DiscoveryPage(DiscoveryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }
}
