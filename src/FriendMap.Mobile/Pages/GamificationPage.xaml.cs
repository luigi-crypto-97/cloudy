using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class GamificationPage : ContentPage
{
    private readonly GamificationViewModel _vm;

    public GamificationPage(GamificationViewModel vm)
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
