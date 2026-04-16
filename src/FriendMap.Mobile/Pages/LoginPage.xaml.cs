using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (await _viewModel.TryRestoreAsync())
            {
                await Shell.Current.GoToAsync(nameof(MainMapPage));
            }
        }
        catch (Exception ex)
        {
            _viewModel.Error = ex.Message;
        }
    }
}
