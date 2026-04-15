using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage : ContentPage
{
    private readonly MainMapViewModel _viewModel;

    public MainMapPage(MainMapViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
